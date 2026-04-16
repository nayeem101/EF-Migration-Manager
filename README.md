# EF Migration Manager

A Windows desktop GUI for managing **EF Core migrations** across multi-project
modular-monolith / clean-architecture solutions — without needing Rider or Visual
Studio open.

Point it at a `.sln` or `.slnx`, pick a startup project, a migrations project,
and a `DbContext`, and run any `dotnet ef` command with the output streamed
live into an embedded terminal.

Built with **.NET 10 / WPF / WPF-UI (Fluent)**.

---

## Features

- Open `.sln` or `.slnx` solutions and auto-discover EF-capable projects
- List `DbContext` classes via `dotnet ef dbcontext list --json`
- List migrations with Applied / Pending status, timestamps, human names
- **Add**, **remove**, **update to latest / specific target**, **drop**, **generate SQL script**
- Live terminal output with ANSI-to-Color mapping, copy, clear, "open in external terminal"
- Per-solution memory of last Startup/Migrations project and DbContext
- Recent solutions (MRU, top 10)
- Keyboard shortcuts: `Ctrl+O`, `F5`, `Ctrl+R`, `Ctrl+L`, `Escape`
- Dark / Light theme toggle (persisted)
- Window size/position persisted
- Startup check for `dotnet` + `dotnet-ef` with actionable InfoBar
- Inline error detection: EF tools missing, build failures, connection-string errors, missing DbContexts

---

## Running from source

Requirements:
- Windows 10 / 11 x64
- .NET 10 SDK (https://dot.net)
- `dotnet-ef` tools installed globally:

  ```powershell
  dotnet tool install --global dotnet-ef
  ```

Run:

```powershell
dotnet run --project src/EfMigrationManager.App
```

---

## Building an installer (to give to teammates)

The app ships as a **self-contained single-file EXE** wrapped in an **Inno Setup
installer**. Recipients do **not** need the .NET SDK or runtime — everything is
embedded.

### 1. Install Inno Setup (one-time, on the build machine)

```powershell
winget install -e --id JRSoftware.InnoSetup
```

### 2. Build the installer

```powershell
pwsh ./build/make-installer.ps1
```

This will:
1. `dotnet publish` as self-contained single-file for `win-x64` into `./publish/`
2. Compile `build/installer.iss` with Inno Setup
3. Drop the final installer at `./installer-output/EfMigrationManager-Setup-1.0.0.exe`

Send that `.exe` to anyone on Windows — they just double-click to install. No
admin rights needed (per-user install by default). A Start Menu shortcut is
created; desktop shortcut is optional at install time.

### Just the portable EXE (no installer)

If you only want the single-file executable (no setup wizard):

```powershell
pwsh ./build/publish.ps1
```

Output: `./publish/EfMigrationManager.exe` — copy anywhere and run.

---

## For end users receiving the installer

1. Run `EfMigrationManager-Setup-1.0.0.exe`
2. Accept the default install location (per-user: `%LOCALAPPDATA%\Programs\EF Migration Manager`)
3. Launch from the Start Menu
4. **On first launch:** if you see a yellow banner saying *"dotnet-ef not installed"*,
   open PowerShell and run:

   ```powershell
   dotnet tool install --global dotnet-ef
   ```

   Then restart the app. (The app itself is self-contained, but it invokes your
   system's `dotnet ef` CLI to talk to your solution's EF setup.)

---

## Project Structure

```
src/
├── EfMigrationManager.Core/   ← business logic, no WPF dependency
│   ├── Models/                  plain records/enums
│   ├── Services/                parsing, discovery, process runner, settings
│   └── Helpers/                 ANSI stripping, error detection, path helpers
└── EfMigrationManager.App/    ← WPF shell
    ├── ViewModels/              CommunityToolkit.Mvvm source generators
    ├── Views/                   FluentWindow + dialogs
    ├── Converters/              WPF value converters
    └── Behaviors/               auto-scroll for terminal
build/
├── publish.ps1                  self-contained single-file publish
├── installer.iss                Inno Setup script
└── make-installer.ps1           publish + installer one-shot
```

---

## Settings & logs location

- Settings JSON: `%APPDATA%\EfMigrationManager\settings.json`
- Rolling logs:  `%APPDATA%\EfMigrationManager\logs\app-YYYYMMDD.log`

---

## License

MIT (adjust as needed).
