; 集成测试控制台 — Inno Setup 安装脚本
; 用法(本地需先装 Inno Setup 6):
;   iscc /DMyAppVersion="0.2.0" installer/setup.iss
; CI 会自动从 csproj <Version> 取版本号传入，无需手改。
#ifndef MyAppVersion
  #define MyAppVersion "0.0.0-dev"
#endif

[Setup]
AppName=集成测试控制台
AppVersion={#MyAppVersion}
AppPublisher=riverstill
AppPublisherURL=https://github.com/riverstill/integrated_test_console
DefaultDirName={autopf}\IntegratedTestConsole
DefaultGroupName=集成测试控制台
PrivilegesRequired=lowest
OutputDir=..\installer-out
OutputBaseFilename=IntegratedTestConsole-Setup-{#MyAppVersion}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
UninstallDisplayName=集成测试控制台
ShowLanguageDialog=no

[Languages]
Name: "chinesesimp"; MessagesFile: "compiler:Languages\ChineseSimplified.isl"

[Tasks]
Name: "desktopicon"; Description: "创建桌面快捷方式"; GroupDescription: "附加选项:"

[Files]
; dist/ 由 CI 的 Assemble 步骤产出: exe + engine/ + Themes/ + HELP.html + packages/ + configs/
Source: "..\dist\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\集成测试控制台"; Filename: "{app}\Console.WPF.exe"
Name: "{group}\卸载"; Filename: "{uninstallexe}"
Name: "{autodesktop}\集成测试控制台"; Filename: "{app}\Console.WPF.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\Console.WPF.exe"; Description: "启动集成测试控制台"; Flags: nowait postinstall skipifsilent

[Code]
function HasPython(): Boolean;
var
  Res: Integer;
begin
  Result := False;
  if Exec('cmd.exe', '/c py -3 --version', '', SW_HIDE, ewWaitUntilTerminated, Res) and (Res = 0) then
    Result := True
  else if Exec('cmd.exe', '/c python --version', '', SW_HIDE, ewWaitUntilTerminated, Res) and (Res = 0) then
    Result := True;
end;

function InitializeSetup(): Boolean;
begin
  Result := True;
  if not HasPython() then
    MsgBox('未检测到系统 Python（py -3 / python 均不可用）。' + #13#10 +
           '安装可继续，但运行测试前请先安装 Python 3.9+ 并执行：' + #13#10 +
           'py -3 -m pip install -r engine\requirements.txt',
           mbInformation, MB_OK);
end;
