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
OutputDir=BillingSuite.App\bin\Release\net8.0-windows\win-x64\publish
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
Source: "BillingSuite.App\bin\Release\net8.0-windows\win-x64\publish\BillingSuite.App.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "BillingSuite.App\bin\Release\net8.0-windows\win-x64\publish\splash.mp4"; DestDir: "{app}"; Flags: ignoreversion
Source: "BillingSuite.App\bin\Release\net8.0-windows\win-x64\publish\billease.ico"; DestDir: "{app}"; Flags: ignoreversion
Source: "BillingSuite.App\bin\Release\net8.0-windows\win-x64\publish\ReportsWeb\*"; DestDir: "{app}\ReportsWeb"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "BillingSuite.App\bin\Release\net8.0-windows\win-x64\publish\LatoFont\*"; DestDir: "{app}\LatoFont"; Flags: ignoreversion recursesubdirs createallsubdirs skipifsourcedoesntexist

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent
