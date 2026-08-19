namespace Trading.AcceptanceTests.Support;

public sealed class Stage1ScenarioState
{
    public bool InfrastructureMarkerRecorded { get; set; }
    public object? Subject { get; set; }
    public object? Secondary { get; set; }
    public Exception? Error { get; set; }
    public string? Expected { get; set; }
    public bool Verified { get; set; }
}
