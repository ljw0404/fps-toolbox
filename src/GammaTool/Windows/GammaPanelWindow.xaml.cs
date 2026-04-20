using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using FPSToolbox.Shared.Ipc;
using GammaTool.Core;
using GammaTool.Models;

namespace GammaTool.Windows;

/// <summary>
/// Gamma 调节主面板。状态机：
///   - 未预览（编辑中）：调滑块 → 仅更新 _editingConfig 和曲线；屏幕保持 _currentAppliedLut。
///   - 预览中：_editingConfig 已应用到屏幕；再点「退出预览」→ 屏幕回到 _currentAppliedLut。
///   - 保存：应用 _editingConfig → 成为新的 _currentAppliedLut，并持久化为方案；预览状态重置为"未预览"。
///   - 关闭窗口 / 程序退出：恢复系统默认（_systemDefaultLut），进程退出。
/// </summary>
public partial class GammaPanelWindow : Window
{
    private readonly GammaApplier _applier;
    private readonly SchemeManager _schemes;
    private readonly IpcClient? _ipc;

    private GammaConfig _editingConfig = new();
    private GammaConfig _currentAppliedConfig = new(); // 当前"持久"应用到屏幕的 config（未预览时屏幕处于这个状态）
    private bool _isPreviewing;
    private bool _suppressEvents;
    private bool _isInitialized; // XAML 加载完成前所有控件事件一律忽略

    private List<GammaScheme> _loadedSchemes = new();
    private string? _selectedMonitor; // null = 全部显示器

    public GammaPanelWindow(GammaApplier applier, SchemeManager schemes, IpcClient? ipc)
    {
        InitializeComponent();
        _isInitialized = true;  // InitializeComponent 完成后控件全部就绪
        _applier = applier;
        _schemes = schemes;
        _ipc = ipc;

        WireValueInput(TxtMGamma,    SldMGamma,    ChannelParams.GammaMin,    ChannelParams.GammaMax);
        WireValueInput(TxtMBright,   SldMBright,   ChannelParams.BrightMin,   ChannelParams.BrightMax);
        WireValueInput(TxtMContrast, SldMContrast, ChannelParams.ContrastMin, ChannelParams.ContrastMax);
        WireValueInput(TxtRGamma,    SldRGamma,    ChannelParams.GammaMin,    ChannelParams.GammaMax);
        WireValueInput(TxtRBright,   SldRBright,   ChannelParams.BrightMin,   ChannelParams.BrightMax);
        WireValueInput(TxtRContrast, SldRContrast, ChannelParams.ContrastMin, ChannelParams.ContrastMax);
        WireValueInput(TxtGGamma,    SldGGamma,    ChannelParams.GammaMin,    ChannelParams.GammaMax);
        WireValueInput(TxtGBright,   SldGBright,   ChannelParams.BrightMin,   ChannelParams.BrightMax);
        WireValueInput(TxtGContrast, SldGContrast, ChannelParams.ContrastMin, ChannelParams.ContrastMax);
        WireValueInput(TxtBGamma,    SldBGamma,    ChannelParams.GammaMin,    ChannelParams.GammaMax);
        WireValueInput(TxtBBright,   SldBBright,   ChannelParams.BrightMin,   ChannelParams.BrightMax);
        WireValueInput(TxtBContrast, SldBContrast, ChannelParams.ContrastMin, ChannelParams.ContrastMax);

        Loaded += OnLoaded;
        Closing += (_, _) =>
        {
            // 关闭窗口 = 退出程序（按 spec）
            System.Windows.Application.Current.Shutdown();
        };
    }

