#ifndef AppVersion
  #error AppVersion must be supplied by build-installer.ps1
#endif
#ifndef SourceDir
  #error SourceDir must point to the complete staged portable app
#endif

[Setup]
AppId={{E547F934-57DE-4EED-A32D-1A5E1563B944}
AppName=WirePulse
AppVersion={#AppVersion}
AppPublisher=JLiveOf
AppPublisherURL=https://github.com/JLiveOf/wirepulse
AppSupportURL=https://github.com/JLiveOf/wirepulse/issues
AppUpdatesURL=https://github.com/JLiveOf/wirepulse/releases/latest
DefaultDirName={localappdata}\Programs\WirePulse
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.19041
OutputBaseFilename=WirePulse-{#AppVersion}-win-x64-Setup
SetupIconFile=..\..\Assets\WirePulse.ico
UninstallDisplayIcon={app}\WirePulse.exe
Compression=lzma2/normal
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes
RestartApplications=no
AppMutex={code:ApplicationMutex}
SetupMutex=WirePulseInstaller-E547F934-57DE-4EED-A32D-1A5E1563B944

[Languages]
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\WirePulse"; Filename: "{app}\WirePulse.exe"; WorkingDir: "{app}"
Name: "{autoprograms}\WirePulse (modo compatible)"; Filename: "{app}\WirePulse.exe"; Parameters: "--renderer ""CPU (compatibilidad)"""; WorkingDir: "{app}"
Name: "{autodesktop}\WirePulse"; Filename: "{app}\WirePulse.exe"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\WirePulse.exe"; Description: "{cm:LaunchProgram,WirePulse}"; Flags: nowait postinstall skipifsilent

[Code]
function ApplicationMutex(Param: String): String;
begin
  Result := 'Local\WirePulse-' + Uppercase(Copy(GetSHA256OfString(UTF8Encode(GetUserNameString)), 1, 12));
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  Command, InstalledExe: String;
begin
  if CurUninstallStep = usUninstall then begin
    InstalledExe := '"' + ExpandConstant('{app}\WirePulse.exe') + '"';
    if RegQueryStringValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'WirePulse', Command) then
      if Pos(Lowercase(InstalledExe), Lowercase(Command)) = 1 then
        RegDeleteValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'WirePulse');
  end;
end;
