using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace YmbZoom.Core;

/// <summary>
/// BitBltキャプチャ+畳み込みフィルタによるソフトウェアズーム経路。
/// シャープ化フィルタが有効なときのみ使う(Magnification APIのカラー行列では
/// 輪郭強調を表現できないため)。<see cref="Bitmap"/> をImageコントロールに
/// バインドし、Stretch=Fillで表示側を拡大することでズームを実現する。
/// </summary>
public sealed class SoftwareZoomEngine : IDisposable
{
    private readonly DispatcherTimer _timer;
    private Int32Rect _sourceRect = new(0, 0, 400, 300);
    private float _sharpenAmount;
    private float[] _colorMatrix = ColorMatrixPresets.Identity();

    public WriteableBitmap? Bitmap { get; private set; }

    public SoftwareZoomEngine()
    {
        _timer = new DispatcherTimer(DispatcherPriority.Render)
        {
            Interval = TimeSpan.FromMilliseconds(33) // 約30fps
        };
        _timer.Tick += (_, _) => RenderFrame();
    }

    public void Start() => _timer.Start();

    public void Stop() => _timer.Stop();

    public void SetSourceRect(Int32Rect rect)
    {
        if (rect.Width <= 0 || rect.Height <= 0)
        {
            return;
        }

        if (Bitmap is null || Bitmap.PixelWidth != rect.Width || Bitmap.PixelHeight != rect.Height)
        {
            Bitmap = new WriteableBitmap(rect.Width, rect.Height, 96, 96, PixelFormats.Bgra32, null);
        }

        _sourceRect = rect;
    }

    public void SetSharpenAmount(float amount) => _sharpenAmount = Math.Max(0f, amount);

    public void SetColorMatrix(float[] matrix25) => _colorMatrix = matrix25;

    private void RenderFrame()
    {
        if (Bitmap is null)
        {
            return;
        }

        using var captured = ScreenCapture.Capture(_sourceRect);
        var data = captured.LockBits(
            new System.Drawing.Rectangle(0, 0, captured.Width, captured.Height),
            ImageLockMode.ReadOnly,
            System.Drawing.Imaging.PixelFormat.Format32bppArgb);

        int stride = data.Stride;
        var buffer = new byte[stride * captured.Height];
        try
        {
            Marshal.Copy(data.Scan0, buffer, 0, buffer.Length);
        }
        finally
        {
            captured.UnlockBits(data);
        }

        // 画面キャプチャのアルファ値は信頼できない(0で返る実装がある)ため常に不透明として扱う。
        for (int i = 3; i < buffer.Length; i += 4)
        {
            buffer[i] = 255;
        }

        if (_sharpenAmount > 0f)
        {
            buffer = ConvolutionFilters.ApplySharpen(buffer, captured.Width, captured.Height, stride, _sharpenAmount);
        }

        ApplyColorMatrix(buffer, _colorMatrix);

        Bitmap.WritePixels(new Int32Rect(0, 0, captured.Width, captured.Height), buffer, stride, 0);
    }

    private static void ApplyColorMatrix(byte[] bgra, float[] m)
    {
        bool isIdentity = true;
        for (int i = 0; i < 25 && isIdentity; i++)
        {
            float expected = i % 6 == 0 ? 1f : 0f; // 対角成分=1、それ以外=0
            if (Math.Abs(m[i] - expected) > 0.0001f)
            {
                isIdentity = false;
            }
        }
        if (isIdentity)
        {
            return;
        }

        for (int i = 0; i < bgra.Length; i += 4)
        {
            float b = bgra[i] / 255f;
            float g = bgra[i + 1] / 255f;
            float r = bgra[i + 2] / 255f;
            const float a = 1f;

            float outR = r * m[0] + g * m[5] + b * m[10] + a * m[15] + m[20];
            float outG = r * m[1] + g * m[6] + b * m[11] + a * m[16] + m[21];
            float outB = r * m[2] + g * m[7] + b * m[12] + a * m[17] + m[22];

            bgra[i] = (byte)Math.Clamp(MathF.Round(outB * 255f), 0f, 255f);
            bgra[i + 1] = (byte)Math.Clamp(MathF.Round(outG * 255f), 0f, 255f);
            bgra[i + 2] = (byte)Math.Clamp(MathF.Round(outR * 255f), 0f, 255f);
        }
    }

    public void Dispose() => Stop();
}
