using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QRCoder;
using Stripe.Checkout;
using WebAndMobile.Models;

namespace WebAndMobile.Controllers;

[Authorize(Roles = "Student")]
public class PaymentController : Controller
{
    private const string PendingFeeIdKey = "QrPayment.Pending.FeeId";
    private const string PendingWalletKey = "QrPayment.Pending.Wallet";
    private const string PendingAmountKey = "QrPayment.Pending.Amount";
    private const string PublicBaseUrlKey = "QrPayment.PublicBaseUrl";

    private readonly DB db;
    private readonly IConfiguration configuration;
    private readonly IPasswordHasher<PaymentSecurity> pinHasher;

    public PaymentController(
        DB db,
        IConfiguration configuration,
        IPasswordHasher<PaymentSecurity> pinHasher)
    {
        this.db = db;
        this.configuration = configuration;
        this.pinHasher = pinHasher;
    }

    private static List<FeeItem> GetFeeItems() =>
    [
        new() { Id = 1, Name = "Tuition Fee", Description = "Monthly school tuition fee", Amount = 500.00m, Category = "Tuition", AmountIsFixed = true },
        new() { Id = 2, Name = "Library Fee", Description = "Library membership and maintenance fee", Amount = 50.00m, Category = "Library", AmountIsFixed = true },
        new() { Id = 3, Name = "Hostel Fee", Description = "Monthly student hostel fee", Amount = 300.00m, Category = "Hostel", AmountIsFixed = true },
        new() { Id = 4, Name = "Books and Materials", Description = "Purchase books and learning materials", Amount = 10.00m, Category = "Books", AmountIsFixed = false }
    ];

    [HttpGet]
    public IActionResult SelectFee() => View(GetFeeItems());

