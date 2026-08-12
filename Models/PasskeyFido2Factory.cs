using Fido2NetLib;

namespace WebAndMobile.Services;

/// <summary>
/// Creates a FIDO2/WebAuthn relying-party configuration from the CURRENT request.
/// localhost/private development and a Cloudflare/public HTTPS hostname therefore
/// use the correct RP ID and Origin automatically.
/// </summary>
public sealed class PasskeyFido2Factory
{
    public IFido2 Create(HttpRequest request)
    {
        string host = request.Host.Host.Trim().ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(host))
        {
            throw new InvalidOperationException(
                "Unable to determine the WebAuthn host.");
        }

        string scheme = GetEffectiveScheme(request);

        // WebAuthn allows http://localhost for local development.
        // Public hosts must be HTTPS.
        if (!IsLocalhost(host) &&
            !string.Equals(
                scheme,
                "https",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Passkeys on a public hostname require HTTPS.");
        }

        string origin =
            $"{scheme}://{request.Host.Value}".TrimEnd('/');

        Fido2Configuration configuration = new()
        {
            ServerDomain = host,
            ServerName = "WebAndMobile",
            Origins = new HashSet<string>(
                StringComparer.OrdinalIgnoreCase)
            {
                origin
            }
        };

        return new Fido2(configuration);
    }

    public string GetCurrentRpId(HttpRequest request)
    {
        return request.Host.Host
            .Trim()
            .ToLowerInvariant();
    }

    public string GetMode(HttpRequest request)
    {
        return IsLocalhost(
            request.Host.Host)
                ? "Private"
                : "Public";
    }

    private static string GetEffectiveScheme(
        HttpRequest request)
    {
        // This expects ASP.NET Core ForwardedHeaders middleware to run
        // before authentication/controllers when Cloudflare is used.
        return request.Scheme;
    }

    private static bool IsLocalhost(string host)
    {
        return string.Equals(
                   host,
                   "localhost",
                   StringComparison.OrdinalIgnoreCase) ||
               string.Equals(
                   host,
                   "127.0.0.1",
                   StringComparison.OrdinalIgnoreCase) ||
               string.Equals(
                   host,
                   "::1",
                   StringComparison.OrdinalIgnoreCase);
    }
}
