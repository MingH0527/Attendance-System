using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebAndMobile.Models;
using WebAndMobile.Services;

namespace WebAndMobile.Controllers;

/// <summary>
/// Backs the language dropdown in the student portal.
/// Student-only: the admin portal has no language switcher.
/// </summary>
[Authorize(Roles = "Student")]
public class LanguageController : Controller
{
    // Guard rail so one page cannot post an unbounded batch.
    private const int MaxSegmentsPerCall = 300;

    private readonly DB db;
    private readonly ITranslationService translationService;

    public LanguageController(
        DB db,
        ITranslationService translationService)
    {
        this.db = db;
        this.translationService = translationService;
    }

    /// <summary>
    /// Saves the chosen language against the signed-in student only.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Set(
        [FromBody] SetLanguageRequest? request)
    {
        if (request is null ||
            !translationService.IsSupported(request.Language))
        {
            return BadRequest(new { error = "Unsupported language." });
        }

        string? userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        User? user = await db.Users
            .FirstOrDefaultAsync(u =>
                u.UserId == userId &&
                !u.IsDeleted);

        if (user is null)
        {
            return Unauthorized();
        }

        user.PreferredLanguage =
            request.Language!.Trim().ToLowerInvariant();

        await db.SaveChangesAsync();

        return Json(new { language = user.PreferredLanguage });
    }

    /// <summary>
    /// Translates a batch of on-screen strings into the target language.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Translate(
        [FromBody] TranslateRequest? request)
    {
        if (request?.Texts is null || request.Texts.Count == 0)
        {
            return Json(new { translations = Array.Empty<string>() });
        }

        if (request.Texts.Count > MaxSegmentsPerCall)
        {
            return BadRequest(new { error = "Too many segments." });
        }

        List<string> translations =
            await translationService.TranslateAsync(
                request.Texts,
                request.Language ?? "en",
                HttpContext.RequestAborted);

        return Json(new { translations });
    }
}

public class SetLanguageRequest
{
    public string? Language { get; set; }
}

public class TranslateRequest
{
    public string? Language { get; set; }

    public List<string>? Texts { get; set; }
}
