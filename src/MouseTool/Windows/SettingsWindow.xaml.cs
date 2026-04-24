using System.Windows;
using System.Windows.Input;
using MouseTool.Core;

namespace MouseTool.Windows;

/// <summary>
/// 鼠鼠工具 · 设置窗口。
/// </summary>
public partial class SettingsWindow : Window
{
    private readonly ConfigManager _configManager;
    private readonly FloatingWindow _floating;
    private bool _loading;
    private Key? _pendingHotkey;

    public SettingsWindow(ConfigManager configManager, FloatingWindow floating)
    {
        InitializeComponent();
        _configManager = configManager;
        _floating = floating;
        LoadValues();
    }

    // ──────────────────────────────────────────
    // 初始化
    // ──────────────────────────────────────────
    private void LoadValues()
    {
        _loading = true;
        var cfg = _configManager.Current;

        SliderOpacity.Value = cfg.WindowOpacity;
        TxtOpacity.Text = $"{cfg.WindowOpacity * 100:F0}%";

        SliderFont.Value = cfg.FontSize;
        TxtFont.Text = $"{cfg.FontSize:F0}px";

        ChkBorder.IsChecked = cfg.ShowBorder;
        ChkTopmost.IsChecked = cfg.AlwaysOnTop;

        TxtHotkey.Text = cfg.ToggleHotkey;
        _pendingHotkey = null;

        _loading = false;
    }

    // ──────────────────────────────────────────
    // 透明度
    // ──────────────────────────────────────────
    private void OnOpacityChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_loading || TxtOpacity == null) return;
        var val = Math.Round(e.NewValue, 2);
        TxtOpacity.Text = $"{val * 100:F0}%";
        _configManager.Update(c => c.WindowOpacity = val);
        _floating.Opacity = val;
    }

    // ──────────────────────────────────────────
    // 字体大小
    // ──────────────────────────────────────────
    private void OnFontSizeChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_loading || TxtFont == null) return;
        var val = Math.Round(e.NewValue);
        TxtFont.Text = $"{val:F0}px";
        _configManager.Update(c => c.FontSize = val);
        _floating.ApplyConfig();
    }

    // ──────────────────────────────────────────
    // 背景色预设
    // ──────────────────────────────────────────
    private void OnBgPresetClicked(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button btn && btn.Tag is string hex)
        {
            _configManager.Update(c => c.BackgroundHex = hex);
            _floating.ApplyConfig();
        }
    }

    // ──────────────────────────────────────────
    // 描边 / 置顶
    // ──────────────────────────────────────────
    private void OnBorderToggled(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        _configManager.Update(c => c.ShowBorder = ChkBorder.IsChecked == true);
        _floating.ApplyConfig();
    }

    private void OnTopmostToggled(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        var v = ChkTopmost.IsChecked == true;
        _configManager.Update(c => c.AlwaysOnTop = v);
        _floating.Topmost = v;
    }

    // ──────────────────────────────────────────
    // 热键
    // ──────────────────────────────────────────
    private void OnHotkeyPreviewKeyDown(object sender, KeyEventArgs e)
    {
        e.Handled = true;
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        // 只接受 F1-F12 或字母单键
        if (key >= Key.F1 && key <= Key.F12)
        {
            _pendingHotkey = key;
            TxtHotkey.Text = key.ToString();
        }
        else if (key >= Key.A && key <= Key.Z)
        {
            _pendingHotkey = key;
            TxtHotkey.Text = key.ToString();
        }
    }

    private void OnApplyHotkey(object sender, RoutedEventArgs e)
    {
        if (_pendingHotkey == null)
        {
            MessageBox.Show(this, "请先点击热键输入框后按下目标按键（F1-F12 或字母键）。",
                "鼠鼠工具", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        var keyStr = _pendingHotkey.Value.ToString();
        _configManager.Update(c => c.ToggleHotkey = keyStr);
        _floating.ReRegisterHotkey();
        MessageBox.Show(this, $"热键已设为 {keyStr}。",
            "鼠鼠工具", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    // ──────────────────────────────────────────
    // 位置重置
    // ──────────────────────────────────────────
    private void OnResetPosition(object sender, RoutedEventArgs e)
    {
        _floating.Left = 80;
        _floating.Top = 80;
        _configManager.Update(c => { c.WindowLeft = 80; c.WindowTop = 80; });
        if (!_floating.IsVisible) _floating.Show();
        _floating.Activate();
    }

    // ──────────────────────────────────────────
    // 关闭
    // ──────────────────────────────────────────
    private void OnCloseSettings(object sender, RoutedEventArgs e) => Close();

    private void OnCloseApp(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show(this,
            "确定要关闭鼠鼠工具吗？",
            "鼠鼠工具", MessageBoxButton.OKCancel, MessageBoxImage.Question);
        if (result == MessageBoxResult.OK)
            System.Windows.Application.Current.Shutdown();
    }
}
