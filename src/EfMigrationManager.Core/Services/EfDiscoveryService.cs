namespace EfMigrationManager.Core.Services;

using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using EfMigrationManager.Core.Helpers;
using EfMigrationManager.Core.Models;
using Microsoft.Extensions.Logging;

public sealed class EfDiscoveryException : Exception
{
    public AppNotification Notification { get; }
    public EfDiscoveryException(AppNotification n) : base(n.Message) => Notification = n;
}

public sealed class EfDiscoveryService : IEfDiscoveryService
{
    private static readonly Regex ClassDeclarationRegex = new(
        @"class\s+(?<name>\w+)\s*(?::\s*(?<bases>[^{;\r\n]+))?",
        RegexOptions.Compiled);

    private static readonly Regex NamespaceRegex = new(
        @"^\s*namespace\s+(?<name>[A-Za-z0-9_.]+)\s*;?",
        RegexOptions.Compiled | RegexOptions.Multiline);

    /// <summary>EF migration file names: timestamp_digits + underscore + suffix (14-digit timestamp is common; allow 6+ digits).</summary>
    private static readonly Regex MigrationFileRegex = new(
        @"^(?<id>\d{6,}_.+)$",
        RegexOptions.Compiled);

    private readonly IProcessRunnerService _runner;
    private readonly ILogger<EfDiscoveryService> _logger;

    public EfDiscoveryService(
        IProcessRunnerService runner,
        ILogger<EfDiscoveryService> logger)
    {
        _runner = runner;
        _logger = logger;
    }

    public async Task<List<DbContextInfo>> ListDbContextsAsync(
        EfCommandOptions options, CancellationToken ct = default)
    {
        // Fast path first: do NOT scan all .cs files until CLI fails (scan was blocking UI).
        try
        {
            var args = BuildBaseArgs("dbcontext list", options, includeContext: false) + " --json --no-color";
            var output = await CollectOutputAsync(args, options.WorkingDirectory, ct);

            var err = EfErrorDetector.Detect(output);
            if (err is null)
            {
                var result = ParseDbContextJson(output);
                if (result.Count > 0)
                    return result;
            }
            else
            {
                var fallback = DiscoverDbContextsFromProject(options.MigrationsProjectPath);
                if (fallback.Count > 0)
                    return fallback;
                throw new EfDiscoveryException(err);
            }
        }
        catch (EfDiscoveryException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "dotnet ef dbcontext list failed; using file scan fallback");
        }

