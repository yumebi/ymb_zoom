using System.Reflection;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using YmbZoom.Core;
using YmbZoom.Models;
using YmbZoom.Services;

namespace YmbZoom;

/// <summary>
/// ズーム表示ウィンドウ。枠なし・最前面・リサイズ可。
/// </summary>
public partial class MainZoomWindow : Window, IZoomSource
{
    private DraggableFrameWindow? _draggableFrame;
    private CursorFollowSelector? _cursorFollow;
    private RectSelectMode _currentMode = RectSelectMode.HotkeyOverlay;
    private readonly HashSet<IntPtr> _excludedWindows = [];
    private readonly SoftwareZoomEngine _softwareEngine = new();
    private readonly AppSettings _initialSettings;
    private readonly DispatcherTimer _liveKickTimer;
    private Int32Rect _lastSourceRect;

    /// <summary>ツールバーの「設定...」ボタンから発火(トレイメニューの設定と同じ処理に委ねる)。</summary>
    public event Action? SettingsRequested;

    /// <summary>常駐しない設定のとき、×ボタンでアプリの完全終了を要求する。</summary>
    public event Action? ExitRequested;

    public MainZoomWindow(AppSettings settings)
    {
        _initialSettings = settings;
        InitializeComponent();

        // ホットキー選択/常駐ドラッグ枠のように矩形を1度だけ設定するモードでも
        // ネイティブMagnifierコントロールの描画がフリーズしないよう、定期的にソース矩形を再適用する。
        _liveKickTimer = new DispatcherTimer(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(66) };
        _liveKickTimer.Tick += (_, _) =>
        {
            if (Magnifier.Visibility == Visibility.Visible)
            {
                Magnifier.ForceRepaint();
            }
        };

        Loaded += OnLoaded;
        Closed += (_, _) =>
        {
            _liveKickTimer.Stop();
            StopModeResources();
            _softwareEngine.Dispose();
        };
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version;
        VersionText.Text = version is null ? string.Empty : $"v{version.Major}.{version.Minor}.{version.Build}";

        ApplySettings(_initialSettings);

        var primary = System.Windows.Forms.Screen.PrimaryScreen!.Bounds;
        var sourceRect = new Int32Rect(primary.X, primary.Y, 400, 300);

        SetSourceRect(sourceRect);
        Magnifier.SetZoomFactor(ZoomSlider.Value);
        UpdateColorEffect();

        AddExcludedWindow(new WindowInteropHelper(this).Handle);

        ResizeMagnifierToClientArea();

        // 既定の矩形指定モードが「ホットキー選択」以外なら、ここでモード開始処理を発火させる。
        RectModeCombo.SelectedIndex = (int)_initialSettings.DefaultRectMode;

        _liveKickTimer.Start();
    }

    private void ApplySettings(AppSettings settings)
    {
        ZoomSlider.Value = settings.ZoomFactor;
        GrayscaleCheck.IsChecked = settings.Grayscale;
        InvertCheck.IsChecked = settings.Invert;
        HighContrastCheck.IsChecked = settings.HighContrast;
        ColorblindCombo.SelectedIndex = settings.ColorblindIndex;
        ContrastSlider.Value = settings.Contrast;
        BrightnessSlider.Value = settings.Brightness;
        SharpenCheck.IsChecked = settings.SharpenEnabled;
        SharpenSlider.Value = settings.SharpenAmount;
    }

    /// <summary>現在のツールバー状態を設定オブジェクトへ書き戻す(アプリ終了時の永続化用)。</summary>
    public void SaveCurrentStateTo(AppSettings settings)
    {
        settings.ZoomFactor = ZoomSlider.Value;
        settings.Grayscale = GrayscaleCheck.IsChecked == true;
        settings.Invert = InvertCheck.IsChecked == true;
        settings.HighContrast = HighContrastCheck.IsChecked == true;
        settings.ColorblindIndex = ColorblindCombo.SelectedIndex;
        settings.Contrast = ContrastSlider.Value;
        settings.Brightness = BrightnessSlider.Value;
        settings.SharpenEnabled = SharpenCheck.IsChecked == true;
        settings.SharpenAmount = SharpenSlider.Value;
        settings.DefaultRectMode = _currentMode;
    }

    /// <summary>IZoomSource実装。矩形指定モードの出所を問わず、確定した矩形をここで受け取る。</summary>
    public void SetSourceRect(Int32Rect rect)
    {
        _lastSourceRect = rect;
        Magnifier.SetSourceRect(rect);

        _softwareEngine.SetSourceRect(rect);
        if (SoftwareImage.Source != _softwareEngine.Bitmap)
        {
            SoftwareImage.Source = _softwareEngine.Bitmap;
        }
    }

    private void AddExcludedWindow(IntPtr hwnd)
    {
        if (_excludedWindows.Add(hwnd))
        {
            Magnifier.SetExcludedWindows(_excludedWindows);
        }
    }

    private void RemoveExcludedWindow(IntPtr hwnd)
    {
        if (_excludedWindows.Remove(hwnd))
        {
            Magnifier.SetExcludedWindows(_excludedWindows);
        }
    }

    private void RectModeCombo_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (!IsLoaded)
        {
            return;
        }

        StopModeResources();

        _currentMode = RectModeCombo.SelectedIndex switch
        {
            1 => RectSelectMode.DraggableFrame,
            2 => RectSelectMode.CursorFollow,
            _ => RectSelectMode.HotkeyOverlay
        };

