namespace EfMigrationManager.Core.Services;

using EfMigrationManager.Core.Models;

public interface IProcessRunnerService
{
    IAsyncEnumerable<ProcessLine> RunAsync(
        string executable,
        string arguments,
        string workingDirectory,
        CancellationToken ct = default);

    void OpenInTerminal(string command, string workingDirectory);
}
