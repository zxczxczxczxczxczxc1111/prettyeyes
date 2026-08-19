; prettyeyes installer. Per-user install: a tray utility has no business
; asking for administrator rights.
#define AppName "prettyeyes"
#define AppVersion "1.0.0"
#define AppExe "PrettyEyes.App.exe"
#define AppId "{{8E5C1F42-4E2B-4E4A-9E4B-4B6E4B0A7D31}"
#define PublishDir "..\src\PrettyEyes.App\bin\Release\net10.0-windows10.0.22621.0\win-x64\publish"

[Setup]
AppId={#AppId}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppName}
DefaultDirName={localappdata}\Programs\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
DisableDirPage=auto
PrivilegesRequired=lowest
OutputDir=..\dist
OutputBaseFilename=prettyeyes-setup-{#AppVersion}
SetupIconFile=..\prettyeyes.ico
UninstallDisplayIcon={app}\{#AppExe}
WizardStyle=modern
; Russian is the product language; English stays available through /LANG=english.
ShowLanguageDialog=no
; Without this Inno follows the Windows UI language and ignores the order above.
LanguageDetectionMethod=none
; Without this a language chosen by an earlier install wins over the default.
UsePreviousLanguage=no
; The welcome page is where WizardImageFile lives; modern style hides it by default.
DisableWelcomePage=no
WizardImageFile=assets\wizard-large.bmp
WizardSmallImageFile=assets\wizard-small.bmp
Compression=lzma2/max
SolidCompression=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
; Refuses to install over a running copy instead of leaving a half-updated one.
AppMutex=PrettyEyesSingleInstance

[Languages]
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "autostart"; Description: "{cm:AutostartTask}"; GroupDescription: "{cm:GeneralGroup}"

[CustomMessages]
russian.AutostartTask=Ð—Ð°Ð¿ÑƒÑÐºÐ°Ñ‚ÑŒ Ð²Ð¼ÐµÑÑ‚Ðµ Ñ Windows
russian.GeneralGroup=Ð”Ð¾Ð¿Ð¾Ð»Ð½Ð¸Ñ‚ÐµÐ»ÑŒÐ½Ð¾
russian.LaunchApp=Ð—Ð°Ð¿ÑƒÑÑ‚Ð¸Ñ‚ÑŒ prettyeyes
english.AutostartTask=Start with Windows
english.GeneralGroup=Additional options
english.LaunchApp=Launch prettyeyes

[Files]
Source: "{#PublishDir}\{#AppExe}"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
; AppUserModelID is what makes Windows treat this as a real application and
; show its toast notifications; without a shortcut carrying it they are dropped.
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExe}"; AppUserModelID: "prettyeyes.app"

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; \
    ValueName: "prettyeyes"; ValueData: """{app}\{#AppExe}"""; Flags: uninsdeletevalue; Tasks: autostart

[Run]
Filename: "{app}\{#AppExe}"; Description: "{cm:LaunchApp}"; Flags: nowait postinstall skipifsilent
