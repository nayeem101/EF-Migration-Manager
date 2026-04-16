namespace EfMigrationManager.Core.Models;

public sealed class EfCommandOptions
{
    public required string StartupProjectPath    { get; init; }
    public required string MigrationsProjectPath { get; init; }
    public required string ContextName           { get; init; }
    public required string WorkingDirectory      { get; init; }
}