    /// <summary>把数字输入框与对应的滑块做双向绑定,带范围钳制。</summary>
    private void WireValueInput(TextBox box, Slider slider, double min, double max)
    {
        void Commit()
        {
            var txt = (box.Text ?? "").Trim();
            if (!double.TryParse(txt, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) &&
                !double.TryParse(txt, NumberStyles.Float, CultureInfo.CurrentCulture, out v))
            {
                // 解析失败:还原为滑块当前值
                box.Text = FormatValue(slider.Value);
                return;
            }
            if (v < min) v = min;
            if (v > max) v = max;
            slider.Value = v;  // 触发 OnSliderChanged → 统一回写
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
        // 初始化显示器下拉
        CmbMonitors.Items.Clear();
        CmbMonitors.Items.Add(new ComboBoxItem { Content = "全部显示器", Tag = null });
        foreach (var m in _applier.Monitors)
            CmbMonitors.Items.Add(new ComboBoxItem { Content = m.DisplayName, Tag = m.DeviceName });
        CmbMonitors.SelectedIndex = 0;

        ReloadSchemes();

        // 默认选中「三角洲-黑夜」(首次运行时由 SchemeManager 自动种入)
        var cfg = _schemes.LoadConfig();
        var defaultName = cfg.LastSchemeName;
        if (string.IsNullOrWhiteSpace(defaultName))
            defaultName = _loadedSchemes.Any(s => s.Name == BuiltInSchemes.DeltaNight)
                ? BuiltInSchemes.DeltaNight
                : _loadedSchemes.FirstOrDefault()?.Name;

        if (!string.IsNullOrEmpty(defaultName) &&
            _loadedSchemes.FirstOrDefault(s => s.Name == defaultName) is { } scheme)
        {
            _editingConfig = scheme.Config.Clone();
            _currentAppliedConfig = scheme.Config.Clone();
            _suppressEvents = true;
            try { CmbSchemes.Text = defaultName; }
            finally { _suppressEvents = false; }

            // 应用到屏幕,让用户启动即看到默认方案生效
            ApplyToScreen(_currentAppliedConfig);
            TxtStatus.Text = $"已应用默认方案:{defaultName}";
        }
        else
        {
            _editingConfig = _currentAppliedConfig.Clone();
        }

        PushConfigToUI(_editingConfig);
        UpdateCurve();
        UpdatePreviewStateUi();
    }

    // ──────────────────────────────────────────────────────────────
    // UI ↔ config 同步
    // ──────────────────────────────────────────────────────────────

    private void PushConfigToUI(GammaConfig cfg)
    {
        _suppressEvents = true;
        try
        {
            ChkLinked.IsChecked = cfg.LinkedRgb;
            UpdatePanelVisibility();

            SldMGamma.Value = cfg.Master.Gamma;
            SldMBright.Value = cfg.Master.Brightness;
            SldMContrast.Value = cfg.Master.Contrast;

            SldRGamma.Value = cfg.Red.Gamma;
            SldRBright.Value = cfg.Red.Brightness;
            SldRContrast.Value = cfg.Red.Contrast;

            SldGGamma.Value = cfg.Green.Gamma;
            SldGBright.Value = cfg.Green.Brightness;
            SldGContrast.Value = cfg.Green.Contrast;

            SldBGamma.Value = cfg.Blue.Gamma;
            SldBBright.Value = cfg.Blue.Brightness;
            SldBContrast.Value = cfg.Blue.Contrast;

            RefreshAllValueLabels();
        }
        finally { _suppressEvents = false; }
    }

    private void RefreshAllValueLabels()
    {
        TxtMGamma.Text    = FormatValue(SldMGamma.Value);
        TxtMBright.Text   = FormatValue(SldMBright.Value);
        TxtMContrast.Text = FormatValue(SldMContrast.Value);
        TxtRGamma.Text    = FormatValue(SldRGamma.Value);
        TxtRBright.Text   = FormatValue(SldRBright.Value);
        TxtRContrast.Text = FormatValue(SldRContrast.Value);
        TxtGGamma.Text    = FormatValue(SldGGamma.Value);
        TxtGBright.Text   = FormatValue(SldGBright.Value);
        TxtGContrast.Text = FormatValue(SldGContrast.Value);
        TxtBGamma.Text    = FormatValue(SldBGamma.Value);
        TxtBBright.Text   = FormatValue(SldBBright.Value);
        TxtBContrast.Text = FormatValue(SldBContrast.Value);
    }

    private void PullUIToConfig()
    {
        _editingConfig.LinkedRgb = ChkLinked.IsChecked == true;
        _editingConfig.Master.Gamma = SldMGamma.Value;
        _editingConfig.Master.Brightness = SldMBright.Value;
        _editingConfig.Master.Contrast = SldMContrast.Value;
        _editingConfig.Red.Gamma = SldRGamma.Value;
        _editingConfig.Red.Brightness = SldRBright.Value;
        _editingConfig.Red.Contrast = SldRContrast.Value;
        _editingConfig.Green.Gamma = SldGGamma.Value;
        _editingConfig.Green.Brightness = SldGBright.Value;
        _editingConfig.Green.Contrast = SldGContrast.Value;
        _editingConfig.Blue.Gamma = SldBGamma.Value;
        _editingConfig.Blue.Brightness = SldBBright.Value;
        _editingConfig.Blue.Contrast = SldBContrast.Value;
    }

    // ──────────────────────────────────────────────────────────────
    // Slider / checkbox 事件
    // ──────────────────────────────────────────────────────────────

    private void OnSliderChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_isInitialized || _suppressEvents) return;
        PullUIToConfig();
        RefreshAllValueLabels();
        UpdateCurve();

