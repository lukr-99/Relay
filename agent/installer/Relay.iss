; Relay agent installer (Inno Setup 6)
; Per-user install: no admin/UAC required. Installs the self-contained agent to
; %LocalAppData%\Programs\Relay, adds a Start-menu shortcut, an optional "run at sign-in"
; entry, and registers an uninstaller in "Installed apps".

#define MyAppName "Relay"
#ifndef MyAppVersion
  #define MyAppVersion "0.0.0"
#endif
#define MyAppPublisher "lukr-99"
#define MyAppExeName "Relay.Agent.exe"
#define MyAppURL "https://github.com/lukr-99/Relay"

; Path to the self-contained publish output (passed in via ISCC /D, with a fallback).
#ifndef PublishDir
  #define PublishDir "..\publish"
#endif

[Setup]
AppId={{2F1D6A0E-9C4B-4E77-9C3B-7B2E5A9D4F10}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\Relay
DisableProgramGroupPage=yes
DisableDirPage=auto
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir=.
OutputBaseFilename=Relay-Setup-{#MyAppVersion}
SetupIconFile=..\Relay.Agent\Relay.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
UninstallDisplayName={#MyAppName}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
; The agent locks its own files while running; have Setup close it before replacing them.
CloseApplications=yes
RestartApplications=no
VersionInfoVersion={#MyAppVersion}.0
VersionInfoCompany={#MyAppPublisher}
VersionInfoProductName={#MyAppName}
LicenseFile=..\..\LICENSE.md

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: unchecked
Name: "startup"; Description: "Start Relay when I sign in"; GroupDescription: "Startup:"

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: recursesubdirs createallsubdirs ignoreversion

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"; Tasks: desktopicon
Name: "{userstartup}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"; Tasks: startup

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#MyAppName}}"; Flags: nowait postinstall skipifsilent
