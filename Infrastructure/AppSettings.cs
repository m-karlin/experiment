namespace Sandbox.Infrastructure;

public class AppSettings
{
    private readonly IConfiguration _configuration;

    public string OtelCollector => _configuration.GetValue<string>($"{nameof(OtelCollector)}")
        ?? throw new InvalidOperationException($"{OtelCollector} config is missing.");

    public string JaegerHost => _configuration.GetValue<string>($"{nameof(JaegerHost)}")
        ?? throw new InvalidOperationException($"{JaegerHost} config is missing.");

    public int JaegerPort => _configuration.GetValue<int>($"{nameof(JaegerPort)}");


    public int SiloPort => _configuration.GetValue<int>($"{nameof(SiloPort)}");

    public int DashboardPort => _configuration.GetValue<int>($"{nameof(DashboardPort)}");

    public string ConnectionString => _configuration.GetValue<string>($"{nameof(ConnectionString)}")
        ?? throw new InvalidOperationException($"{ConnectionString} config is missing.");

    public AppSettings(IConfiguration configuration)
    {
        _configuration = configuration;
    }
}