    [HttpGet]
    public async Task<IActionResult> Pay(int feeId)
    {
        FeeItem? fee = GetFeeItems().FirstOrDefault(f => f.Id == feeId);
        if (fee == null) return NotFound();

        int userId = GetCurrentUserId();

        ViewBag.HasFaceId = await db.UserFaceProfiles
            .AnyAsync(p => p.UserId == userId && p.IsActive);

        ViewBag.HasPaymentPin = await db.PaymentSecurities
            .AnyAsync(p => p.UserId == userId && p.PinEnabled);

        ViewBag.PublicBaseUrl = HttpContext.Session.GetString(PublicBaseUrlKey) ?? "";
        ViewBag.PublishableKey = configuration["Stripe:PublishableKey"];
        ViewBag.Message = TempData["Message"];
        ViewBag.Error = TempData["Error"];

        return View(fee);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateCheckoutSession(int feeId, decimal? customAmount)
    {
        FeeItem? selectedFee = GetFeeItems().FirstOrDefault(f => f.Id == feeId);
        if (selectedFee == null) return NotFound();

        decimal finalAmount;

        if (selectedFee.AmountIsFixed)
        {
            finalAmount = selectedFee.Amount;
        }
        else if (!customAmount.HasValue || customAmount.Value <= 0)
        {
            TempData["Error"] = "Please enter a valid payment amount.";
            return RedirectToAction(nameof(Pay), new { feeId });
        }
        else
        {
            finalAmount = customAmount.Value;
        }

        long amountInSen = Convert.ToInt64(decimal.Round(finalAmount * 100, 0, MidpointRounding.AwayFromZero));
        string domain = $"{Request.Scheme}://{Request.Host}";

        SessionCreateOptions options = new()
        {
            Mode = "payment",
            SuccessUrl = $"{domain}/Payment/StripeSuccess?session_id={{CHECKOUT_SESSION_ID}}",
            CancelUrl = $"{domain}/Payment/Cancel",
            LineItems =
            [
                new SessionLineItemOptions
                {
                    Quantity = 1,
                    PriceData = new SessionLineItemPriceDataOptions
                    {
                        Currency = "myr",
                        UnitAmount = amountInSen,
                        ProductData = new SessionLineItemPriceDataProductDataOptions
                        {
                            Name = selectedFee.Name,
                            Description = selectedFee.Description
                        }
                    }
                }
            ],
            Metadata = new Dictionary<string, string>
            {
                ["FeeId"] = selectedFee.Id.ToString(),
                ["FeeName"] = selectedFee.Name,
                ["UserId"] = GetCurrentUserId().ToString()
            }
        };

        try
        {
            Session session = await new SessionService().CreateAsync(options);

            if (string.IsNullOrWhiteSpace(session.Url))
            {
                TempData["Error"] = "Stripe did not return a checkout URL.";
                return RedirectToAction(nameof(Pay), new { feeId });
            }

            return Redirect(session.Url);
        }
        catch (Stripe.StripeException ex)
        {
            TempData["Error"] = $"Stripe error: {ex.Message}";
            return RedirectToAction(nameof(Pay), new { feeId });
        }
    }

    [HttpGet]
    public async Task<IActionResult> StripeSuccess(string? session_id)
    {
        if (string.IsNullOrWhiteSpace(session_id)) return RedirectToAction(nameof(SelectFee));

        string cleanSessionId = session_id.Trim('{', '}', ' ', '"');
        if (!cleanSessionId.StartsWith("cs_", StringComparison.OrdinalIgnoreCase))
            return BadRequest("Invalid Stripe Checkout Session ID.");

        try
        {
            Session session = await new SessionService().GetAsync(cleanSessionId);
            if (!string.Equals(session.PaymentStatus, "paid", StringComparison.OrdinalIgnoreCase))
                return RedirectToAction(nameof(Cancel));

            ViewBag.SessionId = session.Id;
            ViewBag.PaymentStatus = session.PaymentStatus;
            ViewBag.CustomerEmail = session.CustomerDetails?.Email;
            ViewBag.AmountTotal = session.AmountTotal.HasValue ? session.AmountTotal.Value / 100m : 0m;
            ViewBag.Currency = session.Currency?.ToUpperInvariant();
            ViewBag.FeeName = session.Metadata.TryGetValue("FeeName", out string? feeName) ? feeName : "Student Fee";

            return View(session);
        }
        catch (Stripe.StripeException ex)
        {
            return BadRequest($"Unable to retrieve Stripe payment: {ex.Message}");
        }
    }

    [HttpGet]
    public IActionResult Cancel() => View();

    [HttpGet]
    public async Task<IActionResult> SetPin(int returnFeeId)
    {
        int userId = GetCurrentUserId();
        bool exists = await db.PaymentSecurities.AnyAsync(p => p.UserId == userId && p.PinEnabled);

        if (exists)
        {
            TempData["Message"] = "Your payment PIN has already been created.";
            return RedirectToAction(nameof(Pay), new { feeId = returnFeeId });
        }

        return View(new SetPaymentPinViewModel { ReturnFeeId = returnFeeId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetPin(SetPaymentPinViewModel model)
    {
        int userId = GetCurrentUserId();
        bool exists = await db.PaymentSecurities.AnyAsync(p => p.UserId == userId && p.PinEnabled);

        if (exists)
        {
            TempData["Error"] = "The payment PIN can only be created once.";
            return RedirectToAction(nameof(Pay), new { feeId = model.ReturnFeeId });
        }

        if (model.Pin is "000000" or "111111" or "123456" or "654321")
            ModelState.AddModelError(nameof(model.Pin), "This PIN is too easy to guess.");

        if (!ModelState.IsValid) return View(model);

        PaymentSecurity security = new()
        {
            UserId = userId,
            PinEnabled = true,
            CreatedAt = DateTime.UtcNow
        };

        security.PinHash = pinHasher.HashPassword(security, model.Pin);
        db.PaymentSecurities.Add(security);
        await db.SaveChangesAsync();

        TempData["Message"] = "Payment PIN created successfully.";
        return RedirectToAction(nameof(Pay), new { feeId = model.ReturnFeeId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BeginQrPayment(
        int feeId,
        string paymentMethod,
        decimal? customAmount,
        string publicBaseUrl)
    {
        int userId = GetCurrentUserId();
        FeeItem? fee = GetFeeItems().FirstOrDefault(f => f.Id == feeId);
        if (fee == null) return NotFound();

        string[] allowedMethods = ["TouchNGo", "GrabPay", "Boost"];
        if (!allowedMethods.Contains(paymentMethod))
        {
            TempData["Error"] = "Please select a supported e-wallet.";
            return RedirectToAction(nameof(Pay), new { feeId });
        }

        decimal amount = fee.AmountIsFixed ? fee.Amount : customAmount.GetValueOrDefault();
        if (amount <= 0)
        {
            TempData["Error"] = "Please enter a valid payment amount.";
            return RedirectToAction(nameof(Pay), new { feeId });
        }

        if (!TryNormalizePublicBaseUrl(publicBaseUrl, out string normalizedUrl))
        {
            TempData["Error"] = "Paste a valid Cloudflare HTTPS URL, for example https://name.trycloudflare.com.";
            return RedirectToAction(nameof(Pay), new { feeId });
        }

        HttpContext.Session.SetInt32(PendingFeeIdKey, feeId);
        HttpContext.Session.SetString(PendingWalletKey, paymentMethod);
        HttpContext.Session.SetString(PendingAmountKey, amount.ToString(System.Globalization.CultureInfo.InvariantCulture));
        HttpContext.Session.SetString(PublicBaseUrlKey, normalizedUrl);

        bool hasPin = await db.PaymentSecurities.AnyAsync(p => p.UserId == userId && p.PinEnabled);
        if (!hasPin)
        {
            return RedirectToAction(nameof(SetPin), new { returnFeeId = feeId });
        }

        bool hasFace = await db.UserFaceProfiles.AnyAsync(p => p.UserId == userId && p.IsActive);
        if (!hasFace)
        {
            TempData["Message"] = "Please register Face ID before using QR payment.";
            return RedirectToAction("Register", "FaceId", new
            {
                returnFeeId = feeId,
                returnUrl = Url.Action(nameof(Pay), "Payment", new { feeId })
            });
        }

        return RedirectToAction("Verify", "FaceId", new
        {
            feeId,
            returnUrl = Url.Action(nameof(CompleteQrPayment), "Payment")
        });
    }

    [HttpGet]
    public async Task<IActionResult> CompleteQrPayment()
    {
        if (!FaceIdController.HasValidPaymentFaceVerification(HttpContext))
        {
            TempData["Error"] = "Face verification expired. Please scan your face again.";
            int feeId = HttpContext.Session.GetInt32(PendingFeeIdKey) ?? 0;
            return RedirectToAction(nameof(Pay), new { feeId });
        }

        int? feeIdValue = HttpContext.Session.GetInt32(PendingFeeIdKey);
        string? paymentMethod = HttpContext.Session.GetString(PendingWalletKey);
        string? amountText = HttpContext.Session.GetString(PendingAmountKey);

        if (!feeIdValue.HasValue ||
            string.IsNullOrWhiteSpace(paymentMethod) ||
            !decimal.TryParse(amountText, System.Globalization.NumberStyles.Number,
                System.Globalization.CultureInfo.InvariantCulture, out decimal amount))
        {
            FaceIdController.ConsumePaymentFaceVerification(HttpContext);
            TempData["Error"] = "The QR payment request expired. Please start again.";
            return RedirectToAction(nameof(SelectFee));
        }

        FeeItem? fee = GetFeeItems().FirstOrDefault(f => f.Id == feeIdValue.Value);
        if (fee == null) return NotFound();

        PaymentTransaction transaction = new()
        {
            UserId = GetCurrentUserId(),
            FeeId = fee.Id,
            FeeName = fee.Name,
            Amount = amount,
            PaymentMethod = paymentMethod,
            Status = "Pending",
            QrToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)),
            QrExpiresAt = DateTime.UtcNow.AddMinutes(5),
            CreatedAt = DateTime.UtcNow
        };

        db.PaymentTransactions.Add(transaction);
        await db.SaveChangesAsync();

        FaceIdController.ConsumePaymentFaceVerification(HttpContext);
        ClearPendingQrRequest();

        return RedirectToAction(nameof(Qr), new { id = transaction.PaymentTransactionId });
    }

    [HttpGet]
    public async Task<IActionResult> Qr(int id)
    {
        int userId = GetCurrentUserId();

        PaymentTransaction? transaction = await db.PaymentTransactions
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.PaymentTransactionId == id && p.UserId == userId);

        if (transaction == null) return NotFound();

        string? publicBaseUrl = HttpContext.Session.GetString(PublicBaseUrlKey);
        if (!TryNormalizePublicBaseUrl(publicBaseUrl, out string normalizedUrl))
        {
            TempData["Error"] = "Cloudflare public URL is missing. Return to the payment page and paste it again.";
            return RedirectToAction(nameof(Pay), new { feeId = transaction.FeeId });
        }

        string mobileUrl = $"{normalizedUrl}/Payment/MobilePay?token={Uri.EscapeDataString(transaction.QrToken)}";

        using QRCodeGenerator generator = new();
        using QRCodeData data = generator.CreateQrCode(mobileUrl, QRCodeGenerator.ECCLevel.Q);
        PngByteQRCode qrCode = new(data);
        byte[] png = qrCode.GetGraphic(12);

        ViewBag.QrDataUrl = "data:image/png;base64," + Convert.ToBase64String(png);
        ViewBag.MobileUrl = mobileUrl;
        ViewBag.PublicBaseUrl = normalizedUrl;

        return View(transaction);
    }

    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> MobilePay(string token)
    {
        PaymentTransaction? transaction = await db.PaymentTransactions
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.QrToken == token);

        if (transaction == null) return NotFound();

        if (transaction.Status == "Paid") return View("MobileAlreadyPaid", transaction);
        if (transaction.QrExpiresAt <= DateTime.UtcNow || transaction.Status == "Expired")
            return View("MobileExpired", transaction);

        ViewBag.Error = TempData["MobileError"];
        return View(transaction);
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ConfirmMobilePayment(string token, string pin)
    {
        PaymentTransaction? transaction = await db.PaymentTransactions
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.QrToken == token);

        if (transaction == null) return NotFound();
        if (transaction.Status == "Paid") return RedirectToAction(nameof(MobilePay), new { token });

        if (transaction.QrExpiresAt <= DateTime.UtcNow || transaction.Status == "Expired")
        {
            await db.PaymentTransactions
                .Where(p => p.QrToken == token && p.Status == "Pending")
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.Status, "Expired"));

            return View("MobileExpired", transaction);
        }

        PaymentSecurity? security = await db.PaymentSecurities
            .FirstOrDefaultAsync(p => p.UserId == transaction.UserId && p.PinEnabled);

        if (security == null)
        {
            TempData["MobileError"] = "Payment PIN is not available for this account.";
            return RedirectToAction(nameof(MobilePay), new { token });
        }

        if (security.LockoutEnd.HasValue && security.LockoutEnd.Value > DateTime.UtcNow)
        {
            TempData["MobileError"] = "Payment PIN is temporarily locked. Please try again later.";
            return RedirectToAction(nameof(MobilePay), new { token });
        }

        PasswordVerificationResult result = pinHasher.VerifyHashedPassword(
            security,
            security.PinHash,
            (pin ?? string.Empty).Trim());

        if (result == PasswordVerificationResult.Failed)
        {
            security.FailedAttempts++;

            if (security.FailedAttempts >= 4)
            {
                security.LockoutEnd = DateTime.UtcNow.AddMinutes(15);
                security.FailedAttempts = 0;
            }

            await db.SaveChangesAsync();

            TempData["MobileError"] = security.LockoutEnd.HasValue
                ? "Too many incorrect attempts. Payment PIN is locked for 15 minutes."
                : $"Incorrect PIN. {4 - security.FailedAttempts} attempt(s) remaining.";

            return RedirectToAction(nameof(MobilePay), new { token });
        }

        DateTime paidAt = DateTime.UtcNow;

        int updated = await db.PaymentTransactions
            .Where(p =>
                p.QrToken == token &&
                p.Status == "Pending" &&
                p.QrExpiresAt > paidAt)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(p => p.Status, "Paid")
                .SetProperty(p => p.PaidAt, (DateTime?)paidAt));

        security.FailedAttempts = 0;
        security.LockoutEnd = null;
        await db.SaveChangesAsync();

        if (updated == 0)
        {
            return RedirectToAction(nameof(MobilePay), new { token });
        }

        // Load the final values for the success view.
        PaymentTransaction? finalTransaction = await db.PaymentTransactions
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.QrToken == token);

