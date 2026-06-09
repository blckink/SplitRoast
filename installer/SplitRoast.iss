; ============================================================================
;  SplitRoast installer (Inno Setup 6)
;  Packages the self-contained published app (which already includes the .NET
;  runtime, the bundled test window and the prebuilt XInput proxy), so the end
;  user needs no tools, no .NET, no Visual C++ runtime - just run setup.
;
;  This .iss is normally compiled by installer\build-release.ps1, which passes
;  the staging path and version. It can also be compiled directly if you set
;  the staging folder up yourself.
; ============================================================================

#define MyAppName "SplitRoast"
#ifndef AppVersion
  #define AppVersion "0.1.0"
#endif
#define MyAppExeName "SplitRoast.exe"
#ifndef Staging
  ; Default to the layout produced by build-release.ps1.
  #define Staging "staging\app"
#endif

[Setup]
; A stable, unique application id (do not change between versions).
AppId={{8B5C2E4A-9F31-4D6C-AE18-2C7B9D0F4A21}
AppName={#MyAppName}
AppVersion={#AppVersion}
AppPublisher=SplitRoast
DefaultDirName={autopf}\SplitRoast
DefaultGroupName=SplitRoast
DisableProgramGroupPage=auto
UninstallDisplayIcon={app}\{#MyAppExeName}
OutputDir=output
OutputBaseFilename=SplitRoastSetup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
; The app is published for 64-bit; install into the 64-bit Program Files.
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64
PrivilegesRequired=admin

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "german"; MessagesFile: "compiler:Languages\German.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
; The entire published app folder (exe, runtime, TestTarget\, XInputProxy\).
Source: "{#Staging}\*"; DestDir: "{app}"; Flags: recursesubdirs createallsubdirs ignoreversion

[Icons]
Name: "{group}\SplitRoast"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\Uninstall SplitRoast"; Filename: "{uninstallexe}"
Name: "{autodesktop}\SplitRoast"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,SplitRoast}"; Flags: nowait postinstall skipifsilent
