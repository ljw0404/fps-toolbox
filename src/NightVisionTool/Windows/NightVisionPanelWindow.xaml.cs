using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using FPSToolbox.Shared.Hotkeys;
using FPSToolbox.Shared.Ipc;
using NightVisionTool.Core;
using NightVisionTool.Models;

namespace NightVisionTool.Windows;

/// <summary>
/// 智能夜视滤镜主面板。状态机:
///   - 关闭(Disabled):屏幕 LUT = 系统默认(identity);滑块仅改曲线预览。
///   - 开启(Enabled):屏幕 LUT = 当前 _editingConfig 实时应用;滑块即时生效。
///   - 关闭窗口 / 程序退出:恢复系统默认。
/// </summary>
public partial class NightVisionPanelWindow : Window
{
    private readonly NightVisionApplier _applier;
    private readonly SchemeManager _schemes;
    private readonly IpcClient? _ipc;
    private readonly HotkeyManager _hotkeys = new();

    private NightVisionConfig _editingConfig = NightVisionConfig.Default.Clone();
    private bool _isEnabled;
    private bool _suppressEvents;
    private bool _isInitialized;

    private List<NightVisionScheme> _loadedSchemes = new();
    private string? _selectedMonitor;
    private int _hotkeyId = -1;
    private string _hotkeyText = "F9";

    public NightVisionPanelWindow(NightVisionApplier applier, SchemeManager schemes, IpcClient? ipc)
    {
        InitializeComponent();
        _isInitialized = true;
        _applier = applier;
        _schemes = schemes;
        _ipc = ipc;

        WireValueInput(TxtStrength, SldStrength, 0, 1);
        WireValueInput(TxtProtect,  SldProtect,  0, 1);

        Loaded += OnLoaded;
        Closing += (_, _) => System.Windows.Application.Current.Shutdown();
    }