        return View("MobileSuccess", finalTransaction ?? transaction);
    }

    [HttpGet]
    public async Task<IActionResult> Status(int id)
    {
        int userId = GetCurrentUserId();

        PaymentTransaction? transaction = await db.PaymentTransactions
            .FirstOrDefaultAsync(p => p.PaymentTransactionId == id && p.UserId == userId);

        if (transaction == null) return NotFound();

        if (transaction.Status == "Pending" && transaction.QrExpiresAt <= DateTime.UtcNow)
        {
            transaction.Status = "Expired";
            await db.SaveChangesAsync();
        }

        return Json(new
        {
            status = transaction.Status,
            paidAt = transaction.PaidAt?.ToString("O")
        });
    }

    [HttpGet]
    public async Task<IActionResult> Success(int id)
    {
        int userId = GetCurrentUserId();

        PaymentTransaction? transaction = await db.PaymentTransactions
            .AsNoTracking()
            .FirstOrDefaultAsync(p =>
                p.PaymentTransactionId == id &&
                p.UserId == userId &&
                p.Status == "Paid");

        return transaction == null ? NotFound() : View(transaction);
    }

    private static bool TryNormalizePublicBaseUrl(string? value, out string normalized)
    {
        normalized = "";
        string candidate = (value ?? "").Trim().TrimEnd('/');

        if (!Uri.TryCreate(candidate, UriKind.Absolute, out Uri? uri) ||
            uri.Scheme != Uri.UriSchemeHttps ||
            string.IsNullOrWhiteSpace(uri.Host))
        {
            return false;
        }

        normalized = uri.GetLeftPart(UriPartial.Authority);
        return true;
    }

    private void ClearPendingQrRequest()
    {
        HttpContext.Session.Remove(PendingFeeIdKey);
        HttpContext.Session.Remove(PendingWalletKey);
        HttpContext.Session.Remove(PendingAmountKey);
    }

    private int GetCurrentUserId()
    {
        string? value = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!int.TryParse(value, out int userId))
            throw new UnauthorizedAccessException("The login session is invalid.");

        return userId;
    }
}
