using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using YmbZoom.Models;
using YmbZoom.Services;

namespace YmbZoom;

/// <summary>ホットキー・既定の矩形指定モード・既定倍率・自動起動を設定する画面。</summary>
public partial class SettingsWindow : Window
{
    private ModifierKeys _pendingModifiers;
    private Key _pendingKey;

    /// <summary>保存されたら更新後の設定、キャンセルならnull。</summary>
    public AppSettings? Result { get; private set; }

    public SettingsWindow(AppSettings current)
    {
        InitializeComponent();

        _pendingModifiers = (ModifierKeys)current.HotkeyModifiers;
        _pendingKey = (Key)current.HotkeyKey;
        HotkeyBox.Text = FormatHotkey(_pendingModifiers, _pendingKey);

        DefaultRectModeCombo.SelectedIndex = (int)current.DefaultRectMode;
        DefaultZoomSlider.Value = current.ZoomFactor;
        DefaultZoomSlider.ValueChanged += (_, e) => DefaultZoomText.Text = $"{e.NewValue:0.0}x";
        DefaultZoomText.Text = $"{current.ZoomFactor:0.0}x";

        LaunchAtStartupCheck.IsChecked = current.LaunchAtStartup;
        ResidentInTrayCheck.IsChecked = current.ResidentInTray;

        AboutVersionText.Text = $"YMB ZOOM v{UpdateChecker.CurrentVersion}";
    }

    private async void CheckUpdateButton_Click(object sender, RoutedEventArgs e)
    {
        var button = (System.Windows.Controls.Button)sender;
        button.IsEnabled = false;
        try
        {
            var result = await UpdateChecker.CheckAsync();
            if (result.UpdateAvailable)
            {
                var choice = System.Windows.MessageBox.Show(
                    $"新しいバージョン v{result.LatestVersion} があります(現在: v{result.CurrentVersion})。\nダウンロードページを開きますか?",
                    "YMB ZOOM アップデート", MessageBoxButton.YesNo, MessageBoxImage.Information);

                if (choice == MessageBoxResult.Yes && !string.IsNullOrEmpty(result.ReleaseUrl))
                {
                    Process.Start(new ProcessStartInfo(result.ReleaseUrl) { UseShellExecute = true });
                }
            }
            else
            {
                System.Windows.MessageBox.Show("最新版を利用中です。", "YMB ZOOM アップデート",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        finally
        {
            button.IsEnabled = true;
        }
    }

    private void HotkeyBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        e.Handled = true;

        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt
            or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin)
        {
            return; // 修飾キー単独では確定しない
        }

        _pendingModifiers = Keyboard.Modifiers;
        _pendingKey = key;
        HotkeyBox.Text = FormatHotkey(_pendingModifiers, _pendingKey);
    }

    private static string FormatHotkey(ModifierKeys modifiers, Key key)
    {
        var parts = new List<string>();
        if (modifiers.HasFlag(ModifierKeys.Control)) parts.Add("Ctrl");
        if (modifiers.HasFlag(ModifierKeys.Alt)) parts.Add("Alt");
        if (modifiers.HasFlag(ModifierKeys.Shift)) parts.Add("Shift");
        if (modifiers.HasFlag(ModifierKeys.Windows)) parts.Add("Win");
        parts.Add(key.ToString());
        return string.Join("+", parts);
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        Result = new AppSettings
        {
            HotkeyModifiers = (int)_pendingModifiers,
            HotkeyKey = (int)_pendingKey,
            DefaultRectMode = (RectSelectMode)DefaultRectModeCombo.SelectedIndex,
            ZoomFactor = DefaultZoomSlider.Value,
            LaunchAtStartup = LaunchAtStartupCheck.IsChecked == true,
            ResidentInTray = ResidentInTrayCheck.IsChecked == true
        };
        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