    private void WireValueInput(TextBox box, Slider slider, double min, double max)
    {
        void Commit()
        {
            var txt = (box.Text ?? "").Trim();
            if (!double.TryParse(txt, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) &&
                !double.TryParse(txt, NumberStyles.Float, CultureInfo.CurrentCulture, out v))
            {
                box.Text = FormatValue(slider.Value);
                return;
            }
            if (v < min) v = min;
            if (v > max) v = max;
            slider.Value = v;
        }

        box.LostFocus += (_, _) => Commit();
        box.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter)
            {
                Commit();
                Keyboard.ClearFocus();
                e.Handled = true;
            }
        };
    }

    private static string FormatValue(double v) => v.ToString("0.00", CultureInfo.InvariantCulture);

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _hotkeys.Initialize(this);

        CmbMonitors.Items.Clear();
        CmbMonitors.Items.Add(new ComboBoxItem { Content = "全部显示器", Tag = null });
        foreach (var m in _applier.Monitors)
            CmbMonitors.Items.Add(new ComboBoxItem { Content = m.DisplayName, Tag = m.DeviceName });
        CmbMonitors.SelectedIndex = 0;

        ReloadSchemes();

        var cfg = _schemes.LoadConfig();
        _hotkeyText = string.IsNullOrWhiteSpace(cfg.ToggleHotkey) ? "F9" : cfg.ToggleHotkey!;
        TxtHotkey.Text = _hotkeyText;
        RegisterToggleHotkey();

        var defaultName = cfg.LastSchemeName;
        if (string.IsNullOrWhiteSpace(defaultName))
            defaultName = _loadedSchemes.Any(s => s.Name == BuiltInSchemes.Generic)
                ? BuiltInSchemes.Generic
                : _loadedSchemes.FirstOrDefault()?.Name;

        if (!string.IsNullOrEmpty(defaultName) &&
            _loadedSchemes.FirstOrDefault(s => s.Name == defaultName) is { } scheme)
        {
            _editingConfig = scheme.Config.Clone();
            _suppressEvents = true;
            try { CmbSchemes.Text = defaultName; }
            finally { _suppressEvents = false; }
        }

        PushConfigToUI(_editingConfig);
        UpdateCurve();
        UpdateEnabledUi();

        if (cfg.ApplyOnStart)
        {
            _isEnabled = true;
            ApplyCurrentEditing();
            UpdateEnabledUi();
        }
    }

    private void PushConfigToUI(NightVisionConfig cfg)
    {
        _suppressEvents = true;
        try
        {
            SldStrength.Value = cfg.Strength;
            SldProtect.Value = cfg.HighlightProtect;
            TxtStrength.Text = FormatValue(SldStrength.Value);
            TxtProtect.Text  = FormatValue(SldProtect.Value);
        }
        finally { _suppressEvents = false; }
    }

    private void PullUIToConfig()
    {
        _editingConfig.Strength = SldStrength.Value;
        _editingConfig.HighlightProtect = SldProtect.Value;
    }

    private void OnSliderChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_isInitialized || _suppressEvents) return;
        PullUIToConfig();
        TxtStrength.Text = FormatValue(SldStrength.Value);
        TxtProtect.Text  = FormatValue(SldProtect.Value);
        UpdateCurve();
        if (_isEnabled) ApplyCurrentEditing();
    }

    private void OnMonitorChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CmbMonitors.SelectedItem is ComboBoxItem item)
            _selectedMonitor = item.Tag as string;
        if (_isEnabled) ApplyCurrentEditing();
    }

    private void OnToggleClicked(object sender, RoutedEventArgs e) => Toggle();

    public void ToggleFromIpc() => Toggle();

    public void NotifyDisabledFromIpc()
    {
        _isEnabled = false;
        UpdateEnabledUi();
    }

    private void Toggle()
    {
        _isEnabled = !_isEnabled;
        if (_isEnabled) ApplyCurrentEditing();
        else _applier.RestoreSystemDefaults();
        UpdateEnabledUi();
        _ = _ipc?.SendEventAsync(IpcTopics.NightVisionState, new { enabled = _isEnabled });
    }

    private void ApplyCurrentEditing()
    {
        if (_selectedMonitor == null) _applier.ApplyAll(_editingConfig);
        else _applier.Apply(_selectedMonitor, _editingConfig);
    }

    private void UpdateEnabledUi()
    {
        if (_isEnabled)
        {
            BtnToggle.Content = "关闭夜视";
            TxtPreviewState.Text =
                $"已开启 · 强度 {FormatValue(_editingConfig.Strength)} · 亮部保护 {FormatValue(_editingConfig.HighlightProtect)}\n\n" +
                "滑块和方案切换会实时作用到屏幕。";
            TxtPreviewState.Foreground = System.Windows.Media.Brushes.Orange;
            TxtStatus.Text = "夜视已开启";
        }
        else
        {
            BtnToggle.Content = "开启夜视";
            TxtPreviewState.Text = "未开启。\n\n滑块的修改只改变曲线预览,不会影响屏幕。\n点击「开启夜视」或按下全局热键即可应用到屏幕。";
            TxtPreviewState.Foreground = System.Windows.Media.Brushes.LightGray;
            TxtStatus.Text = "夜视已关闭";
        }
    }

    private void OnResetClicked(object sender, RoutedEventArgs e)
    {
        _editingConfig = NightVisionConfig.Default.Clone();
        PushConfigToUI(_editingConfig);
        UpdateCurve();
        if (_isEnabled) ApplyCurrentEditing();
        TxtStatus.Text = "已重置为默认";
    }

    private void OnCloseClicked(object sender, RoutedEventArgs e)
    {
        System.Windows.Application.Current.Shutdown();
    }

    private void UpdateCurve() => CurveView.Config = _editingConfig.Clone();

    private void OnHotkeyChanged(object sender, RoutedEventArgs e)
    {
        var txt = (TxtHotkey.Text ?? "").Trim();
        if (string.IsNullOrEmpty(txt)) { TxtHotkey.Text = _hotkeyText; return; }
        if (!HotkeyManager.TryParseHotkey(txt, out _, out _))
        {
            TxtStatus.Text = $"无法识别热键:{txt}";
            TxtHotkey.Text = _hotkeyText;
            return;
        }
        _hotkeyText = txt;
        RegisterToggleHotkey();

        var cfg = _schemes.LoadConfig();
        cfg.ToggleHotkey = _hotkeyText;
        _schemes.SaveConfig(cfg);
        TxtStatus.Text = $"热键已更新:{_hotkeyText}";
    }

    private void RegisterToggleHotkey()
    {
        if (_hotkeyId >= 0) { _hotkeys.Unregister(_hotkeyId); _hotkeyId = -1; }
        if (!HotkeyManager.TryParseHotkey(_hotkeyText, out var mods, out var vk)) return;
        _hotkeyId = _hotkeys.Register(mods, vk, () => Dispatcher.Invoke(Toggle));
    }

    private void ReloadSchemes()
    {
        _loadedSchemes = _schemes.LoadAllSchemes();
        _suppressEvents = true;
        try
        {
            CmbSchemes.Items.Clear();
            foreach (var s in _loadedSchemes) CmbSchemes.Items.Add(s.Name);
        }
        finally { _suppressEvents = false; }
    }

    private void OnLoadSchemeClicked(object sender, RoutedEventArgs e)
    {
        var name = (CmbSchemes.Text ?? "").Trim();
        if (string.IsNullOrEmpty(name)) return;
        var scheme = _loadedSchemes.FirstOrDefault(s => s.Name == name);
        if (scheme == null) { TxtStatus.Text = $"未找到方案:{name}"; return; }
        _editingConfig = scheme.Config.Clone();
        PushConfigToUI(_editingConfig);
        UpdateCurve();
        if (_isEnabled) ApplyCurrentEditing();

        var cfg = _schemes.LoadConfig();
        cfg.LastSchemeName = name;
        _schemes.SaveConfig(cfg);

        TxtStatus.Text = $"已加载方案:{name}";
        _ = _ipc?.SendEventAsync(IpcTopics.NightVisionSchemeApplied, new { name });
    }

    private void OnSaveSchemeClicked(object sender, RoutedEventArgs e)
    {
        var name = (CmbSchemes.Text ?? "").Trim();
        if (string.IsNullOrEmpty(name)) { TxtStatus.Text = "请输入方案名称"; return; }

        PullUIToConfig();
        var scheme = new NightVisionScheme
        {
            Name = name,
            Config = _editingConfig.Clone(),
            ApplyToAllMonitors = _selectedMonitor == null,
        };
        _schemes.Save(scheme);
        ReloadSchemes();
        CmbSchemes.Text = name;

        var cfg = _schemes.LoadConfig();
        cfg.LastSchemeName = name;
        _schemes.SaveConfig(cfg);

        TxtStatus.Text = $"方案已保存:{name}";
        _ = _ipc?.SendEventAsync(IpcTopics.NightVisionSchemesChanged);
    }

    private void OnDeleteSchemeClicked(object sender, RoutedEventArgs e)
    {
        var name = (CmbSchemes.Text ?? "").Trim();
        if (string.IsNullOrEmpty(name)) return;
        _schemes.Delete(name);
        ReloadSchemes();
        TxtStatus.Text = $"已删除方案:{name}";
        _ = _ipc?.SendEventAsync(IpcTopics.NightVisionSchemesChanged);
    }
}
