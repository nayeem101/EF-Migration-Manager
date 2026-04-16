# EF Core Migration Manager — Complete Implementation Plan

> **For AI Coding Assistants:** This document is a complete, ordered implementation plan.
> Follow each phase sequentially. Do not skip ahead. Each phase builds on the previous one.
> All architectural decisions are final — do not substitute alternatives unless explicitly noted.

---

## Project Overview

A standalone Windows desktop GUI application (.NET 10 / WPF) that wraps the `dotnet ef` CLI,
providing the same migration management experience as JetBrains Rider's EF Core panel —
without needing Rider open. Targets modular monolith / clean architecture solutions with
multiple DbContexts and multiple projects.

**Core value:** The user selects a `.sln` or `.slnx` file, the app discovers all EF-capable
projects and DbContexts, and lets them run any EF migration command via GUI — with full
real-time terminal output shown inline.

---

## Tech Stack (Final, Do Not Change)

| Concern | Choice | Version |
|---|---|---|
| Runtime | .NET 10 | `net10.0-windows` |
| UI Framework | WPF | inbox with .NET 10 |
| UI Component Library | WPF-UI (wpfui) | 4.x |
| MVVM | CommunityToolkit.Mvvm | 8.4.x |
| DI Container | Microsoft.Extensions.DependencyInjection | 10.x |
| App Host | Microsoft.Extensions.Hosting | 10.x |
| Solution Parsing (.sln) | Microsoft.Build + Microsoft.Build.Locator | 17.14.x |
| Solution Parsing (.slnx) | System.Xml.Linq (inbox) | — |
| Settings Persistence | System.Text.Json (inbox) | — |
| Logging | Microsoft.Extensions.Logging + Serilog | Serilog 4.x |
| Serilog Sinks | Serilog.Sinks.File | latest |
| Nullable | enabled | — |
| ImplicitUsings | enabled | — |

---

## Solution Structure

```
EfMigrationManager/
├── EfMigrationManager.sln (or .slnx)
│
├── src/
│   ├── EfMigrationManager.App/          ← WPF application project
│   │   ├── EfMigrationManager.App.csproj
│   │   ├── App.xaml
│   │   ├── App.xaml.cs
│   │   ├── app.manifest
│   │   ├── Assets/
│   │   │   └── app-icon.ico
│   │   ├── Views/
│   │   │   ├── MainWindow.xaml
│   │   │   ├── MainWindow.xaml.cs
│   │   │   └── Dialogs/
│   │   │       ├── AddMigrationDialog.xaml
│   │   │       ├── AddMigrationDialog.xaml.cs
│   │   │       ├── GenerateScriptDialog.xaml
│   │   │       ├── GenerateScriptDialog.xaml.cs
│   │   │       ├── UpdateDatabaseDialog.xaml
│   │   │       └── UpdateDatabaseDialog.xaml.cs
│   │   ├── ViewModels/
│   │   │   ├── MainViewModel.cs
│   │   │   ├── SolutionPanelViewModel.cs
│   │   │   ├── MigrationPanelViewModel.cs
│   │   │   ├── TerminalViewModel.cs
│   │   │   └── Dialogs/
│   │   │       ├── AddMigrationDialogViewModel.cs
│   │   │       ├── GenerateScriptDialogViewModel.cs
│   │   │       └── UpdateDatabaseDialogViewModel.cs
│   │   ├── Converters/
│   │   │   ├── BoolToVisibilityConverter.cs
│   │   │   ├── InverseBoolConverter.cs
│   │   │   ├── MigrationStatusToIconConverter.cs
│   │   │   └── NullToVisibilityConverter.cs
│   │   └── Behaviors/
│   │       └── AutoScrollBehavior.cs
│   │
│   └── EfMigrationManager.Core/         ← business logic, no WPF dependency
│       ├── EfMigrationManager.Core.csproj
│       ├── Models/
│       │   ├── SolutionInfo.cs
│       │   ├── ProjectInfo.cs
│       │   ├── DbContextInfo.cs
│       │   ├── MigrationEntry.cs
│       │   ├── EfCommandOptions.cs
│       │   ├── ProcessLine.cs
│       │   └── AppSettings.cs
│       ├── Services/
│       │   ├── ISolutionParserService.cs
│       │   ├── SolutionParserService.cs
│       │   ├── IEfDiscoveryService.cs
│       │   ├── EfDiscoveryService.cs
│       │   ├── IEfCommandBuilderService.cs
│       │   ├── EfCommandBuilderService.cs
│       │   ├── IProcessRunnerService.cs
│       │   ├── ProcessRunnerService.cs
│       │   ├── ISettingsService.cs
│       │   └── SettingsService.cs
│       └── Helpers/
│           ├── AnsiHelper.cs
│           └── PathHelper.cs
```

---

## Phase 1 — Project Scaffolding & Infrastructure

### Step 1.1 — Create the Solution

```bash
mkdir EfMigrationManager && cd EfMigrationManager
dotnet new sln -n EfMigrationManager
mkdir -p src/EfMigrationManager.Core
mkdir -p src/EfMigrationManager.App
dotnet new classlib -n EfMigrationManager.Core -f net10.0 -o src/EfMigrationManager.Core
dotnet new wpf -n EfMigrationManager.App -f net10.0-windows -o src/EfMigrationManager.App
dotnet sln add src/EfMigrationManager.Core/EfMigrationManager.Core.csproj
dotnet sln add src/EfMigrationManager.App/EfMigrationManager.App.csproj
```

### Step 1.2 — Core Project File

`src/EfMigrationManager.Core/EfMigrationManager.Core.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <LangVersion>latest</LangVersion>
    <RootNamespace>EfMigrationManager.Core</RootNamespace>
    <AssemblyName>EfMigrationManager.Core</AssemblyName>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.Build" Version="17.14.*" ExcludeAssets="runtime" />
    <PackageReference Include="Microsoft.Build.Locator" Version="1.7.*" />
    <PackageReference Include="Microsoft.Extensions.Logging.Abstractions" Version="10.*" />
    <PackageReference Include="CommunityToolkit.Mvvm" Version="8.4.*" />
  </ItemGroup>
</Project>
```

> **Important:** `Microsoft.Build` must have `ExcludeAssets="runtime"` to avoid
> shipping MSBuild binaries. The `MSBuildLocator` will find the installed SDK at runtime.

### Step 1.3 — App Project File

`src/EfMigrationManager.App/EfMigrationManager.App.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net10.0-windows</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <LangVersion>latest</LangVersion>
    <UseWPF>true</UseWPF>
    <RootNamespace>EfMigrationManager.App</RootNamespace>
    <AssemblyName>EfMigrationManager</AssemblyName>
    <ApplicationIcon>Assets\app-icon.ico</ApplicationIcon>
    <ApplicationManifest>app.manifest</ApplicationManifest>
    <StartupObject>EfMigrationManager.App.App</StartupObject>
    <!-- Single-file publish support -->
    <PublishSingleFile>true</PublishSingleFile>
    <SelfContained>true</SelfContained>
    <RuntimeIdentifier>win-x64</RuntimeIdentifier>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="WPF-UI" Version="4.*" />
    <PackageReference Include="CommunityToolkit.Mvvm" Version="8.4.*" />
    <PackageReference Include="Microsoft.Extensions.Hosting" Version="10.*" />
    <PackageReference Include="Serilog" Version="4.*" />
    <PackageReference Include="Serilog.Extensions.Logging" Version="9.*" />
    <PackageReference Include="Serilog.Sinks.File" Version="6.*" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\EfMigrationManager.Core\EfMigrationManager.Core.csproj" />
  </ItemGroup>
</Project>
```

### Step 1.4 — App Manifest

`src/EfMigrationManager.App/app.manifest`:

