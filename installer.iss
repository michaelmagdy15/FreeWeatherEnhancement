; SkyWeave Installer Script for Inno Setup
; Compile with Inno Setup 6.x

[Setup]
AppName=SkyWeave
AppVersion=0.6.0
AppPublisher=SkyWeave
AppPublisherURL=https://github.com/michaelmagdy15/FreeWeatherEnhancement
AppSupportURL=https://github.com/michaelmagdy15/FreeWeatherEnhancement/issues
AppUpdatesURL=https://github.com/michaelmagdy15/FreeWeatherEnhancement/releases
DefaultDirName={autopf}\SkyWeave
DefaultGroupName=SkyWeave
AllowNoIcons=yes
OutputDir=bin\Release\Installer
OutputBaseFilename=SkyWeave-Setup-0.6.0
Compression=lzma
SolidCompression=yes
WizardStyle=modern
SetupIconFile=src\SkyWeave.App\Assets\skyweave-icon.ico
UninstallDisplayIcon={app}\SkyWeave.App.exe
UninstallDisplayName=SkyWeave - Real Weather Engine for MSFS 2024
AppComments=Free, open-source real-weather injection engine for MSFS 2024
AppCopyright=MIT License
AppId={{A1B2C3D4-E5F6-7890-ABCD-EF1234567890}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "bin\Release\App\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "bridge\SkyWeaveWeatherBridge\*"; DestDir: "{app}\bridge\SkyWeaveWeatherBridge"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "bridge\SkyWeaveWeatherBridge\*"; DestDir: "{code:GetMsfsCommunityDir}\SkyWeaveWeatherBridge"; Flags: ignoreversion recursesubdirs createallsubdirs; Check: HasMsfsCommunity

[Icons]
Name: "{group}\SkyWeave"; Filename: "{app}\SkyWeave.App.exe"; WorkingDir: "{app}"; IconFilename: "{app}\SkyWeave.App.exe"
Name: "{group}\{cm:UninstallProgram,SkyWeave}"; Filename: "{uninstallexe}"
Name: "{commondesktop}\SkyWeave"; Filename: "{app}\SkyWeave.App.exe"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\SkyWeave.App.exe"; Description: "{cm:LaunchProgram,SkyWeave}"; Flags: nowait postinstall skipifsilent

[Code]
function GetMsfsCommunityDir(Param: string): string;
var
  StorePath: string;
  SteamPath: string;
begin
  StorePath := ExpandConstant('{localappdata}\Packages\Microsoft.Limitless_8wekyb3d8bbwe\LocalCache\Packages\Community');
  if DirExists(StorePath) then
  begin
    Result := StorePath;
    Exit;
  end;
  
  SteamPath := ExpandConstant('{userappdata}\Microsoft Flight Simulator 2024\Packages\Community');
  if DirExists(SteamPath) then
  begin
    Result := SteamPath;
    Exit;
  end;
  
  Result := '';
end;

function HasMsfsCommunity: Boolean;
begin
  Result := GetMsfsCommunityDir('') <> '';
end;
