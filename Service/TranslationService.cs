using System.Text.Json;

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
/// Translates from a local dictionary file instead of a paid API.
///
/// The site is authored in English, so "en" is never looked up.
/// Anything missing from the dictionary is returned unchanged, which
/// means the page simply keeps that phrase in English - nothing breaks
/// and the file can be filled in gradually.
///
/// Dictionary lives at wwwroot/data/translations.json and is reloaded
/// automatically whenever the file is saved, so you can edit it while
/// the app is running.
/// </summary>
public class DictionaryTranslationService : ITranslationService
{
    private const string SourceLanguage = "en";

    private static readonly string[] Supported = ["en", "ms", "zh"];

    private readonly IWebHostEnvironment environment;
    private readonly ILogger<DictionaryTranslationService> logger;
    private readonly object gate = new();

    private Dictionary<string, Dictionary<string, string>> dictionary = [];
    private DateTime lastLoadedUtc = DateTime.MinValue;
    private DateTime lastFileWriteUtc = DateTime.MinValue;

    public DictionaryTranslationService(
        IWebHostEnvironment environment,
        ILogger<DictionaryTranslationService> logger)
    {
        this.environment = environment;
        this.logger = logger;
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

    public Task<List<string>> TranslateAsync(
        IReadOnlyList<string> texts,
        string targetLanguage,
        CancellationToken cancellationToken = default)
    {
        // Default result is the original text, so an incomplete
        // dictionary degrades to English instead of blank labels.
        List<string> results = [.. texts];

        if (texts.Count == 0)
        {
            return Task.FromResult(results);
        }

        string target = (targetLanguage ?? "").Trim().ToLowerInvariant();

        if (!IsSupported(target) || target == SourceLanguage)
        {
            return Task.FromResult(results);
        }

        Dictionary<string, string>? table = GetTable(target);

        if (table is null || table.Count == 0)
        {
            return Task.FromResult(results);
        }

        for (int i = 0; i < results.Count; i++)
        {
            string key = Normalise(texts[i]);

            if (key.Length == 0)
            {
                continue;
            }

            string? value = Lookup(table, key);

            if (!string.IsNullOrWhiteSpace(value))
            {
                results[i] = value;
            }
        }

        return Task.FromResult(results);
    }

    /// <summary>
    /// Collapses newlines and repeated spaces so a phrase written across
    /// several lines in a .cshtml still matches a single dictionary key.
    /// </summary>
    private static string Normalise(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return "";
        }

        return string.Join(
            ' ',
            text.Split(
                (char[]?)null,
                StringSplitOptions.RemoveEmptyEntries));
    }

    private static string? Lookup(
        Dictionary<string, string> table,
        string key)
    {
        // 1. Exact match (case-insensitive, so "PROGRAM" finds "Program").
        if (table.TryGetValue(key, out string? exact))
        {
            return exact;
        }

        // 2. Placeholder entries such as "Welcome back, {0}", where the
        //    captured part is a name or number that must not be translated.
        foreach ((string pattern, string replacement) in table)
        {
            int marker = pattern.IndexOf("{0}", StringComparison.Ordinal);

            if (marker < 0)
            {
                continue;
            }

            string prefix = pattern[..marker];
            string suffix = pattern[(marker + 3)..];

            if (key.Length < prefix.Length + suffix.Length)
            {
                continue;
            }

            bool matches =
                key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
                key.EndsWith(suffix, StringComparison.OrdinalIgnoreCase);

            if (!matches)
            {
                continue;
            }

            string captured =
                key[prefix.Length..(key.Length - suffix.Length)];

            return replacement.Replace("{0}", captured);
        }

        return null;
    }

    private Dictionary<string, string>? GetTable(string language)
    {
        EnsureLoaded();

        lock (gate)
        {
            return dictionary.TryGetValue(language, out var table)
                ? table
                : null;
        }
    }

    private void EnsureLoaded()
    {
        string path = Path.Combine(
            environment.WebRootPath,
            "data",
            "translations.json");

        DateTime writeTimeUtc;

        try
        {
            if (!File.Exists(path))
            {
                logger.LogWarning(
                    "Translation dictionary not found at {Path} - " +
                    "everything stays in English.",
                    path);

                return;
            }

            writeTimeUtc = File.GetLastWriteTimeUtc(path);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not inspect the translation file.");
            return;
        }

        lock (gate)
        {
            bool loadedAlready = lastLoadedUtc != DateTime.MinValue;
            bool unchanged = writeTimeUtc == lastFileWriteUtc;

            if (loadedAlready && unchanged)
            {
                return;
            }

            try
            {
                string json = File.ReadAllText(path);

                var parsed = JsonSerializer
                    .Deserialize<Dictionary<string, JsonElement>>(json);

                Dictionary<string, Dictionary<string, string>> loaded =
                    new(StringComparer.OrdinalIgnoreCase);

                if (parsed is not null)
                {
                    foreach ((string key, JsonElement value) in parsed)
                    {
                        // Skip "_comment" and anything that is not an object.
                        if (value.ValueKind != JsonValueKind.Object)
                        {
                            continue;
                        }

                        // Case-insensitive so a label styled with
                        // text-transform: uppercase still matches.
                        Dictionary<string, string> table =
                            new(StringComparer.OrdinalIgnoreCase);

                        foreach (JsonProperty entry in value.EnumerateObject())
                        {
                            if (entry.Value.ValueKind == JsonValueKind.String)
                            {
                                table[entry.Name] =
                                    entry.Value.GetString() ?? "";
                            }
                        }

                        loaded[key] = table;
                    }
                }

                dictionary = loaded;
                lastFileWriteUtc = writeTimeUtc;
                lastLoadedUtc = DateTime.UtcNow;

                logger.LogInformation(
                    "Loaded translation dictionary ({Languages} languages).",
                    loaded.Count);
            }
            catch (Exception ex)
            {
                // Keep whatever was loaded before rather than wiping it.
                logger.LogError(
                    ex,
                    "Translation dictionary is not valid JSON - " +
                    "keeping the previous version.");

                lastFileWriteUtc = writeTimeUtc;
            }
        }
    }
}