```xml
<?xml version="1.0" encoding="utf-8"?>
<assembly manifestVersion="1.0" xmlns="urn:schemas-microsoft-com:asm.v1">
  <assemblyIdentity version="1.0.0.0" name="EfMigrationManager.App"/>
  <application xmlns="urn:schemas-microsoft-com:asm.v3">
    <windowsSettings>
      <dpiAwareness xmlns="http://schemas.microsoft.com/SMI/2016/WindowsSettings">PerMonitorV2</dpiAwareness>
    </windowsSettings>
  </application>
  <compatibility xmlns="urn:schemas-microsoft-com:compatibility.v1">
    <application>
      <supportedOS Id="{8e0f7a12-bfb3-4fe8-b9a5-48fd50a15a9a}"/>
    </application>
  </compatibility>
</assembly>
```

---

## Phase 2 — Core Models

Create all models before any services. These are plain records/classes with no logic.

### `Models/SolutionInfo.cs`

```csharp
namespace EfMigrationManager.Core.Models;

public enum SolutionFormat { Sln, Slnx }

public sealed class SolutionInfo
{
    public required string         Path     { get; init; }
    public required string         Name     { get; init; }
    public required SolutionFormat Format   { get; init; }
    public required List<ProjectInfo> Projects { get; init; }

    public string DirectoryPath  => System.IO.Path.GetDirectoryName(Path)!;
    public string DisplayName    => $"{Name} ({(Format == SolutionFormat.Slnx ? ".slnx" : ".sln")})";
}
```

### `Models/ProjectInfo.cs`

```csharp
namespace EfMigrationManager.Core.Models;

public sealed class ProjectInfo
{
    public required string Name            { get; init; }
    public required string AbsolutePath    { get; init; }
    public required string RelativePath    { get; init; }
    /// <summary>Has OutputType=Exe — candidate for startup project</summary>
    public required bool   IsExecutable    { get; init; }
    /// <summary>References any EntityFrameworkCore package</summary>
    public required bool   HasEfCore       { get; init; }
    /// <summary>References Microsoft.EntityFrameworkCore.Design — migrations project candidate</summary>
    public required bool   HasEfDesign     { get; init; }

    public string DirectoryPath => System.IO.Path.GetDirectoryName(AbsolutePath)!;

    public override string ToString() => Name;
}
```

### `Models/DbContextInfo.cs`

```csharp
namespace EfMigrationManager.Core.Models;

public sealed class DbContextInfo
{
    public required string FullName      { get; init; }  // e.g. "Catalog.Infrastructure.CatalogDbContext"
    public required string ShortName     { get; init; }  // e.g. "CatalogDbContext"
    public required string? Namespace    { get; init; }

    public override string ToString() => ShortName;
}
```

### `Models/MigrationEntry.cs`

```csharp
namespace EfMigrationManager.Core.Models;

public enum MigrationStatus { Applied, Pending }

public sealed class MigrationEntry
{
    public required string          Name      { get; init; }   // raw name e.g. "20240101120000_InitialCreate"
    public required MigrationStatus Status    { get; init; }
    public required string?         SafeName  { get; init; }   // human part e.g. "InitialCreate"
    public required DateTimeOffset? Timestamp { get; init; }   // parsed from prefix if numeric

    public string StatusIcon => Status == MigrationStatus.Applied ? "✅" : "🟡";

    public override string ToString() => Name;
}
```

### `Models/EfCommandOptions.cs`

```csharp
namespace EfMigrationManager.Core.Models;

/// <summary>
/// Captures the three project-level inputs required by every dotnet ef command.
/// </summary>
public sealed class EfCommandOptions
{
    public required string StartupProjectPath    { get; init; }  // absolute path to .csproj
    public required string MigrationsProjectPath { get; init; }  // absolute path to .csproj
    public required string ContextName           { get; init; }  // short or full class name

    /// <summary>Working directory — always the solution directory.</summary>
    public required string WorkingDirectory      { get; init; }
}
```

### `Models/ProcessLine.cs`

```csharp
namespace EfMigrationManager.Core.Models;

public enum ProcessLineKind { StdOut, StdErr, System }

public sealed class ProcessLine
{
    public required string          Text      { get; init; }
    public required ProcessLineKind Kind      { get; init; }
    public required DateTimeOffset  Timestamp { get; init; }

    public static ProcessLine Out(string text)    => new() { Text = text, Kind = ProcessLineKind.StdOut,  Timestamp = DateTimeOffset.Now };
    public static ProcessLine Error(string text)  => new() { Text = text, Kind = ProcessLineKind.StdErr,  Timestamp = DateTimeOffset.Now };
    public static ProcessLine System(string text) => new() { Text = text, Kind = ProcessLineKind.System,  Timestamp = DateTimeOffset.Now };
}
```

### `Models/AppSettings.cs`

```csharp
namespace EfMigrationManager.Core.Models;

public sealed class AppSettings
{
    public List<string>                          RecentSolutions  { get; set; } = [];
    public Dictionary<string, SolutionSettings> PerSolution      { get; set; } = [];
    public AppearanceSettings                    Appearance       { get; set; } = new();
}

public sealed class SolutionSettings
{
    public string? LastStartupProjectPath    { get; set; }
    public string? LastMigrationsProjectPath { get; set; }
    public string? LastContextName           { get; set; }
}

public sealed class AppearanceSettings
{
    public string Theme { get; set; } = "Dark";   // "Dark" | "Light"
}
```

---

## Phase 3 — Core Services

Implement services in this exact order (each depends on the previous ones being stable).

### Step 3.1 — `ISettingsService` / `SettingsService`

**Interface** (`Services/ISettingsService.cs`):

```csharp
namespace EfMigrationManager.Core.Services;

using EfMigrationManager.Core.Models;

public interface ISettingsService
{
    AppSettings Settings { get; }
    void Save();
    void AddRecentSolution(string path);
    void SaveSolutionSettings(string solutionPath, SolutionSettings settings);
    SolutionSettings GetSolutionSettings(string solutionPath);
}
```

**Implementation** (`Services/SettingsService.cs`):

```csharp
namespace EfMigrationManager.Core.Services;

using System.Text.Json;
using EfMigrationManager.Core.Models;
using Microsoft.Extensions.Logging;

public sealed class SettingsService : ISettingsService
{
    private readonly string _filePath;
    private readonly ILogger<SettingsService> _logger;
    private const int MaxRecentSolutions = 10;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public AppSettings Settings { get; private set; }

    public SettingsService(ILogger<SettingsService> logger)
    {
        _logger = logger;
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var dir = Path.Combine(appData, "EfMigrationManager");
        Directory.CreateDirectory(dir);
        _filePath = Path.Combine(dir, "settings.json");
        Settings = Load();
    }

    private AppSettings Load()
    {
        try
        {
            if (!File.Exists(_filePath)) return new AppSettings();
            var json = File.ReadAllText(_filePath);
            return JsonSerializer.Deserialize<AppSettings>(json, _jsonOptions) ?? new AppSettings();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load settings, using defaults");
            return new AppSettings();
        }
    }

    public void Save()
    {
        try
        {
            var json = JsonSerializer.Serialize(Settings, _jsonOptions);
            File.WriteAllText(_filePath, json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save settings");
        }
    }

    public void AddRecentSolution(string path)
    {
        Settings.RecentSolutions.Remove(path);                     // remove if already present
        Settings.RecentSolutions.Insert(0, path);                  // add at top
        if (Settings.RecentSolutions.Count > MaxRecentSolutions)
            Settings.RecentSolutions.RemoveRange(MaxRecentSolutions,
                Settings.RecentSolutions.Count - MaxRecentSolutions);
        Save();
    }

    public void SaveSolutionSettings(string solutionPath, SolutionSettings settings)
    {
        Settings.PerSolution[solutionPath] = settings;
        Save();
    }

    public SolutionSettings GetSolutionSettings(string solutionPath)
        => Settings.PerSolution.TryGetValue(solutionPath, out var s) ? s : new SolutionSettings();
}
```

### Step 3.2 — `ISolutionParserService` / `SolutionParserService`

**Interface** (`Services/ISolutionParserService.cs`):

