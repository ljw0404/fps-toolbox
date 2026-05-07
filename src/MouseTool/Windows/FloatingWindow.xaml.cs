using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using MouseTool.Core;
using MouseTool.Models;

namespace MouseTool.Windows;

public partial class FloatingWindow : Window
{
    private readonly ConfigManager _configManager;
    private HotkeyManager? _hotkey;
    private KkrbDataService? _dataService;
    private CancellationTokenSource? _loadCts;

    // resize grip 状态
    private bool _isResizing;
    private Point _resizeStart;
    private double _resizeStartW, _resizeStartH;

    // 改枪 Tab 状态
    private List<DfWeapon>? _weapons;
    private Dictionary<string, List<DfGunCode>>? _gunCodes;
    private bool _gunPanelBuilt;
    private readonly HashSet<string> _expandedWeapons = new();

    // 装备计算 Tab 状态
    private List<ArmorConfigItem>? _armorItems;
    private List<ArmorConfigItem>? _armorComboList;
    private bool _armorPanelBuilt;
    private int? _armorLevelFilter;
    private string? _armorTypeFilter;
    private WrapPanel? _levelFilterRow;
    private WrapPanel? _typeFilterRow;
    private ComboBox? _armorCombo;
    private TextBox? _armorTxtMaxDur;
    private TextBox? _armorTxtCurDur;
    private StackPanel? _armorResultPanel;

    public Action? OnOpenSettings { get; set; }

    public FloatingWindow(ConfigManager configManager)
    {
        InitializeComponent();
        _configManager = configManager;
        ApplyConfig();
        RegisterHotkey();
        LocationChanged += (_, _) =>
        {
            _configManager.Current.WindowLeft = Left;
            _configManager.Current.WindowTop = Top;
        };
        Loaded += async (_, _) => await LoadDataAsync();
        Closing += (_, _) => { _hotkey?.Unregister(); _hotkey?.Dispose(); _configManager.Save(); };
    }

