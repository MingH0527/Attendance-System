using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QRCoder;
using WebAndMobile.Configuration;
using WebAndMobile.Models;
using WebAndMobile.Services;

namespace WebAndMobile.Controllers;

public class PasskeyDeviceBindingController : Controller
{
    private const int MaximumAccountsPerDevice = 4;

    private const string VerifiedBindingSessionKey =
        "Passkey.VerifiedPhoneDeviceIdentifier";

    private readonly DB db;

    public PasskeyDeviceBindingController(DB db)
    {
        this.db = db;
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Start()
    {
        string? userId = GetCurrentUserId();

        if (userId == null)
        {
            return Unauthorized(new
            {
                success = false,
                message = "Your login session has expired."
            });
        }

        if (!Cloudflare.IsConfigured())
        {
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new
                {
                    success = false,
                    code = "CLOUDFLARE_NOT_AVAILABLE",
                    message =
                        "Phone or tablet verification is unavailable. " +
                        "Start Cloudflare Tunnel and update " +
                        "Configuration/Cloudflare.cs, then try again."
                });
        }

        HttpContext.Session.Remove(
            VerifiedBindingSessionKey
        );

        DateTime now = DateTime.UtcNow;

        List<PasskeyDeviceBinding> oldPending =
            await db.PasskeyDeviceBindings
                .Where(
                    x =>
                        x.UserId == userId &&
                        x.Status == "Pending")
                .ToListAsync();

        foreach (PasskeyDeviceBinding item in oldPending)
        {
            item.Status = "Cancelled";
        }

        string token =
            Convert.ToHexString(
                RandomNumberGenerator.GetBytes(32)
            ).ToLowerInvariant();

        PasskeyDeviceBinding binding = new()
        {
            PasskeyDeviceBindingId = await FormattedIdGenerator.NextAsync(
                db.PasskeyDeviceBindings,
                p => p.PasskeyDeviceBindingId,
                "PDB"
            ),
            BindingToken = token,
            UserId = userId,
            Status = "Pending",
            CreatedAt = now,
            ExpiresAt = now.AddMinutes(5)
        };

        db.PasskeyDeviceBindings.Add(binding);
        await db.SaveChangesAsync();

        string baseUrl =
            Cloudflare.GetPublicUrl();

        string bindingUrl =
            $"{baseUrl}/PasskeyDeviceBinding/Bind" +
            $"?token={Uri.EscapeDataString(token)}";

        string qrUrl =
            $"{baseUrl}/PasskeyDeviceBinding/Qr" +
            $"?token={Uri.EscapeDataString(token)}";

        return Json(new
        {
            success = true,
            token,
qrUrl,
            expiresInSeconds = 300
        });
    }

    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> Bind(string token)
    {
        PasskeyDeviceBinding? binding =
            await FindValidPendingBinding(token);

        if (binding == null)
        {
            ViewBag.BindingError =
                "This device binding link is invalid or has expired.";
        }

        ViewBag.BindingToken = token ?? "";

        return View("~/Views/MyProfile/BindPasskeyDevice.cshtml");
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CompleteBinding(
        string token,
        string deviceIdentifier)
    {
        PasskeyDeviceBinding? binding =
            await FindValidPendingBinding(token);

        if (binding == null)
        {
            return BadRequest(new
            {
                success = false,
                message =
                    "This device binding request is invalid or has expired."
            });
        }

        string normalised =
            NormaliseDeviceIdentifier(
                deviceIdentifier
            );

        if (string.IsNullOrWhiteSpace(normalised))
        {
            return BadRequest(new
            {
                success = false,
                message =
                    "The phone or tablet device identity is invalid."
            });
        }

        bool sameAccountAlreadyUsesDevice =
            await db.UserPasskeys.AnyAsync(
                p =>
                    p.UserId == binding.UserId &&
                    p.DeviceIdentifier == normalised
            );

        if (sameAccountAlreadyUsesDevice)
        {
            return Conflict(new
            {
                success = false,
                code = "DEVICE_ALREADY_REGISTERED",
                message =
                    "This phone or tablet already has a Passkey for this account."
            });
        }

        int accountCount =
            await db.UserPasskeys
                .Where(
                    p =>
                        p.DeviceIdentifier ==
                        normalised)
                .Select(p => p.UserId)
                .Distinct()
                .CountAsync();

        if (accountCount >= MaximumAccountsPerDevice)
        {
            binding.Status = "Blocked";
            binding.DeviceIdentifier = normalised;
            await db.SaveChangesAsync();

            return StatusCode(
                StatusCodes.Status429TooManyRequests,
                new
                {
                    success = false,
                    code = "DEVICE_ACCOUNT_LIMIT_REACHED",
                    message =
                        "This phone or tablet is already linked to 4 accounts. " +
                        "Delete an unwanted Passkey association before registering another account."
                });
        }

        binding.DeviceIdentifier = normalised;
        binding.Status = "Verified";
        binding.VerifiedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();

        string? warningMessage = accountCount switch
        {
            2 =>
                "Device verified. After this registration, only 1 account slot will remain.",
            3 =>
                "Device verified. This is the final available account slot on this device.",
            _ => null
        };

        return Json(new
        {
            success = true,
            accountCount,
            maximumAccounts =
                MaximumAccountsPerDevice,
            warningMessage,
            message =
                "Device verified. Return to the computer to continue Passkey registration."
        });
    }

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> Status(string token)
    {
        string? userId = GetCurrentUserId();

        if (userId == null)
        {
            return Unauthorized(new
            {
                success = false,
                message = "Your login session has expired."
            });
        }

        PasskeyDeviceBinding? binding =
            await db.PasskeyDeviceBindings
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    x =>
                        x.BindingToken == token &&
                        x.UserId == userId
                );

        if (binding == null)
        {
            return NotFound(new
            {
                success = false,
                message = "Binding request not found."
            });
        }

        if (binding.ExpiresAt <= DateTime.UtcNow &&
            binding.Status == "Pending")
        {
            return Json(new
            {
                success = true,
                status = "Expired"
            });
        }

        if (binding.Status == "Verified" &&
            !string.IsNullOrWhiteSpace(
                binding.DeviceIdentifier))
        {
            HttpContext.Session.SetString(
                VerifiedBindingSessionKey,
                binding.DeviceIdentifier
            );

            return Json(new
            {
                success = true,
                status = "Verified"
            });
        }

        return Json(new
        {
            success = true,
            status = binding.Status
        });
    }

    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> Qr(string token)
    {
        if (!Cloudflare.IsConfigured())
        {
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                "Cloudflare public URL is not configured."
            );
        }

