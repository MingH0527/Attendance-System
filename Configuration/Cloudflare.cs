namespace WebAndMobile.Configuration;

public static class Cloudflare
{
    // ==================================================
    // CLOUDFLARE QUICK TUNNEL URL
    // ==================================================
    //
    // When Quick Tunnel gives you a new URL,
    // change ONLY this line. https://xxxx.trycloudflare.com/Payment/Verify
    //
    // Do not add a trailing slash.
    //

    public const string PublicUrl =
        "https://observation-diet-calculated-brunette.trycloudflare.com";

    // ==================================================
    // CONFIGURATION CHECK
    // ==================================================

    public static bool IsConfigured()
    {
        string url =
            (PublicUrl ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(url))
        {
            return false;
        }

        if (!url.StartsWith(
                "https://",
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (url.Contains(
                "YOUR-URL",
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return Uri.TryCreate(
                   url,
                   UriKind.Absolute,
                   out Uri? uri
               )
               &&
               string.Equals(
                   uri.Scheme,
                   Uri.UriSchemeHttps,
                   StringComparison.OrdinalIgnoreCase
               );
    }

    // ==================================================
    // PUBLIC BASE URL
    // ==================================================
    //
    // Shared Cloudflare public URL for features that
    // need access from another device or public network.
    //
    // Examples:
    // - Phone / tablet verification QR
    // - Passkey public mode
    // - External laptop access
    //

    public static string GetPublicUrl()
    {
        if (!IsConfigured())
        {
            throw new InvalidOperationException(
                "Cloudflare public URL is not configured. " +
                "Start Cloudflare Tunnel and update " +
                "Configuration/Cloudflare.cs first."
            );
        }

        return PublicUrl
            .Trim()
            .TrimEnd('/');
    }

    // ==================================================
    // BUILD PUBLIC URL
    // ==================================================
    //
    // Example:
    // Cloudflare.BuildPublicUrl("/Payment/Verify")
    //
    // Result:
    // https://xxxx.trycloudflare.com/Payment/Verify
    //

    public static string BuildPublicUrl(
        string path)
    {
        string baseUrl =
            GetPublicUrl();

        if (string.IsNullOrWhiteSpace(path))
        {
            return baseUrl;
        }

        return
            $"{baseUrl}/{path.TrimStart('/')}";
    }
}