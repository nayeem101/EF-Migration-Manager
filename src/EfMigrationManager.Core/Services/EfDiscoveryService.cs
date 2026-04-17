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
    private static readonly Regex DbContextClassRegex = new(
        @"class\s+(?<name>\w+)\s*:\s*[^;{]*\b(DbContext|IdentityDbContext|IdentityUserContext)\b",
        RegexOptions.Compiled);

    private static readonly Regex NamespaceRegex = new(
        @"^\s*namespace\s+(?<name>[A-Za-z0-9_.]+)\s*;?",
        RegexOptions.Compiled | RegexOptions.Multiline);

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
        var fallback = DiscoverDbContextsFromProject(options.MigrationsProjectPath);

        try
        {
            var args = BuildBaseArgs("dbcontext list", options, includeContext: false) + " --json --no-color";
            var output = await CollectOutputAsync(args, options.WorkingDirectory, ct);

            if (EfErrorDetector.Detect(output) is not null)
                return fallback.Count > 0 ? fallback : throw new EfDiscoveryException(EfErrorDetector.Detect(output)!);

            var result = ParseDbContextJson(output);
            return result.Count > 0 ? result : fallback;
        }
        catch (EfDiscoveryException)
        {
            if (fallback.Count > 0) return fallback;
            throw;
        }
        catch (Exception)
        {
            if (fallback.Count > 0) return fallback;
            throw;
        }
    }

    public async Task<List<MigrationEntry>> ListMigrationsAsync(
        EfCommandOptions options, CancellationToken ct = default)
    {
        var args = BuildBaseArgs("migrations list", options, includeContext: true) + " --json --no-color";
        var output = await CollectOutputAsync(args, options.WorkingDirectory, ct);

        if (EfErrorDetector.Detect(output) is { } err)
            throw new EfDiscoveryException(err);

        return ParseMigrationsJson(output);
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

    private static string ExtractShortName(string fullName)
        => fullName.Contains('.') ? fullName[(fullName.LastIndexOf('.') + 1)..] : fullName;

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

        foreach (var file in Directory.EnumerateFiles(projectDir, "*.cs", SearchOption.AllDirectories))
        {
            if (file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
                || file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string content;
            try
            {
                content = File.ReadAllText(file);
            }
            catch
            {
                continue;
            }

            var ns = NamespaceRegex.Match(content);
            var nsValue = ns.Success ? ns.Groups["name"].Value : null;

            foreach (Match match in DbContextClassRegex.Matches(content))
            {
                var shortName = match.Groups["name"].Value;
                if (string.IsNullOrWhiteSpace(shortName)) continue;

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

        return list
            .GroupBy(x => x.FullName, StringComparer.Ordinal)
            .Select(g => g.First())
            .OrderBy(x => x.ShortName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