```csharp
namespace EfMigrationManager.Core.Services;

using EfMigrationManager.Core.Models;

public interface ISolutionParserService
{
    bool IsSupported(string filePath);
    Task<SolutionInfo> ParseAsync(string solutionPath, CancellationToken ct = default);
}
```

**Implementation** (`Services/SolutionParserService.cs`):

```csharp
namespace EfMigrationManager.Core.Services;

using System.Xml.Linq;
using EfMigrationManager.Core.Models;
using Microsoft.Build.Construction;
using Microsoft.Extensions.Logging;

public sealed class SolutionParserService : ISolutionParserService
{
    private readonly ILogger<SolutionParserService> _logger;

    public SolutionParserService(ILogger<SolutionParserService> logger)
        => _logger = logger;

    public bool IsSupported(string filePath)
        => Path.GetExtension(filePath).ToLowerInvariant() is ".sln" or ".slnx";

    public Task<SolutionInfo> ParseAsync(string solutionPath, CancellationToken ct = default)
    {
        _logger.LogInformation("Parsing solution: {Path}", solutionPath);

        return Path.GetExtension(solutionPath).ToLowerInvariant() switch
        {
            ".slnx" => Task.FromResult(ParseSlnx(solutionPath)),
            ".sln"  => Task.FromResult(ParseSln(solutionPath)),
            _       => throw new NotSupportedException($"Unknown solution format: {solutionPath}")
        };
    }

    // ─── .slnx ───────────────────────────────────────────────────────────────

    private SolutionInfo ParseSlnx(string path)
    {
        var solutionDir = Path.GetDirectoryName(path)!;
        var root = XDocument.Load(path).Root
                   ?? throw new InvalidDataException(".slnx has no root element");

        // Recurse all <Project Path="..."> elements regardless of nesting depth
        var projects = root
            .Descendants("Project")
            .Select(e => e.Attribute("Path")?.Value)
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(relativePath =>
            {
                var normalized = relativePath!.Replace('/', Path.DirectorySeparatorChar);
                var fullPath   = Path.GetFullPath(Path.Combine(solutionDir, normalized));
                return BuildProjectInfo(fullPath, solutionDir);
            })
            .OfType<ProjectInfo>()
            .ToList();

        _logger.LogInformation("Parsed .slnx: {Count} projects found", projects.Count);

        return new SolutionInfo
        {
            Path     = path,
            Name     = Path.GetFileNameWithoutExtension(path),
            Format   = SolutionFormat.Slnx,
            Projects = projects
        };
    }

    // ─── .sln ────────────────────────────────────────────────────────────────

    private SolutionInfo ParseSln(string path)
    {
        var solutionDir  = Path.GetDirectoryName(path)!;
        var solutionFile = SolutionFile.Parse(path);

        var projects = solutionFile.ProjectsInOrder
            .Where(p => p.ProjectType == SolutionProjectType.KnownToBeMSBuildFormat)
            .Select(p => BuildProjectInfo(p.AbsolutePath, solutionDir))
            .OfType<ProjectInfo>()
            .ToList();

        _logger.LogInformation("Parsed .sln: {Count} projects found", projects.Count);

        return new SolutionInfo
        {
            Path     = path,
            Name     = Path.GetFileNameWithoutExtension(path),
            Format   = SolutionFormat.Sln,
            Projects = projects
        };
    }

    // ─── Shared ──────────────────────────────────────────────────────────────

    private ProjectInfo? BuildProjectInfo(string csprojPath, string solutionDir)
    {
        if (!File.Exists(csprojPath))
        {
            _logger.LogWarning("Project file not found: {Path}", csprojPath);
            return null;
        }

        string content;
        try   { content = File.ReadAllText(csprojPath); }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not read project file: {Path}", csprojPath);
            return null;
        }

        return new ProjectInfo
        {
            Name          = Path.GetFileNameWithoutExtension(csprojPath),
            AbsolutePath  = csprojPath,
            RelativePath  = Path.GetRelativePath(solutionDir, csprojPath),
            IsExecutable  = content.Contains("<OutputType>Exe</OutputType>",
                                StringComparison.OrdinalIgnoreCase),
            HasEfCore     = content.Contains("EntityFrameworkCore",
                                StringComparison.OrdinalIgnoreCase),
            HasEfDesign   = content.Contains("EntityFrameworkCore.Design",
                                StringComparison.OrdinalIgnoreCase),
        };
    }
}
```

### Step 3.3 — `IProcessRunnerService` / `ProcessRunnerService`

This is the most critical service. It runs external processes and streams their output
as an async enumerable. It must handle stdout and stderr concurrently.

**Interface** (`Services/IProcessRunnerService.cs`):

```csharp
namespace EfMigrationManager.Core.Services;

using EfMigrationManager.Core.Models;

public interface IProcessRunnerService
{
    IAsyncEnumerable<ProcessLine> RunAsync(
        string executable,
        string arguments,
        string workingDirectory,
        CancellationToken ct = default);

    /// <summary>
    /// Opens the user's default terminal (Windows Terminal, then cmd fallback)
    /// with the given command pre-filled but NOT executed.
    /// </summary>
    void OpenInTerminal(string command, string workingDirectory);
}
```

**Implementation** (`Services/ProcessRunnerService.cs`):

```csharp
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

        // Yield the command as a system line first so the UI can show what was executed
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

        // Force English output from the dotnet CLI — critical for reliable parsing
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

        // Register cancellation — kill process if token fires
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

        // Wait for actual exit (already happened since channel is complete, but needed for ExitCode)
        if (!process.HasExited)
            await process.WaitForExitAsync(CancellationToken.None);

        _logger.LogInformation("Process exited with code {Code}", process.ExitCode);
        yield return ProcessLine.System($"[Exit code: {process.ExitCode}]");
    }

    public void OpenInTerminal(string command, string workingDirectory)
    {
        // Try Windows Terminal first, fall back to cmd
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
```

### Step 3.4 — `IEfDiscoveryService` / `EfDiscoveryService`

Runs `dotnet ef` commands to discover DbContexts and list migrations.
Always uses `--json` flag for structured output.

**Interface** (`Services/IEfDiscoveryService.cs`):

```csharp
namespace EfMigrationManager.Core.Services;

using EfMigrationManager.Core.Models;

public interface IEfDiscoveryService
{
    Task<List<DbContextInfo>> ListDbContextsAsync(
        EfCommandOptions options, CancellationToken ct = default);

    Task<List<MigrationEntry>> ListMigrationsAsync(
        EfCommandOptions options, CancellationToken ct = default);
}
```

**Implementation** (`Services/EfDiscoveryService.cs`):

```csharp
namespace EfMigrationManager.Core.Services;

using System.Text;
using System.Text.Json;
using EfMigrationManager.Core.Models;
using Microsoft.Extensions.Logging;

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

        return ParseDbContextJson(output);
    }

    public async Task<List<MigrationEntry>> ListMigrationsAsync(
        EfCommandOptions options, CancellationToken ct = default)
    {
        var args = BuildBaseArgs("migrations list", options) + " --json --no-color";
        var output = await CollectOutputAsync(args, options.WorkingDirectory, ct);

        return ParseMigrationsJson(output);
    }

    // ─── Private Helpers ─────────────────────────────────────────────────────

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
            if (line.Kind == ProcessLineKind.StdOut)
                sb.AppendLine(line.Text);
        }
        return sb.ToString();
    }

    private static List<DbContextInfo> ParseDbContextJson(string rawOutput)
    {
        // dotnet ef --json outputs a JSON array, but may have build output before it.
        // Find the first '[' to skip any preceding text.
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
        // Migration IDs are typically: 20240101120000_InitialCreate
        var underscoreIdx = migrationId.IndexOf('_');
        return underscoreIdx > 0 ? migrationId[(underscoreIdx + 1)..] : migrationId;
    }

    private static DateTimeOffset? ExtractTimestamp(string migrationId)
    {
        // Extract leading digits (14 for yyyyMMddHHmmss)
        var digits = new string(migrationId.TakeWhile(char.IsDigit).ToArray());
        if (digits.Length >= 14
            && DateTimeOffset.TryParseExact(digits[..14], "yyyyMMddHHmmss",
                   null, System.Globalization.DateTimeStyles.None, out var dt))
            return dt;
        return null;
    }
}
```

