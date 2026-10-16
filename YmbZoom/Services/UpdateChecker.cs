using System.Net.Http;
using System.Reflection;
using System.Text.Json;

namespace YmbZoom.Services;

/// <summary>
/// GitHub の最新リリース(Releases API)と現在バージョンを比較する。
/// version.json ではなく公開済みのリリースを見るのは、リリースに失敗しても version.json だけ
/// 先に更新されて、存在しない版を案内してしまうのを避けるため。
/// </summary>
public static class UpdateChecker
{
    private const string Repo = "yumebi/ymb_zoom";

    private static readonly HttpClient Http = CreateHttpClient();

    private static HttpClient CreateHttpClient()
    {
        // GitHub API は User-Agent が必須
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("YmbZoom-UpdateCheck");
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        return client;
    }

    public static string CurrentVersion
    {
        get
        {
            var v = Assembly.GetExecutingAssembly().GetName().Version;
            return v is null ? "0.0.0" : $"{v.Major}.{v.Minor}.{v.Build}";
        }
    }

    public static async Task<UpdateCheckResult> CheckAsync()
    {
        string current = CurrentVersion;
        try
        {
            string json = await Http.GetStringAsync(GitHubRelease.ApiUrl(Repo));
            var release = GitHubRelease.Parse(json, Repo);
            string latest = release?.Version ?? current;
            bool updateAvailable = release is not null && GitHubRelease.IsNewer(latest, current);

            // リリースURLは自リポジトリのものだけ(Parse が検証済み)。さらに SecurityLimits の許可リストでも確認する。
            string? releaseUrl = SecurityLimits.IsAllowedReleaseUrl(release?.Url) ? release!.Url : null;

            return new UpdateCheckResult(current, latest, updateAvailable, releaseUrl);
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or TaskCanceledException)
        {
            return new UpdateCheckResult(current, current, false, null);
        }
    }

}

public sealed record UpdateCheckResult(string CurrentVersion, string LatestVersion, bool UpdateAvailable, string? ReleaseUrl);