        PasskeyDeviceBinding? binding =
            await FindValidPendingBinding(token);

        if (binding == null)
        {
            return NotFound();
        }

        string baseUrl =
            Cloudflare.GetPublicUrl();

        string bindingUrl =
            $"{baseUrl}/PasskeyDeviceBinding/Bind" +
            $"?token={Uri.EscapeDataString(token)}";

        using QRCodeGenerator generator = new();

        QRCodeData data =
            generator.CreateQrCode(
                bindingUrl,
                QRCodeGenerator.ECCLevel.Q
            );

        PngByteQRCode qrCode =
            new(data);

        byte[] bytes =
            qrCode.GetGraphic(12);

        return File(
            bytes,
            "image/png"
        );
    }

    private async Task<PasskeyDeviceBinding?>
        FindValidPendingBinding(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        return await db.PasskeyDeviceBindings
            .FirstOrDefaultAsync(
                x =>
                    x.BindingToken == token &&
                    x.Status == "Pending" &&
                    x.ExpiresAt > DateTime.UtcNow
            );
    }

    private string? GetCurrentUserId()
    {
        return User.FindFirst(
            System.Security.Claims.ClaimTypes.NameIdentifier
        )?.Value;
    }

    private static string NormaliseDeviceIdentifier(
        string? value)
    {
        string cleaned =
            (value ?? "")
                .Trim()
                .Replace("-", "")
                .ToLowerInvariant();

        if (cleaned.Length != 32 ||
            !cleaned.All(
                character =>
                    char.IsAsciiHexDigit(
                        character)))
        {
            return "";
        }

        return cleaned;
    }
}
