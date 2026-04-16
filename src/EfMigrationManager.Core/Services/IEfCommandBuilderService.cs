namespace EfMigrationManager.Core.Services;

using EfMigrationManager.Core.Models;

public interface IEfCommandBuilderService
{
    string AddMigration(EfCommandOptions opts, string migrationName, bool verbose = true);
    string RemoveMigration(EfCommandOptions opts, bool force = false, bool verbose = true);
    string UpdateDatabase(EfCommandOptions opts, string? targetMigration = null, bool verbose = true);
    string DropDatabase(EfCommandOptions opts);
    string GenerateScript(EfCommandOptions opts, string? fromMigration = null, string? toMigration = null, string? outputPath = null, bool idempotent = true);

    string FormatForDisplay(string args);
}
