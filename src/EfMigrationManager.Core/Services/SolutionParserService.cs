namespace EfMigrationManager.Core.Services;

using System.Text.RegularExpressions;
using System.Xml.Linq;
using EfMigrationManager.Core.Models;
using Microsoft.Build.Construction;
using Microsoft.Extensions.Logging;

public sealed class SolutionParserService : ISolutionParserService
{
    private readonly ILogger<SolutionParserService> _logger;

    private static readonly Regex DbContextRegex = new(
        @"class\s+\w+\s*:\s*[^;{]*\b(DbContext|IdentityDbContext|IdentityUserContext)\b",
        RegexOptions.Compiled);

    public SolutionParserService(ILogger<SolutionParserService> logger)
        => _logger = logger;

    public bool IsSupported(string filePath)
        => Path.GetExtension(filePath).ToLowerInvariant() is ".sln" or ".slnx";

    public Task<SolutionInfo> ParseAsync(string solutionPath, CancellationToken ct = default)
    {
        _logger.LogInformation("Parsing solution: {Path}", solutionPath);

        var info = Path.GetExtension(solutionPath).ToLowerInvariant() switch
        {
            ".slnx" => ParseSlnx(solutionPath),
            ".sln"  => ParseSln(solutionPath),
            _       => throw new NotSupportedException($"Unknown solution format: {solutionPath}")
        };

        PropagateTransitive(info.Projects);
        ScanDbContextsAndMigrations(info.Projects);

        return Task.FromResult(info);
    }

    // -------- .slnx --------

    private SolutionInfo ParseSlnx(string path)
    {
        var solutionDir = Path.GetDirectoryName(path)!;
        var root = XDocument.Load(path).Root
                   ?? throw new InvalidDataException(".slnx has no root element");

        var projects = new List<ProjectInfo>();

        foreach (var folderEl in root.Descendants("Folder"))
        {
            var folderName = folderEl.Attribute("Name")?.Value?.Trim('/');
            foreach (var projEl in folderEl.Elements("Project"))
                AddSlnxProject(projEl, folderName, solutionDir, projects);
        }

        foreach (var projEl in root.Elements("Project"))
            AddSlnxProject(projEl, null, solutionDir, projects);

        _logger.LogInformation("Parsed .slnx: {Count} projects found", projects.Count);

        return new SolutionInfo
        {
            Path     = path,
            Name     = Path.GetFileNameWithoutExtension(path),
            Format   = SolutionFormat.Slnx,
            Projects = projects
        };
    }

    private void AddSlnxProject(XElement projEl, string? folder, string solutionDir, List<ProjectInfo> projects)
    {
        var relativePath = projEl.Attribute("Path")?.Value;
        if (string.IsNullOrWhiteSpace(relativePath)) return;

        var normalized = relativePath.Replace('/', Path.DirectorySeparatorChar);
        var fullPath   = Path.GetFullPath(Path.Combine(solutionDir, normalized));
        var info = BuildProjectInfo(fullPath, solutionDir);
        if (info is null) return;

        info.SolutionFolder = folder;
        projects.Add(info);
    }

    // -------- .sln --------

    private SolutionInfo ParseSln(string path)
    {
        var solutionDir  = Path.GetDirectoryName(path)!;
        var solutionFile = SolutionFile.Parse(path);

        var projects = new List<ProjectInfo>();
        foreach (var p in solutionFile.ProjectsInOrder
                             .Where(p => p.ProjectType == SolutionProjectType.KnownToBeMSBuildFormat))
        {
            var info = BuildProjectInfo(p.AbsolutePath, solutionDir);
            if (info is null) continue;
            info.SolutionFolder = InferFolderFromRelativePath(info.RelativePath);
            projects.Add(info);
        }

        _logger.LogInformation("Parsed .sln: {Count} projects found", projects.Count);

        return new SolutionInfo
        {
            Path     = path,
            Name     = Path.GetFileNameWithoutExtension(path),
            Format   = SolutionFormat.Sln,
            Projects = projects
        };
    }

    private static string? InferFolderFromRelativePath(string relativePath)
    {
        var dir = Path.GetDirectoryName(relativePath);
        if (string.IsNullOrEmpty(dir)) return null;

        var parts = dir.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries)
                       .Where(p => !p.Equals("src", StringComparison.OrdinalIgnoreCase))
                       .ToArray();
        if (parts.Length == 0) return null;

