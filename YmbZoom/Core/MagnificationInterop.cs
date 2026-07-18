using System.Runtime.InteropServices;

namespace YmbZoom.Core;

[StructLayout(LayoutKind.Sequential)]
internal struct RECT
{
    public int Left;
    public int Top;
    public int Right;
    public int Bottom;

    public RECT(int left, int top, int right, int bottom)
    {
        Left = left;
        Top = top;
        Right = right;
        Bottom = bottom;
    }
}

[StructLayout(LayoutKind.Sequential)]
internal struct MAGTRANSFORM
{
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 9)]
    public float[] Transform;

    public static MAGTRANSFORM CreateScale(float scale)
    {
        return new MAGTRANSFORM
        {
            Transform =
            [
                scale, 0f, 0f,
                0f, scale, 0f,
                0f, 0f, 1f
            ]
        };
    }
}

[StructLayout(LayoutKind.Sequential)]
internal struct MAGCOLOREFFECT
{
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 25)]
    public float[] Transform;

    public static MAGCOLOREFFECT Identity => new()
    {
        Transform =
        [
            1f, 0f, 0f, 0f, 0f,
            0f, 1f, 0f, 0f, 0f,
            0f, 0f, 1f, 0f, 0f,
            0f, 0f, 0f, 1f, 0f,
            0f, 0f, 0f, 0f, 1f
        ]
    };

    public static MAGCOLOREFFECT FromMatrix(float[] matrix25)
    {
        if (matrix25.Length != 25)
        {
            throw new ArgumentException("カラー行列は25要素(5x5)である必要があります。", nameof(matrix25));
        }

        return new MAGCOLOREFFECT { Transform = (float[])matrix25.Clone() };
    }
}

internal static class MagnificationInterop
{
    public const string WindowClassName = "Magnifier";

    public const int WS_CHILD = 0x40000000;
    public const int WS_VISIBLE = 0x10000000;

    public const int MS_SHOWMAGNIFIEDCURSOR = 0x0001;
    public const int MS_CLIPAROUNDCURSOR = 0x0002;

    public const int MW_FILTERMODE_EXCLUDE = 0;
    public const int MW_FILTERMODE_INCLUDE = 1;

    [DllImport("magnification.dll", SetLastError = true)]
    public static extern bool MagInitialize();

    [DllImport("magnification.dll", SetLastError = true)]
    public static extern bool MagUninitialize();

    [DllImport("magnification.dll", SetLastError = true)]
    public static extern bool MagSetWindowSource(IntPtr hwnd, RECT rect);

    [DllImport("magnification.dll", SetLastError = true)]
    public static extern bool MagGetWindowSource(IntPtr hwnd, out RECT rect);

    [DllImport("magnification.dll", SetLastError = true)]
    public static extern bool MagSetWindowTransform(IntPtr hwnd, ref MAGTRANSFORM transform);

    [DllImport("magnification.dll", SetLastError = true)]
    public static extern bool MagGetWindowTransform(IntPtr hwnd, out MAGTRANSFORM transform);

    [DllImport("magnification.dll", SetLastError = true)]
    public static extern bool MagSetColorEffect(IntPtr hwnd, ref MAGCOLOREFFECT effect);

    [DllImport("magnification.dll", SetLastError = true)]
    public static extern bool MagGetColorEffect(IntPtr hwnd, out MAGCOLOREFFECT effect);

    [DllImport("magnification.dll", SetLastError = true)]
    public static extern bool MagSetWindowFilterList(IntPtr hwnd, int dwFilterMode, int count, IntPtr[] pHWND);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern IntPtr CreateWindowEx(
        int exStyle,
        string className,
        string? windowName,
        int style,
        int x,
        int y,
        int width,
        int height,
        IntPtr hwndParent,
        IntPtr hMenu,
        IntPtr hInstance,
        IntPtr param);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool DestroyWindow(IntPtr hwnd);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool MoveWindow(IntPtr hwnd, int x, int y, int width, int height, bool repaint);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool InvalidateRect(IntPtr hwnd, IntPtr rect, bool erase);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool UpdateWindow(IntPtr hwnd);
}
