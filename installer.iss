; SkyWeave Installer Script for Inno Setup
; Compile with Inno Setup 6.x

[Setup]
AppName=SkyWeave
AppVersion=1.0
AppPublisher=SkyWeave
AppPublisherURL=https://github.com/yourusername/skyweave
AppSupportURL=https://github.com/yourusername/skyweave/issues
AppUpdatesURL=https://github.com/yourusername/skyweave/releases
DefaultDirName={pf}\SkyWeave
DefaultGroupName=SkyWeave
AllowNoIcons=yes
OutputDir=..\..\..\bin\Release\Installer
OutputBaseFilename=SkyWeave-Setup-1.0
Compression=lzma
SolidCompression=yes
WizardStyle=modern
SetupIconFile=..\Assets\skyweave-icon.ico
UninstallDisplayIcon={app}\SkyWeave.exe
UninstallDisplayName=SkyWeave - Real Weather Engine for MSFS 2024
AppComments=Free, open-source real-weather injection engine for MSFS 2024
AppCopyright=MIT License
AppId={{A1B2C3D4-E5F6-7890-ABCD-EF1234567890}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "quicklaunchicon"; Description: "{cm:CreateQuickLaunchIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked; OnlyBelowVersion: 0,6.1

[Files]
; Source: Publish output folder (relative to script location)
; Adjust the source path based on where you publish to
Source: "..\..\..\..\src\SkyWeave.App\bin\Release\net8.0\win-x64\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

; Explicitly include the main EXE
Source: "..\..\..\..\src\SkyWeave.App\bin\Release\net8.0\win-x64\publish\SkyWeave.App.exe"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\SkyWeave"; Filename: "{app}\SkyWeave.App.exe"; WorkingDir: "{app}"; IconFilename: "{app}\SkyWeave.App.exe"
Name: "{group}\{cm:UninstallProgram,SkyWeave}"; Filename: "{uninstallexe}"
Name: "{commondesktop}\SkyWeave"; Filename: "{app}\SkyWeave.App.exe"; WorkingDir: "{app}"; Tasks: desktopicon
Name: "{userappdata}\Microsoft\Internet Explorer\Quick Launch\User Pinned\TaskBar\SkyWeave"; Filename: "{app}\SkyWeave.App.exe}"; Tasks: quicklaunchicon

[Run]
Filename: "{app}\SkyWeave.App.exe"; Description: "{cm:LaunchProgram,SkyWeave}"; Flags: nowait postinstall skipifsilent
