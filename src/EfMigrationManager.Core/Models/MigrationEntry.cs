namespace EfMigrationManager.Core.Models;

public enum MigrationStatus { Applied, Pending }

public sealed class MigrationEntry
{
    public required string          Name      { get; init; }
    public required MigrationStatus Status    { get; init; }
    public required string?         SafeName  { get; init; }
    public required DateTimeOffset? Timestamp { get; init; }

    public string StatusIcon => Status == MigrationStatus.Applied ? "✅" : "🟡";

    public override string ToString() => Name;
}
