namespace EfMigrationManager.Core.Services;

using EfMigrationManager.Core.Models;

public interface ISolutionParserService
{
    bool IsSupported(string filePath);
    Task<SolutionInfo> ParseAsync(string solutionPath, CancellationToken ct = default);
}
