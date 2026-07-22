#define MyAppName "KeyMan"
#define MyAppPublisher "jaywapp"
#define MyAppExeName "KeyMan.exe"

#ifndef MyAppVersion
  #define MyAppVersion "0.1.0"
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

[Setup]
AppId={{A4347C67-9F37-4FAF-A697-E8ED41A87290}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={localappdata}\Programs\KeyMan
DefaultGroupName=KeyMan
DisableProgramGroupPage=yes
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
VersionInfoVersion={#MyAppVersion}
VersionInfoCompany={#MyAppPublisher}
VersionInfoDescription=KeyMan installer
VersionInfoProductName={#MyAppName}

[Languages]
Name: "korean"; MessagesFile: "compiler:Languages\Korean.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "바탕 화면에 바로가기 만들기"; GroupDescription: "추가 바로가기:"; Flags: unchecked

[Files]
Source: "{#GuiSourceDir}\*"; DestDir: "{app}"; Excludes: "*.pdb"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#CliSourceDir}\keyman.exe"; DestDir: "{app}"; DestName: "key-man.exe"; Flags: ignoreversion
Source: "{#CliSourceDir}\keyman.exe"; DestDir: "{app}"; DestName: "keyman.exe"; Flags: ignoreversion
Source: "{#McpSourceDir}\key-man-mcp.exe"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\KeyMan"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\KeyMan"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Environment"; ValueType: expandsz; ValueName: "KEYMAN_HOME"; ValueData: "{app}"; Flags: preservestringtype uninsdeletevalue
Root: HKCU; Subkey: "Software\KeyMan"; ValueType: string; ValueName: "InstallPath"; ValueData: "{app}"; Flags: uninsdeletekey

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "KeyMan 실행"; Flags: nowait postinstall skipifsilent

[Code]
const
  UserEnvironmentKey = 'Environment';
  KeyManRegistryKey = 'Software\KeyMan';

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
    AddInstallDirectoryToPath;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usUninstall then
    RemoveInstallDirectoryFromPath;
end;
