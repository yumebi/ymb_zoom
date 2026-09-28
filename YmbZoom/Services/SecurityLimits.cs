using System;

namespace YmbZoom.Services;

/// <summary>外部URLを開く前のセキュリティ制限。httpsとGitHub系ホストのみ許可し、プロトコル操作(SSRF)を防ぐ。</summary>
public static class SecurityLimits
{
    private static readonly HashSet<string> AllowedReleaseHosts = new(StringComparer.OrdinalIgnoreCase)
    {
        "github.com",
        "www.github.com",
        "raw.githubusercontent.com",
        "api.github.com",
        "objects.githubusercontent.com",
        "release-assets.githubusercontent.com",
        "codeload.github.com",
        "github.io"
    };

    /// <summary>リリースページ/ダウンロード用URLとして開いて安全か。https + 許可ホスト + ユーザー情報なし。</summary>
    public static bool IsAllowedReleaseUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return false;
        }

        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.IsNullOrEmpty(uri.UserInfo))
        {
            return false;
        }

        var host = uri.DnsSafeHost;
        return AllowedReleaseHosts.Contains(host)
            || host.EndsWith(".github.com", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".githubusercontent.com", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".github.io", StringComparison.OrdinalIgnoreCase);
    }
}
