using System.Runtime.InteropServices;
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
    // 保存先を環境変数で切り替えている場合(動作確認用)は、本番の起動と二重起動扱いにならないよう名前を分ける。
    private static readonly string InstanceSuffix = DataDirSuffix();
    private static readonly string MutexName = "YmbZoom.SingleInstance" + InstanceSuffix;
    private static readonly string ActivateEventName = "YmbZoom.Activate" + InstanceSuffix;

    private Mutex? _singleInstanceMutex;
    private EventWaitHandle? _activateEvent;
    private TrayIconService? _tray;
    private HotkeyManager? _hotkeys;
    private MainZoomWindow? _mainWindow;
    private AppSettings _settings = new();

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _singleInstanceMutex = new Mutex(true, MutexName, out bool createdNew);
        if (!createdNew)
        {
            // 先に起動している本体へ「表示して」と知らせて終了する(ダイアログを閉じる手間を無くす)。
            // 通知の仕組みを持たない旧バージョンが動いている場合だけ、従来どおりメッセージを出す。
            if (!SignalFirstInstance())
            {
                System.Windows.MessageBox.Show("YMB ZOOM は既に起動しています(タスクトレイをご確認ください)。", "YMB ZOOM");
            }
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
        StartActivationListener();

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

    private static string DataDirSuffix()
    {
        var dir = Environment.GetEnvironmentVariable(SettingsService.DataDirEnvVar);
        if (string.IsNullOrWhiteSpace(dir)) return "";
        var hash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(dir.ToLowerInvariant()));
        return "." + Convert.ToHexString(hash)[..12];
    }

    [DllImport("user32.dll")]
    private static extern bool AllowSetForegroundWindow(int dwProcessId);

    private const int ASFW_ANY = -1;

    /// <summary>2つ目の起動から「表示して」と通知を受ける待ち受けを始める。</summary>
    private void StartActivationListener()
    {
        var activateEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ActivateEventName);
        _activateEvent = activateEvent;
        var thread = new Thread(() =>
        {
            while (true)
            {
                try
                {
                    activateEvent.WaitOne();
                }
                catch (ObjectDisposedException)
                {
                    return;
                }
                Dispatcher.BeginInvoke(BringMainWindowToFront);
            }
        })
        {
            IsBackground = true,
            Name = "activate-listener",
        };
        thread.Start();
    }

    private static bool SignalFirstInstance()
    {
        try
        {
            // 2つ目のプロセスはユーザー操作で起動された直後なので、前面化の権限を持っている。
            // それを先に起動していた本体にも許可して、前面に出られるようにする。
            AllowSetForegroundWindow(ASFW_ANY);
            using var handle = EventWaitHandle.OpenExisting(ActivateEventName);
            return handle.Set();
        }
        catch (Exception e) when (e is WaitHandleCannotBeOpenedException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>トレイに隠れていれば表示し、最小化されていれば戻して、手前に出す。</summary>
    private void BringMainWindowToFront()
    {
        if (_mainWindow is null) return;
        if (!_mainWindow.IsVisible) _mainWindow.Show();
        if (_mainWindow.WindowState == WindowState.Minimized) _mainWindow.WindowState = WindowState.Normal;
        _mainWindow.Activate();
        // 他のアプリが前面にあるとActivateだけでは前に出ないことがある。Topmostを一瞬切り替えて確実に出す。
        var topmost = _mainWindow.Topmost;
        _mainWindow.Topmost = true;
        _mainWindow.Topmost = topmost;
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
