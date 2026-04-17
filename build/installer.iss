; Inno Setup script for EF Migration Manager
; Compile with: "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" installer.iss
; Run build/publish.ps1 FIRST to produce ..\publish\EfMigrationManager.exe

#define MyAppName       "EF Migration Manager"
#define MyAppVersion    "1.0.6"
#define MyAppPublisher  "EfMigrationManager"
#define MyAppExeName    "EfMigrationManager.exe"
#define MyAppId         "{{6C3A6E5B-0F91-4F5A-9C1B-EF2A6F7D2A11}"

[Setup]
AppId={#MyAppId}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
OutputDir=..\installer-output
OutputBaseFilename=EfMigrationManager-Setup-{#MyAppVersion}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
UninstallDisplayIcon={app}\{#MyAppExeName}
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon";   Description: "Create a &desktop icon";        GroupDescription: "Additional icons:"; Flags: unchecked
Name: "startmenuicon"; Description: "Create a &Start Menu shortcut"; GroupDescription: "Additional icons:"

[Files]
Source: "..\publish\EfMigrationManager.exe"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#MyAppName}";                     Filename: "{app}\{#MyAppExeName}"; Tasks: startmenuicon
Name: "{group}\Uninstall {#MyAppName}";           Filename: "{uninstallexe}";        Tasks: startmenuicon
Name: "{autodesktop}\{#MyAppName}";               Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch {#MyAppName}"; Flags: nowait postinstall skipifsilent
