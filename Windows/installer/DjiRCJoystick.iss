#define AppName "DJI RC Joystick"
#define AppVersion GetFileVersion("..\publish\DjiRCJoystick.exe")
#define AppPublisher "ext-rup"
#define AppUrl "https://github.com/ext-rup/DjiMini2RCasJoystick-macOS"
#define AppExecutable "DjiRCJoystick.exe"

[Setup]
AppId={{E9569B98-75D2-4BCE-B947-CF73F767164A}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppUrl}
AppSupportURL={#AppUrl + "/issues"}
AppUpdatesURL={#AppUrl + "/releases"}
DefaultDirName={autopf}\DJI RC Joystick
DefaultGroupName=DJI RC Joystick
UninstallDisplayIcon={app}\{#AppExecutable}
ArchitecturesAllowed=x64os
ArchitecturesInstallIn64BitMode=x64os
MinVersion=10.0
PrivilegesRequired=admin
OutputDir=output
OutputBaseFilename=DjiRCJoystickSetup-{#AppVersion}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes
RestartApplications=no
SetupLogging=yes
DisableProgramGroupPage=yes

[Files]
Source: "..\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "vendor\vJoySetup.exe"; DestDir: "{app}\redist"; Flags: ignoreversion
Source: "third-party\vJoy-LICENSE.txt"; DestDir: "{app}\licenses"; Flags: ignoreversion

[Icons]
Name: "{group}\DJI RC Joystick"; Filename: "{app}\{#AppExecutable}"
Name: "{group}\Uninstall DJI RC Joystick"; Filename: "{uninstallexe}"

[Run]
Filename: "{app}\{#AppExecutable}"; Description: "Launch DJI RC Joystick"; Flags: nowait postinstall skipifsilent; Check: CanLaunchApplication

[Code]
var
  VJoyNeedsRestart: Boolean;

function IsVJoyInstalled: Boolean;
begin
  Result :=
    RegKeyExists(HKEY_LOCAL_MACHINE, 'SYSTEM\CurrentControlSet\Services\vjoy') and
    FileExists(ExpandConstant('{autopf}\vJoy\x64\vJoyInterface.dll'));
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  ResultCode: Integer;
begin
  if (CurStep <> ssPostInstall) or IsVJoyInstalled then
    Exit;

  WizardForm.StatusLabel.Caption := 'Installing the vJoy virtual controller...';
  if not Exec(
    ExpandConstant('{app}\redist\vJoySetup.exe'),
    '/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /SP-',
    '',
    SW_HIDE,
    ewWaitUntilTerminated,
    ResultCode
  ) then
    RaiseException('Could not start the vJoy driver installer.');

  if ResultCode = 8 then
  begin
    VJoyNeedsRestart := True
    RegWriteStringValue(
      HKEY_LOCAL_MACHINE,
      'Software\Microsoft\Windows\CurrentVersion\RunOnce',
      'vJoy Installer Phase 2',
      AddQuotes(ExpandConstant('{app}\redist\vJoySetup.exe')) +
        ' -ph2 1 /VERYSILENT /SUPPRESSMSGBOXES /NORESTART /SP-'
    );
  end
  else if ResultCode <> 0 then
    RaiseException(Format('The vJoy driver installer failed with exit code %d.', [ResultCode]));

  if not IsVJoyInstalled and not VJoyNeedsRestart then
    RaiseException('vJoy did not install successfully. See the setup log for details.');
end;

function NeedRestart: Boolean;
begin
  Result := VJoyNeedsRestart;
end;

function CanLaunchApplication: Boolean;
begin
  Result := not VJoyNeedsRestart;
end;
