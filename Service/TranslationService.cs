using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;

namespace WebAndMobile.Services;

public interface ITranslationService
{
    IReadOnlyList<string> SupportedLanguages { get; }

    bool IsSupported(string? language);

    Task<List<string>> TranslateAsync(
        IReadOnlyList<string> texts,
        string targetLanguage,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Wraps the Azure AI Translator REST API (v3.0).
///
/// The site is authored in English, so "en" never calls the API.
/// Every translated string is cached in memory so the same label is
/// only ever paid for once per language until the cache expires.
/// </summary>
public class AzureTranslationService : ITranslationService
{
    private const string DefaultEndpoint =
        "https://api.cognitive.microsofttranslator.com";

    private const string SourceLanguage = "en";

    // Azure allows 1,000 array elements and 50,000 characters per
    // request; stay well under both.
    private const int MaxSegmentsPerRequest = 80;

    // Codes stored in the database / used by the dropdown.
    private static readonly string[] Supported = ["en", "ms", "zh"];

    private static readonly TimeSpan CacheLifetime = TimeSpan.FromHours(12);

    private readonly IHttpClientFactory httpClientFactory;
    private readonly IMemoryCache cache;
    private readonly ILogger<AzureTranslationService> logger;
    private readonly string? subscriptionKey;
    private readonly string? region;
    private readonly string endpoint;

    public AzureTranslationService(
        IHttpClientFactory httpClientFactory,
        IMemoryCache cache,
        IConfiguration configuration,
        ILogger<AzureTranslationService> logger)
    {
        this.httpClientFactory = httpClientFactory;
        this.cache = cache;
        this.logger = logger;

        subscriptionKey = configuration["AzureTranslator:Key"];
        region = configuration["AzureTranslator:Region"];

        string? configured = configuration["AzureTranslator:Endpoint"];

        endpoint =
            string.IsNullOrWhiteSpace(configured)
                ? DefaultEndpoint
                : configured.TrimEnd('/');
    }

    public IReadOnlyList<string> SupportedLanguages => Supported;

    public bool IsSupported(string? language)
    {
        if (string.IsNullOrWhiteSpace(language))
        {
            return false;
        }

        return Supported.Contains(
            language.Trim().ToLowerInvariant());
    }

    /// <summary>
    /// Maps our short codes onto the codes Azure expects.
    /// Azure needs the script variant for Chinese.
    /// </summary>
    private static string ToAzureCode(string language) =>
        language switch
        {
            "zh" => "zh-Hans",
            _ => language
        };

    public async Task<List<string>> TranslateAsync(
        IReadOnlyList<string> texts,
        string targetLanguage,
        CancellationToken cancellationToken = default)
    {
        // Default result is the original text, so a misconfigured or
        // failing API degrades to plain English instead of a blank page.
        List<string> results = [.. texts];

        if (texts.Count == 0)
        {
            return results;
        }

        string target = (targetLanguage ?? "").Trim().ToLowerInvariant();

        if (!IsSupported(target) || target == SourceLanguage)
        {
            return results;
        }

        if (string.IsNullOrWhiteSpace(subscriptionKey))
        {
            logger.LogWarning(
                "AzureTranslator:Key is not configured - " +
                "returning untranslated text.");

            return results;
        }

        Dictionary<string, string> resolved = new(StringComparer.Ordinal);
        List<string> pending = [];

        foreach (string text in texts)
        {
            if (resolved.ContainsKey(text) || pending.Contains(text))
            {
                continue;
            }

            if (cache.TryGetValue(
                    CacheKey(target, text),
                    out string? cached) &&
                cached is not null)
            {
                resolved[text] = cached;
            }
            else
            {
                pending.Add(text);
            }
        }

        for (int offset = 0;
             offset < pending.Count;
             offset += MaxSegmentsPerRequest)
        {
            List<string> chunk =
                pending
                    .Skip(offset)
                    .Take(MaxSegmentsPerRequest)
                    .ToList();

            List<string>? translated =
                await CallApiAsync(chunk, target, cancellationToken);

            if (translated is null)
            {
                // API failed - keep English for this chunk.
                break;
            }

            for (int i = 0; i < chunk.Count && i < translated.Count; i++)
            {
                resolved[chunk[i]] = translated[i];

                cache.Set(
                    CacheKey(target, chunk[i]),
                    translated[i],
                    CacheLifetime);
            }
        }

        for (int i = 0; i < results.Count; i++)
        {
            if (resolved.TryGetValue(texts[i], out string? value))
            {
                results[i] = value;
            }
        }

        return results;
    }

    private async Task<List<string>?> CallApiAsync(
        List<string> chunk,
        string target,
        CancellationToken cancellationToken)
    {
        try
        {
            HttpClient client = httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(15);

            string url =
                $"{endpoint}/translate" +
                $"?api-version=3.0" +
                $"&from={SourceLanguage}" +
                $"&to={ToAzureCode(target)}" +
                $"&textType=plain";

            // Azure expects [{ "Text": "..." }, ...]
            var payload =
                chunk
                    .Select(text => new { Text = text })
                    .ToList();

            using HttpRequestMessage request =
                new(HttpMethod.Post, url)
                {
                    Content = new StringContent(
                        JsonSerializer.Serialize(payload),
                        Encoding.UTF8,
                        "application/json")
                };

            request.Headers.Add(
                "Ocp-Apim-Subscription-Key",
                subscriptionKey);

            // Required for a resource created in a specific region.
            if (!string.IsNullOrWhiteSpace(region))
            {
                request.Headers.Add(
                    "Ocp-Apim-Subscription-Region",
                    region);
            }

            using HttpResponseMessage response =
                await client.SendAsync(request, cancellationToken);

            string raw = await response.Content
                .ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogError(
                    "Azure Translator returned {Status}: {Body}",
                    (int)response.StatusCode,
                    raw);

                return null;
            }

            using JsonDocument document = JsonDocument.Parse(raw);

            List<string> output = [];

            // [{ "translations": [ { "text": "...", "to": "ms" } ] }]
            foreach (JsonElement item in document.RootElement.EnumerateArray())
            {
                if (!item.TryGetProperty("translations", out JsonElement list) ||
                    list.GetArrayLength() == 0)
                {
                    output.Add("");
                    continue;
                }

                output.Add(
                    list[0].GetProperty("text").GetString() ?? "");
            }

            return output;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Azure Translator call failed.");
            return null;
        }
    }

    private static string CacheKey(string target, string text) =>
        $"translate::{target}::{text}";
}
