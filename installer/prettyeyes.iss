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
russian.AutostartTask=Запускать вместе с Windows
russian.GeneralGroup=Дополнительно
russian.LaunchApp=Запустить prettyeyes
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

[Code]
// Inno has no dark theme, so the wizard is repainted by hand. Colours are
// Delphi BGR literals, not the RGB used everywhere else in this project.
const
  Bg = $0B0A0A;          // #0A0A0B, the surface token
  BgDeep = $000000;      // pure black, same as the app background
  TextStrong = $F0F0F0;
  TextDim = $9A9A9A;

procedure PaintPage(Page: TNewNotebookPage);
var
  Index: Integer;
  Child: TControl;
begin
  Page.Color := Bg;

  for Index := 0 to Page.ControlCount - 1 do
  begin
    Child := Page.Controls[Index];

    if Child is TNewStaticText then
    begin
      TNewStaticText(Child).Font.Color := TextStrong;
      TNewStaticText(Child).Color := Bg;
    end
    else if Child is TNewCheckBox then
    begin
      TNewCheckBox(Child).Font.Color := TextStrong;
      TNewCheckBox(Child).Color := Bg;
    end
    else if Child is TNewCheckListBox then
    begin
      TNewCheckListBox(Child).Color := Bg;
      TNewCheckListBox(Child).Font.Color := TextStrong;
      TNewCheckListBox(Child).BorderStyle := bsNone;
    end
    else if Child is TNewEdit then
    begin
      TNewEdit(Child).Color := BgDeep;
      TNewEdit(Child).Font.Color := TextStrong;
    end
    else if Child is TNewMemo then
    begin
      TNewMemo(Child).Color := BgDeep;
      TNewMemo(Child).Font.Color := TextStrong;
    end
    else if Child is TLabel then
      TLabel(Child).Font.Color := TextDim
    else if Child is TBevel then
      TBevel(Child).Visible := False;
  end;
end;

procedure PaintForm();
var
  Index: Integer;
  Child: TControl;
begin
  // The button strip at the bottom is a panel of its own; without this it
  // stays system-grey under an otherwise dark wizard.
  for Index := 0 to WizardForm.ControlCount - 1 do
  begin
    Child := WizardForm.Controls[Index];

    if Child is TPanel then
      TPanel(Child).Color := Bg
    else if Child is TBevel then
      TBevel(Child).Visible := False;
  end;
end;

procedure InitializeWizard();
begin
  WizardForm.Color := Bg;
  WizardForm.MainPanel.Color := Bg;
  PaintForm();
  WizardForm.Bevel.Visible := False;
  WizardForm.Bevel1.Visible := False;

  WizardForm.PageNameLabel.Font.Color := TextStrong;
  WizardForm.PageDescriptionLabel.Font.Color := TextDim;
  WizardForm.WelcomeLabel1.Font.Color := TextStrong;
  WizardForm.WelcomeLabel2.Font.Color := TextDim;
  WizardForm.FinishedHeadingLabel.Font.Color := TextStrong;
  WizardForm.FinishedLabel.Font.Color := TextDim;
  WizardForm.StatusLabel.Font.Color := TextDim;
  WizardForm.FilenameLabel.Font.Color := TextDim;

  PaintPage(WizardForm.WelcomePage);
  PaintPage(WizardForm.SelectDirPage);
  PaintPage(WizardForm.SelectComponentsPage);
  PaintPage(WizardForm.SelectProgramGroupPage);
  PaintPage(WizardForm.SelectTasksPage);
  PaintPage(WizardForm.ReadyPage);
  PaintPage(WizardForm.PreparingPage);
  PaintPage(WizardForm.InstallingPage);
  PaintPage(WizardForm.FinishedPage);
end;
