#define MyAppName "한키"
#define MyAppVersion "0.2.1"
#define MyAppBinaryVersion "0.2.1.0"
#define MyAppPublisher "Yulbyte"
#define MyAppExeName "Hanki.exe"
#define ReleaseOutputRoot "..\dist\0.2.1"
#define ReleasePayload "..\dist\0.2.1\Hanki-0.2.1-win-x64"

[Setup]
AppId={{D17F11A7-3AE2-4B85-A6B0-5E4C0A1F7D0E}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppSupportURL=mailto:huiyuli.dev@gmail.com
DefaultDirName={localappdata}\Programs\Yulbyte\Hanki
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir={#ReleaseOutputRoot}
OutputBaseFilename=HankiSetup-{#MyAppVersion}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes
RestartApplications=no
UninstallDisplayIcon={app}\{#MyAppExeName}
UninstallDisplayName={#MyAppName}
SetupIconFile=..\assets\branding\hanki-logo.ico
WizardImageFile=..\assets\branding\hanki-installer-wizard.png
WizardSmallImageFile=..\assets\branding\hanki-installer-small.png
VersionInfoVersion={#MyAppBinaryVersion}
VersionInfoCompany={#MyAppPublisher}
VersionInfoDescription={#MyAppName} Windows {#MyAppVersion} 설치 프로그램
VersionInfoProductName={#MyAppName}

[Tasks]
Name: "desktopicon"; Description: "바탕 화면 바로가기 만들기"; GroupDescription: "추가 바로가기"; Flags: unchecked

[Files]
Source: "{#ReleasePayload}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "설치 후 한키 실행"; Flags: nowait postinstall skipifsilent

[Code]
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  DataDir: String;
begin
  if CurUninstallStep = usPostUninstall then
  begin
    DataDir := ExpandConstant('{localappdata}\Yulbyte\Hanki');
    if DirExists(DataDir) then
    begin
      if UninstallSilent then
      begin
        Log('Silent uninstall: preserving user data at ' + DataDir);
      end
      else if MsgBox(
        '한키의 로그인 세션과 설정 데이터도 삭제할까요?' + #13#10 +
        '아니요를 선택하면 나중에 다시 설치하여 사용할 수 있습니다.',
        mbConfirmation,
        MB_YESNO) = IDYES then
      begin
        DelTree(DataDir, True, True, True);
      end;
    end;
  end;
end;
