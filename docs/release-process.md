# FPS 工具箱发版流�?
本项目采�?**GitHub Releases** 作为分发渠道，主框架和两个子工具�?*独立升版�?*�?
---

## 1. Tag 命名约定（必须遵守）

| 组件 | Tag 格式 | 举例 |
| --- | --- | --- |
| FPS 工具箱主框架 | `toolbox-v<SemVer>` | `toolbox-v1.0.0` |
| 屏幕准心工具 | `crosshair-v<SemVer>` | `crosshair-v1.2.0` |
| 屏幕调节工具 | `gamma-v<SemVer>` | `gamma-v1.1.0` |

> `UpdateChecker` �?tag 前缀区分组件�?*前缀一个字母都不能�?*�?
## 2. Release Asset 命名约定

| 组件 | Asset 文件名格�?|
| --- | --- |
| 主框架在线版 | `FPSToolbox_Setup_v<Version>.exe`（~3 MB�?|
| 主框架离线版 | `FPSToolbox_Setup_v<Version>_offline.exe`（~60 MB，内�?.NET 8�?|
| 屏幕准心工具 | `CrosshairTool-v<Version>.zip` |
| 屏幕调节工具 | `GammaTool-v<Version>.zip` |

> 文件名规则在 `src/FPSToolbox/Core/UpdateConstants.cs` �?`AssetNamePattern` 里�?
## 3. 依赖工具

- .NET 8 SDK：`dotnet --version` �?8.0
- Inno Setup 6：默认路�?`D:\Program Files\Inno Setup 6\iscc.exe`
- GitHub CLI：`gh --version`；首次使用先 `gh auth login`

## 4. 发版命令

所有命令在仓库根目录执行�?
### 4.1 发布主框架（在线�?+ 离线版一并上传）

```powershell
.\scripts\release.ps1 -Target toolbox -Version 1.0.1 -Notes @"
- 修复 xxx
- 优化 xxx
"@
```

第一次跑会自动下�?`.NET 8.0.11 Desktop Runtime` �?`installer\runtimes\` 作为缓存，之后跑不再下载�?
### 4.2 单独发布子工�?
```powershell
# 屏幕调节工具
.\scripts\release.ps1 -Target gamma -Version 1.1.0 -Notes "新增 RGB 独立调节"

# 屏幕准心工具
.\scripts\release.ps1 -Target crosshair -Version 1.2.0
```

### 4.3 三合一全发

```powershell
.\scripts\release.ps1 -Target all -Version 1.0.0
```

### 4.4 其他参数

| 参数 | 作用 |
| --- | --- |
| `-DryRun` | 只打包不上传，用来先看产物是否正�?|
| `-Draft` | 发成 GitHub 草稿 Release（不公开，便于内部预览） |
| `-InnoSetup <path>` | 自定�?Inno Setup 6 `iscc.exe` 路径 |

## 5. 客户端如何拿到更�?
1. **主框架启�?3 秒后**自动�?GitHub Releases API�?   `GET https://api.github.com/repos/ljw0404/fps-toolbox/releases?per_page=30`
2. 按前缀挑出 `toolbox-*` / `crosshair-*` / `gamma-*` 各自的最新版本�?3. 与本地版本做语义化比较：
   - 主框架本地版本：`Assembly.GetExecutingAssembly().Version`
   - 子工具本地版本：`InstalledTool.Version`（从 `%AppData%\FPSToolbox\tools-registry.json` 读）
4. 有新版时�?   - 主框架：顶栏亮红色徽�?+ "立即更新" 按钮 �?下载对应 `.exe` �?启动 �?自身退�?   - 子工具：卡片上出�?"更新�?v1.2.0" 按钮 �?下载 zip �?`ToolPackageInstaller` 覆盖安装
5. 用户可随时手动点顶栏 **🔄 检查更�?* 立即复查�?
## 6. 版本号修改位�?
三个项目各有独立 `<Version>` 字段�?
- `src/FPSToolbox/FPSToolbox.csproj`
- `src/CrosshairTool/CrosshairTool.csproj`
- `src/GammaTool/GammaTool.csproj`

发版�?*务必**把对�?csproj 里的 `<Version>` 也同步改了，否则�?
- 主框架：即使装了新版，顶栏显示的还是老版本号
- 子工具：pack 出的 zip �?`manifest.json` �?version 来自 `-Version` 参数，所以不用动 csproj 也能工作，但建议也同步改以便本地调试显示正确

## 7. 迁仓库怎么�?
只改一处：`src/FPSToolbox/Core/UpdateConstants.cs`

```csharp
public const string GitHubOwner = "ljw0404";   // �?改这�?public const string GitHubRepo  = "game-aiming";    // �?和这�?```

## 8. 常见问题

### Q：GitHub API 有调用次数限制吗�?A：匿名访问每小时 60 �?IP。本项目启动只查 1 �?+ 用户手动点击，完全够用�?
### Q：公司内网用户访问不�?GitHub�?A：离线版 installer 不需要联网（除了首次 clone 仓库）。子工具升级可手动下�?zip，用主界面下方的 **"�?从本地安装工�?** 卡片导入�?
### Q：Tag 误发了想重做�?A：`release.ps1` 会自动删�?tag 再发（见 `Publish-GhRelease`）。生产建议直接升小版本号�?.0.0 �?1.0.1），不要覆盖�?tag�?