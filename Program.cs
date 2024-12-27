#pragma warning disable CA1031
using Microsoft.AspNetCore.Mvc;
using Microsoft.IO;
using Sandbox.Grains;
using Sandbox.Infrastructure;
using Sandbox.Migrations;
using Sandbox.Repositories;
using LogMessages = Sandbox.Logging.LogMessages;

var builder = WebApplication.CreateBuilder(args);

var appSettings = new AppSettings(builder.Configuration);
builder.Services.AddSingleton(appSettings);

builder.Services.AddSingleton<RecyclableMemoryStreamManager>();

builder.AddOrleans(appSettings);
builder.AddLogging();
builder.AddDiagnostics(appSettings);

builder.Services.AddHealthChecks();

builder.Services.AddStorage(appSettings.ConnectionString);
builder.Services.AddMigrations(appSettings.ConnectionString);

builder.Services.AddSingleton<SagaContextRepository>();

var app = builder.Build();

app.UseHealthChecks();

app.MapGet("/", ([FromServices] IClusterClient client, [FromServices] ILogger<Program> logger, CancellationToken ct) =>
{
    var grain = client.GetGrain<ITestGrain>(Guid.NewGuid().ToString());
    return grain.StartSaga(ct);
});

app.UseDiagnostics();

var logger = app.Services.GetRequiredService<ILogger<Program>>();
LogMessages.RunningMigrations(logger);

try
{
    var dbInitializer = app.Services.GetRequiredService<DbInitializer>();
    await dbInitializer.Init(CancellationToken.None);
}
catch (Exception e)
{
    LogMessages.MigrationsFailed(logger, e);
    throw;
}

LogMessages.MigrationsApplied(logger);

await app.RunAsync();