    // ──────────────────────────────────────────
    // 拖动（排除 ResizeGrip 区域）
    // ──────────────────────────────────────────
    private void OnWindowMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (_isResizing) return;
        if (e.ChangedButton == MouseButton.Left && e.OriginalSource != SizeGrip)
            DragMove();
    }

    // ──────────────────────────────────────────
    // Resize Grip
    // ──────────────────────────────────────────
    private void OnResizeGripMouseDown(object sender, MouseButtonEventArgs e)
    {
        _isResizing = true;
        _resizeStart = PointToScreen(e.GetPosition(this));
        _resizeStartW = Width;
        _resizeStartH = Height;
        SizeGrip.CaptureMouse();
        e.Handled = true;
    }

    private void OnResizeGripMouseMove(object sender, MouseEventArgs e)
    {
        if (!_isResizing) return;
        var cur = PointToScreen(e.GetPosition(this));
        var dw = cur.X - _resizeStart.X;
        var dh = cur.Y - _resizeStart.Y;
        Width  = Math.Max(MinWidth,  _resizeStartW + dw);
        Height = Math.Max(MinHeight, _resizeStartH + dh);
    }

    private void OnResizeGripMouseUp(object sender, MouseButtonEventArgs e)
    {
        _isResizing = false;
        SizeGrip.ReleaseMouseCapture();
    }

    // ──────────────────────────────────────────
    // Tab 切换
    // ──────────────────────────────────────────
    private void OnTabChanged(object sender, RoutedEventArgs e)
    {
        if (PanelPassword == null || PanelCraft == null || PanelItems == null
            || PanelAmmo == null || PanelArmor == null || PanelGun == null) return;

        PanelPassword.Visibility = TabPassword.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        PanelCraft.Visibility    = TabCraft.IsChecked    == true ? Visibility.Visible : Visibility.Collapsed;
        PanelItems.Visibility    = TabItems.IsChecked    == true ? Visibility.Visible : Visibility.Collapsed;
        PanelAmmo.Visibility     = TabAmmo.IsChecked     == true ? Visibility.Visible : Visibility.Collapsed;
        PanelArmor.Visibility    = TabArmor.IsChecked    == true ? Visibility.Visible : Visibility.Collapsed;
        PanelGun.Visibility      = TabGun.IsChecked      == true ? Visibility.Visible : Visibility.Collapsed;

        if (TabArmor.IsChecked == true && !_armorPanelBuilt)
            _ = LoadArmorPanelAsync();

        if (TabGun.IsChecked == true && !_gunPanelBuilt)
            _ = LoadGunPanelAsync();
    }

    // ──────────────────────────────────────────
    // 刷新 / 加载数据
    // ──────────────────────────────────────────
    private void OnRefreshClicked(object sender, RoutedEventArgs e)
    {
        _ = LoadDataAsync();
    }

    private async Task LoadDataAsync()
    {
        _loadCts?.Cancel();
        _loadCts = new CancellationTokenSource();
        var ct = _loadCts.Token;

        ShowLoading("⏳ 加载中...");

        try
        {
            _dataService ??= new KkrbDataService(_configManager.Current.DbConnectionString);
            var snap = await _dataService.GetLatestAsync(ct);
            if (ct.IsCancellationRequested) return;

            Dispatcher.Invoke(() =>
            {
                if (snap == null) { ShowError("⚠ 无数据"); return; }
                RenderAll(snap);
                HideLoading();
            });
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            Dispatcher.Invoke(() => ShowError($"❌ {ex.Message.Split('\n')[0]}"));
        }
    }

    private void ShowLoading(string msg)
    {
        TxtLoadingMsg.Text = msg;
        LoadingMask.Visibility = Visibility.Visible;
    }
    private void ShowError(string msg)
    {
        TxtLoadingMsg.Text = msg;
        LoadingMask.Visibility = Visibility.Visible;
    }
    private void HideLoading() => LoadingMask.Visibility = Visibility.Collapsed;

    // ──────────────────────────────────────────
    // 改枪 Tab
    // ──────────────────────────────────────────

    private async Task LoadGunPanelAsync()
    {
        StackGunList.Children.Clear();
        StackGunList.Children.Add(new TextBlock
        {
            Text = "⏳ 加载武器数据...",
            FontSize = 12,
            Foreground = new SolidColorBrush(Color.FromRgb(0x88, 0xAA, 0xBB)),
            Margin = new Thickness(0, 20, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Center,
        });

        try
        {
            _dataService ??= new KkrbDataService(_configManager.Current.DbConnectionString);
            _weapons  = await _dataService.GetWeaponsAsync();
            _gunCodes = await _dataService.GetAllGunCodesAsync();
            Dispatcher.Invoke(RenderWeaponList);
            _gunPanelBuilt = true;
        }
        catch (Exception ex)
        {
            Dispatcher.Invoke(() =>
            {
                StackGunList.Children.Clear();
                StackGunList.Children.Add(Txt($"❌ 加载失败: {ex.Message.Split('\n')[0]}", "#EF5350", 11));
            });
        }
    }

    private void OnGunSearchChanged(object sender, TextChangedEventArgs e)
    {
        if (!_gunPanelBuilt) return;
        RenderWeaponList();
    }

    /// <summary>根据搜索词过滤后重新渲染武器列表。</summary>
    private void RenderWeaponList()
    {
        StackGunList.Children.Clear();
        if (_weapons == null) return;

        var keyword = TxtGunSearch?.Text?.Trim() ?? "";
        var filtered = string.IsNullOrEmpty(keyword)
            ? _weapons
            : _weapons.Where(w =>
                w.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                w.Key.Contains(keyword, StringComparison.OrdinalIgnoreCase)).ToList();

        if (filtered.Count == 0)
        {
            StackGunList.Children.Add(Txt("没有匹配的武器", "#668899", 12));
            return;
        }

        foreach (var weapon in filtered)
            StackGunList.Children.Add(BuildWeaponAccordion(weapon));
    }

    /// <summary>构建单个武器的手风琴条目。</summary>
    private Border BuildWeaponAccordion(DfWeapon weapon)
    {
        var codes = (_gunCodes != null && _gunCodes.TryGetValue(weapon.Key, out var c)) ? c : new List<DfGunCode>();
        var isExpanded = _expandedWeapons.Contains(weapon.Key);

        // ── 外层容器 ──
        var outer = new Border
        {
            Margin          = new Thickness(0, 0, 0, 4),
            CornerRadius    = new CornerRadius(6),
            BorderThickness = new Thickness(1),
            BorderBrush     = new SolidColorBrush(Color.FromArgb(0x33, 0x7E, 0xC8, 0xE3)),
            Background      = new SolidColorBrush(Color.FromArgb(0x18, 0xFF, 0xFF, 0xFF)),
            ClipToBounds    = true,
        };

        var outerStack = new StackPanel();

        // ── 标题行（点击展开/收起）──
        var header = new Border
        {
            Padding    = new Thickness(10, 8, 10, 8),
            Background = Brushes.Transparent,
            Cursor     = Cursors.Hand,
        };

        var headerGrid = new Grid();
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(18) });
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var arrow = new TextBlock
        {
            Text               = isExpanded ? "▼" : "▶",
            FontSize           = 10,
            Foreground         = new SolidColorBrush(Color.FromRgb(0x7E, 0xC8, 0xE3)),
            VerticalAlignment  = VerticalAlignment.Center,
        };
        Grid.SetColumn(arrow, 0);

        var nameBlock = new TextBlock
        {
            Text              = weapon.Name,
            FontSize          = 13,
            FontWeight        = FontWeights.SemiBold,
            Foreground        = new SolidColorBrush(Color.FromRgb(0xE0, 0xE8, 0xF0)),
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(nameBlock, 1);

        var countBlock = new TextBlock
        {
            Text              = codes.Count > 0 ? $"{codes.Count} 个配置" : "暂无配置",
            FontSize          = 11,
            Foreground        = new SolidColorBrush(Color.FromArgb(0xAA, 0x7E, 0xC8, 0xE3)),
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(countBlock, 2);

        headerGrid.Children.Add(arrow);
        headerGrid.Children.Add(nameBlock);
        headerGrid.Children.Add(countBlock);
        header.Child = headerGrid;

        // ── 内容区（改枪码列表）──
        var content = new StackPanel
        {
            Visibility = isExpanded ? Visibility.Visible : Visibility.Collapsed,
            Margin     = new Thickness(8, 0, 8, 8),
        };

        if (codes.Count > 0)
        {
            foreach (var gc in codes)
                content.Children.Add(BuildGunCodeCard(gc));
        }
        else
        {
            content.Children.Add(Txt("暂无改枪码数据", "#557788", 11));
        }

        // ── 点击头部展开/收起 ──
        header.MouseLeftButtonDown += (_, _) =>
        {
            if (_expandedWeapons.Contains(weapon.Key))
            {
                _expandedWeapons.Remove(weapon.Key);
                content.Visibility = Visibility.Collapsed;
                arrow.Text         = "▶";
                outer.BorderBrush  = new SolidColorBrush(Color.FromArgb(0x33, 0x7E, 0xC8, 0xE3));
                outer.Background   = new SolidColorBrush(Color.FromArgb(0x18, 0xFF, 0xFF, 0xFF));
            }
            else
            {
                _expandedWeapons.Add(weapon.Key);
                content.Visibility = Visibility.Visible;
                arrow.Text         = "▼";
                outer.BorderBrush  = new SolidColorBrush(Color.FromArgb(0x88, 0x7E, 0xC8, 0xE3));
                outer.Background   = new SolidColorBrush(Color.FromArgb(0x22, 0x7E, 0xC8, 0xE3));
            }
        };

        outerStack.Children.Add(header);
        outerStack.Children.Add(content);
        outer.Child = outerStack;
        return outer;
    }

    /// <summary>构建单条改枪码卡片。底部行：💰价值  🔥热度  [复制按钮]。</summary>
    private Border BuildGunCodeCard(DfGunCode gc)
    {
        var card = new Border
        {
            Background      = new SolidColorBrush(Color.FromArgb(0x28, 0x00, 0x80, 0xCC)),
            BorderBrush     = new SolidColorBrush(Color.FromArgb(0x44, 0x44, 0x88, 0xBB)),
            BorderThickness = new Thickness(1),
            CornerRadius    = new CornerRadius(5),
            Padding         = new Thickness(10, 8, 10, 8),
            Margin          = new Thickness(0, 0, 0, 5),
        };

        var sp = new StackPanel();

        // ── 改枪码 ──
        sp.Children.Add(new TextBlock
        {
            Text         = gc.Code,
            FontSize     = 13,
            FontWeight   = FontWeights.Bold,
            FontFamily   = new FontFamily("Consolas, Courier New"),
            Foreground   = new SolidColorBrush(Color.FromRgb(0xFF, 0xD7, 0x54)),
            TextWrapping = TextWrapping.Wrap,
        });

        // ── 描述 ──
        if (!string.IsNullOrWhiteSpace(gc.Description))
            sp.Children.Add(new TextBlock
            {
                Text         = gc.Description,
                FontSize     = 11,
                Foreground   = new SolidColorBrush(Color.FromRgb(0xB0, 0xC8, 0xD8)),
                Margin       = new Thickness(0, 3, 0, 0),
                TextWrapping = TextWrapping.Wrap,
            });

        // ── 底部行：💰价值 | 🔥热度 | (弹簧) | [复制按钮] —— 全部 Grid 单元，逐个垂直居中 ──
        var bottomGrid = new Grid { Margin = new Thickness(0, 10, 0, 0) };
        bottomGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        bottomGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        bottomGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        bottomGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        // Col 0: 💰 价值（用单 TextBlock + Inline，避免嵌套 StackPanel 对齐问题）
        if (!string.IsNullOrWhiteSpace(gc.Value))
        {
            var moneyTb = new TextBlock
            {
                FontSize          = 11,
                VerticalAlignment = VerticalAlignment.Center,
            };
            moneyTb.Inlines.Add(new System.Windows.Documents.Run("💰 ")
            {
                Foreground = new SolidColorBrush(Color.FromRgb(0x60, 0x7D, 0x8B)),
            });
            moneyTb.Inlines.Add(new System.Windows.Documents.Run(gc.Value)
            {
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(0x66, 0xBB, 0x6A)),
            });
            Grid.SetColumn(moneyTb, 0);
            bottomGrid.Children.Add(moneyTb);
        }

        // Col 1: 🔥 热度
        if (gc.CopyCount > 0)
        {
            var fireTb = new TextBlock
            {
                FontSize          = 11,
                Margin            = new Thickness(12, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
            };
            fireTb.Inlines.Add(new System.Windows.Documents.Run("🔥 ")
            {
                Foreground = new SolidColorBrush(Color.FromRgb(0x60, 0x7D, 0x8B)),
            });
            fireTb.Inlines.Add(new System.Windows.Documents.Run($"{gc.CopyCount:N0}")
            {
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0xAB, 0x40)),
            });
            Grid.SetColumn(fireTb, 1);
            bottomGrid.Children.Add(fireTb);
        }

        // Col 3: 复制按钮（Border + PreviewMouseLeftButtonDown，最可靠）
        // 关键：PreviewMouseLeftButtonDown 在隧道阶段触发，
        // 此时 e.Handled = true 会阻止冒泡阶段的窗口 MouseLeftButtonDown 触发 DragMove()，
        // UI 线程不会被 DragMove 阻塞，文字变化能立即渲染。
        var copyLabel = new TextBlock
        {
            Text                = "复制",
            FontSize            = 12,
            FontWeight          = FontWeights.SemiBold,
            Foreground          = new SolidColorBrush(Color.FromRgb(0xE0, 0xF0, 0xFF)),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment   = VerticalAlignment.Center,
            IsHitTestVisible    = false,   // 让点击穿透到 Border
        };
        var copyBtn = new Border
        {
            Background        = new SolidColorBrush(Color.FromArgb(0xCC, 0x7E, 0xC8, 0xE3)),
            BorderBrush       = new SolidColorBrush(Color.FromArgb(0xFF, 0x7E, 0xC8, 0xE3)),
            BorderThickness   = new Thickness(1),
            CornerRadius      = new CornerRadius(4),
            Padding           = new Thickness(14, 4, 14, 4),
            Cursor            = Cursors.Hand,
            VerticalAlignment = VerticalAlignment.Center,
            Child             = copyLabel,
        };

        // 悬停效果
        copyBtn.MouseEnter += (_, _) =>
            copyBtn.Background = new SolidColorBrush(Color.FromArgb(0xFF, 0x9E, 0xD8, 0xF3));
        copyBtn.MouseLeave += (_, _) =>
        {
            // 仅当不在"已复制"状态时还原
            if (copyLabel.Text == "复制")
                copyBtn.Background = new SolidColorBrush(Color.FromArgb(0xCC, 0x7E, 0xC8, 0xE3));
        };

        // PreviewMouseLeftButtonDown：隧道阶段触发，先于主窗口的 MouseLeftButtonDown
        copyBtn.PreviewMouseLeftButtonDown += (_, e) =>
        {
            e.Handled = true;   // 阻断冒泡 → 主窗口 DragMove 不会被调用

            // Windows 剪贴板被其他进程临时占用时，SetText 会抛 CLIPBRD_E_CANT_OPEN，
            // 但内容往往已经成功写入。重试 + 回查双保险。
            bool ok = TryCopyToClipboard(gc.Code);

            if (ok)
            {
                copyLabel.Text       = "✅ 已复制";
                copyLabel.Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0xFF, 0xFF));
                copyBtn.Background   = new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0x50));
                copyBtn.BorderBrush  = new SolidColorBrush(Color.FromRgb(0x66, 0xBB, 0x6A));
            }
            else
            {
                copyLabel.Text       = "❌ 失败";
                copyLabel.Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0xFF, 0xFF));
                copyBtn.Background   = new SolidColorBrush(Color.FromRgb(0xEF, 0x53, 0x50));
                copyBtn.BorderBrush  = new SolidColorBrush(Color.FromRgb(0xFF, 0x80, 0x80));
            }

            var timer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(1500),
            };
            timer.Tick += (_, _) =>
            {
                copyLabel.Text       = "复制";
                copyLabel.Foreground = new SolidColorBrush(Color.FromRgb(0xE0, 0xF0, 0xFF));
                copyBtn.Background   = new SolidColorBrush(Color.FromArgb(0xCC, 0x7E, 0xC8, 0xE3));
                copyBtn.BorderBrush  = new SolidColorBrush(Color.FromArgb(0xFF, 0x7E, 0xC8, 0xE3));
                timer.Stop();
            };
            timer.Start();
        };

        Grid.SetColumn(copyBtn, 3);
        bottomGrid.Children.Add(copyBtn);

        sp.Children.Add(bottomGrid);
        card.Child = sp;
        return card;
    }

    // ──────────────────────────────────────────
    // 装备计算 Tab
    // ──────────────────────────────────────────
    private async Task LoadArmorPanelAsync()
    {
        StackArmor.Children.Clear();
        StackArmor.Children.Add(new TextBlock
        {
            Text = "⏳ 加载装备数据...",
            FontSize = 12, Foreground = new SolidColorBrush(Color.FromRgb(0x88, 0xAA, 0xBB)),
            Margin = new Thickness(0, 20, 0, 0), HorizontalAlignment = HorizontalAlignment.Center
        });

        try
        {
            _dataService ??= new KkrbDataService(_configManager.Current.DbConnectionString);
            _armorItems = await _dataService.GetArmorConfigAsync();
            Dispatcher.Invoke(BuildArmorPanel);
            _armorPanelBuilt = true;
        }
        catch (Exception ex)
        {
            Dispatcher.Invoke(() =>
            {
                StackArmor.Children.Clear();
                StackArmor.Children.Add(Txt($"❌ 加载失败: {ex.Message.Split('\n')[0]}", "#EF5350", 11));
            });
        }
    }

    private void BuildArmorPanel()
    {
        StackArmor.Children.Clear();
        if (_armorItems == null || _armorItems.Count == 0) return;

        // ── 护甲等级快捷筛选 ──
        var levels = _armorItems
            .Select(a => a.Level)
            .Distinct()
            .OrderBy(l => l)
            .ToList();

        if (levels.Count > 0)
        {
            StackArmor.Children.Add(Txt("护甲等级", "#7EC8E3", 11));
            var filterRow = new WrapPanel { Margin = new Thickness(0, 4, 0, 8), Orientation = Orientation.Horizontal };
            filterRow.Children.Add(MakeLevelBtn("全部", null));
            foreach (var lv in levels)
                filterRow.Children.Add(MakeLevelBtn($"{lv} 级", lv));
            StackArmor.Children.Add(filterRow);
            _levelFilterRow = filterRow;
            UpdateLevelBtnStyles();
        }

        // ── 部位筛选 ──
        StackArmor.Children.Add(Txt("防护部位", "#7EC8E3", 11));
        var typeRow = new WrapPanel { Margin = new Thickness(0, 4, 0, 8), Orientation = Orientation.Horizontal };
        typeRow.Children.Add(MakeTypeBtn("全部", null));
        typeRow.Children.Add(MakeTypeBtn("⛑ 头盔", "头盔"));
        typeRow.Children.Add(MakeTypeBtn("🛡 护甲", "护甲"));
        StackArmor.Children.Add(typeRow);
        _typeFilterRow = typeRow;
        UpdateTypeBtnStyles();

        // ── 选择防具 ──
        StackArmor.Children.Add(Txt("选择防具", "#7EC8E3", 11));
        _armorCombo = new ComboBox
        {
            Margin = new Thickness(0, 4, 0, 10),
            Style = (Style)FindResource("DarkCombo"),
            MaxDropDownHeight = 220,
        };
        _armorComboList = new List<ArmorConfigItem>();
        PopulateArmorCombo();
        _armorCombo.SelectionChanged += (_, _) =>
        {
            var idx = _armorCombo.SelectedIndex;
            var armor = (idx >= 0 && _armorComboList != null && idx < _armorComboList.Count)
                ? _armorComboList[idx] : null;
            if (armor != null && _armorTxtMaxDur != null && _armorTxtCurDur != null)
            {
                _armorTxtMaxDur.Text = armor.InitDur.ToString("F0");
                _armorTxtCurDur.Text = "0";
            }
            RecalcArmor();
        };
        StackArmor.Children.Add(_armorCombo);

        // ── 输入框 ──
        var inputGrid = new Grid { Margin = new Thickness(0, 0, 0, 10) };
        inputGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        inputGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
        inputGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        inputGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        inputGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var lblMax = Txt("当前耐久上限", "#7EC8E3", 11);
        Grid.SetColumn(lblMax, 0); Grid.SetRow(lblMax, 0);
        var lblCur = Txt("当前耐久", "#7EC8E3", 11);
        Grid.SetColumn(lblCur, 2); Grid.SetRow(lblCur, 0);

        _armorTxtMaxDur = MakeNumericBox();
        Grid.SetColumn(_armorTxtMaxDur, 0); Grid.SetRow(_armorTxtMaxDur, 1);
        _armorTxtCurDur = MakeNumericBox();
        Grid.SetColumn(_armorTxtCurDur, 2); Grid.SetRow(_armorTxtCurDur, 1);

        _armorTxtMaxDur.TextChanged += (_, _) => RecalcArmor();
        _armorTxtCurDur.TextChanged += (_, _) => RecalcArmor();

        inputGrid.Children.Add(lblMax);
        inputGrid.Children.Add(lblCur);
        inputGrid.Children.Add(_armorTxtMaxDur);
        inputGrid.Children.Add(_armorTxtCurDur);
        StackArmor.Children.Add(inputGrid);

        // ── 结果面板 ──
        _armorResultPanel = new StackPanel { Visibility = Visibility.Collapsed };
        StackArmor.Children.Add(_armorResultPanel);
    }

    private Button MakeTypeBtn(string label, string? type)
    {
        var btn = new Button
        {
            Content = label,
            Tag = type,
            FontSize = 11,
            Padding = new Thickness(8, 3, 8, 3),
            Margin = new Thickness(0, 0, 4, 4),
            Cursor = Cursors.Hand,
            BorderThickness = new Thickness(1),
            Style = (Style)FindResource("LevelBtn"),
        };
        ApplyLevelBtnStyle(btn, type == _armorTypeFilter);
        btn.Click += (_, _) =>
        {
            _armorTypeFilter = btn.Tag as string;
            UpdateTypeBtnStyles();
            PopulateArmorCombo();
        };
        return btn;
    }

    private void UpdateTypeBtnStyles()
    {
        if (_typeFilterRow == null) return;
        foreach (Button btn in _typeFilterRow.Children.OfType<Button>())
            ApplyLevelBtnStyle(btn, (btn.Tag as string) == _armorTypeFilter);
    }

    private Button MakeLevelBtn(string label, int? level)
    {
        var btn = new Button
        {
            Content = label,
            Tag = level,
            FontSize = 11,
            Padding = new Thickness(8, 3, 8, 3),
            Margin = new Thickness(0, 0, 4, 4),
            Cursor = Cursors.Hand,
            BorderThickness = new Thickness(1),
            Style = (Style)FindResource("LevelBtn"),
        };
        ApplyLevelBtnStyle(btn, level == _armorLevelFilter);
        btn.Click += (_, _) =>
        {
            _armorLevelFilter = btn.Tag as int?;
            UpdateLevelBtnStyles();
            PopulateArmorCombo();
        };
        return btn;
    }

    private void ApplyLevelBtnStyle(Button btn, bool active)
    {
        if (active)
        {
            btn.Background = new SolidColorBrush(Color.FromArgb(0xCC, 0x1A, 0x3A, 0x5A));
            btn.BorderBrush = new SolidColorBrush(Color.FromRgb(0x7E, 0xC8, 0xE3));
            btn.Foreground = new SolidColorBrush(Color.FromRgb(0x7E, 0xC8, 0xE3));
        }
        else
        {
            btn.Background = new SolidColorBrush(Color.FromArgb(0x33, 0xFF, 0xFF, 0xFF));
            btn.BorderBrush = new SolidColorBrush(Color.FromArgb(0x44, 0xAA, 0xBB, 0xCC));
            btn.Foreground = new SolidColorBrush(Color.FromRgb(0x88, 0xAA, 0xBB));
        }
    }

    private void UpdateLevelBtnStyles()
    {
        if (_levelFilterRow == null) return;
        foreach (Button btn in _levelFilterRow.Children.OfType<Button>())
        {
            var lv = btn.Tag as int?;
            ApplyLevelBtnStyle(btn, lv == _armorLevelFilter);
        }
    }

    private void PopulateArmorCombo()
    {
        if (_armorCombo == null || _armorItems == null) return;
        _armorCombo.Items.Clear();
        _armorComboList = new List<ArmorConfigItem>();
        // 切换筛选时清空输入框和结果
        if (_armorTxtMaxDur != null) _armorTxtMaxDur.Text = "";
        if (_armorTxtCurDur != null) _armorTxtCurDur.Text = "";
        if (_armorResultPanel != null) _armorResultPanel.Visibility = Visibility.Collapsed;
        var filtered = _armorItems
            .Where(a => !_armorLevelFilter.HasValue || a.Level == _armorLevelFilter.Value)
            .Where(a => _armorTypeFilter == null
                || (_armorTypeFilter == "头盔" && a.IsHelmet)
                || (_armorTypeFilter == "护甲" && a.IsArmor));
        foreach (var a in filtered)
        {
            _armorCombo.Items.Add(a.Name);
            _armorComboList.Add(a);
        }
    }

    private static TextBox MakeNumericBox() => new()
    {
        Background = new SolidColorBrush(Color.FromRgb(0x1E, 0x2D, 0x3D)),
        Foreground = new SolidColorBrush(Color.FromRgb(0xE0, 0xE8, 0xF0)),
        BorderBrush = new SolidColorBrush(Color.FromRgb(0x44, 0x66, 0x88)),
        CaretBrush = new SolidColorBrush(Color.FromRgb(0x7E, 0xC8, 0xE3)),
        Padding = new Thickness(6, 4, 6, 4),
        FontSize = 13,
        Margin = new Thickness(0, 3, 0, 0),
    };

    private void RecalcArmor()
    {
        if (_armorCombo == null || _armorResultPanel == null) return;

        // 取选中装备
        var idx = _armorCombo.SelectedIndex;
        var armor = (idx >= 0 && _armorComboList != null && idx < _armorComboList.Count)
            ? _armorComboList[idx] : null;
        if (armor == null) { _armorResultPanel.Visibility = Visibility.Collapsed; return; }

        // 解析输入 — cur = 当前耐久上限, rem = 当前耐久
        if (!double.TryParse(_armorTxtMaxDur?.Text, out var cur) || cur <= 0)
        { _armorResultPanel.Visibility = Visibility.Collapsed; return; }
        double.TryParse(_armorTxtCurDur?.Text, out var rem);
        if (rem < 0) rem = 0;

        // ── calc.js 核心公式 ──
        // ratio    = (cur - rem) / cur
        // logTerm  = log10(cur / init)
        // after_in = round((cur - cur*ratio*(loss - logTerm)) * 10) / 10
        // after_out= floor(...)  min 1
        double ratio    = (cur - rem) / cur;
        double logTerm  = Math.Log10(cur / armor.InitDur);
        double rawAfter = cur - cur * ratio * (armor.LossFactor - logTerm);

        double afterIn  = Math.Round(rawAfter * 10.0) / 10.0;
        double afterOut = Math.Max(1, Math.Floor(rawAfter));
        double delta    = afterIn - rem;

        bool canSellIn  = afterIn  >= armor.SellThr;
        bool canSellOut = afterOut >= armor.SellThr;

        (string Name, double Eff)[] packs =
        {
            ("自制", armor.EffM1),
            ("标准", armor.EffM2),
            ("精密", armor.EffM3),
            ("高级", armor.EffM4),
        };

        // ── 渲染 ──
        _armorResultPanel.Children.Clear();
        _armorResultPanel.Visibility = Visibility.Visible;

        _armorResultPanel.Children.Add(Txt("装备可以维修", "#66BB6A", 13, bold: true));

        // 局内维修卡片
        var inBorder = new Border
        {
            Background      = new SolidColorBrush(Color.FromArgb(0xCC, 0x0D, 0x1F, 0x33)),
            BorderBrush     = new SolidColorBrush(Color.FromArgb(0xAA, 0x7E, 0xC8, 0xE3)),
            BorderThickness = new Thickness(1),
            CornerRadius    = new CornerRadius(8),
            Padding         = new Thickness(12, 10, 12, 10),
            Margin          = new Thickness(0, 8, 0, 0),
        };
        var inSp = new StackPanel();
        inSp.Children.Add(Txt("📦 局内维修", "#7EC8E3", 12, bold: true));
        inSp.Children.Add(MakeResultRow("维修后耐久上限", $"{afterIn:F1}", "#E0E8F0"));
        inSp.Children.Add(MakeResultRow("出售状态", canSellIn ? "可以出售" : "不可出售",
            canSellIn ? "#66BB6A" : "#EF5350"));
        if (delta > 0)
        {
            foreach (var (name, eff) in packs)
            {
                if (eff <= 0) continue;
                int pts = (int)Math.Round(delta / eff);
                inSp.Children.Add(MakeResultRow(name, $"{pts} 点", "#FFAB40"));
            }
        }
        else
        {
            inSp.Children.Add(Txt("无需维修", "#88AABB", 11));
        }
        inBorder.Child = inSp;
        _armorResultPanel.Children.Add(inBorder);

        // 局外维修卡片
        var outBorder = new Border
        {
            Background      = new SolidColorBrush(Color.FromArgb(0xCC, 0x1A, 0x1A, 0x2A)),
            BorderBrush     = new SolidColorBrush(Color.FromArgb(0x88, 0x88, 0x88, 0xCC)),
            BorderThickness = new Thickness(1),
            CornerRadius    = new CornerRadius(8),
            Padding         = new Thickness(12, 10, 12, 10),
            Margin          = new Thickness(0, 6, 0, 0),
        };
        var outSp = new StackPanel();
        outSp.Children.Add(Txt("🏪 局外维修", "#B0C8D8", 12, bold: true));
        outSp.Children.Add(MakeResultRow("维修后耐久上限", $"{afterOut}", "#E0E8F0"));
        outSp.Children.Add(MakeResultRow("出售状态", canSellOut ? "可以出售" : "不可出售",
            canSellOut ? "#66BB6A" : "#EF5350"));
        outBorder.Child = outSp;
        _armorResultPanel.Children.Add(outBorder);
    }

    private static StackPanel MakeResultRow(string label, string value, string valueHex)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 3, 0, 0) };
        row.Children.Add(new TextBlock
        {
            Text = label + "：",
            FontSize = 12,
            Foreground = new SolidColorBrush(Color.FromRgb(0x88, 0xAA, 0xBB)),
            VerticalAlignment = VerticalAlignment.Center,
        });
        row.Children.Add(new TextBlock
        {
            Text = value,
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            Foreground = (SolidColorBrush)new BrushConverter().ConvertFrom(valueHex)!,
            VerticalAlignment = VerticalAlignment.Center,
        });
        return row;
    }

    // ──────────────────────────────────────────
    // 渲染
    // ──────────────────────────────────────────
    private void RenderAll(KkrbSnapshot snap)
    {
        // 日期
        TxtDate.Text = "  " + snap.SourceDate;

        RenderPasswords(snap.PasswordBlocks);
        RenderCraft(snap.HourlyProducts);
        RenderItems(snap.ActivityItems, snap.MaterialItems);
        RenderAmmo(snap.AmmoTop10, snap.AmmoProfitItems);
    }

    // ── Tab1: 密码（2 列卡片网格）──
    private void RenderPasswords(List<PasswordBlock> list)
    {
        GridPassword.Children.Clear();
        foreach (var p in list)
        {
            // 3 列布局 + 紧凑内边距，纯展示卡片（无复制交互）
            var card = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(0x28, 0x7E, 0xC8, 0xE3)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0x33, 0x7E, 0xC8, 0xE3)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Margin = new Thickness(3),
                Padding = new Thickness(6, 8, 6, 8),
                Height = 64,
                VerticalAlignment = VerticalAlignment.Top,
            };

            var sp = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };

            var mapName = new TextBlock
            {
                Text = p.MapName,
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(0x90, 0xB8, 0xCC)),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 4),
                TextTrimming = TextTrimming.CharacterEllipsis,
            };

            var codeBlock = new TextBlock
            {
                Text = p.Code,
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                FontFamily = new FontFamily("Consolas, Courier New"),
                Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0xD7, 0x54)),
                HorizontalAlignment = HorizontalAlignment.Center,
            };

            sp.Children.Add(mapName);
            sp.Children.Add(codeBlock);
            card.Child = sp;

            GridPassword.Children.Add(card);
        }
    }

    // ── Tab2: 制作 ──
    private void RenderCraft(List<HourlyProduct> list)
    {
        StackCraft.Children.Clear();
        foreach (var p in list)
        {
            var card = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(0x18, 0xFF, 0xFF, 0xFF)),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(10, 7, 10, 7),
                Margin = new Thickness(0, 0, 0, 6),
            };
            var sp = new StackPanel();

            // 产品名 + 工作台
            var header = new Grid();
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var nameBlock = new TextBlock
            {
                Text = p.ProductName,
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(0xE8, 0xEA, 0xF0)),
                TextWrapping = TextWrapping.Wrap,
            };
            Grid.SetColumn(nameBlock, 0);
            var wsBlock = new TextBlock
            {
                Text = p.Workstation,
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(0x60, 0x7D, 0x8B)),
                VerticalAlignment = VerticalAlignment.Bottom,
            };
            Grid.SetColumn(wsBlock, 1);
            header.Children.Add(nameBlock);
            header.Children.Add(wsBlock);
            sp.Children.Add(header);

            // 利润 / 理想价格 / 卖出时间
            var infoRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 0) };
            infoRow.Children.Add(MakeKV("利润", FormatNum(p.CurrentProfit), "#66BB6A"));
            infoRow.Children.Add(new TextBlock { Text = "  ", FontSize = 11 });
            infoRow.Children.Add(MakeKV("理想价", FormatNum(p.IdealPrice), "#7EC8E3"));
            infoRow.Children.Add(new TextBlock { Text = "  ", FontSize = 11 });
            infoRow.Children.Add(MakeKV("卖出", p.SellTime, "#FFAB40"));
            sp.Children.Add(infoRow);

            card.Child = sp;
            StackCraft.Children.Add(card);
        }
    }

    // ── Tab3: 活动&材料 ──
    private void RenderItems(List<ActivityItem> activity, List<MaterialItem> materials)
    {
        StackItems.Children.Clear();

        // 活动物品
        StackItems.Children.Add(MakeSectionTitle("📌 活动物品"));
        foreach (var a in activity)
        {
            var row = new Grid { Margin = new Thickness(0, 3, 0, 3) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var name = Txt(a.ItemName, "#B0C8D8", 12);
            Grid.SetColumn(name, 0);

            var cur = Txt(FormatNum(a.CurrentPrice), "#7EC8E3", 12, bold: true);
            Grid.SetColumn(cur, 1);

            var ideal = Txt("→" + FormatNum(a.IdealPrice), "#44667788", 11);
            Grid.SetColumn(ideal, 2);
            ideal.Margin = new Thickness(6, 0, 0, 0);

            row.Children.Add(name);
            row.Children.Add(cur);
            row.Children.Add(ideal);
            StackItems.Children.Add(row);
            StackItems.Children.Add(MakeSep());
        }

        // 分隔
        StackItems.Children.Add(new Rectangle
        {
            Fill = new SolidColorBrush(Color.FromArgb(0x33, 0xFF, 0xFF, 0xFF)),
            Height = 1, Margin = new Thickness(0, 8, 0, 8)
        });

        // 材料
        StackItems.Children.Add(MakeSectionTitle("⚙ 高波动材料"));
        foreach (var m in materials)
        {
            var card = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(0x14, 0xFF, 0xFF, 0xFF)),
                CornerRadius = new CornerRadius(5),
                Padding = new Thickness(8, 5, 8, 5),
                Margin = new Thickness(0, 0, 0, 5),
            };
            var sp = new StackPanel();

            var nameRow = new Grid();
            nameRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            nameRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var nm = Txt(m.ItemName, "#E0E8F0", 12, bold: true);
            Grid.SetColumn(nm, 0);
            var cp = Txt(FormatNum(m.CurrentPrice), "#7EC8E3", 12, bold: true);
            Grid.SetColumn(cp, 1);
            nameRow.Children.Add(nm);
            nameRow.Children.Add(cp);
            sp.Children.Add(nameRow);

            var detailRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 3, 0, 0) };
            detailRow.Children.Add(MakeKV("最低", FormatNum(m.LowestPrice), "#EF9A9A"));
            detailRow.Children.Add(new TextBlock { Text = "  " });
            detailRow.Children.Add(MakeKV("最高", FormatNum(m.HighestPrice), "#66BB6A"));
            detailRow.Children.Add(new TextBlock { Text = "  " });
            detailRow.Children.Add(MakeKV("买", m.BuyTime, "#FFAB40"));
            detailRow.Children.Add(new TextBlock { Text = "  " });
            detailRow.Children.Add(MakeKV("卖", m.SellTime, "#FFAB40"));
            sp.Children.Add(detailRow);

            card.Child = sp;
            StackItems.Children.Add(card);
        }
    }

    // ── Tab4: 子弹 ──
    private void RenderAmmo(List<string> top10, List<AmmoProfitItem> profit)
    {
        StackAmmo.Children.Clear();

        // Top10
        StackAmmo.Children.Add(MakeSectionTitle("🏆 热门子弹 Top10"));
        for (int i = 0; i < top10.Count; i++)
        {
            var row = new Grid { Margin = new Thickness(0, 2, 0, 2) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(26) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var rank = Txt($"{i + 1}", i < 3 ? "#FFD754" : "#44667788", 11, bold: i < 3);
            rank.TextAlignment = TextAlignment.Center;
            Grid.SetColumn(rank, 0);

            var name = Txt(top10[i], "#B0C8D8", 11);
            Grid.SetColumn(name, 1);

            row.Children.Add(rank);
            row.Children.Add(name);
            StackAmmo.Children.Add(row);
        }

        // 分隔
        StackAmmo.Children.Add(new Rectangle
        {
            Fill = new SolidColorBrush(Color.FromArgb(0x33, 0xFF, 0xFF, 0xFF)),
            Height = 1, Margin = new Thickness(0, 8, 0, 8)
        });

        // 利润
        StackAmmo.Children.Add(MakeSectionTitle("💰 兑换利润"));
        foreach (var a in profit)
        {
            var row = new Grid { Margin = new Thickness(0, 3, 0, 3) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var name = Txt(a.AmmoName, "#B0C8D8", 11);
            Grid.SetColumn(name, 0);
            var prf = Txt("+" + FormatNum(a.Profit), "#66BB6A", 12, bold: true);
            Grid.SetColumn(prf, 1);

            row.Children.Add(name);
            row.Children.Add(prf);
            StackAmmo.Children.Add(row);
            StackAmmo.Children.Add(MakeSep());
        }
    }

    // ──────────────────────────────────────────
    // 工具方法
    // ──────────────────────────────────────────
    private static TextBlock Txt(string text, string hex, double size, bool bold = false) =>
        new()
        {
            Text = text,
            FontSize = size,
            FontWeight = bold ? FontWeights.Bold : FontWeights.Normal,
            Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)),
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center,
        };

    private static UIElement MakeKV(string label, string value, string valueHex)
    {
        var sp = new StackPanel { Orientation = Orientation.Horizontal };
        sp.Children.Add(new TextBlock
        {
            Text = label + " ",
            FontSize = 11,
            Foreground = new SolidColorBrush(Color.FromRgb(0x60, 0x7D, 0x8B)),
        });
        sp.Children.Add(new TextBlock
        {
            Text = value,
            FontSize = 11,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(valueHex)),
        });
        return sp;
    }

    private static Rectangle MakeSep() => new()
    {
        Fill = new SolidColorBrush(Color.FromArgb(0x18, 0xFF, 0xFF, 0xFF)),
        Height = 1,
        Margin = new Thickness(0, 2, 0, 2),
    };

    private static TextBlock MakeSectionTitle(string text) => new()
    {
        Text = text,
        FontSize = 12,
        FontWeight = FontWeights.SemiBold,
        Foreground = new SolidColorBrush(Color.FromRgb(0x7E, 0xC8, 0xE3)),
        Margin = new Thickness(0, 0, 0, 6),
    };

    /// <summary>
    /// 复制文本到剪贴板。无重试、无线程阻塞，安全可靠。
    /// 关键：CLIPBRD_E_CANT_OPEN(0x800401D0) 这个 COM 异常意味着 OS 层操作其实已完成，
    /// 仅 OLE 客户端拿不到锁通知，按惯例视为成功（Windows 文档与所有主流 .NET 实践都这样处理）。
    /// </summary>
    private static bool TryCopyToClipboard(string text)
    {
        try
        {
            System.Windows.Clipboard.SetText(text);
            return true;
        }
        catch (System.Runtime.InteropServices.COMException ex)
            when ((uint)ex.HResult == 0x800401D0)
        {
            // 另一进程持有剪贴板锁，但 OS 层写入往往已成功 → 视为成功
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string FormatNum(long n)
    {
        if (n >= 10000) return (n / 10000.0).ToString("F1") + "w";
        return n.ToString("N0");
    }

    // ──────────────────────────────────────────
    // 按钮事件
    // ──────────────────────────────────────────
    private void OnSettingsClicked(object sender, RoutedEventArgs e) => OnOpenSettings?.Invoke();
    private void OnHideClicked(object sender, RoutedEventArgs e) => Hide();

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
            else { Show(); Activate(); }
        });
        _hotkey.Register();
        TxtHint.Text = $"拖动移动  |  {key} 显示/隐藏";
    }

    public void ReRegisterHotkey()
    {
        _dataService = null; // 连接串可能也变了
        RegisterHotkey();
    }

    // ──────────────────────────────────────────
    // 配置应用
    // ──────────────────────────────────────────
    public void ApplyConfig()
    {
        var cfg = _configManager.Current;
        Left = cfg.WindowLeft;
        Top = cfg.WindowTop;
        Opacity = Math.Clamp(cfg.WindowOpacity, 0.1, 1.0);
        Topmost = cfg.AlwaysOnTop;
        if (TryParseColor(cfg.BackgroundHex, out var c))
            PanelBackground.Color = c;
        PanelBorder.BorderBrush = cfg.ShowBorder
            ? new SolidColorBrush(Color.FromArgb(0x33, 0xFF, 0xFF, 0xFF))
            : Brushes.Transparent;
        TxtHint.Text = $"拖动移动  |  {cfg.ToggleHotkey} 显示/隐藏";
    }

    private static bool TryParseColor(string? hex, out Color c)
    {
        c = Colors.Transparent;
        if (string.IsNullOrWhiteSpace(hex)) return false;
        try { c = (Color)ColorConverter.ConvertFromString(hex); return true; }
        catch { return false; }
    }
}

