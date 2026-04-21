using System.Windows;
using System.Windows.Media;
using NightVisionTool.Core;
using NightVisionTool.Models;

namespace NightVisionTool.Controls;

/// <summary>
/// 夜视曲线预览 —— 绘制当前 <see cref="NightVisionConfig"/> 对应的 LUT。
/// 叠加显示:对角虚线(原始/identity)+ 暗部提亮参考线。
/// </summary>
public class NightVisionCurveView : FrameworkElement
{
    public static readonly DependencyProperty ConfigProperty =
        DependencyProperty.Register(nameof(Config), typeof(NightVisionConfig), typeof(NightVisionCurveView),
            new FrameworkPropertyMetadata(NightVisionConfig.Default,
                FrameworkPropertyMetadataOptions.AffectsRender));

    public NightVisionConfig Config
    {
        get => (NightVisionConfig)GetValue(ConfigProperty);
        set => SetValue(ConfigProperty, value);
    }

    protected override void OnRender(DrawingContext dc)
    {
        var w = ActualWidth;
        var h = ActualHeight;
        if (w < 8 || h < 8) return;

        dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x1A)), null,
            new Rect(0, 0, w, h));

        var gridPen = new Pen(new SolidColorBrush(Color.FromArgb(60, 255, 255, 255)), 0.5);
        gridPen.Freeze();
        for (int k = 1; k < 4; k++)
        {
            double x = k * w / 4, y = k * h / 4;
            dc.DrawLine(gridPen, new Point(x, 0), new Point(x, h));
            dc.DrawLine(gridPen, new Point(0, y), new Point(w, y));
        }

        var refPen = new Pen(new SolidColorBrush(Color.FromArgb(80, 255, 255, 255)), 0.5)
        { DashStyle = DashStyles.Dash };
        refPen.Freeze();
        dc.DrawLine(refPen, new Point(0, h), new Point(w, 0));

        var cfg = Config ?? NightVisionConfig.Default;
        DrawCurve(dc, cfg, Color.FromRgb(0x4E, 0xC9, 0xB0), w, h);
    }

    private static void DrawCurve(DrawingContext dc, NightVisionConfig cfg, Color color, double w, double h)
    {
        var lut = NightVisionCurve.BuildNormalized(cfg);
        var pen = new Pen(new SolidColorBrush(color), 1.8);
        pen.Freeze();

        var geo = new StreamGeometry();
        using (var ctx = geo.Open())
        {
            ctx.BeginFigure(new Point(0, h - lut[0] * h), false, false);
            for (int i = 1; i < 256; i++)
            {
                double x = i * w / 255.0;
                double y = h - lut[i] * h;
                ctx.LineTo(new Point(x, y), true, false);
            }
        }
        geo.Freeze();
        dc.DrawGeometry(null, pen, geo);
    }
}
