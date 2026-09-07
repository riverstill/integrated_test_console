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
; 中文语言文件由 CI 从官方 issrc 仓库拉取到本目录（choco 版自带包缺失该文件）
Name: "chinesesimp"; MessagesFile: "ChineseSimplified.isl"

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
function BestPython(var Cmd: String): Boolean;
begin
  Result := True;
  if Exec('cmd.exe', '/c py -3 --version', '', SW_HIDE, ewWaitUntilTerminated, Res) and (Res = 0) then
    Cmd := 'py -3'
  else if Exec('cmd.exe', '/c python --version', '', SW_HIDE, ewWaitUntilTerminated, Res) and (Res = 0) then
    Cmd := 'python'
  else
    Result := False;
end;

function HasDotNet8(): Boolean;
var
  Names: TArrayOfString;
  I: Integer;
begin
  Result := False;
  // 任一 sharedfx Microsoft.WindowsDesktop.App 大版本 >= 8 即算通过
  if RegGetSubkeyNames(HKLM64, 'SOFTWARE\dotnet\Setup\InstalledVersions\x64\sharedfx\Microsoft.WindowsDesktop.App', Names) then
    for I := 0 to GetArrayLength(Names) - 1 do
      if (Length(Names[I]) > 0) and (Names[I][1] >= '8') then
        Result := True;
end;

function HasVisa(): Boolean;
begin
  Result := False;
  if RegKeyExists(HKLM64, 'SOFTWARE\National Instruments\NI-VISA') then Result := True
  else if RegKeyExists(HKLM64, 'SOFTWARE\Keysight\IO Libraries Suite') then Result := True
  else if RegKeyExists(HKLM64, 'SOFTWARE\Rohde-Schwarz\VISA') then Result := True
  else if FileExists(ExpandConstant('{sys}\visa64.dll')) then Result := True
  else if FileExists(ExpandConstant('{sys}\visa32.dll')) then Result := True;
end;

function InitializeSetup(): Boolean;
var
  Warn, PyCmd: String;
  Res: Integer;
begin
  Result := True;  // 全部只警告不拦截：Demo 模式无硬件也能用
  Warn := '';

  if not HasDotNet8() then
    Warn := Warn + '• 未检测到 .NET 8 Desktop Runtime，程序可能无法启动。' + #13#10 +
      '  请安装：https://dotnet.microsoft.com/download/dotnet/8.0' + #13#10#13#10;

  if BestPython(PyCmd) then
  begin
    if not (Exec('cmd.exe', '/c ' + PyCmd + ' -c "import pyvisa,numpy,matplotlib,openpyxl"',
        '', SW_HIDE, ewWaitUntilTerminated, Res) and (Res = 0)) then
      Warn := Warn + '• Python 依赖不全（需 pyvisa/pyvisa-py/numpy/matplotlib/openpyxl）。' + #13#10 +
        '  请执行：' + PyCmd + ' -m pip install -r engine\requirements.txt' + #13#10#13#10;
  end
  else
    Warn := Warn + '• 未检测到系统 Python（py -3 / python 均不可用）。' + #13#10 +
      '  请先安装 Python 3.9+，再执行：py -3 -m pip install -r engine\requirements.txt' + #13#10#13#10;

  if not HasVisa() then
    Warn := Warn + '• 未检测到 VISA 库（NI / Keysight / R&S 均无）。' + #13#10 +
      '  真机测量需要先装 VISA 及仪器 USB 驱动；可先勾选 Demo 模式验证流程。' + #13#10#13#10;

  if Warn <> '' then
    MsgBox('系统检查发现以下问题（安装可继续）：' + #13#10#13#10 + Warn,
      mbInformation, MB_OK);
end;
