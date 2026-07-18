using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace YmbZoom.Core;

/// <summary>
/// magnification.dll のネイティブ "Magnifier" コントロールをWPFにホストする。
/// DWM合成で描画されるためBitBlt方式より低遅延・低CPUで矩形ズームができる。
/// </summary>
public sealed class MagnifierEngine : HwndHost
{
    private static int s_initRefCount;

    private IntPtr _magnifierHwnd = IntPtr.Zero;

    protected override HandleRef BuildWindowCore(HandleRef hwndParent)
    {
        EnsureMagInitialized();

        _magnifierHwnd = MagnificationInterop.CreateWindowEx(
            0,
            MagnificationInterop.WindowClassName,
            string.Empty,
            MagnificationInterop.WS_CHILD | MagnificationInterop.WS_VISIBLE | MagnificationInterop.MS_SHOWMAGNIFIEDCURSOR,
            0, 0,
            (int)Math.Max(1, Width),
            (int)Math.Max(1, Height),
            hwndParent.Handle,
            IntPtr.Zero,
            IntPtr.Zero,
            IntPtr.Zero);

        if (_magnifierHwnd == IntPtr.Zero)
        {
            int error = Marshal.GetLastWin32Error();
            throw new InvalidOperationException($"Magnifierコントロールの作成に失敗しました。Win32エラー: {error}");
        }

        return new HandleRef(this, _magnifierHwnd);
    }

    protected override void DestroyWindowCore(HandleRef hwnd)
    {
        MagnificationInterop.DestroyWindow(hwnd.Handle);
        _magnifierHwnd = IntPtr.Zero;
        ReleaseMagInitialized();
    }

    /// <summary>ホストコントロールのクライアント領域サイズに合わせてネイティブ子ウィンドウをリサイズする。</summary>
    public void ResizeToHostBounds(int width, int height)
    {
        if (_magnifierHwnd == IntPtr.Zero)
        {
            return;
        }

        MagnificationInterop.MoveWindow(_magnifierHwnd, 0, 0, Math.Max(1, width), Math.Max(1, height), true);
    }

    /// <summary>ズーム元となる画面上の矩形(仮想デスクトップ座標・物理ピクセル)を設定する。</summary>
    public bool SetSourceRect(Int32Rect rect)
    {
        if (_magnifierHwnd == IntPtr.Zero)
        {
            return false;
        }

        var nativeRect = new RECT(rect.X, rect.Y, rect.X + rect.Width, rect.Y + rect.Height);
        return MagnificationInterop.MagSetWindowSource(_magnifierHwnd, nativeRect);
    }

    /// <summary>拡大率(等倍=1.0)を設定する。</summary>
    public bool SetZoomFactor(double factor)
    {
        if (_magnifierHwnd == IntPtr.Zero)
        {
            return false;
        }

        var transform = MAGTRANSFORM.CreateScale((float)factor);
        return MagnificationInterop.MagSetWindowTransform(_magnifierHwnd, ref transform);
    }

    /// <summary>
    /// 指定ウィンドウ群をキャプチャ対象から除外する(呼び出すたびに一覧を丸ごと置き換える仕様のため、
    /// 除外したい全ウィンドウを毎回まとめて渡すこと)。ズーム表示ウィンドウ自身を除外しないと、
    /// ソース矩形と表示位置が重なったときに合わせ鏡状の再帰描画が発生する。
    /// </summary>
    public bool SetExcludedWindows(IReadOnlyCollection<IntPtr> hwndsToExclude)
    {
        if (_magnifierHwnd == IntPtr.Zero)
        {
            return false;
        }

        return MagnificationInterop.MagSetWindowFilterList(
            _magnifierHwnd, MagnificationInterop.MW_FILTERMODE_EXCLUDE, hwndsToExclude.Count, [.. hwndsToExclude]);
    }

    /// <summary>
    /// ネイティブMagnifierコントロールに強制的に再描画させる。
    /// ソース矩形の値が前回と同一だとMagSetWindowSourceが再描画を省略することがあるため、
    /// 変化のない領域(ホットキー選択/常駐ドラッグ枠を静止させた場合など)でも映像がフリーズしないよう
    /// 定期的にこれを呼んでWM_PAINTを強制発生させる。
    /// </summary>
    public void ForceRepaint()
    {
        if (_magnifierHwnd == IntPtr.Zero)
        {
            return;
        }

        MagnificationInterop.InvalidateRect(_magnifierHwnd, IntPtr.Zero, false);
        MagnificationInterop.UpdateWindow(_magnifierHwnd);
    }

    /// <summary>5x5カラー行列(25要素、行優先)を適用する。nullで無効化(等倍行列に戻す)。</summary>
    public bool SetColorEffect(float[]? matrix25)
    {
        if (_magnifierHwnd == IntPtr.Zero)
        {
            return false;
        }

        var effect = matrix25 is null ? MAGCOLOREFFECT.Identity : MAGCOLOREFFECT.FromMatrix(matrix25);
        return MagnificationInterop.MagSetColorEffect(_magnifierHwnd, ref effect);
    }

    private static void EnsureMagInitialized()
    {
        if (s_initRefCount == 0)
        {
            if (!MagnificationInterop.MagInitialize())
            {
                int error = Marshal.GetLastWin32Error();
                throw new InvalidOperationException($"MagInitializeに失敗しました。Win32エラー: {error}");
            }
        }

        s_initRefCount++;
    }

    private static void ReleaseMagInitialized()
    {
        if (s_initRefCount == 0)
        {
            return;
        }

        s_initRefCount--;
        if (s_initRefCount == 0)
        {
            MagnificationInterop.MagUninitialize();
        }
    }
}