// ──────────────────────────────────────────────────────────────
// 全局热键（WH_KEYBOARD_LL 专属线程 — 游戏全屏/无边框均有效，消费按键）
// ──────────────────────────────────────────────────────────────
internal sealed class HotkeyManager : IDisposable
{
    // ── P/Invoke ────────────────────────────────────────────────
    private delegate IntPtr LLKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LLKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);
    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandle(string lpModuleName);
    [DllImport("user32.dll")]
    private static extern int GetMessage(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);
    [DllImport("user32.dll")]
    private static extern bool TranslateMessage(ref MSG lpMsg);
    [DllImport("user32.dll")]
    private static extern IntPtr DispatchMessage(ref MSG lpMsg);
    [DllImport("user32.dll")]
    private static extern bool PostThreadMessage(uint idThread, uint Msg, IntPtr wParam, IntPtr lParam);
    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();
    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    [StructLayout(LayoutKind.Sequential)]
    private struct MSG
    {
        public IntPtr hwnd;
        public uint message;
        public IntPtr wParam;
        public IntPtr lParam;
        public uint time;
        public int ptX, ptY;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KBDLLHOOKSTRUCT
    {
        public uint vkCode, scanCode, flags, time;
        public IntPtr dwExtraInfo;
    }

    private const int    WH_KEYBOARD_LL = 13;
    private const int    WM_KEYDOWN     = 0x0100;
    private const int    WM_SYSKEYDOWN  = 0x0104;
    private const int    WM_KEYUP       = 0x0101;
    private const int    WM_SYSKEYUP    = 0x0105;
    private const uint   WM_QUIT        = 0x0012;
    private const int    VK_CONTROL     = 0x11;
    private const int    VK_MENU        = 0x12;
    private const int    VK_SHIFT       = 0x10;

    // ── 状态 ────────────────────────────────────────────────────
    private readonly uint _vk;
    private readonly Action _callback;
    private readonly System.Windows.Threading.Dispatcher _dispatcher;
    private readonly HashSet<uint> _activeVks = new();

    private Thread? _hookThread;
    private volatile uint _hookThreadId;
    private IntPtr _hookId = IntPtr.Zero;
    private LLKeyboardProc? _proc;
    private bool _disposed;

    public HotkeyManager(Window owner, string keyStr, Action callback)
    {
        _callback   = callback;
        _dispatcher = owner.Dispatcher;
        if (Enum.TryParse<Key>(keyStr, true, out var k))
            _vk = (uint)KeyInterop.VirtualKeyFromKey(k);
    }

    public void Register()
    {
        if (_vk == 0 || _hookThread != null) return;
        _proc = HookProc;
        _hookThread = new Thread(HookThreadLoop)
        {
            IsBackground = true,
            Name = "MouseToolHotkeyThread"
        };
        _hookThread.SetApartmentState(ApartmentState.STA);
        _hookThread.Start();
    }

    public void Unregister()
    {
        var tid = _hookThreadId;
        if (tid != 0)
            PostThreadMessage(tid, WM_QUIT, IntPtr.Zero, IntPtr.Zero);
        _hookThread = null;
        _hookThreadId = 0;
    }

    private void HookThreadLoop()
    {
        _hookThreadId = GetCurrentThreadId();

        using var proc = System.Diagnostics.Process.GetCurrentProcess();
        using var mod  = proc.MainModule!;
        _hookId = SetWindowsHookEx(WH_KEYBOARD_LL, _proc!, GetModuleHandle(mod.ModuleName!), 0);

        while (GetMessage(out var msg, IntPtr.Zero, 0, 0) > 0)
        {
            TranslateMessage(ref msg);
            DispatchMessage(ref msg);
        }

        if (_hookId != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_hookId);
            _hookId = IntPtr.Zero;
        }
    }

    private IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            var kb = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);

            if (wParam == WM_KEYUP || wParam == WM_SYSKEYUP)
            {
                _activeVks.Remove(kb.vkCode);
            }
            else if (wParam == WM_KEYDOWN || wParam == WM_SYSKEYDOWN)
            {
                if (!_activeVks.Contains(kb.vkCode))
                {
                    _activeVks.Add(kb.vkCode);

                    if (kb.vkCode == _vk)
                    {
                        _dispatcher.BeginInvoke(_callback);
                        return (IntPtr)1; // 消费此键，游戏不再收到
                    }
                }
            }
        }
        return CallNextHookEx(_hookId, nCode, wParam, lParam);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Unregister();
    }
}
