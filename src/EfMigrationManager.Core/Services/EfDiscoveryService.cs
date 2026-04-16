namespace EfMigrationManager.Core.Services;

using System.Text;
using System.Text.Json;
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
        var args = BuildBaseArgs("dbcontext list", options) + " --json --no-color";
        var output = await CollectOutputAsync(args, options.WorkingDirectory, ct);

        if (EfErrorDetector.Detect(output) is { } err)
            throw new EfDiscoveryException(err);

        var result = ParseDbContextJson(output);
        if (result.Count == 0)
            throw new EfDiscoveryException(AppNotification.Warn(
                "No DbContexts found",
                "Verify the selected projects and that Microsoft.EntityFrameworkCore.Design is referenced."));

        return result;
    }

    public async Task<List<MigrationEntry>> ListMigrationsAsync(
        EfCommandOptions options, CancellationToken ct = default)
    {
        var args = BuildBaseArgs("migrations list", options) + " --json --no-color";
        var output = await CollectOutputAsync(args, options.WorkingDirectory, ct);

        if (EfErrorDetector.Detect(output) is { } err)
            throw new EfDiscoveryException(err);

        return ParseMigrationsJson(output);
    }

    private string BuildBaseArgs(string command, EfCommandOptions options)
        => $"ef {command}" +
           $" --project \"{options.MigrationsProjectPath}\"" +
           $" --startup-project \"{options.StartupProjectPath}\"" +
           $" --context \"{options.ContextName}\"";

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
}
