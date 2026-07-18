namespace YmbZoom.Models;

/// <summary>%AppData%\YmbZoom\settings.json に永続化するアプリ設定。</summary>
public sealed class AppSettings
{
    // ホットキー(ズーム表示のON/OFF切替)。既定値はCtrl+Alt+Z。
    // 値はSystem.Windows.Input.ModifierKeys / Keyの整数値。
    public int HotkeyModifiers { get; set; } = 3; // Control(2) | Alt(1)
    public int HotkeyKey { get; set; } = 46; // Key.Z (WPF Key enum値)

    public bool LaunchAtStartup { get; set; }

    /// <summary>trueなら×ボタンでタスクトレイに常駐(非表示)、falseなら完全終了する。</summary>
    public bool ResidentInTray { get; set; } = true;

    public RectSelectMode DefaultRectMode { get; set; } = RectSelectMode.HotkeyOverlay;
    public int CursorFollowWidth { get; set; } = 400;
    public int CursorFollowHeight { get; set; } = 300;

    public double ZoomFactor { get; set; } = 2.0;
    public bool Grayscale { get; set; }
    public bool Invert { get; set; }
    public bool HighContrast { get; set; }
    public int ColorblindIndex { get; set; } // 0=なし,1=P,2=D,3=T
    public double Contrast { get; set; } = 1.0;
    public double Brightness { get; set; }
    public bool SharpenEnabled { get; set; }
    public double SharpenAmount { get; set; } = 0.5;
}