        // 预览中：滑块变化 → 立即应用到屏幕
        if (_isPreviewing) ApplyCurrentEditing();
    }

    private void OnLinkedChanged(object sender, RoutedEventArgs e)
    {
        if (!_isInitialized || _suppressEvents) return;
        _editingConfig.LinkedRgb = ChkLinked.IsChecked == true;
        UpdatePanelVisibility();
        UpdateCurve();
        if (_isPreviewing) ApplyCurrentEditing();
    }

    private void UpdatePanelVisibility()
    {
        bool linked = ChkLinked.IsChecked == true;
        HdrMaster.Text = linked ? "● 主通道（RGB 联动）" : "● 主通道（已停用，请在下方独立调整 RGB）";
        PnlMaster.IsEnabled = linked;
        PnlPerChannel.Visibility = linked ? Visibility.Collapsed : Visibility.Visible;
    }

    private void OnMonitorChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CmbMonitors.SelectedItem is ComboBoxItem item)
            _selectedMonitor = item.Tag as string;
        // 选择显示器不会自动应用；仅在预览 / 保存时使用这个值
    }

    // ──────────────────────────────────────────────────────────────
    // 核心：预览 / 保存 / 重置 / 关闭
    // ──────────────────────────────────────────────────────────────

    private void OnPreviewClicked(object sender, RoutedEventArgs e)
    {
        if (_isPreviewing)
        {
            // 退出预览 → 恢复屏幕到 _currentAppliedConfig
            ApplyToScreen(_currentAppliedConfig);
            _isPreviewing = false;
        }
        else
        {
            // 进入预览 → 应用 _editingConfig 到屏幕
            ApplyCurrentEditing();
            _isPreviewing = true;
        }
        UpdatePreviewStateUi();
        _ = _ipc?.SendEventAsync(IpcTopics.GammaPreviewState, new { previewing = _isPreviewing });
    }

    private void ApplyCurrentEditing() => ApplyToScreen(_editingConfig);

    private void ApplyToScreen(GammaConfig cfg)
    {
        if (_selectedMonitor == null)
            _applier.ApplyAll(cfg);
        else
            _applier.Apply(_selectedMonitor, cfg);
    }

    private void UpdatePreviewStateUi()
    {
        if (_isPreviewing)
        {
            BtnPreview.Content = "退出预览";
            TxtPreviewState.Text = "正在预览：屏幕已实时应用当前编辑中的设置。\n\n点击「退出预览」可还原到上次保存的配置；\n点击「保存」会将当前配置保存为方案，并自动结束预览。";
            TxtPreviewState.Foreground = System.Windows.Media.Brushes.Orange;
        }
        else
        {
            BtnPreview.Content = "预览";
            TxtPreviewState.Text = "未预览（编辑中）。\n\n滑块的修改仅更新曲线，不会影响屏幕。\n点击「预览」可以把当前设置应用到屏幕查看效果。";
            TxtPreviewState.Foreground = System.Windows.Media.Brushes.LightGray;
        }
    }

    private void OnResetClicked(object sender, RoutedEventArgs e)
    {
        _editingConfig = new GammaConfig();
        PushConfigToUI(_editingConfig);
        UpdateCurve();
        if (_isPreviewing) ApplyCurrentEditing();
    }

    private void OnCloseClicked(object sender, RoutedEventArgs e)
    {
        // 关闭程序（按 spec）
        System.Windows.Application.Current.Shutdown();
    }

    private void UpdateCurve() => CurveView.Config = _editingConfig.Clone();

    // ──────────────────────────────────────────────────────────────
    // 方案管理
    // ──────────────────────────────────────────────────────────────

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

    private void OnSchemeSelected(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressEvents) return;
        // 纯选中不自动加载，避免误操作；用户点「加载」按钮才会套用
    }

    private void OnLoadSchemeClicked(object sender, RoutedEventArgs e)
    {
        var name = (CmbSchemes.Text ?? "").Trim();
        if (string.IsNullOrEmpty(name)) return;
        var scheme = _loadedSchemes.FirstOrDefault(s => s.Name == name);
        if (scheme == null) { TxtStatus.Text = $"未找到方案：{name}"; return; }
        _editingConfig = scheme.Config.Clone();
        PushConfigToUI(_editingConfig);
        UpdateCurve();
        if (_isPreviewing) ApplyCurrentEditing();
        TxtStatus.Text = $"已加载方案：{name}（未保存生效）";
    }

    private void OnSaveSchemeClicked(object sender, RoutedEventArgs e)
    {
        var name = (CmbSchemes.Text ?? "").Trim();
        if (string.IsNullOrEmpty(name)) { TxtStatus.Text = "请输入方案名称"; return; }

        PullUIToConfig();
        var scheme = new GammaScheme
        {
            Name = name,
            Config = _editingConfig.Clone(),
            ApplyToAllMonitors = _selectedMonitor == null,
        };
        _schemes.Save(scheme);

        // 应用到屏幕 & 成为新的"已应用"
        _currentAppliedConfig = _editingConfig.Clone();
        ApplyToScreen(_currentAppliedConfig);
        _isPreviewing = false;
        UpdatePreviewStateUi();

        ReloadSchemes();
        CmbSchemes.Text = name;
        TxtStatus.Text = $"方案已保存并应用：{name}";

        // 记住作为下次启动的默认
        var cfg = _schemes.LoadConfig();
        cfg.LastSchemeName = name;
        _schemes.SaveConfig(cfg);

        _ = _ipc?.SendEventAsync(IpcTopics.GammaSchemeApplied, new { name });
        _ = _ipc?.SendEventAsync(IpcTopics.GammaSchemesChanged);
    }

    private void OnDeleteSchemeClicked(object sender, RoutedEventArgs e)
    {
        var name = (CmbSchemes.Text ?? "").Trim();
        if (string.IsNullOrEmpty(name)) return;
        _schemes.Delete(name);
        ReloadSchemes();
        TxtStatus.Text = $"已删除方案：{name}";
        _ = _ipc?.SendEventAsync(IpcTopics.GammaSchemesChanged);
    }
}
