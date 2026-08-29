using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebAndMobile.Models;
using WebAndMobile.Services;

namespace WebAndMobile.Controllers;

[Authorize(Roles = "Student")]
public class FaceIdController : Controller
{
    private const string VerificationCountKey = "PaymentFace.VerificationCount";
    private const string VerifiedUntilKey = "PaymentFace.VerifiedUntil";
    private const int RequiredSuccessfulFrames = 5;
    private const double MatchThreshold = 0.42;

    private readonly DB db;
    private readonly IFaceRecognitionService faceRecognition;

    public FaceIdController(DB db, IFaceRecognitionService faceRecognition)
    {
        this.db = db;
        this.faceRecognition = faceRecognition;
    }

    [HttpGet]
    public async Task<IActionResult> Manage(int? returnFeeId = null)
    {
        string userId = GetCurrentUserId();

        UserFaceProfile? profile = await db.UserFaceProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.UserId == userId && p.IsActive);

        ViewBag.HasFaceProfile = profile != null;
        ViewBag.RegisteredAt = profile?.RegisteredAt;
        ViewBag.ReturnFeeId = returnFeeId;
        ViewBag.Error = TempData["Error"];
        ViewBag.Message = TempData["Message"];

        return View();
    }

    [HttpGet]
    public async Task<IActionResult> Register(int? returnFeeId = null, string? returnUrl = null)
    {
        string userId = GetCurrentUserId();

        bool alreadyRegistered = await db.UserFaceProfiles
            .AnyAsync(p => p.UserId == userId && p.IsActive);

        if (alreadyRegistered)
        {
            TempData["Message"] = "Face ID has already been registered for this account.";

            if (IsSafeLocalUrl(returnUrl))
            {
                return LocalRedirect(returnUrl!);
            }

            return RedirectToAction(nameof(Manage), new { returnFeeId });
        }

        ViewBag.ReturnFeeId = returnFeeId;
        ViewBag.ReturnUrl = IsSafeLocalUrl(returnUrl)
            ? returnUrl
            : returnFeeId.HasValue
                ? Url.Action("Pay", "Payment", new { feeId = returnFeeId })
                : Url.Action(nameof(Manage));

        ViewBag.Message = TempData["Message"];
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RegisterFrame([FromBody] FaceFrameRequest request)
    {
        string userId = GetCurrentUserId();

        bool alreadyRegistered = await db.UserFaceProfiles
            .AnyAsync(p => p.UserId == userId && p.IsActive);

        if (alreadyRegistered)
        {
            return Conflict(new
            {
                success = false,
                message = "Face ID has already been registered and cannot be replaced here."
            });
        }

        if (!request.IsStable)
        {
            return BadRequest(new
            {
                success = false,
                code = "MOVING",
                message = "Please keep your face still."
            });
        }

        FaceAnalysisResult analysis = faceRecognition.Analyze(request.ImageDataUrl);

        if (!analysis.Success || analysis.Embedding == null)
        {
            return BadRequest(new
            {
                success = false,
                faceCount = analysis.FaceCount,
                code = analysis.FaceCount > 1 ? "MULTIPLE_FACES" : "NO_FACE",
                message = analysis.Message
            });
        }

        UserFaceProfile profile = new()
        {
            UserFaceProfileId = await FormattedIdGenerator.NextAsync(
                db.UserFaceProfiles,
                p => p.UserFaceProfileId,
                "FCE"
            ),
            UserId = userId,
            EmbeddingJson = JsonSerializer.Serialize(analysis.Embedding),
            RegisteredAt = DateTime.UtcNow,
            IsActive = true
        };

        db.UserFaceProfiles.Add(profile);
        await db.SaveChangesAsync();

        return Json(new
        {
            success = true,
            message = "Face ID registered successfully."
        });
    }

    [HttpGet]
    public async Task<IActionResult> Verify(int? feeId = null, string? returnUrl = null)
    {
        string userId = GetCurrentUserId();

        bool hasFace = await db.UserFaceProfiles
            .AnyAsync(p => p.UserId == userId && p.IsActive);

        if (!hasFace)
        {
            TempData["Message"] = "Please register Face ID before using QR payment.";
            return RedirectToAction(nameof(Register), new { returnFeeId = feeId, returnUrl });
        }

        HttpContext.Session.Remove(VerificationCountKey);
        HttpContext.Session.Remove(VerifiedUntilKey);

        ViewBag.FeeId = feeId;
        ViewBag.ReturnUrl = IsSafeLocalUrl(returnUrl)
            ? returnUrl
            : feeId.HasValue
                ? Url.Action("Pay", "Payment", new { feeId })
                : Url.Action("SelectFee", "Payment");

        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> VerifyFrame([FromBody] FaceFrameRequest request)
    {
        string userId = GetCurrentUserId();

        if (!request.IsStable)
        {
            int previous = HttpContext.Session.GetInt32(VerificationCountKey) ?? 0;
            int reduced = Math.Max(0, previous - 2);
            HttpContext.Session.SetInt32(VerificationCountKey, reduced);

            return BadRequest(new
            {
                success = false,
                progress = reduced * 100 / RequiredSuccessfulFrames,
                code = "MOVING",
                message = "Movement detected. Please keep your face still."
            });
        }

        UserFaceProfile? profile = await db.UserFaceProfiles
            .FirstOrDefaultAsync(p => p.UserId == userId && p.IsActive);

        if (profile == null)
        {
            return NotFound(new
            {
                success = false,
                redirectUrl = Url.Action(nameof(Register)),
                message = "No Face ID is registered for this account."
            });
        }

        FaceAnalysisResult analysis = faceRecognition.Analyze(request.ImageDataUrl);

        if (!analysis.Success || analysis.Embedding == null)
        {
            int previous = HttpContext.Session.GetInt32(VerificationCountKey) ?? 0;
            int reduced = Math.Max(0, previous - 2);
            HttpContext.Session.SetInt32(VerificationCountKey, reduced);

            return BadRequest(new
            {
                success = false,
                progress = reduced * 100 / RequiredSuccessfulFrames,
                faceCount = analysis.FaceCount,
                code = analysis.FaceCount > 1 ? "MULTIPLE_FACES" : "NO_FACE",
                message = analysis.Message
            });
        }

        double similarity = faceRecognition.Compare(profile.EmbeddingJson, analysis.Embedding);

        if (similarity < MatchThreshold)
        {
            HttpContext.Session.SetInt32(VerificationCountKey, 0);

            return BadRequest(new
            {
                success = false,
                progress = 0,
                code = "FACE_MISMATCH",
                similarity = Math.Round(similarity * 100, 1),
                message = "This face does not match the current account."
            });
        }

        int count = Math.Min(
            RequiredSuccessfulFrames,
            (HttpContext.Session.GetInt32(VerificationCountKey) ?? 0) + 1);

        HttpContext.Session.SetInt32(VerificationCountKey, count);

        int progress = count * 100 / RequiredSuccessfulFrames;
        bool verified = count >= RequiredSuccessfulFrames;

        if (verified)
        {
            DateTime verifiedUntil = DateTime.UtcNow.AddMinutes(2);
            HttpContext.Session.SetString(VerifiedUntilKey, verifiedUntil.ToString("O"));

            profile.LastVerifiedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }

        return Json(new
        {
            success = true,
            verified,
            progress,
            similarity = Math.Round(similarity * 100, 1),
            message = verified
                ? "Face verification successful."
                : "Identity matched. Please remain still."
        });
    }

    public static bool HasValidPaymentFaceVerification(HttpContext httpContext)
    {
        string? value = httpContext.Session.GetString(VerifiedUntilKey);

        return DateTime.TryParse(value, out DateTime verifiedUntil) &&
               verifiedUntil.ToUniversalTime() > DateTime.UtcNow;
    }

    public static void ConsumePaymentFaceVerification(HttpContext httpContext)
    {
        httpContext.Session.Remove(VerificationCountKey);
        httpContext.Session.Remove(VerifiedUntilKey);
    }

    private bool IsSafeLocalUrl(string? url) =>
        !string.IsNullOrWhiteSpace(url) && Url.IsLocalUrl(url);

    private string GetCurrentUserId()
    {
        string? userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new UnauthorizedAccessException(
                "The login session is invalid."
            );
        }

        return userId;
    }
}
