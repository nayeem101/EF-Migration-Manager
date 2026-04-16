namespace EfMigrationManager.Core.Services;

using System.Xml.Linq;
using EfMigrationManager.Core.Models;
using Microsoft.Build.Construction;
using Microsoft.Extensions.Logging;

public sealed class SolutionParserService : ISolutionParserService
{
    private readonly ILogger<SolutionParserService> _logger;

    public SolutionParserService(ILogger<SolutionParserService> logger)
        => _logger = logger;

    public bool IsSupported(string filePath)
        => Path.GetExtension(filePath).ToLowerInvariant() is ".sln" or ".slnx";

    public Task<SolutionInfo> ParseAsync(string solutionPath, CancellationToken ct = default)
    {
        _logger.LogInformation("Parsing solution: {Path}", solutionPath);

        return Path.GetExtension(solutionPath).ToLowerInvariant() switch
        {
            ".slnx" => Task.FromResult(ParseSlnx(solutionPath)),
            ".sln"  => Task.FromResult(ParseSln(solutionPath)),
            _       => throw new NotSupportedException($"Unknown solution format: {solutionPath}")
        };
    }

    private SolutionInfo ParseSlnx(string path)
    {
        var solutionDir = Path.GetDirectoryName(path)!;
        var root = XDocument.Load(path).Root
                   ?? throw new InvalidDataException(".slnx has no root element");

        var projects = root
            .Descendants("Project")
            .Select(e => e.Attribute("Path")?.Value)
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(relativePath =>
            {
                var normalized = relativePath!.Replace('/', Path.DirectorySeparatorChar);
                var fullPath   = Path.GetFullPath(Path.Combine(solutionDir, normalized));
                return BuildProjectInfo(fullPath, solutionDir);
            })
            .OfType<ProjectInfo>()
            .ToList();

        _logger.LogInformation("Parsed .slnx: {Count} projects found", projects.Count);

        return new SolutionInfo
        {
            Path     = path,
            Name     = Path.GetFileNameWithoutExtension(path),
            Format   = SolutionFormat.Slnx,
            Projects = projects
        };
    }

    private SolutionInfo ParseSln(string path)
    {
        var solutionDir  = Path.GetDirectoryName(path)!;
        var solutionFile = SolutionFile.Parse(path);

        var projects = solutionFile.ProjectsInOrder
            .Where(p => p.ProjectType == SolutionProjectType.KnownToBeMSBuildFormat)
            .Select(p => BuildProjectInfo(p.AbsolutePath, solutionDir))
            .OfType<ProjectInfo>()
            .ToList();

        _logger.LogInformation("Parsed .sln: {Count} projects found", projects.Count);

        return new SolutionInfo
        {
            Path     = path,
            Name     = Path.GetFileNameWithoutExtension(path),
            Format   = SolutionFormat.Sln,
            Projects = projects
        };
    }

    private ProjectInfo? BuildProjectInfo(string csprojPath, string solutionDir)
    {
        if (!File.Exists(csprojPath))
        {
            _logger.LogWarning("Project file not found: {Path}", csprojPath);
            return null;
        }

        string content;
        try   { content = File.ReadAllText(csprojPath); }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not read project file: {Path}", csprojPath);
            return null;
        }

        return new ProjectInfo
        {
            Name          = Path.GetFileNameWithoutExtension(csprojPath),
            AbsolutePath  = csprojPath,
            RelativePath  = Path.GetRelativePath(solutionDir, csprojPath),
            IsExecutable  = content.Contains("<OutputType>Exe</OutputType>",
                                StringComparison.OrdinalIgnoreCase),
            HasEfCore     = content.Contains("EntityFrameworkCore",
                                StringComparison.OrdinalIgnoreCase),
            HasEfDesign   = content.Contains("EntityFrameworkCore.Design",
                                StringComparison.OrdinalIgnoreCase),
        };
    }
}