### Step 3.5 — `IEfCommandBuilderService` / `EfCommandBuilderService`

Builds the `dotnet ef` CLI argument strings for each user action.

**Interface** (`Services/IEfCommandBuilderService.cs`):

```csharp
namespace EfMigrationManager.Core.Services;

using EfMigrationManager.Core.Models;

public interface IEfCommandBuilderService
{
    string AddMigration(EfCommandOptions opts, string migrationName, bool verbose = true);
    string RemoveMigration(EfCommandOptions opts, bool force = false, bool verbose = true);
    string UpdateDatabase(EfCommandOptions opts, string? targetMigration = null, bool verbose = true);
    string DropDatabase(EfCommandOptions opts);
    string GenerateScript(EfCommandOptions opts, string? fromMigration = null, string? toMigration = null, string? outputPath = null, bool idempotent = true);

    /// <summary>Returns the full "dotnet ef ..." string for display in the terminal header.</summary>
    string FormatForDisplay(string args);
}
```

**Implementation** (`Services/EfCommandBuilderService.cs`):

```csharp
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

    // ─── Internal Builder ────────────────────────────────────────────────────

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
```

### Step 3.6 — Helper Classes

**`Helpers/AnsiHelper.cs`** — strips ANSI escape codes from terminal output:

```csharp
namespace EfMigrationManager.Core.Helpers;

using System.Text.RegularExpressions;

public static partial class AnsiHelper
{
    [GeneratedRegex(@"\x1B\[[0-9;]*[a-zA-Z]", RegexOptions.Compiled)]
    private static partial Regex AnsiPattern();

    public static string Strip(string input) => AnsiPattern().Replace(input, string.Empty);

    /// <summary>
    /// Attempts to map ANSI foreground color codes to a WPF color name.
    /// Returns null if no color detected.
    /// </summary>
    public static string? ExtractColor(string rawLine)
    {
        if (rawLine.Contains("\x1B[31m") || rawLine.Contains("\x1B[91m")) return "Red";      // error
        if (rawLine.Contains("\x1B[33m") || rawLine.Contains("\x1B[93m")) return "Yellow";   // warning
        if (rawLine.Contains("\x1B[32m") || rawLine.Contains("\x1B[92m")) return "LimeGreen"; // success
        if (rawLine.Contains("\x1B[36m") || rawLine.Contains("\x1B[96m")) return "Cyan";     // info
        return null;
    }
}
```

**`Helpers/PathHelper.cs`**:

```csharp
namespace EfMigrationManager.Core.Helpers;

public static class PathHelper
{
    public static string ToRelativeDisplay(string absolutePath, string basePath)
    {
        var rel = Path.GetRelativePath(basePath, absolutePath);
        return rel.StartsWith("..") ? absolutePath : rel;
    }

    public static bool IsSolutionFile(string path)
        => Path.GetExtension(path).ToLowerInvariant() is ".sln" or ".slnx";
}
```

---

## Phase 4 — Application Host & DI

### Step 4.1 — `App.xaml`

```xml
<Application
    x:Class="EfMigrationManager.App.App"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    xmlns:ui="http://schemas.lepo.co/wpfui/2022/xaml">
    <Application.Resources>
        <ResourceDictionary>
            <ResourceDictionary.MergedDictionaries>
                <ui:ThemesDictionary Theme="Dark" />
                <ui:ControlsDictionary />
            </ResourceDictionary.MergedDictionaries>
        </ResourceDictionary>
    </Application.Resources>
</Application>
```

### Step 4.2 — `App.xaml.cs`

```csharp
namespace EfMigrationManager.App;

using System.Windows;
using EfMigrationManager.App.ViewModels;
using EfMigrationManager.App.Views;
using EfMigrationManager.Core.Services;
using Microsoft.Build.Locator;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;

public partial class App : Application
{
    private IHost? _host;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // MSBuildLocator MUST be registered before any Microsoft.Build types are loaded
        if (!MSBuildLocator.IsRegistered)
            MSBuildLocator.RegisterDefaults();

        // Configure Serilog
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.File(
                Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "EfMigrationManager", "logs", "app-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7)
            .CreateLogger();

        _host = Host.CreateDefaultBuilder()
            .UseSerilog()
            .ConfigureServices(ConfigureServices)
            .Build();

        await _host.StartAsync();

        var mainWindow = _host.Services.GetRequiredService<MainWindow>();
        mainWindow.Show();
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // Core services
        services.AddSingleton<ISettingsService,       SettingsService>();
        services.AddSingleton<ISolutionParserService, SolutionParserService>();
        services.AddSingleton<IProcessRunnerService,  ProcessRunnerService>();
        services.AddSingleton<IEfDiscoveryService,    EfDiscoveryService>();
        services.AddSingleton<IEfCommandBuilderService, EfCommandBuilderService>();

        // ViewModels
        services.AddTransient<MainViewModel>();
        services.AddTransient<SolutionPanelViewModel>();
        services.AddTransient<MigrationPanelViewModel>();
        services.AddTransient<TerminalViewModel>();

        // Views
        services.AddTransient<MainWindow>();
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_host is not null)
        {
            await _host.StopAsync();
            _host.Dispose();
        }
        Log.CloseAndFlush();
        base.OnExit(e);
    }
}
```

---

## Phase 5 — ViewModels

### Step 5.1 — `TerminalViewModel`

Manages all terminal output. Must be thread-safe (Process events fire on thread pool).

```csharp
namespace EfMigrationManager.App.ViewModels;

using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EfMigrationManager.Core.Helpers;
using EfMigrationManager.Core.Models;

public sealed partial class TerminalViewModel : ObservableObject
{
    [ObservableProperty] private bool _isRunning;
    [ObservableProperty] private string _lastCommand = string.Empty;

    // Raw lines backing collection — updated on UI thread via Dispatcher
    public ObservableCollection<TerminalLine> Lines { get; } = [];

    private string _lastWorkingDirectory = string.Empty;

    public void AppendLine(ProcessLine line)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            var color = AnsiHelper.ExtractColor(line.Text);
            var clean = AnsiHelper.Strip(line.Text);

            // Skip empty lines from build output noise
            if (string.IsNullOrWhiteSpace(clean) && Lines.Count > 0
                && string.IsNullOrWhiteSpace(Lines.Last().Text))
                return;

            Lines.Add(new TerminalLine
            {
                Text      = clean,
                ColorHint = color ?? (line.Kind == ProcessLineKind.StdErr ? "Red" : null),
                IsSystem  = line.Kind == ProcessLineKind.System
            });
        });
    }

    public void BeginCommand(string command, string workingDirectory)
    {
        _lastCommand           = command;
        _lastWorkingDirectory  = workingDirectory;
        IsRunning              = true;

        Application.Current.Dispatcher.Invoke(() =>
        {
            if (Lines.Count > 0)
                Lines.Add(new TerminalLine { Text = new string('─', 60), IsSystem = true });
        });
    }

    public void EndCommand() => IsRunning = false;

    [RelayCommand]
    private void Clear() => Lines.Clear();

    [RelayCommand]
    private void CopyAll()
    {
        var text = string.Join(Environment.NewLine, Lines.Select(l => l.Text));
        Clipboard.SetText(text);
    }

    [RelayCommand]
    private void OpenInTerminal(IProcessRunnerService? runner)
        => runner?.OpenInTerminal(_lastCommand, _lastWorkingDirectory);
}

public sealed class TerminalLine
{
    public required string  Text      { get; init; }
    public string?          ColorHint { get; init; }  // null = default foreground
    public bool             IsSystem  { get; init; }
}
```

### Step 5.2 — `SolutionPanelViewModel`

Handles solution loading and project list display.

