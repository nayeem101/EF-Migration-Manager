namespace EfMigrationManager.Core.Models;

public enum SolutionFormat { Sln, Slnx }

public sealed class SolutionInfo
{
    public required string         Path     { get; init; }
    public required string         Name     { get; init; }
    public required SolutionFormat Format   { get; init; }
    public required List<ProjectInfo> Projects { get; init; }

    public string DirectoryPath  => System.IO.Path.GetDirectoryName(Path)!;
    public string DisplayName    => $"{Name} ({(Format == SolutionFormat.Slnx ? ".slnx" : ".sln")})";
}
