using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sandbox.Infrastructure;

public static class CommonJsonConfig
{
    public static JsonSerializerOptions Options { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters =
        {
            new JsonStringEnumConverter(),
        },
    };
}
