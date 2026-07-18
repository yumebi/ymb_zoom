using System.Net.Http;
using System.Reflection;
using System.Text.Json;

namespace YmbZoom.Services;

/// <summary>GitHubリポジトリのversion.json(raw.githubusercontent.com)と現在バージョンを比較する。</summary>
public static class UpdateChecker
{
    private const string VersionUrl =
        "https://raw.githubusercontent.com/yumebi/ymb_zoom/master/version.json";

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(5) };
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

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
            string json = await Http.GetStringAsync(VersionUrl);
            var remote = JsonSerializer.Deserialize<RemoteVersion>(json, JsonOptions);
            string latest = remote?.Version ?? current;

            bool updateAvailable = Version.TryParse(latest, out var latestV)
                && Version.TryParse(current, out var currentV)
                && latestV > currentV;

            return new UpdateCheckResult(current, latest, updateAvailable, remote?.Url);
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or TaskCanceledException)
        {
            return new UpdateCheckResult(current, current, false, null);
        }
    }

    private sealed class RemoteVersion
    {
        public string? Version { get; set; }
        public string? Url { get; set; }
    }
}

public sealed record UpdateCheckResult(string CurrentVersion, string LatestVersion, bool UpdateAvailable, string? ReleaseUrl);
