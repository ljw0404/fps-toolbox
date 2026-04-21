# NightVisionTool —— 智能夜视滤镜规格

## 背景

`GammaTool` 的纯 gamma 曲线(`y = x^(1/γ)`)在黑夜场景下会同时推高暗、中、亮三段,导致进入室内(有灯光)时亮部过曝,反而看不清目标。

`NightVisionTool` 是并列于 `GammaTool` 的独立子工具,目标是"暗部提亮 + 亮部保护":暗部大幅提亮的同时,亮部向原始值回靠,使室内光源不会被推爆。

核心约束:**只用 `SetDeviceGammaRamp`,不注入游戏进程**,避免触发主流 FPS 的反作弊。

---

## 曲线设计

对 `x ∈ [0, 1]`,输出 `y ∈ [0, 1]`,两个参数控制:

```
strength ∈ [0, 1]      夜视强度,默认 0.60
protect  ∈ [0, 1]      亮部保护,默认 0.50

γ     = 1 + strength * 1.5              // strength=0 → γ=1;strength=1 → γ=2.5
yLift = x^(1/γ)                          // 纯 gamma 提亮
w     = protect * smoothstep(0.4, 1, x)  // 亮部向原始 x 混合的权重
y     = (1 - w) * yLift + w * x          // 最终输出
```

**退化行为(供直观验证)**

| strength | protect | 效果 |
| --- | --- | --- |
| 0    | 任意 | `y = x`(identity,屏幕无变化) |
| 1    | 0    | `y = x^(1/2.5)`,约等于 GammaTool 的强 gamma 提亮 |
| 1    | 1    | 暗部仍大幅提亮,`x → 1` 时 `y → 1`(亮部完全不被推爆) |
| 0.75 | 0.65 | 内置 "三角洲-夜战" 预设。暗部显著提亮,灯光附近的过曝大幅减轻 |

实现是 256 点 LUT,构建耗时可忽略,R/G/B 三通道共用同一条曲线。

---

## 架构

### 新增项目
```
src/NightVisionTool/
├── NightVisionTool.csproj
├── App.xaml / App.xaml.cs                  # Mutex、IPC、ParentWatcher、退出恢复 LUT
├── Windows/
│   └── NightVisionPanelWindow.xaml(.cs)    # 极简主面板
├── Controls/
│   └── NightVisionCurveView.cs             # LUT 曲线预览
├── Core/
│   ├── NightVisionCurve.cs                 # 曲线公式 + LUT 构建(核心)
│   ├── NightVisionApplier.cs               # 多显示器 HDC 管理 + Set/RestoreGammaRamp
│   ├── SchemeManager.cs                    # 预设 CRUD + 工具配置
│   ├── StartupArgs.cs                      # 解析 --parent-pid / --pipe
│   └── MonitorEnumerator.cs                # 显示器枚举(本地拷贝)
├── Models/
│   └── NightVisionConfig.cs                # Strength / HighlightProtect / Scheme
└── Resources/icon.ico
```

