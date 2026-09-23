#define MyAppName "Billease Pro"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "NexaAutomate Studio"
#define MyAppURL "https://github.com/thevirajdev/Billease-pro"
#define MyAppExeName "BillingSuite.App.exe"

[Setup]
; Unique application GUID for Windows Add/Remove Programs
AppId={{C17A4D7E-9781-4FE8-9993-8A2D3C4B5E6F}}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
AllowNoIcons=yes
OutputDir=installer_output
OutputBaseFilename=BilleasePro_Setup
Compression=lzma2/fast
SolidCompression=yes
WizardStyle=modern
SetupIconFile=BillingSuite.App\billease.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
DisableProgramGroupPage=auto

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
Source: "BillingSuite.App\bin\Release\net8.0-windows\win-x64\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Excludes: "BilleasePro_Setup.exe,*.bak,billing.db"

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent
