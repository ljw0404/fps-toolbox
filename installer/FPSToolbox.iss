; FPSToolbox Inno Setup 安装脚本
;
; 编译命令(在线版,小):
;   iscc.exe installer\FPSToolbox.iss
;
; 编译命令(离线版,内嵌 .NET 8 运行时,~60 MB):
;   iscc.exe /DOFFLINE /DDOTNET_RUNTIME_FILE=".\runtimes\windowsdesktop-runtime-8.0.11-win-x64.exe" installer\FPSToolbox.iss
;
; 指定版本号:
;   iscc.exe /DAppVersion=1.2.3 installer\FPSToolbox.iss
;
; 在线版会在安装时从 Microsoft 官方 CDN 自动下载 .NET 8 运行时。
; 离线版会直接静默安装随包的 runtime 安装器。

#ifndef AppVersion
  #define AppVersion "1.0.0"
#endif

#define AppName      "FPS 工具箱"
#define AppId        "FPSToolbox"
#define AppPublisher "FPSToolbox"
#define AppURL       "https://github.com/ljw0404/fps-toolbox"
#define AppExeName   "FPSToolbox.exe"
#define SourceDir    "..\dist\payload"

; .NET 8 Desktop Runtime 版本(离线版需要对应下载)
#define DotNetVersion "8.0.11"
#define DotNetUrl     "https://builds.dotnet.microsoft.com/dotnet/WindowsDesktop/8.0.11/windowsdesktop-runtime-8.0.11-win-x64.exe"

#ifdef OFFLINE
  #define OutputSuffix "_offline"
#else
  #define OutputSuffix ""
#endif

