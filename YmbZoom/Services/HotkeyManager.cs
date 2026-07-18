using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace YmbZoom.Services;

/// <summary>グローバルホットキーの登録・解除(RegisterHotKey)。既存ウィンドウのメッセージループに相乗りする。</summary>
public sealed class HotkeyManager : IDisposable
{
    private const int WM_HOTKEY = 0x0312;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    private readonly IntPtr _hwnd;
    private readonly HwndSource _source;
    private readonly Dictionary<int, Action> _handlers = [];
    private int _nextId = 1;

    public HotkeyManager(Window window)
    {
        _hwnd = new WindowInteropHelper(window).EnsureHandle();
        _source = HwndSource.FromHwnd(_hwnd) ?? throw new InvalidOperationException("ウィンドウのHwndSourceを取得できませんでした。");
        _source.AddHook(WndProc);
    }

    /// <summary>登録に成功したらtrue。同じキーが既に他アプリに使われている場合などはfalse。</summary>
    public bool Register(ModifierKeys modifiers, Key key, Action onPressed)
    {
        int id = _nextId++;
        uint vk = (uint)KeyInterop.VirtualKeyFromKey(key);

        if (!RegisterHotKey(_hwnd, id, (uint)modifiers, vk))
        {
            return false;
        }

        _handlers[id] = onPressed;
        return true;
    }

    public void UnregisterAll()
    {
        foreach (int id in _handlers.Keys)
        {
            UnregisterHotKey(_hwnd, id);
        }
        _handlers.Clear();
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY && _handlers.TryGetValue(wParam.ToInt32(), out var action))
        {
            action();
            handled = true;
        }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        UnregisterAll();
        _source.RemoveHook(WndProc);
    }
}
