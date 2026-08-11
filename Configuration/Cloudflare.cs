namespace WebAndMobile.Configuration;

public static class Cloudflare
{
    // ==================================================
    // CLOUDFLARE QUICK TUNNEL URL
    // ==================================================
    //
    // When Quick Tunnel gives you a new URL,
    // change ONLY this line.
    //
    // Do not add a trailing slash.
    //

    public const string PublicUrl =
        "https://pale-checked-physiology-yellow.trycloudflare.com";

    // ==================================================
    // CONFIGURATION CHECK
    // ==================================================

    public static bool IsConfigured()
    {
        string url =
            (PublicUrl ?? "").Trim();

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
        ) &&
        string.Equals(
            uri.Scheme,
            Uri.UriSchemeHttps,
            StringComparison.OrdinalIgnoreCase
        );
    }

    // ==================================================
    // PUBLIC URL FOR PHONE/TABLET BINDING ONLY
    // ==================================================
    //
    // IMPORTANT:
    // This does NOT control WebAuthn Private/Public mode.
    // It is used only for the phone/tablet verification QR.
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
}
