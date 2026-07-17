using System.Security.Claims;
using Fido2NetLib;
using Fido2NetLib.Objects;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebAndMobile.Models;

namespace WebAndMobile.Controllers;

public class PasskeyController : Controller
{
    private const string AssertionOptionsSessionKey =
        "Passkey.LoginAssertionOptions";

    private const string PendingAdminUserIdKey =
        "AdminPin.PendingUserId";

    private const string PendingAdminPasskeyKey =
        "AdminPin.PasskeyVerified";

    private const string PendingAdminExpiryKey =
        "AdminPin.Expiry";

    private readonly DB db;

    private readonly IFido2 fido2;

    private readonly IPasswordHasher<AdminSecurity>
        adminPinHasher;

    public PasskeyController(
        DB db,
        IFido2 fido2,
        IPasswordHasher<AdminSecurity>
            adminPinHasher)
    {
        this.db = db;
        this.fido2 = fido2;
        this.adminPinHasher = adminPinHasher;
    }

    // ==================================================
    // CREATE PASSKEY LOGIN OPTIONS
    // ==================================================

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult>
        CreateLoginOptions()
    {
        bool hasAnyPasskey =
            await db.UserPasskeys.AnyAsync();

        if (!hasAnyPasskey)
        {
            return BadRequest(new
            {
                success = false,

                message =
                    "No passkeys have been registered " +
                    "in this system."
            });
        }

        AssertionOptions options =
            fido2.GetAssertionOptions(
                new GetAssertionOptionsParams
                {
                    AllowedCredentials =
                        new List<
                            PublicKeyCredentialDescriptor>(),

                    UserVerification =
                        UserVerificationRequirement
                            .Required
                }
            );

        HttpContext.Session.SetString(
            AssertionOptionsSessionKey,
            options.ToJson()
        );

        return Json(options);
    }

    // ==================================================
    // COMPLETE PASSKEY LOGIN
    // ==================================================

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CompleteLogin(
        [FromBody]
        AuthenticatorAssertionRawResponse
            clientResponse,

        CancellationToken cancellationToken)
    {
        string? optionsJson =
            HttpContext.Session.GetString(
                AssertionOptionsSessionKey
            );

        if (string.IsNullOrWhiteSpace(
                optionsJson))
        {
            return BadRequest(new
            {
                success = false,

                message =
                    "Passkey login request expired. " +
                    "Please try again."
            });
        }

        HttpContext.Session.Remove(
            AssertionOptionsSessionKey
        );

        try
        {
            AssertionOptions originalOptions =
                AssertionOptions.FromJson(
                    optionsJson
                );

            UserPasskey? storedPasskey =
                await db.UserPasskeys
                    .Include(p => p.User)
                    .FirstOrDefaultAsync(
                        p =>
                            p.CredentialId ==
                            clientResponse.RawId,

                        cancellationToken
                    );

            if (storedPasskey == null)
            {
                return Unauthorized(new
                {
                    success = false,

                    message =
                        "This passkey is not registered " +
                        "in the system."
                });
            }

            User user =
                storedPasskey.User;

            if (!string.Equals(
                    user.Status,
                    "Active",
                    StringComparison.OrdinalIgnoreCase))
            {
                return Unauthorized(new
                {
                    success = false,

                    message =
                        "This account is not active."
                });
            }

            IsUserHandleOwnerOfCredentialIdAsync
                userHandleCallback =
                    (args, token) =>
                    {
                        bool credentialMatches =
                            args.CredentialId
                                .SequenceEqual(
                                    storedPasskey
                                        .CredentialId
                                );

                        bool userHandleMatches =
                            storedPasskey.UserHandle != null &&
                            args.UserHandle
                                .SequenceEqual(
                                    storedPasskey
                                        .UserHandle
                                );

                        return Task.FromResult(
                            credentialMatches &&
                            userHandleMatches
                        );
                    };

            var verificationResult =
                await fido2.MakeAssertionAsync(
                    new MakeAssertionParams
                    {
                        AssertionResponse =
                            clientResponse,

                        OriginalOptions =
                            originalOptions,

                        StoredPublicKey =
                            storedPasskey.PublicKey,

                        StoredSignatureCounter =
                            storedPasskey.SignCount,

                        IsUserHandleOwnerOfCredentialIdCallback =
                            userHandleCallback
                    },

                    cancellationToken:
                        cancellationToken
                );

            storedPasskey.SignCount =
                verificationResult.SignCount;

            storedPasskey.LastUsedAt =
                DateTime.UtcNow;

            await db.SaveChangesAsync(
                cancellationToken
            );

            bool isAdmin =
                string.Equals(
                    user.Position,
                    "Admin",
                    StringComparison.OrdinalIgnoreCase
                );

            if (isAdmin)
            {
                AdminSecurity? adminSecurity =
                    await db.AdminSecurities
                        .AsNoTracking()
                        .FirstOrDefaultAsync(
                            a =>
                                a.UserId ==
                                user.UserId &&

                                a.PinEnabled,

                            cancellationToken
                        );

                if (adminSecurity == null)
                {
                    return Unauthorized(new
                    {
                        success = false,

                        message =
                            "This Admin account has not " +
                            "configured a Security PIN."
                    });
                }

                HttpContext.Session.SetInt32(
                    PendingAdminUserIdKey,
                    user.UserId
                );

                HttpContext.Session.SetString(
                    PendingAdminPasskeyKey,
                    "true"
                );

                HttpContext.Session.SetString(
                    PendingAdminExpiryKey,

                    DateTime.UtcNow
                        .AddMinutes(5)
                        .ToString("O")
                );

                return Json(new
                {
                    success = true,

                    requiresAdminPin = true,

                    message =
                        "Passkey verified. " +
                        "Enter the Admin Security PIN.",

                    redirectUrl =
                        "/Passkey/VerifyAdminPin"
                });
            }

            await SignInUserAsync(
                user,
                passkeyVerified: true,
                adminPinVerified: false
            );

            return Json(new
            {
                success = true,

                message =
                    "Passkey login successful.",

                redirectUrl =
                    GetRedirectUrl(
                        user.Position
                    )
            });
        }
        catch (Exception exception)
        {
            return BadRequest(new
            {
                success = false,

                message =
                    "Passkey login failed. " +
                    GetRealExceptionMessage(
                        exception
                    )
            });
        }
    }

