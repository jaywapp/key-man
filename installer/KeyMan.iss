#define MyAppName "KeyMan"
#define MyAppPublisher "jaywapp"
#define MyAppExeName "KeyMan.Gui.exe"

#ifndef MyAppVersion
  #define MyAppVersion "1.1.0-alpha.1"
#endif

#ifndef MyAppFileVersion
  #define MyAppFileVersion "1.1.0.1"
#endif

#ifndef GuiSourceDir
  #define GuiSourceDir "..\artifacts\publish\gui"
#endif

#ifndef CliSourceDir
  #define CliSourceDir "..\artifacts\publish\cli"
#endif

#ifndef McpSourceDir
  #define McpSourceDir "..\artifacts\publish\mcp"
#endif

#ifndef InstallerOutputDir
  #define InstallerOutputDir "..\artifacts\installer"
#endif

#ifndef NoticesSourceDir
  #define NoticesSourceDir "..\artifacts\publish\notices"
#endif

[Setup]
AppId={{A4347C67-9F37-4FAF-A697-E8ED41A87290}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={localappdata}\Programs\KeyMan
DefaultGroupName=KeyMan
DisableProgramGroupPage=yes
CreateUninstallRegKey=IsNormalInstall
UsePreviousAppDir=IsNormalInstall
UsePreviousTasks=IsNormalInstall
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
OutputDir={#InstallerOutputDir}
OutputBaseFilename=KeyMan-Setup-{#MyAppVersion}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
UninstallDisplayIcon={app}\{#MyAppExeName}
ChangesEnvironment=yes
CloseApplications=yes
RestartApplications=no
VersionInfoVersion={#MyAppFileVersion}
VersionInfoCompany={#MyAppPublisher}
VersionInfoDescription=KeyMan installer
VersionInfoProductName={#MyAppName}

[Languages]
Name: "korean"; MessagesFile: "compiler:Languages\Korean.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "addtopath"; Description: "사용자 PATH에 KeyMan 추가"; GroupDescription: "명령줄 사용:"; Check: IsNormalInstall
Name: "desktopicon"; Description: "바탕 화면에 바로가기 만들기"; GroupDescription: "추가 바로가기:"; Flags: unchecked

[Files]
Source: "..\LICENSE"; DestDir: "{app}"; DestName: "LICENSE.txt"; Flags: ignoreversion
Source: "{#GuiSourceDir}\KeyMan.exe"; DestDir: "{app}"; DestName: "{#MyAppExeName}"; Flags: ignoreversion
Source: "{#CliSourceDir}\keyman.exe"; DestDir: "{app}"; DestName: "key-man.exe"; Flags: ignoreversion
Source: "{#CliSourceDir}\keyman.exe"; DestDir: "{app}"; DestName: "keyman.exe"; Flags: ignoreversion
Source: "{#McpSourceDir}\key-man-mcp.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#NoticesSourceDir}\*"; DestDir: "{app}\ThirdPartyNotices"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\KeyMan"; Filename: "{app}\{#MyAppExeName}"; Check: IsNormalInstall
Name: "{autodesktop}\KeyMan"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon; Check: IsNormalInstall

[Registry]
Root: HKCU; Subkey: "Environment"; ValueType: expandsz; ValueName: "KEYMAN_HOME"; ValueData: "{app}"; Flags: preservestringtype uninsdeletevalue; Check: IsNormalInstall
Root: HKCU; Subkey: "Software\KeyMan"; ValueType: string; ValueName: "InstallPath"; ValueData: "{app}"; Flags: uninsdeletekey; Check: IsNormalInstall

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "KeyMan 실행"; Flags: nowait postinstall skipifsilent; Check: IsNormalInstall

[UninstallDelete]
Type: files; Name: "{app}\.keyman-isolated-test"

[Code]
const
  UserEnvironmentKey = 'Environment';
  KeyManRegistryKey = 'Software\KeyMan';

function IsNormalInstall: Boolean;
begin
  Result := ExpandConstant('{param:ISOLATEDTEST|0}') <> '1';
end;

function NormalizePathEntry(Value: String): String;
begin
  Result := Trim(Value);
  while (Length(Result) > 3) and (Result[Length(Result)] = '\') do
    Delete(Result, Length(Result), 1);
end;

function PathContainsEntry(PathValue: String; Entry: String): Boolean;
var
  SeparatorPosition: Integer;
  Token: String;
begin
  Result := False;
  Entry := NormalizePathEntry(Entry);

  while PathValue <> '' do
  begin
    SeparatorPosition := Pos(';', PathValue);
    if SeparatorPosition = 0 then
    begin
      Token := PathValue;
      PathValue := '';
    end
    else
    begin
      Token := Copy(PathValue, 1, SeparatorPosition - 1);
      Delete(PathValue, 1, SeparatorPosition);
    end;

    if CompareText(NormalizePathEntry(Token), Entry) = 0 then
    begin
      Result := True;
      Exit;
    end;
  end;
end;

function RemovePathEntry(PathValue: String; Entry: String): String;
var
  SeparatorPosition: Integer;
  Token: String;
begin
  Result := '';
  Entry := NormalizePathEntry(Entry);

  while PathValue <> '' do
  begin
    SeparatorPosition := Pos(';', PathValue);
    if SeparatorPosition = 0 then
    begin
      Token := PathValue;
      PathValue := '';
    end
    else
    begin
      Token := Copy(PathValue, 1, SeparatorPosition - 1);
      Delete(PathValue, 1, SeparatorPosition);
    end;

    Token := Trim(Token);
    if (Token <> '') and (CompareText(NormalizePathEntry(Token), Entry) <> 0) then
    begin
      if Result <> '' then
        Result := Result + ';';
      Result := Result + Token;
    end;
  end;
end;

procedure AddInstallDirectoryToPath;
var
  CurrentPath: String;
  InstallDirectory: String;
  PathAdded: Cardinal;
begin
  InstallDirectory := NormalizePathEntry(ExpandConstant('{app}'));
  if not RegQueryStringValue(HKCU, UserEnvironmentKey, 'Path', CurrentPath) then
    CurrentPath := '';

  if not PathContainsEntry(CurrentPath, InstallDirectory) then
  begin
    if (CurrentPath <> '') and (CurrentPath[Length(CurrentPath)] <> ';') then
      CurrentPath := CurrentPath + ';';
    CurrentPath := CurrentPath + InstallDirectory;
    RegWriteExpandStringValue(HKCU, UserEnvironmentKey, 'Path', CurrentPath);
    RegWriteDWordValue(HKCU, KeyManRegistryKey, 'PathAdded', 1);
  end
  else if not RegQueryDWordValue(HKCU, KeyManRegistryKey, 'PathAdded', PathAdded) then
    RegWriteDWordValue(HKCU, KeyManRegistryKey, 'PathAdded', 0);
end;

procedure RemoveInstallDirectoryFromPath;
var
  CurrentPath: String;
  UpdatedPath: String;
  PathAdded: Cardinal;
begin
  if not RegQueryDWordValue(HKCU, KeyManRegistryKey, 'PathAdded', PathAdded) or (PathAdded <> 1) then
    Exit;

  if RegQueryStringValue(HKCU, UserEnvironmentKey, 'Path', CurrentPath) then
  begin
    UpdatedPath := RemovePathEntry(CurrentPath, ExpandConstant('{app}'));
    if UpdatedPath = '' then
      RegDeleteValue(HKCU, UserEnvironmentKey, 'Path')
    else
      RegWriteExpandStringValue(HKCU, UserEnvironmentKey, 'Path', UpdatedPath);
  end;
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
  begin
    if not IsNormalInstall then
      SaveStringToFile(ExpandConstant('{app}\.keyman-isolated-test'), '1', False)
    else if WizardIsTaskSelected('addtopath') then
      AddInstallDirectoryToPath;
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if (CurUninstallStep = usUninstall) and
    not FileExists(ExpandConstant('{app}\.keyman-isolated-test')) then
    RemoveInstallDirectoryFromPath;
end;