        return DiscoverDbContextsFromProject(options.MigrationsProjectPath);
    }

    public async Task<List<MigrationEntry>> ListMigrationsAsync(
        EfCommandOptions options, CancellationToken ct = default)
    {
        try
        {
            var args = BuildBaseArgs("migrations list", options, includeContext: true) + " --json --no-color";
            var output = await CollectOutputAsync(args, options.WorkingDirectory, ct);

            if (EfErrorDetector.Detect(output) is null)
            {
                var parsed = ParseMigrationsJson(output);
                if (parsed.Count > 0)
                    return parsed;
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "dotnet ef migrations list failed; using Migrations folder scan");
        }

        return DiscoverMigrationsFromProject(options.MigrationsProjectPath);
    }

    private string BuildBaseArgs(string command, EfCommandOptions options, bool includeContext)
    {
        var args = new StringBuilder($"ef {command}");
        args.Append($" --project \"{options.MigrationsProjectPath}\"");
        args.Append($" --startup-project \"{options.StartupProjectPath}\"");

        if (includeContext && !string.IsNullOrWhiteSpace(options.ContextName))
            args.Append($" --context \"{options.ContextName}\"");

        return args.ToString();
    }

    private async Task<string> CollectOutputAsync(
        string args, string workingDir, CancellationToken ct)
    {
        var sb = new StringBuilder();
        await foreach (var line in _runner.RunAsync("dotnet", args, workingDir, ct))
        {
            sb.AppendLine(line.Text);
        }
        return sb.ToString();
    }

    private static List<DbContextInfo> ParseDbContextJson(string rawOutput)
    {
        var jsonStart = rawOutput.IndexOf('[');
        if (jsonStart < 0) return [];

        try
        {
            var json = rawOutput[jsonStart..];
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.EnumerateArray()
                .Select(e =>
                {
                    var fullName = e.GetProperty("fullName").GetString() ?? string.Empty;
                    return new DbContextInfo
                    {
                        FullName  = fullName,
                        ShortName = e.TryGetProperty("name", out var n)
                                    ? n.GetString() ?? ExtractShortName(fullName)
                                    : ExtractShortName(fullName),
                        Namespace = e.TryGetProperty("namespace", out var ns)
                                    ? ns.GetString()
                                    : null
                    };
                })
                .ToList();
        }
        catch { return []; }
    }

    private static string ExtractShortName(string fullName)
        => fullName.Contains('.') ? fullName[(fullName.LastIndexOf('.') + 1)..] : fullName;

    private static List<MigrationEntry> ParseMigrationsJson(string rawOutput)
    {
        var jsonStart = rawOutput.IndexOf('[');
        if (jsonStart < 0) return [];

        try
        {
            var json = rawOutput[jsonStart..];
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.EnumerateArray()
                .Select(e =>
                {
                    var name    = e.GetProperty("id").GetString() ?? string.Empty;
                    var applied = e.TryGetProperty("applied", out var a) && a.GetBoolean();
                    return new MigrationEntry
                    {
                        Name      = name,
                        Status    = applied ? MigrationStatus.Applied : MigrationStatus.Pending,
                        SafeName  = ExtractHumanName(name),
                        Timestamp = ExtractTimestamp(name)
                    };
                })
                .ToList();
        }
        catch { return []; }
    }

    private static string? ExtractHumanName(string migrationId)
    {
        var underscoreIdx = migrationId.IndexOf('_');
        return underscoreIdx > 0 ? migrationId[(underscoreIdx + 1)..] : migrationId;
    }

    private static DateTimeOffset? ExtractTimestamp(string migrationId)
    {
        var digits = new string(migrationId.TakeWhile(char.IsDigit).ToArray());
        if (digits.Length >= 14
            && DateTimeOffset.TryParseExact(digits[..14], "yyyyMMddHHmmss",
                   null, System.Globalization.DateTimeStyles.None, out var dt))
            return dt;
        return null;
    }

    private static List<DbContextInfo> DiscoverDbContextsFromProject(string projectPath)
    {
        var projectDir = Path.GetDirectoryName(projectPath);
        if (string.IsNullOrWhiteSpace(projectDir) || !Directory.Exists(projectDir))
            return [];

        var list = new List<DbContextInfo>();

        void ParseFile(string file)
        {
            string content;
            try
            {
                content = File.ReadAllText(file);
            }
            catch
            {
                return;
            }

            if (!content.Contains("DbContext", StringComparison.Ordinal))
                return;

            var ns = NamespaceRegex.Match(content);
            var nsValue = ns.Success ? ns.Groups["name"].Value : null;

            foreach (Match match in ClassDeclarationRegex.Matches(content))
            {
                var shortName = match.Groups["name"].Value;
                if (string.IsNullOrWhiteSpace(shortName)) continue;
                var bases = match.Groups["bases"].Success ? match.Groups["bases"].Value : string.Empty;
                if (!IsLikelyDbContext(shortName, bases)) continue;

                var fullName = string.IsNullOrWhiteSpace(nsValue)
                    ? shortName
                    : $"{nsValue}.{shortName}";

                list.Add(new DbContextInfo
                {
                    FullName = fullName,
                    ShortName = shortName,
                    Namespace = nsValue
                });
            }
        }

        static bool SkipBinObj(string file)
            => file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
               || file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase);

        // Prefer filenames containing DbContext — avoids reading thousands of unrelated .cs files.
        foreach (var file in Directory.EnumerateFiles(projectDir, "*DbContext*.cs", SearchOption.AllDirectories))
        {
            if (SkipBinObj(file)) continue;
            ParseFile(file);
        }

        if (list.Count == 0)
        {
            foreach (var file in Directory.EnumerateFiles(projectDir, "*.cs", SearchOption.AllDirectories))
            {
                if (SkipBinObj(file)) continue;
                if (Path.GetFileName(file).Contains("DbContext", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (!FileQuickContainsDbContext(file))
                    continue;

                ParseFile(file);
            }
        }

        return list
            .GroupBy(x => x.FullName, StringComparer.Ordinal)
            .Select(g => g.First())
            .OrderBy(x => x.ShortName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool FileQuickContainsDbContext(string path)
    {
        try
        {
            using var fs = File.OpenRead(path);
            var len = (int)Math.Min(fs.Length, 16384);
            if (len <= 0) return false;
            var buf = new byte[len];
            var read = fs.Read(buf, 0, len);
            if (read <= 0) return false;
            return System.Text.Encoding.UTF8.GetString(buf, 0, read).Contains("DbContext", StringComparison.Ordinal);
        }
        catch
        {
            return false;
        }
    }

    private static bool IsLikelyDbContext(string className, string baseList)
    {
        if (className.EndsWith("DbContext", StringComparison.Ordinal))
            return true;

        if (string.IsNullOrWhiteSpace(baseList))
            return false;

        var tokens = baseList
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(static t =>
            {
                var genericIdx = t.IndexOf('<');
                return genericIdx >= 0 ? t[..genericIdx] : t;
            })
            .Select(static t =>
            {
                var lastDot = t.LastIndexOf('.');
                return lastDot >= 0 ? t[(lastDot + 1)..] : t;
            });

        foreach (var token in tokens)
        {
            if (token.EndsWith("DbContext", StringComparison.Ordinal)
                || token.Equals("IdentityUserContext", StringComparison.Ordinal)
                || token.Equals("AbpDbContext", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static bool PathContainsMigrationsSegment(string path)
    {
        var normalized = path.Replace('/', Path.DirectorySeparatorChar);
        var sep = Path.DirectorySeparatorChar;
        return normalized.Contains($"{sep}Migrations{sep}", StringComparison.OrdinalIgnoreCase)
               || normalized.EndsWith($"{sep}Migrations", StringComparison.OrdinalIgnoreCase);
    }

    private static List<MigrationEntry> DiscoverMigrationsFromProject(string projectPath)
    {
        var projectDir = Path.GetDirectoryName(projectPath);
        if (string.IsNullOrWhiteSpace(projectDir) || !Directory.Exists(projectDir))
            return [];

        var result = new List<MigrationEntry>();

        foreach (var file in Directory.EnumerateFiles(projectDir, "*.cs", SearchOption.AllDirectories))
        {
            if (!PathContainsMigrationsSegment(file))
                continue;
            if (file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
                || file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
                continue;

            var fileName = Path.GetFileNameWithoutExtension(file);
            if (fileName.EndsWith(".Designer", StringComparison.OrdinalIgnoreCase)
                || fileName.EndsWith("ModelSnapshot", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var match = MigrationFileRegex.Match(fileName);
            if (!match.Success) continue;

            var id = match.Groups["id"].Value;
            result.Add(new MigrationEntry
            {
                Name = id,
                Status = MigrationStatus.Pending,
                SafeName = ExtractHumanName(id),
                Timestamp = ExtractTimestamp(id)
            });
        }

        return result
            .GroupBy(x => x.Name, StringComparer.Ordinal)
            .Select(g => g.First())
            .OrderBy(x => x.Name, StringComparer.Ordinal)
            .ToList();
    }
}