```csharp
namespace EfMigrationManager.App.ViewModels;

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EfMigrationManager.Core.Models;
using EfMigrationManager.Core.Services;
using Microsoft.Win32;

public sealed partial class SolutionPanelViewModel : ObservableObject
{
    private readonly ISolutionParserService _parser;
    private readonly ISettingsService       _settings;

    [ObservableProperty] private SolutionInfo? _currentSolution;
    [ObservableProperty] private bool          _isLoading;
    [ObservableProperty] private string?       _errorMessage;

    public ObservableCollection<string>      RecentSolutions { get; } = [];
    public ObservableCollection<ProjectInfo> EfProjects      { get; } = [];

    /// <summary>Raised when a solution is loaded so parent VM can react.</summary>
    public event Action<SolutionInfo>? SolutionLoaded;

    public SolutionPanelViewModel(
        ISolutionParserService parser,
        ISettingsService       settings)
    {
        _parser   = parser;
        _settings = settings;

        foreach (var s in settings.Settings.RecentSolutions)
            RecentSolutions.Add(s);
    }

    [RelayCommand]
    private async Task BrowseAsync()
    {
        var dialog = new OpenFileDialog
        {
            Title       = "Open Solution",
            Filter      = "Solution Files|*.sln;*.slnx|Legacy Solution (*.sln)|*.sln|XML Solution (*.slnx)|*.slnx",
            FilterIndex = 1,
        };

        if (dialog.ShowDialog() != true) return;
        await LoadSolutionAsync(dialog.FileName);
    }

    [RelayCommand]
    private async Task OpenRecentAsync(string path)
        => await LoadSolutionAsync(path);

    private async Task LoadSolutionAsync(string path)
    {
        ErrorMessage = null;
        IsLoading    = true;

        try
        {
            var solution = await _parser.ParseAsync(path);
            CurrentSolution = solution;

            // Populate EF-capable projects only
            EfProjects.Clear();
            foreach (var p in solution.Projects.Where(p => p.HasEfCore))
                EfProjects.Add(p);

            _settings.AddRecentSolution(path);
            RefreshRecentSolutions();

            SolutionLoaded?.Invoke(solution);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to load solution: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void RefreshRecentSolutions()
    {
        RecentSolutions.Clear();
        foreach (var s in _settings.Settings.RecentSolutions)
            RecentSolutions.Add(s);
    }
}
```

### Step 5.3 — `MigrationPanelViewModel`

The main ViewModel. Coordinates project selection, DbContext discovery, migration listing,
and command execution. **All EF commands flow through this VM.**

```csharp
namespace EfMigrationManager.App.ViewModels;

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EfMigrationManager.Core.Models;
using EfMigrationManager.Core.Services;
using Wpf.Ui.Controls;

public sealed partial class MigrationPanelViewModel : ObservableObject
{
    private readonly IEfDiscoveryService      _discovery;
    private readonly IEfCommandBuilderService _cmdBuilder;
    private readonly IProcessRunnerService    _runner;
    private readonly ISettingsService         _settings;
    private readonly TerminalViewModel        _terminal;

    // ── Selections ───────────────────────────────────────────────────────────
    [ObservableProperty] private SolutionInfo?   _currentSolution;
    [ObservableProperty] private ProjectInfo?    _selectedStartupProject;
    [ObservableProperty] private ProjectInfo?    _selectedMigrationsProject;
    [ObservableProperty] private DbContextInfo?  _selectedContext;

    // ── State ────────────────────────────────────────────────────────────────
    [ObservableProperty] private bool   _isLoadingContexts;
    [ObservableProperty] private bool   _isLoadingMigrations;
    [ObservableProperty] private bool   _isExecuting;
    [ObservableProperty] private string? _statusMessage;

    // ── Collections ──────────────────────────────────────────────────────────
    public ObservableCollection<ProjectInfo>   StartupProjects     { get; } = [];
    public ObservableCollection<ProjectInfo>   MigrationProjects   { get; } = [];
    public ObservableCollection<DbContextInfo> Contexts            { get; } = [];
    public ObservableCollection<MigrationEntry> Migrations         { get; } = [];

    private CancellationTokenSource? _cts;

    public MigrationPanelViewModel(
        IEfDiscoveryService      discovery,
        IEfCommandBuilderService cmdBuilder,
        IProcessRunnerService    runner,
        ISettingsService         settings,
        TerminalViewModel        terminal)
    {
        _discovery  = discovery;
        _cmdBuilder = cmdBuilder;
        _runner     = runner;
        _settings   = settings;
        _terminal   = terminal;
    }

    // ── Property Changed Reactions ────────────────────────────────────────────

    partial void OnSelectedStartupProjectChanged(ProjectInfo? value)
    {
        PersistCurrentSelections();
        RefreshContextsCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedMigrationsProjectChanged(ProjectInfo? value)
    {
        PersistCurrentSelections();
        RefreshContextsCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedContextChanged(DbContextInfo? value)
    {
        PersistCurrentSelections();
        if (value is not null) _ = RefreshMigrationsAsync();
        RefreshMigrationsCommand.NotifyCanExecuteChanged();
    }

    // ── Solution Loaded (called from SolutionPanelViewModel event) ────────────

    public void OnSolutionLoaded(SolutionInfo solution)
    {
        CurrentSolution = solution;

        StartupProjects.Clear();
        MigrationProjects.Clear();
        Contexts.Clear();
        Migrations.Clear();

        // All EF projects can be a migrations project
        // Executable projects are startup project candidates (but also show all as fallback)
        var efProjects     = solution.Projects.Where(p => p.HasEfCore).ToList();
        var startupCandidates = efProjects.Where(p => p.IsExecutable).ToList();
        if (!startupCandidates.Any()) startupCandidates = efProjects; // fallback: show all

        foreach (var p in startupCandidates) StartupProjects.Add(p);
        foreach (var p in efProjects)        MigrationProjects.Add(p);

        // Restore previous selections for this solution
        var saved = _settings.GetSolutionSettings(solution.Path);

        SelectedStartupProject = StartupProjects
            .FirstOrDefault(p => p.AbsolutePath == saved.LastStartupProjectPath)
            ?? StartupProjects.FirstOrDefault();

        SelectedMigrationsProject = MigrationProjects
            .FirstOrDefault(p => p.AbsolutePath == saved.LastMigrationsProjectPath)
            ?? MigrationProjects.FirstOrDefault(p => p.HasEfDesign)
            ?? MigrationProjects.FirstOrDefault();
    }

    // ── Refresh Commands ──────────────────────────────────────────────────────

    [RelayCommand(CanExecute = nameof(CanRefresh))]
    private async Task RefreshContextsAsync()
    {
        if (BuildOptions() is not { } opts) return;

        IsLoadingContexts = true;
        Contexts.Clear();
        SelectedContext = null;

        try
        {
            var contexts = await _discovery.ListDbContextsAsync(opts);
            foreach (var c in contexts) Contexts.Add(c);

            var saved = _settings.GetSolutionSettings(CurrentSolution!.Path);
            SelectedContext = Contexts.FirstOrDefault(c => c.FullName == saved.LastContextName)
                              ?? Contexts.FirstOrDefault();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error discovering DbContexts: {ex.Message}";
        }
        finally
        {
            IsLoadingContexts = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanRefreshMigrations))]
    private async Task RefreshMigrationsAsync()
    {
        if (BuildOptions() is not { } opts) return;

        IsLoadingMigrations = true;
        Migrations.Clear();

        try
        {
            var migrations = await _discovery.ListMigrationsAsync(opts);
            foreach (var m in migrations) Migrations.Add(m);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error listing migrations: {ex.Message}";
        }
        finally
        {
            IsLoadingMigrations = false;
        }
    }

    private bool CanRefresh() =>
        SelectedStartupProject is not null && SelectedMigrationsProject is not null;

    private bool CanRefreshMigrations() =>
        CanRefresh() && SelectedContext is not null;

    // ── Migration Actions ─────────────────────────────────────────────────────

    [RelayCommand(CanExecute = nameof(CanExecuteCommand))]
    private async Task AddMigrationAsync(string migrationName)
    {
        if (BuildOptions() is not { } opts) return;
        var args = _cmdBuilder.AddMigration(opts, migrationName);
        await ExecuteEfCommandAsync(args, opts);
        await RefreshMigrationsAsync();
    }

    [RelayCommand(CanExecute = nameof(CanExecuteCommand))]
    private async Task RemoveMigrationAsync()
    {
        if (BuildOptions() is not { } opts) return;
        var args = _cmdBuilder.RemoveMigration(opts);
        await ExecuteEfCommandAsync(args, opts);
        await RefreshMigrationsAsync();
    }

    [RelayCommand(CanExecute = nameof(CanExecuteCommand))]
    private async Task UpdateDatabaseAsync(string? targetMigration = null)
    {
        if (BuildOptions() is not { } opts) return;
        var args = _cmdBuilder.UpdateDatabase(opts, targetMigration);
        await ExecuteEfCommandAsync(args, opts);
        await RefreshMigrationsAsync();
    }

    [RelayCommand(CanExecute = nameof(CanExecuteCommand))]
    private async Task DropDatabaseAsync()
    {
        if (BuildOptions() is not { } opts) return;
        var args = _cmdBuilder.DropDatabase(opts);
        await ExecuteEfCommandAsync(args, opts);
    }

    [RelayCommand(CanExecute = nameof(CanExecuteCommand))]
    private async Task GenerateScriptAsync(
        string? fromMigration = null, string? toMigration = null, string? outputPath = null)
    {
        if (BuildOptions() is not { } opts) return;
        var args = _cmdBuilder.GenerateScript(opts, fromMigration, toMigration, outputPath);
        await ExecuteEfCommandAsync(args, opts);
    }

    [RelayCommand]
    private void CancelExecution()
    {
        _cts?.Cancel();
        StatusMessage = "Cancelling...";
    }

    private bool CanExecuteCommand()
        => CanRefreshMigrations() && !IsExecuting;

    // ── Core Execution ────────────────────────────────────────────────────────

    private async Task ExecuteEfCommandAsync(string args, EfCommandOptions opts)
    {
        IsExecuting = true;
        StatusMessage = null;
        _cts = new CancellationTokenSource();

        _terminal.BeginCommand(_cmdBuilder.FormatForDisplay(args), opts.WorkingDirectory);

        try
        {
            await foreach (var line in _runner.RunAsync("dotnet", args, opts.WorkingDirectory, _cts.Token))
                _terminal.AppendLine(line);

            StatusMessage = "Completed successfully.";
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Cancelled.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            _terminal.EndCommand();
            IsExecuting = false;
            _cts.Dispose();
            _cts = null;
        }

        // Notify all action commands to re-evaluate CanExecute
        AddMigrationCommand.NotifyCanExecuteChanged();
        RemoveMigrationCommand.NotifyCanExecuteChanged();
        UpdateDatabaseCommand.NotifyCanExecuteChanged();
        DropDatabaseCommand.NotifyCanExecuteChanged();
        GenerateScriptCommand.NotifyCanExecuteChanged();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private EfCommandOptions? BuildOptions()
    {
        if (CurrentSolution is null
            || SelectedStartupProject is null
            || SelectedMigrationsProject is null
            || SelectedContext is null)
            return null;

        return new EfCommandOptions
        {
            StartupProjectPath    = SelectedStartupProject.AbsolutePath,
            MigrationsProjectPath = SelectedMigrationsProject.AbsolutePath,
            ContextName           = SelectedContext.FullName,
            WorkingDirectory      = CurrentSolution.DirectoryPath
        };
    }

    private void PersistCurrentSelections()
    {
        if (CurrentSolution is null) return;
        _settings.SaveSolutionSettings(CurrentSolution.Path, new()
        {
            LastStartupProjectPath    = SelectedStartupProject?.AbsolutePath,
            LastMigrationsProjectPath = SelectedMigrationsProject?.AbsolutePath,
            LastContextName           = SelectedContext?.FullName
        });
    }
}
```

