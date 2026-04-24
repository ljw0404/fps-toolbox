using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using MouseTool.Core;

namespace MouseTool.Windows;

/// <summary>
/// 悬浮窗 —— 可拖动、透明、置顶、热键显隐。
/// </summary>
public partial class FloatingWindow : Window
{
    private readonly ConfigManager _configManager;
    private HotkeyManager? _hotkey;

    /// <summary>点击"⚙"时通知 App 层打开设置窗。</summary>
    public Action? OnOpenSettings { get; set; }

    public FloatingWindow(ConfigManager configManager)
    {
        InitializeComponent();
        _configManager = configManager;

        ApplyConfig();
        RegisterHotkey();

        // 保存位置到 config
        LocationChanged += (_, _) =>
        {
            _configManager.Current.WindowLeft = Left;
            _configManager.Current.WindowTop = Top;
        };

        Closing += OnWindowClosing;
    }

    // ──────────────────────────────────────────
    // 拖动
    // ──────────────────────────────────────────
    private void OnWindowMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
            DragMove();
    }

    // ──────────────────────────────────────────
    // 按钮
    // ──────────────────────────────────────────
    private void OnSettingsClicked(object sender, RoutedEventArgs e)
    {
        OnOpenSettings?.Invoke();
    }

    private void OnHideClicked(object sender, RoutedEventArgs e)
    {
        Hide();
    }

    // ──────────────────────────────────────────
    // 热键
    // ──────────────────────────────────────────
    private void RegisterHotkey()
    {
        _hotkey?.Unregister();
        var key = _configManager.Current.ToggleHotkey;
        if (string.IsNullOrEmpty(key)) return;
        _hotkey = new HotkeyManager(this, key, () =>
        {
            if (IsVisible) Hide();
            else
            {
                Show();
                Activate();
            }
        });
        _hotkey.Register();
        UpdateHintText();
    }

    private void UpdateHintText()
    {
        var key = _configManager.Current.ToggleHotkey;
        TxtHint.Text = $"拖动移动  |  {key} 显示/隐藏";
    }

    // ──────────────────────────────────────────
    // 配置应用
    // ──────────────────────────────────────────
    public void ApplyConfig()
    {
        var cfg = _configManager.Current;

        // 位置
        Left = cfg.WindowLeft;
        Top = cfg.WindowTop;

        // 透明度
        Opacity = Math.Clamp(cfg.WindowOpacity, 0.1, 1.0);

        // 置顶
        Topmost = cfg.AlwaysOnTop;

        // 背景色
        if (TryParseArgbHex(cfg.BackgroundHex, out var bgColor))
            PanelBackground.Color = bgColor;

        // 文字颜色
        if (TryParseRgbHex(cfg.ForegroundHex, out var fgBrush))
        {
            foreach (var tb in new[] { TxtDpi, TxtSens, TxtCm360, TxtEdpi })
                ; // 示例文字颜色保持固定，由 XAML 直接定义
        }

        // 描边
        PanelBorder.BorderBrush = cfg.ShowBorder
            ? new SolidColorBrush(Color.FromArgb(0x44, 0xFF, 0xFF, 0xFF))
            : Brushes.Transparent;

        // 字体
        foreach (FrameworkElement child in FindVisualChildren<System.Windows.Controls.TextBlock>(this))
        {
            if (child is System.Windows.Controls.TextBlock tb
                && tb.Tag as string != "fixed")
            {
                tb.FontSize = cfg.FontSize;
            }
        }

        UpdateHintText();
    }

    public void ReRegisterHotkey() => RegisterHotkey();

    // ──────────────────────────────────────────
    // 关闭
    // ──────────────────────────────────────────
    private void OnWindowClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        _hotkey?.Unregister();
        _hotkey?.Dispose();
        _configManager.Save();
    }

    // ──────────────────────────────────────────
    // 工具方法
    // ──────────────────────────────────────────
    private static bool TryParseArgbHex(string? hex, out Color color)
    {
        color = Colors.Transparent;
        if (string.IsNullOrWhiteSpace(hex)) return false;
        try
        {
            color = (Color)ColorConverter.ConvertFromString(hex);
            return true;
        }
        catch { return false; }
    }

    private static bool TryParseRgbHex(string? hex, out SolidColorBrush brush)
    {
        brush = Brushes.White;
        if (TryParseArgbHex(hex, out var c))
        {
            brush = new SolidColorBrush(c);
            return true;
        }
        return false;
    }

    private static IEnumerable<T> FindVisualChildren<T>(DependencyObject parent)
        where T : DependencyObject
    {
        int count = VisualTreeHelper.GetChildrenCount(parent);
        for (int i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T t) yield return t;
            foreach (var sub in FindVisualChildren<T>(child))
                yield return sub;
        }
    }
}

/// <summary>
/// 全局热键封装（Win32 RegisterHotKey）。
/// </summary>
internal sealed class HotkeyManager : IDisposable
{
    [DllImport("user32.dll")] static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
    [DllImport("user32.dll")] static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    private readonly Window _owner;
    private readonly string _keyStr;
    private readonly Action _callback;
    private readonly int _id;
    private bool _registered;

    private static int _nextId = 9200;

    public HotkeyManager(Window owner, string keyStr, Action callback)
    {
        _owner = owner;
        _keyStr = keyStr;
        _callback = callback;
        _id = System.Threading.Interlocked.Increment(ref _nextId);
        System.Windows.Interop.ComponentDispatcher.ThreadPreprocessMessage += OnMessage;
    }

    public void Register()
    {
        if (_registered) return;
        if (!Enum.TryParse<Key>(_keyStr, true, out var wpfKey)) return;
        var vk = KeyInterop.VirtualKeyFromKey(wpfKey);
        var hwnd = new System.Windows.Interop.WindowInteropHelper(_owner).EnsureHandle();
        _registered = RegisterHotKey(hwnd, _id, 0, (uint)vk);
    }

    public void Unregister()
    {
        if (!_registered) return;
        var hwnd = new System.Windows.Interop.WindowInteropHelper(_owner).Handle;
        if (hwnd != IntPtr.Zero) UnregisterHotKey(hwnd, _id);
        _registered = false;
    }

    private void OnMessage(ref System.Windows.Interop.MSG msg, ref bool handled)
    {
        const int WM_HOTKEY = 0x0312;
        if (msg.message == WM_HOTKEY && (int)msg.wParam == _id)
        {
            _callback();
            handled = true;
        }
    }

    public void Dispose()
    {
        Unregister();
        System.Windows.Interop.ComponentDispatcher.ThreadPreprocessMessage -= OnMessage;
    }
}
