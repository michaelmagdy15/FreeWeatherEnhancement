; SkyWeave Installer Script for Inno Setup
; Compile with Inno Setup 6.x

[Setup]
AppName=SkyWeave
AppVersion=0.4.0-beta
AppPublisher=SkyWeave
AppPublisherURL=https://github.com/yourusername/skyweave
AppSupportURL=https://github.com/yourusername/skyweave/issues
AppUpdatesURL=https://github.com/yourusername/skyweave/releases
DefaultDirName={autopf}\SkyWeave
DefaultGroupName=SkyWeave
AllowNoIcons=yes
OutputDir=bin\Release\Installer
OutputBaseFilename=SkyWeave-Setup-0.4.0-beta
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

[Icons]
Name: "{group}\SkyWeave"; Filename: "{app}\SkyWeave.App.exe"; WorkingDir: "{app}"; IconFilename: "{app}\SkyWeave.App.exe"
Name: "{group}\{cm:UninstallProgram,SkyWeave}"; Filename: "{uninstallexe}"
Name: "{commondesktop}\SkyWeave"; Filename: "{app}\SkyWeave.App.exe"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\SkyWeave.App.exe"; Description: "{cm:LaunchProgram,SkyWeave}"; Flags: nowait postinstall skipifsilent
