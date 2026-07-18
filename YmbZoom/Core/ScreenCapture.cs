using System.Drawing;
using System.Drawing.Imaging;
using System.Windows;

namespace YmbZoom.Core;

/// <summary>GDI BitBlt (Graphics.CopyFromScreen) による画面矩形キャプチャ。</summary>
public static class ScreenCapture
{
    /// <summary>
    /// 指定矩形(仮想デスクトップ座標・物理ピクセル)を32bpp ARGBビットマップとしてキャプチャする。
    /// 呼び出し側でDisposeすること。
    /// </summary>
    public static Bitmap Capture(Int32Rect rect)
    {
        var bitmap = new Bitmap(Math.Max(1, rect.Width), Math.Max(1, rect.Height), PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bitmap);
        g.CopyFromScreen(rect.X, rect.Y, 0, 0, bitmap.Size, CopyPixelOperation.SourceCopy);
        return bitmap;
    }
}
