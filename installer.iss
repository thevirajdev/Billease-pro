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
PrivilegesRequired=admin
UsedUserAreasWarning=no

; Auto-close running application and background sync before install/update
AppMutex=Global\BilleasePro_App_SingleInstance_Mutex_99938A2D,Global\BilleasePro_SyncService_Mutex_482D1C90
CloseApplications=yes
CloseApplicationsFilter=BillingSuite.App.exe
RestartApplications=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
Source: "BillingSuite.App\bin\Release\net8.0-windows\win-x64\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Excludes: "BilleasePro_Setup.exe,*.bak,*.db"
Source: "BillingSuite.App\billease.ico"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "BilleasePro"; ValueData: """{app}\{#MyAppExeName}"" --sync-service"; Flags: uninsdeletevalue

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[Code]
procedure KillAppProcess();
var
  ResultCode: Integer;
begin
  Exec('taskkill.exe', '/f /im BillingSuite.App.exe', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Sleep(300);
end;

function InitializeSetup(): Boolean;
begin
  KillAppProcess();
  Result := True;
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  KillAppProcess();
  Result := '';
end;

function InitializeUninstall(): Boolean;
begin
  KillAppProcess();
  Result := True;
end;