### Step 5.4 — `MainViewModel`

Glues everything together.

```csharp
namespace EfMigrationManager.App.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;

public sealed partial class MainViewModel : ObservableObject
{
    public SolutionPanelViewModel  SolutionPanel  { get; }
    public MigrationPanelViewModel MigrationPanel { get; }
    public TerminalViewModel       Terminal        { get; }

    public MainViewModel(
        SolutionPanelViewModel  solutionPanel,
        MigrationPanelViewModel migrationPanel,
        TerminalViewModel       terminal)
    {
        SolutionPanel  = solutionPanel;
        MigrationPanel = migrationPanel;
        Terminal       = terminal;

        // Wire the solution-loaded event
        SolutionPanel.SolutionLoaded += MigrationPanel.OnSolutionLoaded;
    }
}
```

---

## Phase 6 — WPF Views

### Step 6.1 — `MainWindow.xaml`

3-column layout: Solution (250px fixed) | Migration Panel (450px) | Terminal (flexible).

```xml
<ui:FluentWindow
    x:Class="EfMigrationManager.App.Views.MainWindow"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    xmlns:ui="http://schemas.lepo.co/wpfui/2022/xaml"
    xmlns:vm="clr-namespace:EfMigrationManager.App.ViewModels"
    Title="EF Migration Manager"
    Width="1200" Height="750"
    MinWidth="900" MinHeight="550"
    WindowStartupLocation="CenterScreen"
    ExtendsContentIntoTitleBar="True">

    <ui:FluentWindow.Resources>
        <!-- Status colors -->
        <SolidColorBrush x:Key="AppliedBrush"  Color="#4CAF50"/>
        <SolidColorBrush x:Key="PendingBrush"  Color="#FFC107"/>
    </ui:FluentWindow.Resources>

    <Grid>
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto"/>  <!-- Title bar -->
            <RowDefinition Height="*"/>     <!-- Content -->
            <RowDefinition Height="Auto"/>  <!-- Status bar -->
        </Grid.RowDefinitions>

        <!-- Title Bar -->
        <ui:TitleBar Grid.Row="0"
                     Title="EF Migration Manager"
                     ShowMinimize="True"
                     ShowMaximize="True"
                     ShowClose="True"/>

        <!-- Main Content: 3 columns -->
        <Grid Grid.Row="1">
            <Grid.ColumnDefinitions>
                <ColumnDefinition Width="260" MinWidth="200"/>
                <ColumnDefinition Width="Auto"/>   <!-- Splitter -->
                <ColumnDefinition Width="420" MinWidth="350"/>
                <ColumnDefinition Width="Auto"/>   <!-- Splitter -->
                <ColumnDefinition Width="*"   MinWidth="300"/>
            </Grid.ColumnDefinitions>

            <!-- Column 0: Solution Panel (UserControl) -->
            <!-- Content defined in SolutionPanel.xaml — include as UserControl -->

            <GridSplitter Grid.Column="1" Width="4" HorizontalAlignment="Center"
                          Background="Transparent" Cursor="SizeWE"/>

            <!-- Column 2: Migration Panel (UserControl) -->

            <GridSplitter Grid.Column="3" Width="4" HorizontalAlignment="Center"
                          Background="Transparent" Cursor="SizeWE"/>

            <!-- Column 4: Terminal Panel (UserControl) -->
        </Grid>

        <!-- Status Bar -->
        <StatusBar Grid.Row="2" Padding="8,2">
            <StatusBarItem>
                <TextBlock Text="{Binding MigrationPanel.StatusMessage}"
                           Foreground="{DynamicResource TextFillColorSecondaryBrush}"
                           FontSize="11"/>
            </StatusBarItem>
        </StatusBar>
    </Grid>
</ui:FluentWindow>
```

### Step 6.2 — `MainWindow.xaml.cs`

```csharp
namespace EfMigrationManager.App.Views;

using EfMigrationManager.App.ViewModels;

public partial class MainWindow
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
```

### Step 6.3 — Solution Panel XAML (left column)

The solution panel shows: Open button, recent solutions MRU list, and the EF project list.

Key XAML elements:
- `ui:Button` "Open Solution..." bound to `BrowseCommand`
- `ListBox` of `RecentSolutions` — each item is a clickable path that fires `OpenRecentCommand`
- `ItemsControl` showing `EfProjects` with project name + "EF Design" badge if `HasEfDesign`
- `ui:ProgressRing` shown when `IsLoading` is true

### Step 6.4 — Migration Panel XAML (center column)

Sections from top to bottom:

**1. Project Configuration Section** (GroupBox "Configuration"):
```
Startup Project:     [ComboBox bound to StartupProjects / SelectedStartupProject]
Migrations Project:  [ComboBox bound to MigrationProjects / SelectedMigrationsProject]
DbContext:           [ComboBox bound to Contexts / SelectedContext]  [🔄 Refresh Button]
```

**2. Actions Section** (GroupBox "Actions") — vertical StackPanel of `ui:Button` elements:

| Button Label | Command | Dialog Required |
|---|---|---|
| ➕ Add Migration | `AddMigrationCommand` | Yes — name input |
| ➖ Remove Last Migration | `RemoveMigrationCommand` | Confirmation only |
| ⬆ Update to Latest | `UpdateDatabaseCommand` (null target) | No |
| ⬆ Update to... | `UpdateDatabaseCommand` (specific) | Yes — pick from list |
| 📄 Generate Script | `GenerateScriptCommand` | Yes — from/to/output |
| 🗑 Drop Database | `DropDatabaseCommand` | Yes — type "DROP" to confirm |
| ⏹ Cancel | `CancelExecutionCommand` | No — only visible when `IsExecuting` |

**3. Migration History Section** (GroupBox "Migration History"):
```
ListView:
  Column 1: Status icon (✅ / 🟡) 
  Column 2: Human name (SafeName)
  Column 3: Full migration ID (grayed)
  Column 4: Timestamp
```
Each row: right-click context menu with "Update to this migration" and "Copy name".

### Step 6.5 — Terminal Panel XAML (right column)

```xml
<!-- Toolbar at top -->
<StackPanel Orientation="Horizontal">
    <ui:Button Content="Clear"    Command="{Binding Terminal.ClearCommand}"/>
    <ui:Button Content="Copy All" Command="{Binding Terminal.CopyAllCommand}"/>
    <ui:Button Content="↗ Open in Terminal" 
               Command="{Binding Terminal.OpenInTerminalCommand}"/>
    <ui:ProgressRing IsIndeterminate="True" 
                     Visibility="{Binding Terminal.IsRunning, 
                                  Converter={StaticResource BoolToVisibilityConverter}}"/>
</StackPanel>

<!-- Output area -->
<ScrollViewer x:Name="TerminalScroller" VerticalScrollBarVisibility="Auto">
    <ItemsControl ItemsSource="{Binding Terminal.Lines}">
        <ItemsControl.ItemTemplate>
            <DataTemplate>
                <TextBlock Text="{Binding Text}"
                           Foreground="{Binding ColorHint, 
                                        Converter={StaticResource ColorHintToBrushConverter}}"
                           FontFamily="Cascadia Code, Consolas, Courier New"
                           FontSize="12"
                           TextWrapping="Wrap"
                           Margin="0,1"/>
            </DataTemplate>
        </ItemsControl.ItemTemplate>
    </ItemsControl>
</ScrollViewer>
```

### Step 6.6 — Auto-Scroll Behavior

`Behaviors/AutoScrollBehavior.cs`:

```csharp
namespace EfMigrationManager.App.Behaviors;

using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Xaml.Behaviors;

public sealed class AutoScrollBehavior : Behavior<ScrollViewer>
{
    private ItemsControl? _itemsControl;

    protected override void OnAttached()
    {
        base.OnAttached();
        AssociatedObject.Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _itemsControl = AssociatedObject.Content as ItemsControl;
        if (_itemsControl?.ItemsSource is INotifyCollectionChanged observable)
            observable.CollectionChanged += OnCollectionChanged;
    }

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Add)
            AssociatedObject.ScrollToEnd();
    }

    protected override void OnDetaching()
    {
        if (_itemsControl?.ItemsSource is INotifyCollectionChanged observable)
            observable.CollectionChanged -= OnCollectionChanged;
        base.OnDetaching();
    }
}
```

Apply to the terminal `ScrollViewer`:
```xml
xmlns:b="http://schemas.microsoft.com/xaml/behaviors"
<b:Interaction.Behaviors>
    <behaviors:AutoScrollBehavior/>
</b:Interaction.Behaviors>
```

---

## Phase 7 — Dialogs

### Step 7.1 — Add Migration Dialog

A `ui:ContentDialog` (WPF-UI) with a single `TextBox` for the migration name.

**ViewModel** (`ViewModels/Dialogs/AddMigrationDialogViewModel.cs`):
```csharp
public sealed partial class AddMigrationDialogViewModel : ObservableObject
{
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConfirmCommand))]
    private string _migrationName = string.Empty;

    public bool IsConfirmed { get; private set; }

    [RelayCommand(CanExecute = nameof(IsValid))]
    private void Confirm()
    {
        IsConfirmed = true;
    }

    private bool IsValid()
        => !string.IsNullOrWhiteSpace(MigrationName)
           && MigrationName.All(c => char.IsLetterOrDigit(c) || c == '_');
}
```

**Validation rules** for migration name:
- Not empty
- Only alphanumeric characters and underscores
- Does not start with a digit
- Max 100 characters

Show validation error inline below the TextBox using `ui:InfoBar` with `Severity="Error"`.

### Step 7.2 — Update Database Dialog

A dialog that shows the full migration list as a `ListBox`.
User selects a target migration. "Update to Latest" is a separate button (selects `null`).

### Step 7.3 — Generate Script Dialog

Two ComboBoxes (From migration, To migration) both pre-populated from the current migration list.
"From" defaults to the last applied migration.
"To" defaults to the last migration (latest).
A `TextBox` + browse button for the output `.sql` file path.
Checkbox: "Idempotent" (checked by default).

### Step 7.4 — Drop Database Dialog

Confirmation dialog. Contains:
- Warning `ui:InfoBar` with `Severity="Warning"` explaining this is irreversible
- TextBox labeled: `Type "DROP" to confirm`
- OK button only enabled when text equals "DROP" (case-sensitive)

---

## Phase 8 — Value Converters

### `Converters/BoolToVisibilityConverter.cs`

```csharp
[ValueConversion(typeof(bool), typeof(Visibility))]
public sealed class BoolToVisibilityConverter : IValueConverter
{
    public bool Invert { get; set; }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var b = value is bool boolVal && boolVal;
        if (Invert) b = !b;
        return b ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
```

### `Converters/MigrationStatusToIconConverter.cs`

```csharp
[ValueConversion(typeof(MigrationStatus), typeof(string))]
public sealed class MigrationStatusToIconConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is MigrationStatus.Applied ? "✅" : "🟡";

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
```

### `Converters/ColorHintToBrushConverter.cs`

```csharp
[ValueConversion(typeof(string), typeof(Brush))]
public sealed class ColorHintToBrushConverter : IValueConverter
{
    private static readonly Dictionary<string, SolidColorBrush> _map = new()
    {
        ["Red"]       = new SolidColorBrush(Color.FromRgb(255, 80, 80)),
        ["Yellow"]    = new SolidColorBrush(Color.FromRgb(255, 193, 7)),
        ["LimeGreen"] = new SolidColorBrush(Color.FromRgb(76, 175, 80)),
        ["Cyan"]      = new SolidColorBrush(Color.FromRgb(0, 188, 212)),
    };

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is string s && _map.TryGetValue(s, out var brush)
           ? brush
           : (Application.Current.Resources["TextFillColorPrimaryBrush"] as Brush
              ?? Brushes.White);

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
```

---

