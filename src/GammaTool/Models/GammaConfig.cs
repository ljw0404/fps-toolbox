namespace GammaTool.Models;

/// <summary>
/// 单通道的三参数。均使用 Gamma Panel 惯例:
///   Gamma      ∈ [0.10, 4.00]  (1.00 = 线性/中性)
///   Brightness ∈ [-1.00, 1.00] (0.00 = 中性)
///   Contrast   ∈ [0.10, 3.00]  (1.00 = 中性;1.40 表示对比度 +40%)
/// </summary>
public class ChannelParams
{
    public const double GammaMin = 0.10, GammaMax = 4.00, GammaNeutral = 1.00;
    public const double BrightMin = -1.00, BrightMax = 1.00, BrightNeutral = 0.00;
    public const double ContrastMin = 0.10, ContrastMax = 3.00, ContrastNeutral = 1.00;

    public double Gamma { get; set; } = GammaNeutral;
    public double Brightness { get; set; } = BrightNeutral;
    public double Contrast { get; set; } = ContrastNeutral;

    public ChannelParams Clone() => new()
    {
        Gamma = Gamma,
        Brightness = Brightness,
        Contrast = Contrast
    };

    public bool IsDefault =>
        Math.Abs(Gamma - GammaNeutral) < 1e-4 &&
        Math.Abs(Brightness - BrightNeutral) < 1e-4 &&
        Math.Abs(Contrast - ContrastNeutral) < 1e-4;

    /// <summary>
    /// 旧版本(v1.0.0)Contrast 中性值为 0.0。如果加载的方案里是老值,自动迁移到 1.0。
    /// </summary>
    public void MigrateFromLegacyContrast()
    {
        if (Contrast <= 1e-4) Contrast = ContrastNeutral;
    }
}

/// <summary>
/// 一组 Gamma 配置：可以是 RGB 联动（只用 Master），也可以拆成三通道。
/// </summary>
public class GammaConfig
{
    public bool LinkedRgb { get; set; } = true;
    public ChannelParams Master { get; set; } = new();
    public ChannelParams Red { get; set; } = new();
    public ChannelParams Green { get; set; } = new();
    public ChannelParams Blue { get; set; } = new();

    public GammaConfig Clone() => new()
    {
        LinkedRgb = LinkedRgb,
        Master = Master.Clone(),
        Red = Red.Clone(),
        Green = Green.Clone(),
        Blue = Blue.Clone(),
    };

    public static GammaConfig Default => new();
}

/// <summary>配色方案（预设）。</summary>
public class GammaScheme
{
    public string Name { get; set; } = "默认";
    public GammaConfig Config { get; set; } = new();
    public string? Hotkey { get; set; }
    public Dictionary<string, GammaConfig> PerMonitor { get; set; } = new();
    public bool ApplyToAllMonitors { get; set; } = true;
}
