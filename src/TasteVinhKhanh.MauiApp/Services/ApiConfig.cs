using Microsoft.Maui.Storage;

namespace TasteVinhKhanh.MauiApp.Services;

public static class ApiConfig
{
    public const string ApiBaseUrlPreferenceKey = "api_base_url";

    // TODO: Replace with your stable API domain when available.
    private const string DefaultApiBaseUrl = "https://troubleshooting-laboratories-genres-trembl.trycloudflare.com/";

    public static string GetApiBaseUrl()
    {
        var raw = Preferences.Get(ApiBaseUrlPreferenceKey, DefaultApiBaseUrl)?.Trim();
        return NormalizeBaseUrl(raw);
    }

    public static string ToAbsoluteUrl(string pathOrUrl)
    {
        if (pathOrUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            pathOrUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return pathOrUrl;
        }

        var baseUrl = GetApiBaseUrl();
        var cleanPath = pathOrUrl.TrimStart('/');
        return $"{baseUrl}{cleanPath}";
    }

    private static string NormalizeBaseUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return DefaultApiBaseUrl;
        }

        if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            url = $"https://{url}";
        }

        return url.EndsWith("/", StringComparison.Ordinal) ? url : $"{url}/";
    }
}
