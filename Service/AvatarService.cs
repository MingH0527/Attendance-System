namespace WebAndMobile.Services;

public interface IAvatarService
{
    string ResolveAvatar(string userId, string? storedAvatar);
}

public sealed class AvatarService : IAvatarService
{
    private const string DefaultAvatarUrl = "/image/default.png";
    private readonly IWebHostEnvironment environment;

    public AvatarService(IWebHostEnvironment environment)
    {
        this.environment = environment;
    }

    public string ResolveAvatar(string userId, string? storedAvatar)
    {
        string value = (storedAvatar ?? string.Empty).Trim();

        if (value.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            value.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return value;
        }

        if (IsCustomAvatar(value))
        {
            string normalizedUrl = value.StartsWith("/", StringComparison.Ordinal)
                ? value
                : $"/uploads/avatars/{Path.GetFileName(value)}";

            if (StaticFileExists(normalizedUrl))
            {
                return normalizedUrl;
            }
        }

        string? recoveredAvatar = FindLatestAvatarForUser(userId);

        return recoveredAvatar ?? DefaultAvatarUrl;
    }

    private static bool IsCustomAvatar(string value)
    {
        return !string.IsNullOrWhiteSpace(value) &&
               !value.Equals("default.png", StringComparison.OrdinalIgnoreCase) &&
               !value.Equals("default-avatar.png", StringComparison.OrdinalIgnoreCase) &&
               !value.Equals(DefaultAvatarUrl, StringComparison.OrdinalIgnoreCase);
    }

    private bool StaticFileExists(string url)
    {
        if (!url.StartsWith("/", StringComparison.Ordinal))
        {
            return false;
        }

        string relativePath = url.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
        string fullPath = Path.Combine(environment.WebRootPath, relativePath);
        return File.Exists(fullPath);
    }

    private string? FindLatestAvatarForUser(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return null;
        }

        string avatarFolder = Path.Combine(environment.WebRootPath, "uploads", "avatars");

        if (!Directory.Exists(avatarFolder))
        {
            return null;
        }

        string safeUserId = Path.GetFileName(userId.Trim());
        string[] allowedExtensions = [".jpg", ".jpeg", ".png", ".webp"];

        FileInfo? latest = new DirectoryInfo(avatarFolder)
            .EnumerateFiles($"{safeUserId}_*", SearchOption.TopDirectoryOnly)
            .Where(file => allowedExtensions.Contains(file.Extension.ToLowerInvariant()))
            .OrderByDescending(file => file.LastWriteTimeUtc)
            .FirstOrDefault();

        return latest == null
            ? null
            : $"/uploads/avatars/{latest.Name}";
    }
}
