namespace NightVisionTool.Models;

/// <summary>
/// 夜视曲线参数。
///   Strength         ∈ [0, 1] — 夜视强度,控制暗部提亮幅度。0 = 关闭(identity);1 = 强提亮(≈gamma 2.5)。
///   HighlightProtect ∈ [0, 1] — 亮部保护,越大亮部越接近原始,避免室内光源过曝。
///                               0 = 不保护(行为接近纯 gamma);1 = 最强保护,亮部几乎还原。
/// </summary>
public class NightVisionConfig
{
    public const double StrengthMin = 0.0, StrengthMax = 1.0, StrengthNeutral = 0.0;
    public const double ProtectMin = 0.0, ProtectMax = 1.0, ProtectNeutral = 0.0;

    public double Strength { get; set; } = 0.60;
    public double HighlightProtect { get; set; } = 0.50;

    public NightVisionConfig Clone() => new()
    {
        Strength = Strength,
        HighlightProtect = HighlightProtect,
    };

    public bool IsIdentity => Strength <= 1e-4;

    public static NightVisionConfig Default => new() { Strength = 0, HighlightProtect = 0 };
}

/// <summary>夜视预设方案。</summary>
public class NightVisionScheme
{
    public string Name { get; set; } = "默认";
    public NightVisionConfig Config { get; set; } = new();
    public string? Hotkey { get; set; }
    public Dictionary<string, NightVisionConfig> PerMonitor { get; set; } = new();
    public bool ApplyToAllMonitors { get; set; } = true;
}
