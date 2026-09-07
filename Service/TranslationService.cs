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
            string key = (texts[i] ?? "").Trim();

            if (key.Length == 0)
            {
                continue;
            }

            if (table.TryGetValue(key, out string? value) &&
                !string.IsNullOrWhiteSpace(value))
            {
                results[i] = value;
            }
        }

        return Task.FromResult(results);
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

                        Dictionary<string, string> table =
                            new(StringComparer.Ordinal);

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
