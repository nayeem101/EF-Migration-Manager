namespace EfMigrationManager.Core.Models;

public sealed class ProjectInfo
{
    public required string Name            { get; init; }
    public required string AbsolutePath    { get; init; }
    public required string RelativePath    { get; init; }
    public required bool   IsExecutable    { get; init; }
    public required bool   HasEfCore       { get; init; }
    public required bool   HasEfDesign     { get; init; }

    public bool HasEfCoreTransitive   { get; set; }
    public bool HasEfDesignTransitive { get; set; }
    public bool HasDbContext          { get; set; }
    public bool HasMigrationsFolder   { get; set; }

    public required IReadOnlyList<string> ProjectReferences { get; init; }
    public string?  SolutionFolder { get; set; }

    public bool IsMigrationCandidate
        => HasEfCoreTransitive && (HasDbContext || HasMigrationsFolder);

    public bool IsStartupCandidate
        => IsExecutable || HasEfDesignTransitive;

    public string DirectoryPath => System.IO.Path.GetDirectoryName(AbsolutePath)!;

    public override string ToString() => Name;
}
