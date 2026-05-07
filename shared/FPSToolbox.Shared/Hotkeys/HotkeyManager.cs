using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using FPSToolbox.Shared.Native;

namespace FPSToolbox.Shared.Hotkeys;

/// <summary>
/// 全局热键管理器（WH_KEYBOARD_LL + 专属消息循环线程）。
///
/// 与 RegisterHotKey 不同，此方案不受游戏抢注同一热键的影响。
/// 与 UI 线程 LL Hook 不同，专属线程的消息循环轻量纯净，
/// 钩子回调始终在超时（300ms）内返回，不会被系统自动卸载。
/// 游戏全屏、无边框窗口模式均有效，且消费按键（不传入游戏）。
/// </summary>
public sealed class HotkeyManager : IDisposable
{
    // ── P/Invoke ────────────────────────────────────────────────────
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

    private const int WH_KEYBOARD_LL  = 13;
    private const int WM_KEYDOWN      = 0x0100;
    private const int WM_SYSKEYDOWN   = 0x0104;
    private const int WM_KEYUP        = 0x0101;
    private const int WM_SYSKEYUP     = 0x0105;
    private const uint WM_QUIT        = 0x0012;

    // ── 状态 ────────────────────────────────────────────────────────
    private record HotkeyEntry(uint Modifiers, uint Vk, int Id, Action Callback);

    private readonly List<HotkeyEntry> _hotkeys = new();
    private readonly HashSet<uint> _activeVks = new();
    private int _idCounter = 9000;
    private bool _disposed;
    private Dispatcher? _dispatcher;

    private Thread? _hookThread;
    private volatile uint _hookThreadId;
    private IntPtr _hookId = IntPtr.Zero;
    private LLKeyboardProc? _proc; // 必须持有引用，防止 GC

    public HotkeyManager() { }

    // ──────────────────────────────────────────────────────────────
    // 公共 API（与旧版完全兼容）
    // ──────────────────────────────────────────────────────────────

    public void Initialize(Window window)
    {
        _dispatcher = window.Dispatcher;
        StartHookThread();
    }

    public int Register(uint modifiers, uint vk, Action callback)
    {
        int id = _idCounter++;
        lock (_hotkeys) _hotkeys.Add(new HotkeyEntry(modifiers, vk, id, callback));
        return id;
    }

    public void Unregister(int id)
    {
        lock (_hotkeys) _hotkeys.RemoveAll(h => h.Id == id);
    }

    public void UnregisterAll()
    {
        lock (_hotkeys) _hotkeys.Clear();
    }

    // ──────────────────────────────────────────────────────────────
    // 钩子专属线程
    // ──────────────────────────────────────────────────────────────

    private void StartHookThread()
    {
        if (_hookThread != null) return;
        _proc = HookProc; // 在主线程创建委托，防止装箱后被 GC
        _hookThread = new Thread(HookThreadLoop)
        {
            IsBackground = true,
            Name = "HotkeyHookThread"
        };
        _hookThread.SetApartmentState(ApartmentState.STA);
        _hookThread.Start();
    }

    private void StopHookThread()
    {
        var tid = _hookThreadId;
        if (tid != 0)
            PostThreadMessage(tid, WM_QUIT, IntPtr.Zero, IntPtr.Zero);
        _hookThread = null;
        _hookThreadId = 0;
    }

    /// <summary>钩子线程入口：安装 LL Hook，运行纯净消息循环。</summary>
    private void HookThreadLoop()
    {
        _hookThreadId = GetCurrentThreadId();

        using var proc = Process.GetCurrentProcess();
        using var mod  = proc.MainModule!;
        _hookId = SetWindowsHookEx(WH_KEYBOARD_LL, _proc!, GetModuleHandle(mod.ModuleName!), 0);

        // 纯净 Win32 消息循环（不依赖 WPF Dispatcher，延迟极低）
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

    // ──────────────────────────────────────────────────────────────
    // 钩子回调（在钩子线程上被调用，必须快速返回）
    // ──────────────────────────────────────────────────────────────

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

                    // 检查修饰键
                    uint curMods = NativeMethods.MOD_NONE;
                    if ((GetAsyncKeyState(NativeMethods.VK_CONTROL) & 0x8000) != 0) curMods |= NativeMethods.MOD_CONTROL;
                    if ((GetAsyncKeyState(NativeMethods.VK_MENU)    & 0x8000) != 0) curMods |= NativeMethods.MOD_ALT;
                    if ((GetAsyncKeyState(NativeMethods.VK_SHIFT)   & 0x8000) != 0) curMods |= NativeMethods.MOD_SHIFT;

                    List<HotkeyEntry> snapshot;
                    lock (_hotkeys) snapshot = new List<HotkeyEntry>(_hotkeys);

                    foreach (var entry in snapshot)
                    {
                        if (entry.Vk == kb.vkCode && entry.Modifiers == curMods)
                        {
                            _dispatcher?.BeginInvoke(entry.Callback); // 投递到 UI 线程
                            return (IntPtr)1; // 消费此键，游戏不再收到
                        }
                    }
                }
            }
        }
        return CallNextHookEx(_hookId, nCode, wParam, lParam);
    }

    // ──────────────────────────────────────────────────────────────
    // 解析热键字符串
    // ──────────────────────────────────────────────────────────────

    public static bool TryParseHotkey(string hotkey, out uint modifiers, out uint vk)
    {
        modifiers = NativeMethods.MOD_NONE;
        vk = 0;
        if (string.IsNullOrWhiteSpace(hotkey)) return false;

        var parts = hotkey.Split('+');
        var keyPart = parts[^1].Trim();

        foreach (var part in parts[..^1])
        {
            switch (part.Trim().ToLowerInvariant())
            {
                case "ctrl":  modifiers |= NativeMethods.MOD_CONTROL; break;
                case "alt":   modifiers |= NativeMethods.MOD_ALT;     break;
                case "shift": modifiers |= NativeMethods.MOD_SHIFT;   break;
                case "win":   modifiers |= NativeMethods.MOD_WIN;     break;
            }
        }

        if (Enum.TryParse<Key>(keyPart, true, out var key))
        {
            vk = (uint)KeyInterop.VirtualKeyFromKey(key);
            return vk != 0;
        }
        return false;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        UnregisterAll();
        StopHookThread();
    }
}
