namespace EfMigrationManager.Core.Models;

public enum ProcessLineKind { StdOut, StdErr, System }

public sealed class ProcessLine
{
    public required string          Text      { get; init; }
    public required ProcessLineKind Kind      { get; init; }
    public required DateTimeOffset  Timestamp { get; init; }

    public static ProcessLine Out(string text)    => new() { Text = text, Kind = ProcessLineKind.StdOut,  Timestamp = DateTimeOffset.Now };
    public static ProcessLine Error(string text)  => new() { Text = text, Kind = ProcessLineKind.StdErr,  Timestamp = DateTimeOffset.Now };
    public static ProcessLine System(string text) => new() { Text = text, Kind = ProcessLineKind.System,  Timestamp = DateTimeOffset.Now };
}
