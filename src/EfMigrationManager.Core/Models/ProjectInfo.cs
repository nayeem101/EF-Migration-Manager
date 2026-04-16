namespace EfMigrationManager.Core.Models;

public sealed class ProjectInfo
{
    public required string Name            { get; init; }
    public required string AbsolutePath    { get; init; }
    public required string RelativePath    { get; init; }
    public required bool   IsExecutable    { get; init; }
    public required bool   HasEfCore       { get; init; }
    public required bool   HasEfDesign     { get; init; }

    public string DirectoryPath => System.IO.Path.GetDirectoryName(AbsolutePath)!;

    public override string ToString() => Name;
}
