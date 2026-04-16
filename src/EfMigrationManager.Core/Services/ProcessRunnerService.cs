namespace EfMigrationManager.Core.Services;

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using EfMigrationManager.Core.Models;
using Microsoft.Extensions.Logging;

public sealed class ProcessRunnerService : IProcessRunnerService
{
    private readonly ILogger<ProcessRunnerService> _logger;

    public ProcessRunnerService(ILogger<ProcessRunnerService> logger)
        => _logger = logger;

    public async IAsyncEnumerable<ProcessLine> RunAsync(
        string executable,
        string arguments,
        string workingDirectory,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        _logger.LogInformation("Running: {Exe} {Args} in {Dir}", executable, arguments, workingDirectory);

        yield return ProcessLine.System($"> {executable} {arguments}");

        var channel = Channel.CreateUnbounded<ProcessLine>(
            new UnboundedChannelOptions { SingleReader = true });

        var psi = new ProcessStartInfo(executable, arguments)
        {
            WorkingDirectory       = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            UseShellExecute        = false,
            CreateNoWindow         = true,
        };

        psi.Environment["DOTNET_CLI_UI_LANGUAGE"] = "en";
        psi.Environment["DOTNET_NOLOGO"]          = "1";

        using var process = new Process { StartInfo = psi, EnableRaisingEvents = true };

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is not null)
                channel.Writer.TryWrite(ProcessLine.Out(e.Data));
        };

        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null)
                channel.Writer.TryWrite(ProcessLine.Error(e.Data));
        };

        process.Exited += (_, _) => channel.Writer.TryComplete();

        try
        {
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start process: {Exe}", executable);
            channel.Writer.TryComplete(ex);
        }

        await using var registration = ct.Register(() =>
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                    channel.Writer.TryWrite(ProcessLine.System("[Process cancelled by user]"));
                }
            }
            catch { /* process may have already exited */ }
        });

        await foreach (var line in channel.Reader.ReadAllAsync(CancellationToken.None))
        {
            ct.ThrowIfCancellationRequested();
            yield return line;
        }

        if (!process.HasExited)
            await process.WaitForExitAsync(CancellationToken.None);

        _logger.LogInformation("Process exited with code {Code}", process.ExitCode);
        yield return ProcessLine.System($"[Exit code: {process.ExitCode}]");
    }

    public void OpenInTerminal(string command, string workingDirectory)
    {
        try
        {
            Process.Start(new ProcessStartInfo("wt.exe", $"--startingDirectory \"{workingDirectory}\"")
            {
                UseShellExecute = true
            });
            return;
        }
        catch { /* Windows Terminal not installed */ }

        Process.Start(new ProcessStartInfo("cmd.exe", $"/K cd /d \"{workingDirectory}\" && {command}")
        {
            UseShellExecute = true
        });
    }
}