## Phase 9 — Error Handling & Edge Cases

Implement these explicitly — do not leave them as happy-path only.

### EF Tools Not Installed

When any `dotnet ef` command produces output containing `"No executable found matching command"` or
`"dotnet-ef"` not found:
- Detect in `EfDiscoveryService` by checking for this string in collected output
- Surface via a `ui:InfoBar` with `Severity="Error"` and message:
  `"dotnet-ef tools not found. Run: dotnet tool install --global dotnet-ef"`
- Include a clickable link that copies the install command to clipboard

### No DbContexts Found

When `dotnet ef dbcontext list` returns an empty array:
- Show `ui:InfoBar` with `Severity="Warning"`:
  `"No DbContexts found. Verify the selected projects and that EF Design package is referenced."`

### Build Errors

`dotnet ef` commands build the project first. Build errors appear in stderr.
- When stderr contains `"Build FAILED"`, show a prominent error badge in the terminal header
- The `StatusMessage` in the status bar should show `"Build failed — check terminal for details"`

### Connection String Not Found

EF tools require a valid connection string at runtime.
- When output contains `"Unable to create an object of type"` or `"A connection string"`:
  Show an inline `ui:InfoBar` with guidance to check `appsettings.json` in the startup project

### Cancellation

- `CancellationTokenSource` is created fresh per command in `MigrationPanelViewModel`
- The Cancel button triggers `_cts.Cancel()`
- `ProcessRunnerService` kills the process tree on cancellation
- Always reset `IsExecuting = false` in the `finally` block

### Long Migration Lists

`ListView` with `VirtualizingStackPanel.IsVirtualizing="True"` and
`VirtualizingStackPanel.VirtualizationMode="Recycling"` for performance with hundreds of migrations.

---

## Phase 10 — Polish & Final Details

### Window State Persistence

Save/restore window position and size in `AppSettings`:
```csharp
public sealed class WindowSettings
{
    public double Left   { get; set; } = 100;
    public double Top    { get; set; } = 100;
    public double Width  { get; set; } = 1200;
    public double Height { get; set; } = 750;
    public bool   IsMaximized { get; set; }
}
```
Wire in `MainWindow.OnSourceInitialized` (restore) and `MainWindow.OnClosing` (save).

### Keyboard Shortcuts

| Shortcut | Action |
|---|---|
| `Ctrl+O` | Open solution |
| `F5` | Refresh DbContexts |
| `Ctrl+R` | Refresh migrations list |
| `Ctrl+L` | Clear terminal |
| `Escape` | Cancel running command |

### Right-Click Context Menu on Migrations ListView

```
Update database to this migration
──────────────────────────────────
Copy migration name
Copy migration ID (full)
```

### Recent Solutions Display

Show only the filename + parent folder (not the full path) in the MRU list.
Show the full path as a `ToolTip`.

### Theme Toggle

Settings button in title bar area → `ui:ToggleSwitch` for Dark/Light theme.
Apply via `Wpf.Ui.Appearance.ApplicationThemeManager.Apply(ApplicationTheme.Dark)`.

### Startup Check

On startup, after the window loads, check:
1. Is `dotnet` in `PATH`? Run `dotnet --version` silently.
2. Is `dotnet-ef` installed? Run `dotnet ef --version` silently.

If either fails, show a `ui:InfoBar` at the top of the window with the appropriate fix command.

---

## Phase 11 — Build & Publish

### Debug Run

```bash
cd src/EfMigrationManager.App
dotnet run
```

### Self-Contained Single-File Publish

```bash
dotnet publish src/EfMigrationManager.App/EfMigrationManager.App.csproj \
  -c Release \
  -r win-x64 \
  --self-contained true \
  -p:PublishSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  -o ./publish
```

Output: `publish/EfMigrationManager.exe` — copy this to any Windows machine, no .NET install required.

---

## Implementation Order Summary

Follow this order strictly. Each step should compile and (where applicable) be testable before proceeding.

```
Phase 1: Scaffolding & project files
  └─ 1.1 Create solution + projects
  └─ 1.2 Core .csproj
  └─ 1.3 App .csproj  
  └─ 1.4 app.manifest

Phase 2: Models (all at once — they have no dependencies)
  └─ SolutionInfo, ProjectInfo, DbContextInfo, MigrationEntry
  └─ EfCommandOptions, ProcessLine, AppSettings

Phase 3: Core Services (in this exact order)
  └─ 3.1 SettingsService
  └─ 3.2 SolutionParserService
  └─ 3.3 ProcessRunnerService  ← most critical, test first
  └─ 3.4 EfDiscoveryService
  └─ 3.5 EfCommandBuilderService
  └─ 3.6 AnsiHelper + PathHelper

Phase 4: DI & App Host
  └─ 4.1 App.xaml (theme resources)
  └─ 4.2 App.xaml.cs (host setup, DI registration, MSBuildLocator)

Phase 5: ViewModels (in this exact order)
  └─ 5.1 TerminalViewModel
  └─ 5.2 SolutionPanelViewModel
  └─ 5.3 MigrationPanelViewModel  ← most complex
  └─ 5.4 MainViewModel

Phase 6: Views
  └─ 6.1 MainWindow (shell + layout only)
  └─ 6.2 MainWindow.xaml.cs
  └─ 6.3 SolutionPanel content
  └─ 6.4 MigrationPanel content
  └─ 6.5 TerminalPanel content
  └─ 6.6 AutoScrollBehavior

Phase 7: Dialogs
  └─ 7.1 AddMigrationDialog
  └─ 7.2 UpdateDatabaseDialog
  └─ 7.3 GenerateScriptDialog
  └─ 7.4 DropDatabaseDialog

Phase 8: Converters (implement as needed during Phase 6/7)
  └─ BoolToVisibilityConverter
  └─ MigrationStatusToIconConverter
  └─ ColorHintToBrushConverter
  └─ NullToVisibilityConverter

Phase 9: Error handling (retrofit into existing services/VMs)
Phase 10: Polish (window state, shortcuts, context menus, theme toggle, startup check)
Phase 11: Build & publish verification
```

---

## Important Notes for the AI Coding Assistant

1. **MSBuildLocator must be called before any `Microsoft.Build` type is first used.**
   Call `MSBuildLocator.RegisterDefaults()` at the very start of `App.OnStartup`,
   before DI is built, before `SolutionParserService` is instantiated.

2. **Never use `Dispatcher.Invoke` inside Core services.** Core has no WPF dependency.
   Only ViewModels and Views may use `Application.Current.Dispatcher`.

3. **`IAsyncEnumerable` + `Channel`** is the correct pattern for `ProcessRunnerService`.
   Do not use `Task<string>` or collect all output before returning — streaming is the point.

4. **`EfCommandBuilderService` returns argument strings, not full commands.**
   The executable is always `"dotnet"` passed separately to `ProcessRunnerService.RunAsync`.

5. **`dotnet ef` JSON output has build noise before the JSON array.**
   Always find the first `[` character before attempting `JsonDocument.Parse`.

6. **Use `CommunityToolkit.Mvvm` source generators throughout.**
   Use `[ObservableProperty]`, `[RelayCommand]`, `[NotifyCanExecuteChangedFor]`.
   Never write `INotifyPropertyChanged` boilerplate by hand.

7. **All `ObservableCollection` mutations must happen on the UI thread.**
   In ViewModels that receive data from async operations (which may complete on thread pool),
   wrap collection updates in `Application.Current.Dispatcher.Invoke(...)`.

8. **`EfCommandOptions.WorkingDirectory` is always the solution directory**, not the project
   directory. `dotnet ef` resolves project paths relative to working directory.

9. **For dialogs**, use `Wpf.Ui.Controls.ContentDialog`, not `MessageBox`.
   `ContentDialog` is async, supports custom content, and matches the Fluent design system.

10. **The `app.manifest` with `PerMonitorV2` DPI awareness** is required for sharp rendering
    on high-DPI displays. Without it the UI will appear blurry on 4K monitors.