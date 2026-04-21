using FPSToolbox.Shared.Native;
using NightVisionTool.Models;

namespace NightVisionTool.Core;

/// <summary>
/// 把 <see cref="NightVisionConfig"/> 落到屏幕(SetDeviceGammaRamp),并在退出时恢复系统默认。
/// 每个显示器缓存一个 HDC 与启动前的原始 ramp。
/// 结构与 GammaTool.Core.GammaApplier 一致,便于复用心智模型。
/// </summary>
public sealed class NightVisionApplier : IDisposable
{
    private readonly Dictionary<string, IntPtr> _dcs = new();
    private readonly Dictionary<string, NativeMethods.GammaRamp> _originalRamps = new();
    private bool _initialized;

    public IReadOnlyList<MonitorInfo> Monitors { get; private set; } = Array.Empty<MonitorInfo>();

    public void Initialize()
    {
        if (_initialized) return;
        _initialized = true;

        Monitors = MonitorEnumerator.EnumerateAll();
        foreach (var mon in Monitors)
        {
            var hdc = NativeMethods.CreateDC("DISPLAY", mon.DeviceName, null, IntPtr.Zero);
            if (hdc != IntPtr.Zero)
            {
                _dcs[mon.DeviceName] = hdc;
                var ramp = new NativeMethods.GammaRamp
                {
                    Red = new ushort[256],
                    Green = new ushort[256],
                    Blue = new ushort[256]
                };
                if (NativeMethods.GetDeviceGammaRamp(hdc, ref ramp))
                    _originalRamps[mon.DeviceName] = ramp;
                else
                    _originalRamps[mon.DeviceName] = NativeMethods.GammaRamp.CreateIdentity();
            }
        }
    }

    public bool Apply(string deviceName, NightVisionConfig cfg)
    {
        if (!_dcs.TryGetValue(deviceName, out var hdc)) return false;
        var ramp = NightVisionCurve.Build(cfg);
        return NativeMethods.SetDeviceGammaRamp(hdc, ref ramp);
    }

    public void ApplyAll(NightVisionConfig cfg)
    {
        foreach (var mon in Monitors) Apply(mon.DeviceName, cfg);
    }

    public void ApplyScheme(NightVisionScheme scheme)
    {
        if (scheme.ApplyToAllMonitors)
        {
            ApplyAll(scheme.Config);
        }
        else
        {
            foreach (var mon in Monitors)
            {
                var cfg = scheme.PerMonitor.TryGetValue(mon.DeviceName, out var c)
                    ? c : scheme.Config;
                Apply(mon.DeviceName, cfg);
            }
        }
    }

    public void RestoreSystemDefaults()
    {
        foreach (var (name, hdc) in _dcs)
        {
            if (_originalRamps.TryGetValue(name, out var ramp))
                NativeMethods.SetDeviceGammaRamp(hdc, ref ramp);
            else
            {
                var identity = NativeMethods.GammaRamp.CreateIdentity();
                NativeMethods.SetDeviceGammaRamp(hdc, ref identity);
            }
        }
    }

    public void Dispose()
    {
        try { RestoreSystemDefaults(); } catch { }
        foreach (var hdc in _dcs.Values) NativeMethods.DeleteDC(hdc);
        _dcs.Clear();
        _originalRamps.Clear();
    }
}