### 复用 `FPSToolbox.Shared`(不跨项目引用 GammaTool)
- `Native/NativeMethods.cs` — `SetDeviceGammaRamp` / `GetDeviceGammaRamp` / `GammaRamp`
- `Ipc/IpcClient.cs` + `Ipc/IpcMessage.cs` — IPC 协议
- `Config/JsonConfigStore.cs` — 配置序列化
- `PathService.cs` — `%AppData%\FPSToolbox\NightVisionTool\`
- `ParentWatcher.cs` — 父进程死亡自退
- `Hotkeys/HotkeyManager.cs` — 全局热键

### IPC 协议新增

`shared/FPSToolbox.Shared/Ipc/IpcMessage.cs`:

```csharp
// Toolbox → NightVisionTool
IpcActions.NightVisionOpenPanel
IpcActions.NightVisionToggle
IpcActions.NightVisionApplyPreset
IpcActions.NightVisionListSchemes
IpcActions.NightVisionResetSystem
// NightVisionTool → Toolbox
IpcTopics.NightVisionState
IpcTopics.NightVisionSchemeApplied
IpcTopics.NightVisionSchemesChanged
```

### 主框架集成点
- `ToolIds.NightVisionTool` 已加入 `All`(支持包管理器识别)
- `ToolRegistry` 第三条 `ToolDescriptor` + `FallbackNightVisionVersion`
- `UpdateConstants.TagPrefix.NightVision` / `ComponentId.NightVisionTool` / `AssetNamePattern.NightVisionZip`
- `UpdateChecker.CheckAsync` 查询 `nightvision-v*` tag
- `UpdateCheckResult.NightVision` + `MainWindow.GetUpdateInfoFor`
- `MainWindow` 卡片"打开面板"通过 `IpcActions.NightVisionOpenPanel` 路由
- `TrayIconManager` 第三组菜单"智能夜视滤镜"

---

## UI(极简)

`NightVisionPanelWindow` 的所有可交互元素:

| 控件 | 说明 |
| --- | --- |
| 显示器下拉 | 选择应用到哪个显示器,或"全部" |
| **夜视强度**滑块 + 数字输入 | `strength ∈ [0, 1]`,默认 0.60 |
| **亮部保护**滑块 + 数字输入 | `protect ∈ [0, 1]`,默认 0.50 |
| 全局热键输入 | 默认 `F9`,避开 Crosshair `F8` |
| 方案下拉 + 加载/保存/删除 | 预设管理 |
| LUT 曲线预览 | 实时反映当前编辑中的曲线(对角虚线 = identity) |
| 开启/关闭夜视按钮 | 顶级开关,也可由全局热键触发 |
| 重置为默认 | 把两个滑块拉回 `(0, 0)` |
| 关闭程序 | 恢复系统 LUT + 退出进程 |

**状态机**
- **未开启(Disabled)**:屏幕 LUT = 系统默认;滑块仅改曲线预览。
- **已开启(Enabled)**:屏幕 LUT = 当前 `_editingConfig` 实时应用;滑块/方案切换即时生效。
- **关闭窗口 / 程序退出**:调用 `NightVisionApplier.RestoreSystemDefaults()`。

---

## 内置预设

| 名称 | Strength | HighlightProtect | 定位 |
| --- | --- | --- | --- |
| 通用夜战 | 0.60 | 0.50 | 大多数游戏的夜战场景通用基线 |
| 三角洲-夜战 | 0.75 | 0.65 | 针对三角洲行动夜战地图调优,室内光源不易过曝 |

预设文件位置:`%AppData%\FPSToolbox\NightVisionTool\presets\*.json`。工具配置:`%AppData%\FPSToolbox\NightVisionTool\config.json`(含 `LastSchemeName`、`ApplyOnStart`、`ToggleHotkey`)。

`ApplyOnStart` 默认 `false` —— 避免用户开机屏幕直接变形。

---

## 发布

独立 tag:`nightvision-v<版本号>`,初版 `nightvision-v0.1.0`。通过 `scripts\release.ps1 -Target nightvision -Version 0.1.0` 一键发布。主框架检测到 `nightvision-v*` 新 tag 后,在卡片上显示"更新 vX.Y.Z"按钮。

---

## 验证

1. **构建**:`dotnet build FPSToolbox.sln -c Release` 全绿(已通过)。
2. **曲线正确性**:
   - `strength=0` → LUT = identity
   - `strength=1, protect=0` → 约等于 gamma 2.5
   - `strength=1, protect=1` → 暗部仍明显提亮,`x=1` 时 `y=1`(曲线预览肉眼可确认 rolloff)
3. **实游戏**:三角洲行动夜战地图
   - 户外空旷:`strength=0.75, protect=0.65` 能看清地形/人影
   - 走进室内光源房间:相比单纯开 GammaTool 强 gamma,过曝明显减轻
   - 调高 protect → 室内光源附近过曝进一步减轻,代价是暗部提亮略减
4. **多显示器**:仅对选中显示器应用,其它不受影响。
5. **退出清理**:关工具 / 父进程崩 / 强杀进程 → 系统 LUT 恢复。
6. **热键**:`F9` 全局切换开/关,游戏窗口化/全屏化都生效,与 Crosshair `F8` 不冲突。
7. **安装器**:Inno Setup 主安装包勾/不勾"智能夜视滤镜"组件的两种情况都正确;卸载时按需保留/删除。

---

## 风险 / 未决项

- **Windows Gamma Ramp 限幅**:Win10/11 对 ramp 有钳制,极端 `strength=1` 可能被系统拉回。如果实测发现钳制严重,后续需要复用 GammaTool 的 `GdiICMGammaRange` 注册表抬权流程。
- **未来扩展**:`NightVisionApplier` 的 LUT 构建入口已预留,未来若加入"基于 DXGI Desktop Duplication 的屏幕采样 + 自适应 LUT",可在不重构 UI/IPC/打包流程的情况下接入。