        // drop trailing project-name segment (project folder itself)
        if (parts.Length >= 2) parts = parts[..^1];
        return string.Join('/', parts);
    }

    // -------- per-project --------

    private ProjectInfo? BuildProjectInfo(string csprojPath, string solutionDir)
    {
        if (!File.Exists(csprojPath))
        {
            _logger.LogWarning("Project file not found: {Path}", csprojPath);
            return null;
        }

        XDocument doc;
        try   { doc = XDocument.Load(csprojPath); }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not parse project XML: {Path}", csprojPath);
            return null;
        }

        var root      = doc.Root;
        var sdk       = root?.Attribute("Sdk")?.Value ?? string.Empty;
        var isWebSdk  = sdk.Contains("Web", StringComparison.OrdinalIgnoreCase);

        var packageRefs = root?.Descendants()
                              .Where(e => e.Name.LocalName == "PackageReference")
                              .Select(e => e.Attribute("Include")?.Value ?? string.Empty)
                              .ToList() ?? [];

        var hasEfCore   = packageRefs.Any(r =>
                              r.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.OrdinalIgnoreCase)
                              || r.Contains("EntityFrameworkCore", StringComparison.OrdinalIgnoreCase));
        var hasEfDesign = packageRefs.Any(r =>
                              r.Equals("Microsoft.EntityFrameworkCore.Design", StringComparison.OrdinalIgnoreCase));

        var outputType  = root?.Descendants()
                              .FirstOrDefault(e => e.Name.LocalName == "OutputType")?.Value;
        var hasExeOut   = string.Equals(outputType, "Exe", StringComparison.OrdinalIgnoreCase);

        var projectDir  = Path.GetDirectoryName(csprojPath)!;
        var hasProgram  = File.Exists(Path.Combine(projectDir, "Program.cs"));

        var projectRefs = root?.Descendants()
                              .Where(e => e.Name.LocalName == "ProjectReference")
                              .Select(e => e.Attribute("Include")?.Value ?? string.Empty)
                              .Where(p => !string.IsNullOrWhiteSpace(p))
                              .Select(p =>
                              {
                                  var norm = p.Replace('/', Path.DirectorySeparatorChar)
                                              .Replace('\\', Path.DirectorySeparatorChar);
                                  return Path.GetFullPath(Path.Combine(projectDir, norm));
                              })
                              .ToList() ?? [];

        return new ProjectInfo
        {
            Name              = Path.GetFileNameWithoutExtension(csprojPath),
            AbsolutePath      = csprojPath,
            RelativePath      = Path.GetRelativePath(solutionDir, csprojPath),
            IsExecutable      = hasExeOut || isWebSdk || hasProgram,
            HasEfCore         = hasEfCore,
            HasEfDesign       = hasEfDesign,
            ProjectReferences = projectRefs,
        };
    }

    // -------- Pass 2: transitive --------

    private static void PropagateTransitive(IReadOnlyList<ProjectInfo> projects)
    {
        var map = projects.ToDictionary(
            p => p.AbsolutePath,
            p => p,
            StringComparer.OrdinalIgnoreCase);

        foreach (var p in projects)
        {
            p.HasEfCoreTransitive   = Walk(p, map, seen: new(StringComparer.OrdinalIgnoreCase), pick: x => x.HasEfCore);
            p.HasEfDesignTransitive = Walk(p, map, seen: new(StringComparer.OrdinalIgnoreCase), pick: x => x.HasEfDesign);
        }

        static bool Walk(
            ProjectInfo node,
            Dictionary<string, ProjectInfo> map,
            HashSet<string> seen,
            Func<ProjectInfo, bool> pick)
        {
            if (!seen.Add(node.AbsolutePath)) return false;
            if (pick(node)) return true;
            foreach (var refPath in node.ProjectReferences)
                if (map.TryGetValue(refPath, out var child) && Walk(child, map, seen, pick))
                    return true;
            return false;
        }
    }

    // -------- Pass 3: DbContext + Migrations folder --------

    private void ScanDbContextsAndMigrations(IReadOnlyList<ProjectInfo> projects)
    {
        foreach (var p in projects)
        {
            try
            {
                var dir = p.DirectoryPath;
                p.HasMigrationsFolder = HasMigrationsDir(dir);
                p.HasDbContext        = ScanForDbContext(dir);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "DbContext scan failed for {Path}", p.AbsolutePath);
            }
        }
    }

    private static bool HasMigrationsDir(string projectDir)
    {
        if (!Directory.Exists(projectDir)) return false;
        // common paths: Migrations/, Persistence/Migrations/, Data/Migrations/, Infrastructure/Migrations/
        try
        {
            return Directory.EnumerateDirectories(projectDir, "Migrations", SearchOption.AllDirectories)
                .Any(d => !d.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                       && !d.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"));
        }
        catch { return false; }
    }

    private static bool ScanForDbContext(string projectDir)
    {
        if (!Directory.Exists(projectDir)) return false;
        try
        {
            foreach (var file in Directory.EnumerateFiles(projectDir, "*.cs", SearchOption.AllDirectories))
            {
                if (file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                    || file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
                    continue;

                string content;
                try   { content = File.ReadAllText(file); }
                catch { continue; }

                if (DbContextRegex.IsMatch(content)) return true;
            }
        }
        catch { }
        return false;
    }
}
