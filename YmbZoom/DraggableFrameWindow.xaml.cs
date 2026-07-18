using System.Windows;
using System.Windows.Input;

namespace YmbZoom;

/// <summary>
/// 常駐する矩形枠。ドラッグで移動、端でリサイズでき、動くたびに現在の矩形(物理ピクセル)を通知する。
/// </summary>
public partial class DraggableFrameWindow : Window
{
    public event Action<Int32Rect>? RectChanged;

    public DraggableFrameWindow()
    {
        InitializeComponent();
        Loaded += (_, _) => NotifyRectChanged();
    }

    private void Grid_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        DragMove();
    }

    private void Frame_LocationOrSizeChanged(object sender, EventArgs e)
    {
        NotifyRectChanged();
    }

    private void NotifyRectChanged()
    {
        if (!IsLoaded)
        {
            return;
        }

        var topLeftScreen = PointToScreen(new System.Windows.Point(0, 0));
        var bottomRightScreen = PointToScreen(new System.Windows.Point(ActualWidth, ActualHeight));

        int width = Math.Max(1, (int)(bottomRightScreen.X - topLeftScreen.X));
        int height = Math.Max(1, (int)(bottomRightScreen.Y - topLeftScreen.Y));

        RectChanged?.Invoke(new Int32Rect((int)topLeftScreen.X, (int)topLeftScreen.Y, width, height));
    }
}
