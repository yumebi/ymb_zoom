using System.Diagnostics;

namespace YmbZoom.Services;

/// <summary>
/// DRM保護コンテンツ(Netflix等)はYMB ZOOM自前の画面キャプチャでは表示できないため、
/// OS側から特別に許可されているWindows標準拡大鏡(magnify.exe)の起動/終了だけを代行するブリッジ。
///
/// 当初はズーム操作(レンズ切替・拡大縮小・色反転)もUI Automationやキー/マウス合成入力で
/// 遠隔操作しようと試みたが、標準拡大鏡のツールバーはWinUI3(Composition/XAML Islands)製で
/// UIAutomationのプロバイダを実装しておらずボタンがツリーに現れず、キーボードショートカットは
/// LLKHF_INJECTEDとして無視され、合成マウスクリックも"Windows.UI.Input.InputSite"のポインタ
/// 処理に認識されず、いずれも突破できなかった。そのためズーム操作は利用者が標準拡大鏡を
/// 直接(マウスホイール/キーボードで)操作する前提とし、YMB ZOOM側は起動・終了のみ担当する。
/// </summary>
public static class NativeMagnifierBridge
{
    /// <summary>標準拡大鏡(magnify.exe)が起動中か。</summary>
    public static bool IsRunning => Process.GetProcessesByName("magnify").Length > 0;

    /// <summary>未起動なら起動する。</summary>
    public static void Launch()
    {
        if (!IsRunning)
        {
            Process.Start(new ProcessStartInfo("magnify.exe") { UseShellExecute = true });
        }
    }

    /// <summary>標準拡大鏡を終了する。プロセスを直接終了させる(キー送信は効かない環境があったため)。</summary>
    public static void Exit()
    {
        foreach (var process in Process.GetProcessesByName("magnify"))
        {
            try
            {
                process.Kill();
            }
            catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
            {
                // 権限等で終了できない場合は諦める(利用者が手動で閉じる想定)。
            }
            finally
            {
                process.Dispose();
            }
        }
    }
}
