namespace EfMigrationManager.Core.Services;

using EfMigrationManager.Core.Models;

public sealed class EfCommandBuilderService : IEfCommandBuilderService
{
    public string AddMigration(EfCommandOptions opts, string migrationName, bool verbose = true)
        => Build($"migrations add {migrationName}", opts, verbose);

    public string RemoveMigration(EfCommandOptions opts, bool force = false, bool verbose = true)
    {
        var extra = force ? " --force" : string.Empty;
        return Build($"migrations remove{extra}", opts, verbose);
    }

    public string UpdateDatabase(EfCommandOptions opts, string? targetMigration = null, bool verbose = true)
    {
        var target = targetMigration is not null ? $" {targetMigration}" : string.Empty;
        return Build($"database update{target}", opts, verbose);
    }

    public string DropDatabase(EfCommandOptions opts)
        => Build("database drop --force", opts, verbose: false);

    public string GenerateScript(
        EfCommandOptions opts,
        string? fromMigration = null,
        string? toMigration   = null,
        string? outputPath    = null,
        bool    idempotent    = true)
    {
        var sb = new System.Text.StringBuilder("migrations script");
        if (fromMigration is not null) sb.Append($" {fromMigration}");
        if (toMigration   is not null) sb.Append($" {toMigration}");
        if (idempotent)                sb.Append(" --idempotent");
        if (outputPath    is not null) sb.Append($" --output \"{outputPath}\"");
        return Build(sb.ToString(), opts, verbose: false);
    }

    public string FormatForDisplay(string args) => $"dotnet {args}";

    private static string Build(string command, EfCommandOptions opts, bool verbose)
    {
        var sb = new System.Text.StringBuilder($"ef {command}");
        sb.Append($" --project \"{opts.MigrationsProjectPath}\"");
        sb.Append($" --startup-project \"{opts.StartupProjectPath}\"");
        sb.Append($" --context \"{opts.ContextName}\"");
        if (verbose) sb.Append(" --verbose");
        return sb.ToString();
    }
}
