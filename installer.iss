; SkyWeave Installer Script for Inno Setup
; Compile with Inno Setup 6.x

[Setup]
AppName=SkyWeave
AppVersion=0.7.0
AppPublisher=SkyWeave
AppPublisherURL=https://github.com/michaelmagdy15/FreeWeatherEnhancement
AppSupportURL=https://github.com/michaelmagdy15/FreeWeatherEnhancement/issues
AppUpdatesURL=https://github.com/michaelmagdy15/FreeWeatherEnhancement/releases
DefaultDirName={autopf}\SkyWeave
DefaultGroupName=SkyWeave
AllowNoIcons=yes
OutputDir=bin\Release\Installer
OutputBaseFilename=SkyWeave-Setup-0.7.0
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
function ReadInstalledPackagesCommunity(ConfigFile: string): string;
var
  Lines: TArrayOfString;
  I: Integer;
  Line: string;
  PosQuote1, PosQuote2: Integer;
  PackagesPath, Candidate: string;
begin
  Result := '';
  if not FileExists(ConfigFile) then Exit;
  if LoadStringsFromFile(ConfigFile, Lines) then
  begin
    for I := 0 to GetArrayLength(Lines) - 1 do
    begin
      Line := Trim(Lines[I]);
      if Pos('InstalledPackagesPath', Line) = 1 then
      begin
        PosQuote1 := Pos('"', Line);
        if PosQuote1 > 0 then
        begin
          Delete(Line, 1, PosQuote1);
          PosQuote2 := Pos('"', Line);
          if PosQuote2 > 0 then
          begin
            PackagesPath := Copy(Line, 1, PosQuote2 - 1);
            Candidate := PackagesPath + '\Community';
            if DirExists(Candidate) then
            begin
              Result := Candidate;
              Exit;
            end;
            if DirExists(PackagesPath) and (Pos('Community', PackagesPath) > 0) then
            begin
              Result := PackagesPath;
              Exit;
            end;
          end;
        end;
      end;
    end;
  end;
end;

function GetMsfsCommunityDir(Param: string): string;
var
  StoreCfg, SteamCfg, CommunityPath: string;
  StorePath, SteamPath: string;
begin
  // 1. Check UserCfg.opt (Store Edition)
  StoreCfg := ExpandConstant('{localappdata}\Packages\Microsoft.Limitless_8wekyb3d8bbwe\LocalCache\UserCfg.opt');
  CommunityPath := ReadInstalledPackagesCommunity(StoreCfg);
  if CommunityPath <> '' then
  begin
    Result := CommunityPath;
    Exit;
  end;

  // 2. Check UserCfg.opt (Steam Edition)
  SteamCfg := ExpandConstant('{userappdata}\Microsoft Flight Simulator 2024\UserCfg.opt');
  CommunityPath := ReadInstalledPackagesCommunity(SteamCfg);
  if CommunityPath <> '' then
  begin
    Result := CommunityPath;
    Exit;
  end;

  // 3. Fallback to direct default paths
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