        StartOverlaySelectButton.Visibility = _currentMode == RectSelectMode.HotkeyOverlay
            ? Visibility.Visible
            : Visibility.Collapsed;

        switch (_currentMode)
        {
            case RectSelectMode.DraggableFrame:
                StartDraggableFrame();
                break;
            case RectSelectMode.CursorFollow:
                StartCursorFollow();
                break;
        }
    }

    private void StartOverlaySelectButton_Click(object sender, RoutedEventArgs e)
    {
        var overlay = new RectSelectOverlay();
        overlay.Completed += rect =>
        {
            if (rect is { } r)
            {
                SetSourceRect(r);
            }
        };
        overlay.Show();
    }

    private void StartDraggableFrame()
    {
        var primary = System.Windows.Forms.Screen.PrimaryScreen!.Bounds;
        _draggableFrame = new DraggableFrameWindow
        {
            Left = primary.X + 100,
            Top = primary.Y + 100
        };
        _draggableFrame.RectChanged += rect => SetSourceRect(rect);
        _draggableFrame.Show();
        AddExcludedWindow(new WindowInteropHelper(_draggableFrame).Handle);
    }

    private void StartCursorFollow()
    {
        _cursorFollow = new CursorFollowSelector();
        _cursorFollow.RectChanged += rect => SetSourceRect(rect);
        _cursorFollow.Start();
    }

    private void StopModeResources()
    {
        if (_draggableFrame is not null)
        {
            RemoveExcludedWindow(new WindowInteropHelper(_draggableFrame).Handle);
            _draggableFrame.Close();
            _draggableFrame = null;
        }

        if (_cursorFollow is not null)
        {
            _cursorFollow.Dispose();
            _cursorFollow = null;
        }
    }

    private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        ResizeMagnifierToClientArea();
    }

    private void ResizeMagnifierToClientArea()
    {
        Magnifier.ResizeToHostBounds((int)Magnifier.ActualWidth, (int)Magnifier.ActualHeight);
    }

    private void ZoomSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!IsInitialized)
        {
            return;
        }

        ZoomValueText.Text = $"{e.NewValue:0.0}x";
        Magnifier.SetZoomFactor(e.NewValue);
        SoftwareScale.ScaleX = e.NewValue;
        SoftwareScale.ScaleY = e.NewValue;
    }

    private void Filter_Changed(object sender, RoutedEventArgs e)
    {
        UpdateColorEffect();
    }

    private void Filter_Changed(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        UpdateColorEffect();
    }

    private void Filter_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        UpdateColorEffect();
    }

    private void UpdateColorEffect()
    {
        if (!IsLoaded)
        {
            return;
        }

        var matrices = new List<float[]>
        {
            ColorMatrixPresets.ContrastBrightness((float)ContrastSlider.Value, (float)BrightnessSlider.Value)
        };

        if (GrayscaleCheck.IsChecked == true)
        {
            matrices.Add(ColorMatrixPresets.Grayscale());
        }

        if (InvertCheck.IsChecked == true)
        {
            matrices.Add(ColorMatrixPresets.Invert());
        }

        if (HighContrastCheck.IsChecked == true)
        {
            matrices.Add(ColorMatrixPresets.HighContrast());
        }

        matrices.Add(ColorblindCombo.SelectedIndex switch
        {
            1 => ColorMatrixPresets.Protanopia(),
            2 => ColorMatrixPresets.Deuteranopia(),
            3 => ColorMatrixPresets.Tritanopia(),
            _ => ColorMatrixPresets.Identity()
        });

        var composed = ColorMatrixPresets.Compose(matrices);
        Magnifier.SetColorEffect(composed);
        _softwareEngine.SetColorMatrix(composed);

        UpdateRenderMode();
    }

    /// <summary>
    /// シャープ化が有効な間だけソフトウェア経路(BitBlt+畳み込み)に切り替える。
    /// Magnification APIのカラー行列では輪郭強調を表現できないため。
    /// </summary>
    private void UpdateRenderMode()
    {
        bool useSoftware = SharpenCheck.IsChecked == true && SharpenSlider.Value > 0;
        _softwareEngine.SetSharpenAmount(useSoftware ? (float)SharpenSlider.Value : 0f);

        if (useSoftware)
        {
            Magnifier.Visibility = Visibility.Collapsed;
            SoftwareImage.Visibility = Visibility.Visible;
            _softwareEngine.Start();
        }
        else
        {
            SoftwareImage.Visibility = Visibility.Collapsed;
            Magnifier.Visibility = Visibility.Visible;
            _softwareEngine.Stop();
        }
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        DragMove();
    }

    /// <summary>ズーム表示領域上でのマウスホイールで倍率を増減する。</summary>
    private void ZoomContent_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        double step = e.Delta > 0 ? 0.5 : -0.5;
        ZoomSlider.Value = Math.Clamp(ZoomSlider.Value + step, ZoomSlider.Minimum, ZoomSlider.Maximum);
        e.Handled = true;
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        SettingsRequested?.Invoke();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        if (_initialSettings.ResidentInTray)
        {
            // トレイ常駐設定のときは×で完全終了せず非表示にする。終了はトレイメニューから行う。
            Hide();
        }
        else
        {
            ExitRequested?.Invoke();
        }
    }
}
