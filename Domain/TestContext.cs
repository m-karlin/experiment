namespace Sandbox.Domain;

public class SagaContext
{
    public string? Id { get; set; }
    public string? StoredTraceId { get; set; }
    public string? StoredTraceState { get; set; }
}