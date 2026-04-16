namespace EfMigrationManager.Core.Models;

public sealed class DbContextInfo
{
    public required string FullName      { get; init; }
    public required string ShortName     { get; init; }
    public required string? Namespace    { get; init; }

    public override string ToString() => ShortName;
}
