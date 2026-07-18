using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace YmbZoom;

/// <summary>
/// 全モニタを覆う半透明オーバーレイを表示し、ドラッグで矩形を1回だけ選択させる。
/// 選択完了(または中止)すると自身を閉じ、結果を <see cref="Completed"/> で通知する。
/// </summary>
public partial class RectSelectOverlay : Window
{
    private System.Windows.Point? _dragStart;

    /// <summary>選択確定時に物理ピクセルの矩形を渡す。中止時はnull。</summary>
    public event Action<Int32Rect?>? Completed;

    public RectSelectOverlay()
    {
        InitializeComponent();

        Left = SystemParameters.VirtualScreenLeft;
        Top = SystemParameters.VirtualScreenTop;
        Width = SystemParameters.VirtualScreenWidth;
        Height = SystemParameters.VirtualScreenHeight;

        Loaded += (_, _) => Keyboard.Focus(this);
    }

    private void Overlay_MouseDown(object sender, MouseButtonEventArgs e)
    {
        _dragStart = e.GetPosition(RootCanvas);
        SelectionRect.Visibility = Visibility.Visible;
        Canvas.SetLeft(SelectionRect, _dragStart.Value.X);
        Canvas.SetTop(SelectionRect, _dragStart.Value.Y);
        SelectionRect.Width = 0;
        SelectionRect.Height = 0;
        CaptureMouse();
    }

    private void Overlay_MouseMove(object sender, MouseEventArgs e)
    {
        if (_dragStart is null)
        {
            return;
        }

        var current = e.GetPosition(RootCanvas);
        double x = Math.Min(current.X, _dragStart.Value.X);
        double y = Math.Min(current.Y, _dragStart.Value.Y);
        double w = Math.Abs(current.X - _dragStart.Value.X);
        double h = Math.Abs(current.Y - _dragStart.Value.Y);

        Canvas.SetLeft(SelectionRect, x);
        Canvas.SetTop(SelectionRect, y);
        SelectionRect.Width = w;
        SelectionRect.Height = h;
    }

    private void Overlay_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_dragStart is null)
        {
            return;
        }

        ReleaseMouseCapture();

        var current = e.GetPosition(RootCanvas);
        var topLeftLocal = new System.Windows.Point(
            Math.Min(current.X, _dragStart.Value.X),
            Math.Min(current.Y, _dragStart.Value.Y));
        var bottomRightLocal = new System.Windows.Point(
            Math.Max(current.X, _dragStart.Value.X),
            Math.Max(current.Y, _dragStart.Value.Y));

        _dragStart = null;

        // 物理ピクセルへの変換はウィンドウ自体のDPI変換に委ねる(PointToScreenはデバイスピクセルを返す)。
        var topLeftScreen = PointToScreen(topLeftLocal);
        var bottomRightScreen = PointToScreen(bottomRightLocal);

        int width = (int)(bottomRightScreen.X - topLeftScreen.X);
        int height = (int)(bottomRightScreen.Y - topLeftScreen.Y);

        if (width < 8 || height < 8)
        {
            // 誤クリック扱い。選択やり直しを促すため閉じない。
            SelectionRect.Visibility = Visibility.Collapsed;
            return;
        }

        var rect = new Int32Rect((int)topLeftScreen.X, (int)topLeftScreen.Y, width, height);
        Completed?.Invoke(rect);
        Close();
    }

    private void Overlay_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Completed?.Invoke(null);
            Close();
        }
    }
}
