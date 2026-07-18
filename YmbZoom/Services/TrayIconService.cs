using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Forms;

namespace YmbZoom.Services;

/// <summary>システムトレイ常駐アイコンと右クリックメニュー。</summary>
public sealed class TrayIconService : IDisposable
{
    private readonly NotifyIcon _icon;
    private string? _pendingReleaseUrl;

    public event Action? OpenRequested;
    public event Action? SettingsRequested;
    public event Action? ExitRequested;

    public TrayIconService()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("開く/隠す", null, (_, _) => OpenRequested?.Invoke());
        menu.Items.Add("設定...", null, (_, _) => SettingsRequested?.Invoke());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("終了", null, (_, _) => ExitRequested?.Invoke());

        _icon = new NotifyIcon
        {
            Icon = LoadAppIcon(),
            Text = "YMB ZOOM",
            Visible = true,
            ContextMenuStrip = menu
        };
        _icon.DoubleClick += (_, _) => OpenRequested?.Invoke();
        _icon.BalloonTipClicked += (_, _) =>
        {
            if (_pendingReleaseUrl is { } url)
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
        };
    }

    /// <summary>新しいバージョンが見つかったことをトレイの吹き出しで知らせる(クリックでダウンロードページを開く)。</summary>
    public void ShowUpdateAvailableNotice(string latestVersion, string? releaseUrl)
    {
        _pendingReleaseUrl = releaseUrl;
        _icon.ShowBalloonTip(8000, "YMB ZOOM",
            $"新しいバージョン v{latestVersion} があります。クリックでダウンロードページを開きます。",
            ToolTipIcon.Info);
    }

    private static System.Drawing.Icon LoadAppIcon()
    {
        try
        {
            var uri = new Uri("pack://application:,,,/Resources/icon.ico");
            var streamInfo = System.Windows.Application.GetResourceStream(uri);
            if (streamInfo is not null)
            {
                return new System.Drawing.Icon(streamInfo.Stream);
            }
        }
        catch (IOException)
        {
            // フォールバックへ
        }

        return System.Drawing.SystemIcons.Application;
    }

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
    }
}