    // ==================================================
    // ADMIN PIN PAGE
    // ==================================================

    [HttpGet]
    public IActionResult VerifyAdminPin()
    {
        int? userId =
            HttpContext.Session.GetInt32(
                PendingAdminUserIdKey
            );

        string? passkeyVerified =
            HttpContext.Session.GetString(
                PendingAdminPasskeyKey
            );

        DateTime? expiry =
            GetPendingAdminExpiry();

        if (userId == null ||
            passkeyVerified != "true" ||
            expiry == null ||
            expiry <= DateTime.UtcNow)
        {
            ClearPendingAdminLogin();

            TempData["Error"] =
                "Admin verification expired. " +
                "Please scan the passkey again.";

            return RedirectToAction(
                "Login",
                "Account"
            );
        }

        ViewBag.Error =
            TempData["Error"];

        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> VerifyAdminPin(
        string pin)
    {
        int? userId =
            HttpContext.Session.GetInt32(
                PendingAdminUserIdKey
            );

        string? passkeyVerified =
            HttpContext.Session.GetString(
                PendingAdminPasskeyKey
            );

        DateTime? expiry =
            GetPendingAdminExpiry();

        if (userId == null ||
            passkeyVerified != "true" ||
            expiry == null ||
            expiry <= DateTime.UtcNow)
        {
            ClearPendingAdminLogin();

            TempData["Error"] =
                "Admin verification expired. " +
                "Please scan the passkey again.";

            return RedirectToAction(
                "Login",
                "Account"
            );
        }

        pin = (pin ?? "").Trim();

        if (!System.Text.RegularExpressions
                .Regex.IsMatch(
                    pin,
                    @"^\d{6}$"))
        {
            ViewBag.Error =
                "Enter the complete 6-digit " +
                "Admin PIN.";

            return View();
        }

        User? user =
            await db.Users.FirstOrDefaultAsync(
                u =>
                    u.UserId == userId.Value &&
                    u.Position == "Admin"
            );

        AdminSecurity? security =
            await db.AdminSecurities
                .FirstOrDefaultAsync(
                    a =>
                        a.UserId == userId.Value &&
                        a.PinEnabled
                );

        if (user == null ||
            security == null)
        {
            ClearPendingAdminLogin();

            TempData["Error"] =
                "Admin security configuration " +
                "could not be found.";

            return RedirectToAction(
                "Login",
                "Account"
            );
        }

        if (security.LockoutEnd.HasValue &&
            security.LockoutEnd.Value >
            DateTime.UtcNow)
        {
            TimeSpan remaining =
                security.LockoutEnd.Value -
                DateTime.UtcNow;

            ViewBag.Error =
                "Admin PIN is locked. Try again in " +
                Math.Ceiling(
                    remaining.TotalMinutes
                ) +
                " minute(s).";

            return View();
        }

        PasswordVerificationResult result =
            adminPinHasher
                .VerifyHashedPassword(
                    security,
                    security.PinHash,
                    pin
                );

        if (result ==
            PasswordVerificationResult.Failed)
        {
            security.FailedAttempts++;

            if (security.FailedAttempts >= 5)
            {
                security.LockoutEnd =
                    DateTime.UtcNow
                        .AddMinutes(15);

                security.FailedAttempts = 0;

                ViewBag.Error =
                    "Too many incorrect attempts. " +
                    "Admin PIN is locked for " +
                    "15 minutes.";
            }
            else
            {
                int remainingAttempts =
                    5 -
                    security.FailedAttempts;

                ViewBag.Error =
                    "Incorrect Admin PIN. " +
                    remainingAttempts +
                    " attempt(s) remaining.";
            }

            await db.SaveChangesAsync();

            return View();
        }

        security.FailedAttempts = 0;
        security.LockoutEnd = null;
        security.LastVerifiedAt =
            DateTime.UtcNow;

        await db.SaveChangesAsync();

        ClearPendingAdminLogin();

        await SignInUserAsync(
            user,
            passkeyVerified: true,
            adminPinVerified: true
        );

        return RedirectToAction(
            "Index",
            "Admin"
        );
    }

    // ==================================================
    // HELPERS
    // ==================================================

    private async Task SignInUserAsync(
        User user,
        bool passkeyVerified,
        bool adminPinVerified)
    {
        List<Claim> claims =
        [
            new Claim(
                ClaimTypes.NameIdentifier,
                user.UserId.ToString()
            ),

            new Claim(
                ClaimTypes.Name,
                user.Username
            ),

            new Claim(
                ClaimTypes.Email,
                user.Email
            ),

            new Claim(
                ClaimTypes.Role,
                user.Position
            ),

            new Claim(
                "PasskeyVerified",
                passkeyVerified
                    ? "true"
                    : "false"
            ),

            new Claim(
                "AdminPinVerified",
                adminPinVerified
                    ? "true"
                    : "false"
            )
        ];

        ClaimsIdentity identity = new(
            claims,

            CookieAuthenticationDefaults
                .AuthenticationScheme
        );

        AuthenticationProperties properties =
            new()
            {
                IsPersistent = false,
                AllowRefresh = true
            };

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults
                .AuthenticationScheme,

            new ClaimsPrincipal(identity),

            properties
        );
    }

    private DateTime? GetPendingAdminExpiry()
    {
        string? value =
            HttpContext.Session.GetString(
                PendingAdminExpiryKey
            );

        return DateTime.TryParse(
            value,
            null,
            System.Globalization.DateTimeStyles
                .RoundtripKind,
            out DateTime date)
                ? date.ToUniversalTime()
                : null;
    }

    private void ClearPendingAdminLogin()
    {
        HttpContext.Session.Remove(
            PendingAdminUserIdKey
        );

        HttpContext.Session.Remove(
            PendingAdminPasskeyKey
        );

        HttpContext.Session.Remove(
            PendingAdminExpiryKey
        );
    }

    private static string GetRedirectUrl(
        string position)
    {
        return position switch
        {
            "Admin" =>
                "/Admin/Index",

            "Teacher" =>
                "/Teacher/Index",

            _ =>
                "/Student/Index"
        };
    }

    private static string
        GetRealExceptionMessage(
            Exception exception)
    {
        Exception current =
            exception;

        while (current.InnerException != null)
        {
            current =
                current.InnerException;
        }

        return current.Message;
    }
}