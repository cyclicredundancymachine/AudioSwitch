#ifndef AppVersion
  #error AppVersion is required
#endif
#ifndef PayloadDir
  #error PayloadDir is required
#endif
#ifndef ReleaseDir
  #error ReleaseDir is required
#endif

[Setup]
AppId=AudioSwitch
AppName=AudioSwitch
AppVersion={#AppVersion}
AppPublisher=cyclicredundancymachine
AppPublisherURL=https://github.com/cyclicredundancymachine/AudioSwitch
AppSupportURL=https://github.com/cyclicredundancymachine/AudioSwitch/issues
AppUpdatesURL=https://github.com/cyclicredundancymachine/AudioSwitch/releases/latest
DefaultDirName={localappdata}\Programs\AudioSwitch
DefaultGroupName=AudioSwitch
PrivilegesRequired=lowest
ArchitecturesAllowed=x64os
ArchitecturesInstallIn64BitMode=x64os
MinVersion=10.0.19041
AppMutex=Local\AudioSwitchTray
SetupMutex=Local\AudioSwitchSetup
OutputDir={#ReleaseDir}
OutputBaseFilename=AudioSwitch-Setup-{#AppVersion}-win-x64
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
UninstallDisplayIcon={app}\AudioSwitchTray.exe
CloseApplications=yes
RestartApplications=no
SetupLogging=yes
DisableProgramGroupPage=yes

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; Flags: unchecked

[Files]
Source: "{#PayloadDir}\AudioSwitchTray.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#PayloadDir}\AudioSwitch.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#PayloadDir}\README.txt"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#PayloadDir}\THIRD-PARTY-LICENSE.txt"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\AudioSwitch"; Filename: "{app}\AudioSwitchTray.exe"
Name: "{group}\Help"; Filename: "{app}\README.txt"
Name: "{autodesktop}\AudioSwitch"; Filename: "{app}\AudioSwitchTray.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\AudioSwitchTray.exe"; Description: "Open AudioSwitch"; Flags: nowait postinstall skipifsilent

; Only explicitly installed files are removed. User settings, source, evidence,
; and unrelated files in a custom installation directory are never deleted.
