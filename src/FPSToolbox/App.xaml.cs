using System.Diagnostics;
using System.IO;
using System.Security.Principal;
using System.Threading;
using System.Windows;
using FPSToolbox.Core;
using FPSToolbox.Shared;
using FPSToolbox.Shared.Config;
using FPSToolbox.Shared.Ipc;
using FPSToolbox.Tray;
using Settings = FPSToolbox.Models.ToolboxSettings;

namespace FPSToolbox;

public partial class App : System.Windows.Application
{
    private static readonly string LogPath = PathService.GetLogFile("FPSToolbox");

    private Mutex? _mutex;
    private IpcServer? _ipcServer;
    private ToolRegistry? _registry;
    private ToolManager? _toolManager;
    private TrayIconManager? _tray;
    private MainWindow? _mainWindow;
    private Settings _settings = new();

    /// <summary>当前进程是否以管理员（High Integrity）身份运行。</summary>
    private static bool IsRunningAsAdmin()
    {
        try
        {
            using var id = WindowsIdentity.GetCurrent();
            return new WindowsPrincipal(id).IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch { return false; }
    }

    /// <summary>
    /// 以管理员身份重新启动本程序（ShellExecute + runas verb 触发 UAC），
    /// 传入原始命令行参数，然后退出当前实例。
    /// </summary>
    private void RestartAsAdmin(string[] args)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName        = Process.GetCurrentProcess().MainModule!.FileName,
                Arguments       = string.Join(" ", args),
                UseShellExecute = true,
                Verb            = "runas",
            });
        }
        catch
        {
            // 用户在 UAC 对话框点了"否"，或 ShellExecute 失败，直接退出。
        }
        Shutdown();
    }

    protected override void OnStartup(System.Windows.StartupEventArgs e)
    {
        AppDomain.CurrentDomain.UnhandledException += (_, ex) => WriteLog(ex.ExceptionObject as Exception);
        DispatcherUnhandledException += (_, ex) => { WriteLog(ex.Exception); ex.Handled = true; };

        // ── 管理员权限检查（须最先执行，在 Mutex 和任何 UI 之前）──────────────
        // 原因：子工具（MouseTool 等）的 manifest 声明 requireAdministrator；
        //       若主框架是普通权限，启动子工具时会触发 ERROR_ELEVATION_REQUIRED，
        //       导致"点击启动没有反应"。游戏中的全局热键也会因 Windows UIPI
        //       被 ACE-Guard 等反作弊的高完整性进程屏蔽而失效。
        if (!IsRunningAsAdmin())
        {
            var result = System.Windows.MessageBox.Show(
                "FPS 工具箱需要以管理员身份运行。\n\n" +
                "原因：\n" +
                "• 全局热键（F8/F9/F10 等）在三角洲行动等游戏中生效，需要与游戏进程相同的权限级别\n" +
                "• 子工具（鼠鼠工具、准心工具等）的启动也依赖管理员权限\n\n" +
                "点击「是」将以管理员身份重新启动。",
                "需要管理员权限 — FPS 工具箱",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Warning);

            if (result == System.Windows.MessageBoxResult.Yes)
                RestartAsAdmin(e.Args);
            else
                Shutdown();
            return;
        }
        // ────────────────────────────────────────────────────────────────────────

        try
        {
            _mutex = new Mutex(true, @"Global\FPSToolbox_SingleInstance", out bool createdNew);
            if (!createdNew)
            {
                _mutex = null;
                System.Windows.MessageBox.Show("FPS 工具箱已经在运行中。\n请查看系统托盘。",
                    "FPS 工具箱", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                Shutdown();
                return;
            }

            base.OnStartup(e);

            // 1. 加载设置
            _settings = JsonConfigStore.Load<Settings>(PathService.ToolboxSettingsPath);

            // 2. 注册表 / 工具列表
            _registry = new ToolRegistry();
            _registry.Load();
            _registry.AutoDiscover(AppDomain.CurrentDomain.BaseDirectory);

            // 3. IPC server
            var pipeName = PipeNames.ForToolbox(Environment.ProcessId);
            _ipcServer = new IpcServer(pipeName);
            _ipcServer.Start();

            // 4. ToolManager
            _toolManager = new ToolManager(_registry, _ipcServer, pipeName);

            // 5. 主窗口
            _mainWindow = new MainWindow(_toolManager, _registry, _settings, SaveSettings);
            if (!IsStartedMinimized(e.Args))
                _mainWindow.Show();

            // 6. Tray
            _tray = new TrayIconManager(_toolManager, _registry, ShowMainWindow, ExitAll);
            _tray.Initialize();
            _tray.RefreshMenu();
        }
        catch (Exception ex)
        {
            WriteLog(ex);
            System.Windows.MessageBox.Show(
                $"FPS 工具箱启动失败：\n\n{ex.Message}\n\n日志：{LogPath}",
                "FPS 工具箱", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    private static bool IsStartedMinimized(string[] args) =>
        args.Any(a => a.Equals("--minimized", StringComparison.OrdinalIgnoreCase));

    private void ShowMainWindow()
    {
        if (_mainWindow == null) return;
        if (!_mainWindow.IsVisible) _mainWindow.Show();
        _mainWindow.WindowState = WindowState.Normal;
        _mainWindow.Activate();
    }

    private async void ExitAll()
    {
        if (_toolManager != null) await _toolManager.StopAllAsync();
        if (_mainWindow != null)
        {
            _mainWindow.AllowClose = true;
            _mainWindow.Close();
        }
        _tray?.Dispose();
        if (_ipcServer != null) await _ipcServer.DisposeAsync();
        ReleaseMutex();
        Shutdown();
    }

    private void SaveSettings() => JsonConfigStore.Save(PathService.ToolboxSettingsPath, _settings);

    private void ReleaseMutex()
    {
        if (_mutex == null) return;
        try { _mutex.ReleaseMutex(); } catch { }
        _mutex = null;
    }

    protected override void OnExit(System.Windows.ExitEventArgs e)
    {
        ReleaseMutex();
        base.OnExit(e);
    }

    private static void WriteLog(Exception? ex)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
            File.AppendAllText(LogPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]\n{ex}\n\n");
        }
        catch { }
    }
}