[Setup]
AppId={{C3D4E5F6-A7B8-9012-CDEF-234567890123}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppURL}
DefaultDirName={autopf}\FPSToolbox
DefaultGroupName=FPS 工具箱
CreateUninstallRegKey=yes
UsePreviousGroup=yes
AllowNoIcons=yes
OutputDir=output
OutputBaseFilename=FPSToolbox_Setup_v{#AppVersion}{#OutputSuffix}
WizardStyle=modern
Compression=lzma2/ultra64
SolidCompression=yes
ArchitecturesInstallIn64BitMode=x64compatible
ArchitecturesAllowed=x64compatible
SetupIconFile=..\src\FPSToolbox\Resources\icon.ico
UninstallDisplayIcon={app}\{#AppExeName}
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=commandline dialog
DisableDirPage=no
DisableProgramGroupPage=no
VersionInfoVersion={#AppVersion}
VersionInfoDescription={#AppName} Installer
VersionInfoCopyright=Copyright (C) 2026 {#AppPublisher}

[Languages]
Name: "chinesesimplified"; MessagesFile: "Languages\ChineseSimplified.isl"
Name: "english";           MessagesFile: "compiler:Default.isl"

[Types]
Name: "full";    Description: "完整安装（主程序 + 四个工具）"
Name: "compact"; Description: "最小安装（仅主程序）"
Name: "custom";  Description: "自定义安装";                       Flags: iscustom

[Components]
Name: "main";        Description: "FPS 工具箱主程序（必需）"; Types: full compact custom; Flags: fixed
Name: "crosshair";   Description: "屏幕准心工具";            Types: full
Name: "gamma";       Description: "屏幕调节工具";            Types: full
Name: "nightvision"; Description: "智能夜视滤镜";            Types: full
Name: "mousetool";   Description: "鼠鼠工具（行情 + 装备维修）"; Types: full

[Tasks]
Name: "desktopicon";  Description: "创建桌面快捷方式"; GroupDescription: "附加图标:"; Flags: unchecked
Name: "startupentry"; Description: "开机自动启动 FPS 工具箱（最小化到托盘）"; GroupDescription: "启动选项:"; Flags: unchecked

[Files]
; 主程序
Source: "{#SourceDir}\FPSToolbox.exe";               DestDir: "{app}"; Flags: ignoreversion; Components: main
Source: "{#SourceDir}\FPSToolbox.dll";               DestDir: "{app}"; Flags: ignoreversion; Components: main
Source: "{#SourceDir}\FPSToolbox.runtimeconfig.json"; DestDir: "{app}"; Flags: ignoreversion; Components: main
Source: "{#SourceDir}\FPSToolbox.deps.json";         DestDir: "{app}"; Flags: ignoreversion; Components: main
Source: "{#SourceDir}\FPSToolbox.Shared.dll";        DestDir: "{app}"; Flags: ignoreversion; Components: main
Source: "{#SourceDir}\*.dll";                        DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist; Excludes: "FPSToolbox.Shared.dll,FPSToolbox.dll"; Components: main
Source: "{#SourceDir}\Resources\*";                  DestDir: "{app}\Resources"; Flags: ignoreversion recursesubdirs skipifsourcedoesntexist; Components: main

; 屏幕准心工具(uninsneveruninstall:Inno 默认不删;卸载时由 [Code] 段根据用户勾选决定)
Source: "{#SourceDir}\tools\CrosshairTool\*"; DestDir: "{app}\tools\CrosshairTool"; \
    Flags: ignoreversion recursesubdirs createallsubdirs uninsneveruninstall; Components: crosshair

; 屏幕调节工具(同上)
Source: "{#SourceDir}\tools\GammaTool\*"; DestDir: "{app}\tools\GammaTool"; \
    Flags: ignoreversion recursesubdirs createallsubdirs uninsneveruninstall; Components: gamma

; 智能夜视滤镜(同上)
Source: "{#SourceDir}\tools\NightVisionTool\*"; DestDir: "{app}\tools\NightVisionTool"; \
    Flags: ignoreversion recursesubdirs createallsubdirs uninsneveruninstall; Components: nightvision

; 鼠鼠工具(同上)
Source: "{#SourceDir}\tools\MouseTool\*"; DestDir: "{app}\tools\MouseTool"; \
    Flags: ignoreversion recursesubdirs createallsubdirs uninsneveruninstall; Components: mousetool

#ifdef OFFLINE
; 离线版:内嵌 .NET Desktop Runtime 安装器
Source: "{#DOTNET_RUNTIME_FILE}"; DestDir: "{tmp}"; Flags: deleteafterinstall; DestName: "dotnet-runtime.exe"; Check: NeedsDotNet
#endif

[Icons]
Name: "{group}\FPS 工具箱";      Filename: "{app}\{#AppExeName}"
Name: "{group}\卸载 FPS 工具箱"; Filename: "{uninstallexe}"
Name: "{userdesktop}\FPS 工具箱"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "SOFTWARE\Microsoft\Windows\CurrentVersion\Run"; \
  ValueType: string; ValueName: "FPSToolbox"; \
  ValueData: """{app}\{#AppExeName}"" --minimized"; \
  Flags: uninsdeletevalue; Tasks: startupentry

[Run]
#ifdef OFFLINE
; 离线版:安装前先装 .NET(如果缺)
Filename: "{tmp}\dotnet-runtime.exe"; Parameters: "/install /quiet /norestart"; \
    StatusMsg: "正在安装 .NET 8 运行时..."; Check: NeedsDotNet
#endif
; 交互安装:在向导末页显示"立即运行"勾选框(用户可勾可不勾)
Filename: "{app}\{#AppExeName}"; Description: "立即运行 FPS 工具箱"; \
    Flags: nowait postinstall skipifsilent
; 静默安装(主框架"立即更新"走 /SILENT):装完自动启动新版,不显示任何提示
Filename: "{app}\{#AppExeName}"; Flags: nowait runasoriginaluser; Check: WizardSilent

[UninstallRun]
; 卸载前先关掉所有相关进程
Filename: "taskkill.exe"; Parameters: "/f /im FPSToolbox.exe";     Flags: runhidden; RunOnceId: "KillMain"
Filename: "taskkill.exe"; Parameters: "/f /im CrosshairTool.exe";  Flags: runhidden; RunOnceId: "KillCross"
Filename: "taskkill.exe"; Parameters: "/f /im GammaTool.exe";      Flags: runhidden; RunOnceId: "KillGamma"
Filename: "taskkill.exe"; Parameters: "/f /im NightVisionTool.exe"; Flags: runhidden; RunOnceId: "KillNight"
Filename: "taskkill.exe"; Parameters: "/f /im MouseTool.exe";      Flags: runhidden; RunOnceId: "KillMouse"

[Code]
#ifndef OFFLINE
var
  DownloadPage: TDownloadWizardPage;
#endif

// ──────────────────────────────────────────────────────────────
// .NET 8 检测(只要装了 8.x 的任一小版本就满足)
// ──────────────────────────────────────────────────────────────
function ExistsDotNet8Dir(): Boolean;
var
  FindRec: TFindRec;
  Pattern: String;
begin
  Result := False;
  Pattern := ExpandConstant('{commonpf64}\dotnet\shared\Microsoft.WindowsDesktop.App\8.*');
  if FindFirst(Pattern, FindRec) then
  try
    repeat
      if ((FindRec.Attributes and FILE_ATTRIBUTE_DIRECTORY) <> 0)
         and (FindRec.Name <> '.') and (FindRec.Name <> '..') then
      begin
        Result := True;
        Exit;
      end;
    until not FindNext(FindRec);
  finally
    FindClose(FindRec);
  end;
end;

function IsDotNet8Installed(): Boolean;
var
  Names: TArrayOfString;
  I: Integer;
begin
  Result := False;
  // 最准:注册表里列出的 WindowsDesktop 子版本
  if RegGetSubkeyNames(HKLM64, 'SOFTWARE\dotnet\Setup\InstalledVersions\x64\sharedfx\Microsoft.WindowsDesktop.App', Names) then
  begin
    for I := 0 to GetArrayLength(Names) - 1 do
      if (Length(Names[I]) >= 2) and (Copy(Names[I], 1, 2) = '8.') then
      begin
        Result := True;
        Exit;
      end;
  end;
  // 兜底:查 Program Files\dotnet\shared\Microsoft.WindowsDesktop.App\8.*
  if not Result then
    Result := ExistsDotNet8Dir();
end;

// 是否需要安装 .NET(供 [Files]/[Run] 的 Check 使用)
function NeedsDotNet(): Boolean;
begin
  Result := not IsDotNet8Installed();
end;

// ──────────────────────────────────────────────────────────────
// 在线版:创建下载页,在进入 ssInstall 之前拉 .NET runtime
// ──────────────────────────────────────────────────────────────
#ifndef OFFLINE
procedure InitializeWizard;
begin
  DownloadPage := CreateDownloadPage(
    SetupMessage(msgWizardPreparing),
    '正在下载 .NET 8 运行时(仅首次需要)...',
    nil);
end;

// PrepareToInstall 在所有页过完、文件落盘之前执行。对 /SILENT 和交互两种模式都通用。
// 返回非空字符串会中止安装并把字符串当作错误显示。
function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  Result := '';
  if not NeedsDotNet() then Exit;

  DownloadPage.Clear;
  DownloadPage.Add('{#DotNetUrl}', 'dotnet-runtime.exe', '');
  DownloadPage.Show;
  try
    try
      DownloadPage.Download;
    except
      Result := '下载 .NET 8 运行时失败:' + #13#10 + GetExceptionMessage() + #13#10 + #13#10 +
                '请检查网络后重试,或改用离线版安装包(含 runtime)。' + #13#10 +
                '官方下载: https://dotnet.microsoft.com/download/dotnet/8.0';
    end;
  finally
    DownloadPage.Hide;
  end;
end;

// 安装阶段执行下载好的 runtime
procedure CurStepChanged(CurStep: TSetupStep);
var
  ResultCode: Integer;
begin
  if CurStep = ssInstall then
  begin
    Exec('taskkill.exe', '/f /im FPSToolbox.exe',      '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
    Exec('taskkill.exe', '/f /im CrosshairTool.exe',   '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
    Exec('taskkill.exe', '/f /im GammaTool.exe',       '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
    Exec('taskkill.exe', '/f /im NightVisionTool.exe', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
    Exec('taskkill.exe', '/f /im MouseTool.exe',       '', SW_HIDE, ewWaitUntilTerminated, ResultCode);

    if NeedsDotNet() then
    begin
      WizardForm.StatusLabel.Caption := '正在安装 .NET 8 运行时...';
      if not Exec(ExpandConstant('{tmp}\dotnet-runtime.exe'),
                  '/install /quiet /norestart',
                  '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
        MsgBox('.NET 8 安装失败(错误码 ' + IntToStr(ResultCode) + ')。' + #13#10 +
               '主程序可能无法启动,请手动安装 .NET 8 Desktop Runtime。',
               mbError, MB_OK);
    end;
  end;
end;
#else
// 离线版:只在 ssInstall 时 kill 进程, runtime 由 [Run] 段用 Check=NeedsDotNet 安装
procedure CurStepChanged(CurStep: TSetupStep);
var
  ResultCode: Integer;
begin
  if CurStep = ssInstall then
  begin
    Exec('taskkill.exe', '/f /im FPSToolbox.exe',      '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
    Exec('taskkill.exe', '/f /im CrosshairTool.exe',   '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
    Exec('taskkill.exe', '/f /im GammaTool.exe',       '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
    Exec('taskkill.exe', '/f /im NightVisionTool.exe', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
    Exec('taskkill.exe', '/f /im MouseTool.exe',       '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  end;
end;
#endif

// ──────────────────────────────────────────────────────────────
// 进入组件选择页时,扫 {app}\tools\ 把已存在的子工具自动勾选
// (升级 / 重装 / 之前卸载时保留了子工具 三种场景都能识别)
// ──────────────────────────────────────────────────────────────
procedure AutoCheckExistingTools;
var
  I: Integer;
  AppDir, Desc: String;
  HasCrosshair, HasGamma, HasNight, HasMouse: Boolean;
begin
  AppDir := WizardForm.DirEdit.Text;
  HasCrosshair := FileExists(AppDir + '\tools\CrosshairTool\CrosshairTool.exe');
  HasGamma     := FileExists(AppDir + '\tools\GammaTool\GammaTool.exe');
  HasNight     := FileExists(AppDir + '\tools\NightVisionTool\NightVisionTool.exe');
  HasMouse     := FileExists(AppDir + '\tools\MouseTool\MouseTool.exe');
  if (not HasCrosshair) and (not HasGamma) and (not HasNight) and (not HasMouse) then Exit;

  for I := 0 to WizardForm.ComponentsList.Items.Count - 1 do
  begin
    Desc := WizardForm.ComponentsList.ItemCaption[I];
    if HasCrosshair and (Pos('准心', Desc) > 0) then
      WizardForm.ComponentsList.Checked[I] := True;
    if HasGamma and (Pos('调节', Desc) > 0) then
      WizardForm.ComponentsList.Checked[I] := True;
    if HasNight and (Pos('夜视', Desc) > 0) then
      WizardForm.ComponentsList.Checked[I] := True;
    if HasMouse and (Pos('鼠鼠', Desc) > 0) then
      WizardForm.ComponentsList.Checked[I] := True;
  end;
end;

procedure CurPageChanged(CurPageID: Integer);
begin
  if CurPageID = wpSelectComponents then
    AutoCheckExistingTools();
end;

// ──────────────────────────────────────────────────────────────
// 卸载前:弹出一个统一界面,让用户勾选要删除的子工具 + 是否清数据
// ──────────────────────────────────────────────────────────────
var
  UninstCrosshair: Boolean;
  UninstGamma:     Boolean;
  UninstNight:     Boolean;
  UninstMouse:     Boolean;
  UninstData:      Boolean;

function InitializeUninstall(): Boolean;
var
  Form: TForm;
  LblTitle: TLabel;
  BtnOK, BtnSkip: TButton;
  ChkCrosshair, ChkGamma, ChkNight, ChkMouse, ChkData: TCheckBox;
  ToolsRoot, DataPath: String;
  HasCrosshair, HasGamma, HasNight, HasMouse, HasData: Boolean;
  Y: Integer;
begin
  Result := True;
  UninstCrosshair := False;
  UninstGamma     := False;
  UninstNight     := False;
  UninstMouse     := False;
  UninstData      := False;

  ToolsRoot := ExpandConstant('{app}\tools');
  DataPath  := ExpandConstant('{userappdata}\FPSToolbox');
  HasCrosshair := DirExists(ToolsRoot + '\CrosshairTool');
  HasGamma     := DirExists(ToolsRoot + '\GammaTool');
  HasNight     := DirExists(ToolsRoot + '\NightVisionTool');
  HasMouse     := DirExists(ToolsRoot + '\MouseTool');
  HasData      := DirExists(DataPath);

  // 没有子工具也没有数据,直接跳过弹窗
  if (not HasCrosshair) and (not HasGamma) and (not HasNight)
     and (not HasMouse) and (not HasData) then
    Exit;

  ChkCrosshair := nil;
  ChkGamma     := nil;
  ChkNight     := nil;
  ChkMouse     := nil;
  ChkData      := nil;

  Form := TForm.Create(nil);
  try
    Form.Caption     := 'FPS 工具箱 — 卸载选项';
    Form.ClientWidth := ScaleX(480);
    Form.Position    := poScreenCenter;

    Y := ScaleY(16);

    LblTitle          := TLabel.Create(Form);
    LblTitle.Parent   := Form;
    LblTitle.AutoSize := False;
    LblTitle.WordWrap := True;
    LblTitle.Left     := ScaleX(16);
    LblTitle.Top      := Y;
    LblTitle.Width    := ScaleX(448);
    LblTitle.Height   := ScaleY(36);
    LblTitle.Caption  := '主程序将被卸载。以下子工具和用户数据默认保留，勾选后将一并删除：';
    Y := Y + ScaleY(44);

    if HasCrosshair then
    begin
      ChkCrosshair         := TCheckBox.Create(Form);
      ChkCrosshair.Parent  := Form;
      ChkCrosshair.Left    := ScaleX(16);
      ChkCrosshair.Top     := Y;
      ChkCrosshair.Width   := ScaleX(448);
      ChkCrosshair.Caption := '屏幕准心工具（' + ToolsRoot + '\CrosshairTool）';
      Y := Y + ScaleY(22);
    end;

    if HasGamma then
    begin
      ChkGamma         := TCheckBox.Create(Form);
      ChkGamma.Parent  := Form;
      ChkGamma.Left    := ScaleX(16);
      ChkGamma.Top     := Y;
      ChkGamma.Width   := ScaleX(448);
      ChkGamma.Caption := '屏幕调节工具（' + ToolsRoot + '\GammaTool）';
      Y := Y + ScaleY(22);
    end;

    if HasNight then
    begin
      ChkNight         := TCheckBox.Create(Form);
      ChkNight.Parent  := Form;
      ChkNight.Left    := ScaleX(16);
      ChkNight.Top     := Y;
      ChkNight.Width   := ScaleX(448);
      ChkNight.Caption := '智能夜视滤镜（' + ToolsRoot + '\NightVisionTool）';
      Y := Y + ScaleY(22);
    end;

    if HasMouse then
    begin
      ChkMouse         := TCheckBox.Create(Form);
      ChkMouse.Parent  := Form;
      ChkMouse.Left    := ScaleX(16);
      ChkMouse.Top     := Y;
      ChkMouse.Width   := ScaleX(448);
      ChkMouse.Caption := '鼠鼠工具（' + ToolsRoot + '\MouseTool）';
      Y := Y + ScaleY(22);
    end;

    if HasData then
    begin
      Y := Y + ScaleY(6);
      ChkData         := TCheckBox.Create(Form);
      ChkData.Parent  := Form;
      ChkData.Left    := ScaleX(16);
      ChkData.Top     := Y;
      ChkData.Width   := ScaleX(448);
      ChkData.Caption := '清除用户配置数据（' + DataPath + '）';
      Y := Y + ScaleY(22);
    end;

    Y := Y + ScaleY(16);

    BtnSkip            := TButton.Create(Form);
    BtnSkip.Parent     := Form;
    BtnSkip.Caption    := '全部保留';
    BtnSkip.ModalResult := mrCancel;
    BtnSkip.Width      := ScaleX(88);
    BtnSkip.Height     := ScaleY(28);
    BtnSkip.Left       := Form.ClientWidth - ScaleX(104);
    BtnSkip.Top        := Y;

    BtnOK              := TButton.Create(Form);
    BtnOK.Parent       := Form;
    BtnOK.Caption      := '确定';
    BtnOK.ModalResult  := mrOK;
    BtnOK.Width        := ScaleX(88);
    BtnOK.Height       := ScaleY(28);
    BtnOK.Left         := BtnSkip.Left - ScaleX(100);
    BtnOK.Top          := Y;
    BtnOK.Default      := True;

    Form.ClientHeight  := Y + ScaleY(44);

    if Form.ShowModal() = mrOK then
    begin
      if ChkCrosshair <> nil then UninstCrosshair := ChkCrosshair.Checked;
      if ChkGamma     <> nil then UninstGamma     := ChkGamma.Checked;
      if ChkNight     <> nil then UninstNight     := ChkNight.Checked;
      if ChkMouse     <> nil then UninstMouse     := ChkMouse.Checked;
      if ChkData      <> nil then UninstData      := ChkData.Checked;
    end;
  finally
    Form.Free();
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  AppPath, DataPath: String;
begin
  if CurUninstallStep = usUninstall then
  begin
    AppPath := ExpandConstant('{app}');
    if UninstCrosshair then DelTree(AppPath + '\tools\CrosshairTool',   True, True, True);
    if UninstGamma     then DelTree(AppPath + '\tools\GammaTool',       True, True, True);
    if UninstNight     then DelTree(AppPath + '\tools\NightVisionTool', True, True, True);
    if UninstMouse     then DelTree(AppPath + '\tools\MouseTool',       True, True, True);
    // 所有子工具都删掉后 tools\ 为空,顺手移除(非空时 RemoveDir 静默失败)
    RemoveDir(AppPath + '\tools');
  end;
  if CurUninstallStep = usPostUninstall then
  begin
    DataPath := ExpandConstant('{userappdata}\FPSToolbox');
    if UninstData and DirExists(DataPath) then
      DelTree(DataPath, True, True, True);
  end;
end;
