#define AppExeName "SyncTrayzor.exe"
#define AppRoot ".."
#define AppBin AppRoot + "\dist"
#define AppSrc AppRoot + "\src\SyncTrayzor"
#define AppExe AppBin + "\SyncTrayzor.exe"
#define AppName GetStringFileInfo(AppExe, "ProductName")
#define AppVersion GetVersionNumbersString(AppExe)
#define AppPublisher "SyncTrayzor"
#define AppURL "https://github.com/GermanCoding/SyncTrayzor"
#define AppDataFolder "SyncTrayzor"
#define V1AppData   "{userappdata}\SyncTrayzor"
#define LegacyX86AppId "{c9bab27b-d754-4b62-ad8c-3509e1cac15c}"
#define BackupStamp "_v1_backup"
#define SyncthingFolder "Syncthing"

[Setup]
AppId={{#AppId}
AppName={#AppName} ({#Arch})
AppVersion={#AppVersion}
VersionInfoVersion={#AppVersion}
;AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppURL}
AppSupportURL={#AppURL}
AppUpdatesURL={#AppURL}
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
AllowNoIcons=yes
OutputDir="..\release\"
OutputBaseFilename={#AppName}Setup-{#Arch}
SetupIconFile={#AppSrc}\Icons\default.ico
WizardSmallImageFile=icon.bmp
Compression=lzma2/max
;Compression=None
SolidCompression=yes
PrivilegesRequired=admin
PrivilegesRequiredOverridesAllowed=dialog
CloseApplications=yes
RestartApplications=no
; If we try and close CefSharp.BrowserSubprocess.exe we'll fail - it doesn't respond well
; However if we close *just* SyncTrayzor, that will take care of shutting down CefSharp and syncthing
CloseApplicationsFilter=SyncTrayzor.exe
TouchDate=current
WizardStyle=modern
; We do access user areas, but only as a best-effort attempt to clean up after ourselves
UsedUserAreasWarning=no
#if "x64" == Arch
ArchitecturesInstallIn64BitMode=x64os
ArchitecturesAllowed=x64os
#else
ArchitecturesInstallIn64BitMode=arm64
ArchitecturesAllowed=arm64
#endif

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Dirs]
Name: "{userappdata}\{#AppDataFolder}"

[Files]
Source: "{#AppBin}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExeName}"
Name: "{group}\{cm:UninstallProgram,{#AppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(AppName, '&', '&&')}}"; Flags: nowait postinstall; Parameters: {code:SyncTrayzorStartFlags}; Check: ShouldStartSyncTrayzor

[Code]
// -------------------------------------------------------------------
// GLOBALS
// -------------------------------------------------------------------
var
  OldVersionDetected: Boolean;
  OldVersion      : string;
  OldUninstString    : string;
  OldInstallDir   : string;
  BackupDir       : string;
  BackupAutostartValue: string;
  BackupAutostartExists: Boolean;

  InfoPage : TWizardPage;
  PgLabel : TNewStaticText;
  PgCheckbox : TNewCheckBox;

// -------------------------------------------------------------------
// AUTOSTART REGISTRY BACKUP  ----------------------------------------
// -------------------------------------------------------------------
function BackupAutostartRegistry(): Boolean;
var
  RegValue: string;
begin
  Result := False;
  BackupAutostartExists := False;
  BackupAutostartValue := '';

  if RegQueryStringValue(HKEY_CURRENT_USER, 'SOFTWARE\Microsoft\Windows\CurrentVersion\Run', 'SyncTrayzor', RegValue) then
  begin
    BackupAutostartExists := True;
    BackupAutostartValue := RegValue;
    Result := True;
    Log('Backed up autostart registry: ' + RegValue);
  end
  else
  begin
    Log('No autostart registry entry found to backup');
  end;
end;

procedure RestoreAutostartRegistry();
var
  NewValue: string;
  HasMinimized: Boolean;
begin
  if not BackupAutostartExists then
  begin
    Log('No autostart registry to restore');
    Exit;
  end;

  // Check if the old value contained -minimized
  HasMinimized := Pos('-minimized', BackupAutostartValue) > 0;

  // Build new registry value with current install path
  NewValue := '"' + ExpandConstant('{app}\{#AppExeName}') + '"';
  if HasMinimized then
    NewValue := NewValue + ' -minimized';

  // Write the new autostart registry entry
  if RegWriteStringValue(HKEY_CURRENT_USER, 'SOFTWARE\Microsoft\Windows\CurrentVersion\Run', 'SyncTrayzor', NewValue) then
  begin
    Log('Restored autostart registry: ' + NewValue);
  end
  else
  begin
    Log('Failed to restore autostart registry');
  end;
end;

procedure BumpInstallCount;
var
  FileContents: AnsiString;
  InstallCount: integer;
begin
  { Increment the install count in InstallCount.txt if it exists, or create it with the contents '1' if it doesn't }
  if LoadStringFromFile(ExpandConstant('{app}\InstallCount.txt'), FileContents) then
  begin
    InstallCount := StrTointDef(Trim(string(FileContents)), 0) + 1;
  end
  else
  begin
    InstallCount := 1;
  end;

  SaveStringToFile(ExpandConstant('{app}\InstallCount.txt'), IntToStr(InstallCount), False);
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  FindRec: TFindRec;
  FolderPath: String;
  FilePath: String;
  ExeConfig: String;
begin
  if CurStep = ssInstall then
  begin
    BumpInstallCount();

    { We might be being run from ProcessRunner.exe, *and* we might be trying to update it. Funsies. Let's rename it (which Windows lets us do) }
    DeleteFile(ExpandConstant('{app}\ProcessRunner.exe.old'));
    RenameFile(ExpandConstant('{app}\ProcessRunner.exe'), ExpandConstant('{app}\ProcessRunner.exe.old'));

    Log(ExpandConstant('Looking for resource files in {app}\*'));
    { Remove resource files. This means that out-of-date languages will be removed, which (as a last-ditch resore) will alert maintainers that something's wrong }
    if FindFirst(ExpandConstant('{app}\*'), FindRec) then
    begin
      try
        repeat
          if (FindRec.Attributes and FILE_ATTRIBUTE_DIRECTORY <> 0) and (FindRec.Name <> '.') and (FindRec.Name <> '..') then
          begin
            FolderPath :=  ExpandConstant('{app}\') + FindRec.Name;
            FilePath := FolderPath + '\SyncTrayzor.resources.dll';
            if DeleteFile(FilePath) then
            begin
              Log('Deleted ' + FilePath);
              if DelTree(FolderPath, True, False, False) then
                Log('Deleted ' + FolderPath);
            end;
          end;
        until not FindNext(FindRec);
      finally
        FindClose(FindRec);
      end;
    end;
  end
  else if CurStep = ssPostInstall then
  begin
    if OldVersionDetected then
      RestoreAutostartRegistry();
    ExeConfig := ExpandConstant('{param:SyncTrayzorExeConfig}');
    if ExeConfig <> '' then
    begin
      if FileExists(ExeConfig) then
      begin
        CopyFile(ExeConfig, ExpandConstant('{app}\SyncTrayzor.dll.config'), false);
      end
      else
      begin
        MsgBox('Could not find SyncTrayzorExeConfig file: ' + ExeConfig + '. Using default.', mbError, MB_OK);
      end
    end
  end
end;

function CmdLineParamGiven(const Value: String): Boolean;
var
  I: Integer;  
begin
  // Can't use {param}, as it doesn't match flags with no value
  Result := False;
  for I := 1 to ParamCount do
    if CompareText(ParamStr(I), Value) = 0 then
    begin
      Result := True;
      Exit;
    end;
end;

function ShouldStartSyncTrayzor(): Boolean;
begin
  Result := (not WizardSilent()) or CmdLineParamGiven('/StartSyncTrayzor');
end;

function SyncTrayzorStartFlags(param: String): String;
begin
   if WizardSilent() then begin
      Result := '-minimized'
   end else begin
      Result := ''
   end;
end;

// From https://stackoverflow.com/a/14415103
function CmdLineParamExists(const Value: string): Boolean;
var
  I: Integer;
begin
  Result := False;
  for I := 1 to ParamCount do
    if CompareText(ParamStr(I), Value) = 0 then
    begin
      Result := True;
      Exit;
    end;
end;

// We won't be able to find keys for users other than the one running the installer, but try and do
// a best-effort attempt to cleaning ourselves up.
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  keyValueNames: TArrayOfString;
  keyValue: String;
  i: Integer;
  mres : integer;
begin
  case CurUninstallStep of
      usPostUninstall:
        begin
            if CmdLineParamExists('/DELETEALL') then
              mres := IDYES
            else
              mres := SuppressibleMsgBox('Remove all syncthing configuration, metadata, folders, device ID? This cannot be undone.', mbConfirmation, MB_YESNO or MB_DEFBUTTON2, IDNO);

            if mres = IDYES then
            begin
              DelTree(ExpandConstant('{userappdata}\{#AppDataFolder}'), True, True, True);
              DelTree(ExpandConstant('{localappdata}\{#AppDataFolder}'), True, True, True);
              DelTree(ExpandConstant('{localappdata}\{#SyncthingFolder}'), True, True, True);
            end;
       end;
   end;
  if CurUninstallStep = usPostUninstall then
  begin
    if RegGetValueNames(HKEY_CURRENT_USER, 'SOFTWARE\Microsoft\Windows\CurrentVersion\Run', keyValueNames) then
    begin
      for i := 0 to GetArrayLength(keyValueNames)-1 do
      begin
        if Pos('SyncTrayzor', keyValueNames[i]) = 1 then
        begin
          if RegQueryStringValue(HKEY_CURRENT_USER, 'SOFTWARE\Microsoft\Windows\CurrentVersion\Run', keyValueNames[i], keyValue) then
          begin
            if Pos(ExpandConstant('"{app}\{#AppExeName}"'), keyValue) = 1 then
            begin
              RegDeleteValue(HKEY_CURRENT_USER, 'SOFTWARE\Microsoft\Windows\CurrentVersion\Run', keyValueNames[i]);
            end;
          end;
        end
      end;
    end;
  end;
end;

// -------------------------------------------------------------------
// v1 DETECTION  -----------------------------------------------------
// -------------------------------------------------------------------
function DetectV1(): Boolean;
var
  RootKey, UninstPath: string;
  OldPackedVersion: Int64;
begin
  Result := False;

  RootKey     := 'Software\Microsoft\Windows\CurrentVersion\Uninstall\';
  UninstPath  := RootKey + '{#AppId}' + '_is1';

  if RegQueryStringValue(HKLM, UninstPath, 'DisplayVersion', OldVersion) then
  begin
    if not StrToVersion(OldVersion, OldPackedVersion) then
      begin
        Log('Invalid version format: ' + OldVersion);
        Result := False;
        exit;
      end;

    Result := ComparePackedVersion(OldPackedVersion, PackVersionComponents(2,0,0,0)) < 0;
    RegQueryStringValue(HKLM, UninstPath, 'QuietUninstallString', OldUninstString);
    RegQueryStringValue(HKLM, UninstPath, 'InstallLocation', OldInstallDir);
  end;
end;

function EnsureNoLegacyX86(): Boolean;
var
  RootKey, UninstPath, OldInstallDirX86: string;
begin
  Result := True;

  RootKey     := 'SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\';
  UninstPath  := RootKey + '{#LegacyX86AppId}' + '_is1';

  if RegQueryStringValue(HKLM, UninstPath, 'InstallLocation', OldInstallDirX86) then
  begin
     SuppressibleMsgBox('Found an old 32-bit version of SyncTrayzor already installed. You must uninstall it before installing the 64-bit version. Installed at: '+OldInstallDirX86, mbError, MB_OK, IDOK);
     Result := False;
  end;
end;

// -------------------------------------------------------------------
// APPDATA BACKUP  ----------------------------------------------------
// -------------------------------------------------------------------
procedure RecursiveCopy(const SrcDir, DstDir: string);
var
  FindRec: TFindRec;
begin
  if DirExists(SrcDir) then
  begin
    ForceDirectories(DstDir);
    if FindFirst(AddBackslash(SrcDir) + '*', FindRec) then
    begin
      try
        repeat
          if (FindRec.Name <> '.') and (FindRec.Name <> '..') then
          begin
            if (FindRec.Attributes and FILE_ATTRIBUTE_DIRECTORY) <> 0 then
              RecursiveCopy(AddBackslash(SrcDir) + FindRec.Name,
                            AddBackslash(DstDir) + FindRec.Name)
            else
              CopyFile(AddBackslash(SrcDir) + FindRec.Name,
                       AddBackslash(DstDir) + FindRec.Name, False);
          end;
        until not FindNext(FindRec);
      finally
        FindClose(FindRec);
      end;
    end;
  end;
end;

procedure BackupAppData();
begin
  BackupDir := ExpandConstant('{tmp}') + '\' + '{#AppId}' + '{#BackupStamp}';
  RecursiveCopy(ExpandConstant('{#V1AppData}'), BackupDir);
end;

procedure RestoreAppData();
begin
  if DirExists(BackupDir) then
    RecursiveCopy(BackupDir, ExpandConstant('{#V1AppData}'));
end;

// -------------------------------------------------------------------
// WIZARD EVENT HOOKS  ------------------------------------------------
// -------------------------------------------------------------------
function InitializeSetup(): Boolean;
begin
  Result := True;
  if not EnsureNoLegacyX86() then
  begin
    Result := False;
  end;
end;

procedure InitializeWizard();
begin
  OldVersionDetected := DetectV1();
  Log('SyncTrayzor v1 installed: ' + IntToStr(Integer(OldVersionDetected)));

  if OldVersionDetected then
  begin
    // Extra page inserted between Welcome and License
    InfoPage := CreateCustomPage(wpWelcome, '{#AppName} upgrade',
      'A previous version of SyncTrayzor (' + OldVersion + ') was found. ' +
      'It will be removed automatically before the new version is installed.');

    PgLabel := TNewStaticText.Create(InfoPage);
    PgLabel.Parent := InfoPage.Surface;
    PgLabel.Caption :=
      '• Your settings will be preserved unless you tick the box below.'#13#10 +
      '• If you decide to start fresh, SyncTrayzor will upgrade you to syncthing v2.'#13#10 +
      '• Please wait – the uninstaller may take a moment.';

    PgCheckbox := TNewCheckBox.Create(InfoPage);
    PgCheckbox.Parent := InfoPage.Surface;
    PgCheckbox.Top := PgLabel.Top + PgLabel.Height + 8;
    PgCheckbox.Width := InfoPage.SurfaceWidth - 2 * PgCheckbox.Left;
    PgCheckbox.Caption := 'Start fresh - DELETE previous SyncTrayzor configuration and old syncthing.exe!';
    PgCheckbox.Checked := False;
  end;
end;

function NextButtonClick(CurPageID: Integer): Boolean;
var
  ResultCode: Integer;
begin
  Result := True;  // allow page switch unless we veto

  if Assigned(InfoPage) and (CurPageID = InfoPage.ID) and OldVersionDetected then
  begin
    // 1) Backup
    if not PgCheckbox.Checked then
    begin
      BackupAppData();
      BackupAutostartRegistry();
    end;

    // 2) Run uninstaller
    if (Exec('>', OldUninstString, '', SW_SHOWNORMAL, ewWaitUntilTerminated, ResultCode)) then
    begin
      // 3) Restore
      if not PgCheckbox.Checked then
        RestoreAppData();
    end
    else
    begin
      MsgBox('Could not launch the previous uninstaller (' +
            OldUninstString + '): ' + SysErrorMessage(ResultCode) + '.'#13#10'Setup cannot continue.',
            mbError, MB_OK);
      Result := False;  // stay on page
    end;
  end;
end;

[UninstallRun]
Filename: "{app}\SyncTrayzor.exe"; Parameters: "--shutdown"; RunOnceId: "ShutdownSyncTrayzor"; Flags: skipifdoesntexist

[UninstallDelete]
Type: files; Name: "{app}\ProcessRunner.exe.old"
Type: files; Name: "{app}\InstallCount.txt"

