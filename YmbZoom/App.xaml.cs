using System.Threading;
using System.Windows;
using System.Windows.Input;
using YmbZoom.Models;
using YmbZoom.Services;

namespace YmbZoom;

/// <summary>
/// アプリのエントリポイント。単一インスタンス制御、トレイ常駐、グローバルホットキーを管理する。
/// </summary>
public partial class App : Application
{
    private Mutex? _singleInstanceMutex;
    private TrayIconService? _tray;
    private HotkeyManager? _hotkeys;
    private MainZoomWindow? _mainWindow;
    private AppSettings _settings = new();

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _singleInstanceMutex = new Mutex(true, "YmbZoom.SingleInstance", out bool createdNew);
        if (!createdNew)
        {
            System.Windows.MessageBox.Show("YMB ZOOM は既に起動しています(タスクトレイをご確認ください)。", "YMB ZOOM");
            Shutdown();
            return;
        }

        _settings = SettingsService.Load();

        _mainWindow = new MainZoomWindow(_settings);
        _mainWindow.SettingsRequested += OpenSettings;
        _mainWindow.ExitRequested += () => Shutdown();
        _mainWindow.Show();

        _tray = new TrayIconService();
        _tray.OpenRequested += ToggleMainWindow;
        _tray.SettingsRequested += OpenSettings;
        _tray.ExitRequested += () => Shutdown();

        _hotkeys = new HotkeyManager(_mainWindow);
        RegisterToggleHotkey();

        _ = CheckForUpdatesSilentlyAsync();
    }

    /// <summary>起動時に一度だけ更新確認する。新版があればトレイの吹き出しで知らせる(強制はしない)。</summary>
    private async Task CheckForUpdatesSilentlyAsync()
    {
        var result = await UpdateChecker.CheckAsync();
        if (result.UpdateAvailable)
        {
            _tray?.ShowUpdateAvailableNotice(result.LatestVersion, result.ReleaseUrl);
        }
    }

    private void RegisterToggleHotkey()
    {
        _hotkeys!.UnregisterAll();
        _hotkeys.Register((ModifierKeys)_settings.HotkeyModifiers, (Key)_settings.HotkeyKey, ToggleMainWindow);
    }

    private void ToggleMainWindow()
    {
        if (_mainWindow is null)
        {
            return;
        }

        if (_mainWindow.IsVisible)
        {
            _mainWindow.Hide();
        }
        else
        {
            _mainWindow.Show();
            _mainWindow.Activate();
        }
    }

    private void OpenSettings()
    {
        var window = new SettingsWindow(_settings) { Owner = _mainWindow };
        if (window.ShowDialog() == true && window.Result is { } updated)
        {
            _settings.HotkeyModifiers = updated.HotkeyModifiers;
            _settings.HotkeyKey = updated.HotkeyKey;
            _settings.DefaultRectMode = updated.DefaultRectMode;
            _settings.ZoomFactor = updated.ZoomFactor;
            _settings.LaunchAtStartup = updated.LaunchAtStartup;
            _settings.ResidentInTray = updated.ResidentInTray;

            SettingsService.SetLaunchAtStartup(_settings.LaunchAtStartup);
            RegisterToggleHotkey();
            SettingsService.Save(_settings);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_mainWindow is not null)
        {
            _mainWindow.SaveCurrentStateTo(_settings);
            SettingsService.Save(_settings);
        }

        _hotkeys?.Dispose();
        _tray?.Dispose();
        _singleInstanceMutex?.ReleaseMutex();
        base.OnExit(e);
    }
}
