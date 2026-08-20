; prettyeyes installer. Per-user install: a tray utility has no business
; asking for administrator rights.
#define AppName "prettyeyes"
#define AppVersion "1.2.0"
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
; Closes the running copy instead of asking the user to do it. AppMutex used to
; guard this, but its check runs first and all it can do is put up a modal that
; says "close it yourself"; this way Setup offers to close it and puts it back
; afterwards, which is also what the built-in updater needs.
CloseApplications=yes
RestartApplications=yes

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
; No skipifsilent on purpose. The built-in updater runs Setup with /SILENT, and
; a skipped entry there means the program never comes back after updating
; itself. RestartApplications cannot cover for it either: the Restart Manager
; only puts back applications that called RegisterApplicationRestart, and this
; one does not. Started twice - here and by the Restart Manager - is harmless:
; the second copy sees the single-instance mutex and exits.
Filename: "{app}\{#AppExe}"; Description: "{cm:LaunchApp}"; Flags: nowait postinstall

[Code]
// Inno has no dark theme, so the wizard is repainted by hand. Colours are
// Delphi BGR literals, not the RGB used everywhere else in this project.
const
  Bg = $0B0A0A;          // #0A0A0B, the surface token
  BgDeep = $000000;      // pure black, same as the app background
  TextStrong = $F0F0F0;
  TextDim = $9A9A9A;

procedure PaintControl(Control: TControl); forward;

procedure PaintChildren(Parent: TWinControl);
var
  Index: Integer;
begin
  for Index := 0 to Parent.ControlCount - 1 do
    PaintControl(Parent.Controls[Index]);
end;

procedure PaintControl(Control: TControl);
begin
  // Inno nests controls inside panels, so a single pass over the direct
  // children of a page leaves half the wizard white.
  if Control is TNewStaticText then
  begin
    TNewStaticText(Control).Font.Color := TextStrong;
    TNewStaticText(Control).Color := Bg;
  end
  else if Control is TNewCheckBox then
  begin
    TNewCheckBox(Control).Font.Color := TextStrong;
    TNewCheckBox(Control).Color := Bg;
  end
  else if Control is TNewRadioButton then
  begin
    TNewRadioButton(Control).Font.Color := TextStrong;
    TNewRadioButton(Control).Color := Bg;
  end
  // The two radios on the "preparing to install" page are plain VCL controls,
  // not the TNew* ones the rest of the wizard is built from, so they slipped
  // through and stayed black on black.
  else if Control is TRadioButton then
  begin
    TRadioButton(Control).Font.Color := TextStrong;
    TRadioButton(Control).Color := Bg;
  end
  else if Control is TCheckBox then
  begin
    TCheckBox(Control).Font.Color := TextStrong;
    TCheckBox(Control).Color := Bg;
  end
  else if Control is TNewCheckListBox then
  begin
    TNewCheckListBox(Control).Color := Bg;
    TNewCheckListBox(Control).Font.Color := TextStrong;
    TNewCheckListBox(Control).BorderStyle := bsNone;
  end
  else if Control is TNewEdit then
  begin
    TNewEdit(Control).Color := BgDeep;
    TNewEdit(Control).Font.Color := TextStrong;
  end
  else if Control is TNewMemo then
  begin
    TNewMemo(Control).Color := BgDeep;
    TNewMemo(Control).Font.Color := TextStrong;
    // The scrollbars are drawn by Windows, not by the wizard, and stay white
    // against everything else here. The box holds a task list of one line, so
    // they are taken away rather than fought with; wrapping covers the case of
    // a long install path that would otherwise need the horizontal one.
    TNewMemo(Control).WordWrap := True;
    TNewMemo(Control).ScrollBars := ssNone;
  end
  else if Control is TListBox then
  begin
    TListBox(Control).Color := BgDeep;
    TListBox(Control).Font.Color := TextStrong;
  end
  else if Control is TLabel then
  begin
    TLabel(Control).Font.Color := TextDim;
    TLabel(Control).Transparent := True;
  end
  else if Control is TBevel then
    TBevel(Control).Visible := False
  else if Control is TPanel then
  begin
    TPanel(Control).Color := Bg;
    TPanel(Control).BevelOuter := bvNone;
  end;

  if Control is TWinControl then
    PaintChildren(TWinControl(Control));
end;

procedure PaintPage(Page: TNewNotebookPage);
begin
  Page.Color := Bg;
  PaintChildren(Page);
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
    begin
      TPanel(Child).Color := Bg;
      TPanel(Child).BevelOuter := bvNone;
      PaintChildren(TWinControl(Child));
    end
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

  // InnerPage is the container the other pages sit inside; unpainted it shows
  // as a white ring around every dark page.
  PaintPage(WizardForm.InnerPage);
  PaintPage(WizardForm.WelcomePage);
  PaintPage(WizardForm.SelectDirPage);
  PaintPage(WizardForm.SelectComponentsPage);
  PaintPage(WizardForm.SelectProgramGroupPage);
  PaintPage(WizardForm.SelectTasksPage);
  PaintPage(WizardForm.ReadyPage);
  PaintPage(WizardForm.PreparingPage);

  WizardForm.PreparingLabel.Font.Color := TextStrong;

  PaintPage(WizardForm.InstallingPage);
  PaintPage(WizardForm.FinishedPage);
end;

procedure CurPageChanged(CurPageID: Integer);
begin
  if CurPageID <> wpPreparing then
    Exit;

  // No choice offered here, and none needed: this installer is run to update
  // prettyeyes, closing it is the only way to replace its own executable, and
  // it is started again afterwards. The radios that used to ask are gone -
  // their captions are drawn by the Windows theme, which ignores every colour
  // it is given and left them as dark text on this dark page.
  WizardForm.PreparingYesRadio.Checked := True;
  WizardForm.PreparingYesRadio.Visible := False;
  WizardForm.PreparingNoRadio.Visible := False;

  WizardForm.PreparingLabel.Caption :=
    'prettyeyes сейчас запущен. Программа установки закроет его, обновит и запустит снова.';
end;
