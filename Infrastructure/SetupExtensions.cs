using System.Net;
using Dapper;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Orleans.Configuration;
using Sandbox.Grains;
using Sandbox.Migrations;
using Serilog;
using Serilog.Enrichers.Span;
using Serilog.Events;
using Serilog.Formatting.Elasticsearch;

namespace Sandbox.Infrastructure;

public static class SetupExtensions
{
    public static IServiceCollection AddStorage(this IServiceCollection services, string connectionString) =>
     services
         .ConfigureDataAccess(connectionString);

    public static IServiceCollection AddMigrations(this IServiceCollection services, string connectionString) =>
        services
            .ConfigureDataAccess(connectionString)
            .AddTransient<DbInitializer>()
            .AddMigrations();

    private static IServiceCollection AddMigrations(this IServiceCollection services)
    {
        var migrationGroupType = typeof(IMigrationGroup);
        var migrationGroups = migrationGroupType
            .Assembly.GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false } && migrationGroupType.IsAssignableFrom(type));

        foreach (var migrationGroup in migrationGroups)
        {
            services.AddTransient(migrationGroupType, migrationGroup);
        }

        return services;
    }

    private static IServiceCollection ConfigureDataAccess(this IServiceCollection services, string connectionString)
    {
        DefaultTypeMap.MatchNamesWithUnderscores = true;

        services.AddSingleton<IDbSessionFactory>(new DbSessionFactory(connectionString));
        return services;
    }

    public static WebApplication UseHealthChecks(this WebApplication app)
    {
        app.MapHealthChecks(
            "/health/ready",
            new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") }
        );
        app.MapHealthChecks(
            "/health/live",
            new HealthCheckOptions { Predicate = check => check.Tags.Contains("live") }
        );

        return app;
    }

    public static WebApplicationBuilder AddDiagnostics(this WebApplicationBuilder builder, AppSettings appSettings)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.AddOpenTelemetry()
            .AddMetrics()
            .AddTracing(builder.Environment, appSettings);

        return builder;
    }

    private static OpenTelemetryBuilder AddTracing(this OpenTelemetryBuilder otelBuilder, IWebHostEnvironment env, AppSettings appSettings)
    {
        var attributes = new List<KeyValuePair<string, object>>
        {
            new("environment", env.EnvironmentName),
            new("cluster", Environment.GetEnvironmentVariable("CLUSTER") ?? string.Empty),
            new("hostname", Dns.GetHostName())
        };

        otelBuilder.WithTracing(
            builder =>
            {
                builder.SetResourceBuilder(
                        ResourceBuilder.CreateDefault()
                            .AddService("sandbox", autoGenerateServiceInstanceId: false)
                            .AddAttributes(attributes)
                    )
                    .AddAspNetCoreInstrumentation(options => options.Filter = ExcludeHealthChecksAndMetrics)
                    .AddHttpClientInstrumentation()
                    .AddSource("MySqlConnector")
                    .AddSource("Microsoft.Orleans.Runtime")
                    .AddSource("Microsoft.Orleans.Application")
                    .AddSource(nameof(TestGrain))
                    .SetSampler(new TraceIdRatioBasedSampler(1))
                    .AddJaegerExporter(options =>
                    {
                        options.AgentHost = appSettings.JaegerHost;
                        options.AgentPort = appSettings.JaegerPort;
                    });
            }
        );

        return otelBuilder;
    }

    private static bool ExcludeHealthChecksAndMetrics(HttpContext context)
    {
        return !context.Request.Path.StartsWithSegments("/health", StringComparison.OrdinalIgnoreCase) &&
               !context.Request.Path.StartsWithSegments("/metrics", StringComparison.OrdinalIgnoreCase);
    }

    private static OpenTelemetryBuilder AddMetrics(this OpenTelemetryBuilder otelBuilder)
    {
        otelBuilder.WithMetrics(
            builder => builder.AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation()
                .AddMeter("MySqlConnector")
                .AddMeter("Microsoft.Orleans")
                .AddOtlpExporter()
                .AddPrometheusExporter(options => options.DisableTotalNameSuffixForCounters = true)
        );

        return otelBuilder;
    }

    public static bool IsLocal(this IWebHostEnvironment environment)
    {
        return environment.IsEnvironment("local");
    }

    public static WebApplication UseDiagnostics(this WebApplication app)
    {
        app.UseOpenTelemetryPrometheusScrapingEndpoint();
        return app;
    }

    public static void AddLogging(this WebApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Host.UseSerilog(
            (context, configuration) =>
            {
                configuration
                    .MinimumLevel.Information()
                    .Enrich.FromLogContext()
                    .Enrich.WithProperty("Environment", context.HostingEnvironment.EnvironmentName)
                    .Enrich.WithProperty("ServiceName", "sandbox")
                    .Enrich.WithSpan();


                configuration
                    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
                    .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Warning)
                    .MinimumLevel.Override("System", LogEventLevel.Warning)
                    .MinimumLevel.Override("Jaeger", LogEventLevel.Warning)
                    .MinimumLevel.Override("OpenTracing.Contrib.NetCore", LogEventLevel.Warning)
                    .MinimumLevel.Override("CorrelationId", LogEventLevel.Warning)
                    .MinimumLevel.Override("Orleans", LogEventLevel.Error)
                    .WriteTo.Async(
                        to => to.Console(
                            new ExceptionAsObjectJsonFormatter(renderMessage: true, inlineFields: true),
                            standardErrorFromLevel: LogEventLevel.Warning
                        )
                    );
            }
        );
    }

    private const string MysqlConnectorInvariant = "MySql.Data.MySqlConnector";

    public static void AddOrleans(this WebApplicationBuilder builder, AppSettings appSettings)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(appSettings);

        builder.UseOrleans(siloBuilder =>
        {
            siloBuilder
                .Configure<SiloOptions>(options => options.SiloName = GetSiloName())
                .Configure<ClusterOptions>(options =>
                {
                    options.ServiceId = "SandboxApp";
                    options.ClusterId = "SandboxApp";
                })
                .Configure<ClusterMembershipOptions>(options =>
                {
                    // Максимальное время, за которое силос должен присоединиться к кластеру
                    options.MaxJoinAttemptTime = TimeSpan.FromMinutes(1);
                    // Если силос не отвечает в течение этого времени, он будет считаться "мёртвым"
                    options.DefunctSiloExpiration = TimeSpan.FromMinutes(5);
                    // Как часто система будет проверять и очищать информацию о неактивных силосах
                    options.DefunctSiloCleanupPeriod = TimeSpan.FromMinutes(3);
                    // Как долго система будет ждать обновления таблицы членов, прежде чем считать попытку неудачной
                    options.TableRefreshTimeout = TimeSpan.FromSeconds(5);
                    // Как долго будет ждать система, прежде чем сдаться в попытке опубликовать своё состояние
                    options.IAmAliveTablePublishTimeout = TimeSpan.FromMinutes(1);
                })
                .Configure<GrainCollectionOptions>(options =>
                {
                    // Если грейн не использовался в течение 5 минут, он может быть собран сборщиком мусора
                    options.CollectionAge = TimeSpan.FromMinutes(5);
                })
                .Configure<SchedulingOptions>(options =>
                {
                    // Максимальное время работы грейна, после которого система сгенерирует предупреждение что он завис
                    options.TurnWarningLengthThreshold = TimeSpan.FromMilliseconds(500);
                    // Максимальное время задержки грейна в планировщике задач, после которого сгенерирует предупреждение что он застрял
                    options.DelayWarningThreshold = TimeSpan.FromSeconds(5);
                })
                .Configure<SiloMessagingOptions>(options =>
                {
                    // Клиенты будут обновлять свою регистрацию в Silo каждые 5 секунд
                    options.ClientRegistrationRefresh = TimeSpan.FromSeconds(5);
                    // Максимальное количество "пересылок" сообщений другим Silo
                    options.MaxForwardCount = 3;
                })
                .UseAdoNetReminderService(options =>
                {
                    // Настройки ADO.NET для напоминаний
                    options.ConnectionString = appSettings.ConnectionString;
                    options.Invariant = MysqlConnectorInvariant;
                })
                .ConfigureLogging(logging => logging.SetMinimumLevel(LogLevel.Warning))
                // https://github.com/OrleansContrib/OrleansDashboard
                .UseDashboard(options => options.Port = appSettings.DashboardPort)
                // Add Activity propagation through grain calls
                .AddActivityPropagation();

            siloBuilder
                .ConfigureEndpoints(
                    siloPort: appSettings.SiloPort,
                    gatewayPort: 0 // не используетя
                )
                .UseAdoNetClustering(
                    options =>
                    {
                        options.ConnectionString = appSettings.ConnectionString;
                        options.Invariant = MysqlConnectorInvariant;
                    }
                );
        });
    }

    /// <summary>
    /// Выбираем имя silo на основе имени пода
    /// </summary>
    private static string GetSiloName()
    {
        var podName = Environment.GetEnvironmentVariable("POD_NAME");
        var suffix = Guid.NewGuid().ToString("D")[..13];
        var fallbackName = $"sandbox-app-{suffix}";
        return podName ?? fallbackName;
    }
}
