using System.Windows;
using System.Windows.Threading;

namespace YmbZoom.Services;

/// <summary>
/// 固定サイズの矩形をマウスカーソルに追従させ、一定間隔で現在の矩形(物理ピクセル)を通知する。
/// </summary>
public sealed class CursorFollowSelector : IDisposable
{
    private readonly DispatcherTimer _timer;

    public event Action<Int32Rect>? RectChanged;

    public int RectWidth { get; set; } = 400;
    public int RectHeight { get; set; } = 300;

    public CursorFollowSelector()
    {
        _timer = new DispatcherTimer(DispatcherPriority.Render)
        {
            Interval = TimeSpan.FromMilliseconds(20)
        };
        _timer.Tick += (_, _) => Tick();
    }

    public void Start() => _timer.Start();

    public void Stop() => _timer.Stop();

    private void Tick()
    {
        var cursor = System.Windows.Forms.Cursor.Position;
        var rect = new Int32Rect(
            cursor.X - RectWidth / 2,
            cursor.Y - RectHeight / 2,
            RectWidth,
            RectHeight);

        RectChanged?.Invoke(rect);
    }

    public void Dispose() => Stop();
}
