namespace MouseTool.Models;

/// <summary>
/// 鼠鼠工具的持久化配置。
/// </summary>
public class MouseToolConfig
{
    /// <summary>悬浮窗左边距（屏幕坐标）。</summary>
    public double WindowLeft { get; set; } = 200;

    /// <summary>悬浮窗顶部坐标（屏幕坐标）。</summary>
    public double WindowTop { get; set; } = 200;

    /// <summary>悬浮窗整体透明度，0.1 ~ 1.0，默认 0.85。</summary>
    public double WindowOpacity { get; set; } = 0.85;

    /// <summary>是否置顶，默认 true。</summary>
    public bool AlwaysOnTop { get; set; } = true;

    /// <summary>面板背景色（ARGB hex，含透明通道）。</summary>
    public string BackgroundHex { get; set; } = "#CC101827";

    /// <summary>文字颜色（RGB hex）。</summary>
    public string ForegroundHex { get; set; } = "#E8EAF0";

    /// <summary>字体大小，默认 14。</summary>
    public double FontSize { get; set; } = 14;

    /// <summary>是否显示面板描边。</summary>
    public bool ShowBorder { get; set; } = true;

    /// <summary>显示 / 隐藏悬浮窗的全局热键，默认 F10（不与 Crosshair F8 / NightVision F9 冲突）。</summary>
    public string ToggleHotkey { get; set; } = "F10";
}
