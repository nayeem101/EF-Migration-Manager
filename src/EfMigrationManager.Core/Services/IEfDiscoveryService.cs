namespace EfMigrationManager.Core.Services;

using EfMigrationManager.Core.Models;

public interface IEfDiscoveryService
{
    Task<List<DbContextInfo>> ListDbContextsAsync(
        EfCommandOptions options, CancellationToken ct = default);

    Task<List<MigrationEntry>> ListMigrationsAsync(
        EfCommandOptions options, CancellationToken ct = default);
}
