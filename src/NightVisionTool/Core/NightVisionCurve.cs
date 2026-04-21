using FPSToolbox.Shared.Native;
using NightVisionTool.Models;

namespace NightVisionTool.Core;

/// <summary>
/// 智能夜视曲线 —— 暗部强提亮 + 亮部 rolloff,避免室内光源过曝。
///
/// 曲线公式(x ∈ [0,1]):
///   γ      = 1 + Strength * 1.5                       // Strength=0 → 线性;1 → γ≈2.5
///   yLift  = x ^ (1/γ)                                // 纯 gamma 提亮
///   w      = HighlightProtect * smoothstep(tLo, 1, x) // 亮部向原始 x 混合的权重
///   y      = (1 - w) * yLift + w * x                  // 最终输出
///
/// 关键属性:
///   Strength=0 且 HighlightProtect=任意值 → y=x,完全不改变屏幕(identity)
///   Strength=1 且 HighlightProtect=0     → y = x^(1/2.5),效果接近现有 GammaTool 强提亮
///   Strength=1 且 HighlightProtect=1     → 暗部仍显著提亮,但 x→1 时 y→1,亮部不再被推爆
///
/// 亮部过渡区 tLo:固定为 0.4。在 x<tLo 时 w=0(纯 gamma 提亮),x∈[tLo,1] 时 smoothstep 渐进到
/// HighlightProtect,让室内高光像素能向原始值回靠。
///
/// 实现为 256 点 LUT,耗时可忽略。
/// </summary>
public static class NightVisionCurve
{
    private const double HighlightStartX = 0.40;

    public static NativeMethods.GammaRamp Build(NightVisionConfig cfg)
    {
        var ramp = new NativeMethods.GammaRamp
        {
            Red = new ushort[256],
            Green = new ushort[256],
            Blue = new ushort[256],
        };
        for (int i = 0; i < 256; i++)
        {
            double y = SampleNormalized(i / 255.0, cfg);
            int v = (int)Math.Round(y * 65535.0);
            if (v < 0) v = 0;
            if (v > 65535) v = 65535;
            ramp.Red[i] = ramp.Green[i] = ramp.Blue[i] = (ushort)v;
        }
        return ramp;
    }

    /// <summary>为预览控件生成 0..1 归一化 LUT。</summary>
    public static double[] BuildNormalized(NightVisionConfig cfg)
    {
        var arr = new double[256];
        for (int i = 0; i < 256; i++) arr[i] = SampleNormalized(i / 255.0, cfg);
        return arr;
    }

    public static double SampleNormalized(double x, NightVisionConfig cfg)
    {
        if (x < 0) x = 0;
        if (x > 1) x = 1;

        double strength = Clamp01(cfg.Strength);
        double protect = Clamp01(cfg.HighlightProtect);

        double gamma = 1.0 + strength * 1.5;
        double yLift = Math.Pow(x, 1.0 / gamma);

        double w = protect * SmoothStep(HighlightStartX, 1.0, x);
        double y = (1.0 - w) * yLift + w * x;

        if (y < 0) y = 0;
        if (y > 1) y = 1;
        return y;
    }

    private static double SmoothStep(double edge0, double edge1, double x)
    {
        double t = (x - edge0) / (edge1 - edge0);
        if (t < 0) t = 0;
        if (t > 1) t = 1;
        return t * t * (3.0 - 2.0 * t);
    }

    private static double Clamp01(double v) => v < 0 ? 0 : (v > 1 ? 1 : v);
}
