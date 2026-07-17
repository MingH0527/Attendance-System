using System.Security.Claims;
using System.Text;
using Fido2NetLib;
using Fido2NetLib.Objects;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebAndMobile.Models;

namespace WebAndMobile.Controllers;

[Authorize]
public class MyProfileController : Controller
{
    private const string PasskeyOptionsSessionKey =
        "Passkey.RegistrationOptions";

    private const int MaximumPasskeys = 3;

    private readonly DB db;
    private readonly IFido2 fido2;

    private readonly IPasswordHasher<AdminSecurity>
        adminPinHasher;

    public MyProfileController(
        DB db,
        IFido2 fido2,
        IPasswordHasher<AdminSecurity> adminPinHasher)
    {
        this.db = db;
        this.fido2 = fido2;
        this.adminPinHasher = adminPinHasher;
    }

    // ==================================================
    // MY PROFILE
    // ==================================================

    [HttpGet]
    public IActionResult Index()
    {
        return View();
    }

    // ==================================================
    // PASSKEY PAGE
    // ==================================================

    [HttpGet]
    public async Task<IActionResult> Passkey()
    {
        int? userId =
            GetCurrentUserId();

        if (userId == null)
        {
            return RedirectToAction(
                "Login",
                "Account"
            );
        }

        List<UserPasskey> passkeys =
            await db.UserPasskeys
                .AsNoTracking()
                .Where(
                    p =>
                        p.UserId ==
                        userId.Value
                )
                .OrderByDescending(
                    p => p.CreatedAt
                )
                .ToListAsync();

        ViewBag.PasskeyCount =
            passkeys.Count;

        ViewBag.MaximumPasskeys =
            MaximumPasskeys;

        User? user =
            await db.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    u =>
                        u.UserId ==
                        userId.Value
                );

        if (user != null &&
            string.Equals(
                user.Position,
                "Admin",
                StringComparison.OrdinalIgnoreCase))
        {
            AdminSecurity? security =
                await db.AdminSecurities
                    .AsNoTracking()
                    .FirstOrDefaultAsync(
                        a =>
                            a.UserId ==
                            user.UserId
                    );

            ViewBag.IsAdmin = true;

            ViewBag.AdminPinEnabled =
                security?.PinEnabled == true;
        }
        else
        {
            ViewBag.IsAdmin = false;
            ViewBag.AdminPinEnabled = false;
        }

        ViewBag.Error =
            TempData["Error"];

        ViewBag.Message =
            TempData["Message"];

        return View(passkeys);
    }

    // ==================================================
    // CREATE PASSKEY OPTIONS
    // ==================================================

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult>
        CreatePasskeyOptions()
    {
        int? userId =
            GetCurrentUserId();

        if (userId == null)
        {
            return Unauthorized(new
            {
                success = false,
                message =
                    "Your login session has expired."
            });
        }

        User? user =
            await db.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    u =>
                        u.UserId ==
                        userId.Value
                );

        if (user == null)
        {
            return NotFound(new
            {
                success = false,
                message =
                    "User account could not be found."
            });
        }

        List<UserPasskey> existingPasskeys =
            await db.UserPasskeys
                .AsNoTracking()
                .Where(
                    p =>
                        p.UserId ==
                        userId.Value
                )
                .ToListAsync();

        if (existingPasskeys.Count >=
            MaximumPasskeys)
        {
            return BadRequest(new
            {
                success = false,
                message =
                    "This account already has " +
                    "the maximum of 3 passkeys."
            });
        }

        Fido2User fidoUser = new()
        {
            Id =
                Encoding.UTF8.GetBytes(
                    user.UserId.ToString()
                ),

            Name =
                user.Username,

            DisplayName =
                user.Username
        };

        List<PublicKeyCredentialDescriptor>
            excludedCredentials =
                existingPasskeys
                    .Select(
                        p =>
                            new PublicKeyCredentialDescriptor(
                                p.CredentialId
                            )
                    )
                    .ToList();

        AuthenticatorSelection
            authenticatorSelection = new()
            {
                ResidentKey =
                    ResidentKeyRequirement.Required,

                UserVerification =
                    UserVerificationRequirement.Required
            };

        CredentialCreateOptions options =
            fido2.RequestNewCredential(
                new RequestNewCredentialParams
                {
                    User =
                        fidoUser,

                    ExcludeCredentials =
                        excludedCredentials,

                    AuthenticatorSelection =
                        authenticatorSelection,

                    AttestationPreference =
                        AttestationConveyancePreference.None,

                    Extensions =
                        new AuthenticationExtensionsClientInputs
                        {
                            CredProps = true
                        }
                }
            );

        HttpContext.Session.SetString(
            PasskeyOptionsSessionKey,
            options.ToJson()
        );

        return Json(options);
    }

    // ==================================================
    // COMPLETE PASSKEY REGISTRATION
    // ==================================================

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult>
        CompletePasskeyRegistration(
            [FromBody]
            AuthenticatorAttestationRawResponse
                attestationResponse,

            CancellationToken cancellationToken)
    {
        int? userId =
            GetCurrentUserId();

        if (userId == null)
        {
            return Unauthorized(new
            {
                success = false,
                message =
                    "Your login session has expired."
            });
        }

        string? optionsJson =
            HttpContext.Session.GetString(
                PasskeyOptionsSessionKey
            );

        if (string.IsNullOrWhiteSpace(
                optionsJson))
        {
            return BadRequest(new
            {
                success = false,
                message =
                    "Passkey registration expired. " +
                    "Please try again."
            });
        }

        HttpContext.Session.Remove(
            PasskeyOptionsSessionKey
        );

        try
        {
            CredentialCreateOptions originalOptions =
                CredentialCreateOptions.FromJson(
                    optionsJson
                );

            IsCredentialIdUniqueToUserAsyncDelegate
                uniqueCredentialCallback =
                    async (args, token) =>
                    {
                        bool credentialExists =
                            await db.UserPasskeys
                                .AnyAsync(
                                    p =>
                                        p.CredentialId ==
                                        args.CredentialId,

                                    token
                                );

                        return !credentialExists;
                    };

            var result =
                await fido2.MakeNewCredentialAsync(
                    new MakeNewCredentialParams
                    {
                        AttestationResponse =
                            attestationResponse,

                        OriginalOptions =
                            originalOptions,

                        IsCredentialIdUniqueToUserCallback =
                            uniqueCredentialCallback
                    },

                    cancellationToken:
                        cancellationToken
                );

            int currentCount =
                await db.UserPasskeys.CountAsync(
                    p =>
                        p.UserId ==
                        userId.Value,

                    cancellationToken
                );

            if (currentCount >=
                MaximumPasskeys)
            {
                return BadRequest(new
                {
                    success = false,
                    message =
                        "This account already has " +
                        "the maximum of 3 passkeys."
                });
            }

            bool duplicateExists =
                await db.UserPasskeys.AnyAsync(
                    p =>
                        p.CredentialId ==
                        result.Id,

                    cancellationToken
                );

            if (duplicateExists)
            {
                return Conflict(new
                {
                    success = false,
                    message =
                        "This passkey has already " +
                        "been registered."
                });
            }

            string attachment =
                "Unknown";

            string transports =
                attestationResponse.Response.Transports == null
                    ? ""
                    : string.Join(
                        ",",
                        attestationResponse.Response.Transports
                    );

            UserPasskey passkey = new()
            {
                UserId =
                    userId.Value,

                CredentialId =
                    result.Id,

                PublicKey =
                    result.PublicKey,

                SignCount =
                    result.SignCount,

                AaGuid =
                    result.AaGuid,

                UserHandle =
                    originalOptions.User.Id,

                CredentialType =
                    "public-key",

                DeviceName =
                    GetDeviceDisplayName(
                        attachment,
                        transports
                    ),

                AuthenticatorAttachment =
                    attachment,

                AuthenticatorTransports =
                    transports,

                CreatedAt =
                    DateTime.UtcNow
            };

            db.UserPasskeys.Add(passkey);

            await db.SaveChangesAsync(
                cancellationToken
            );

            int newCount =
                await db.UserPasskeys.CountAsync(
                    p =>
                        p.UserId ==
                        userId.Value,

                    cancellationToken
                );

            User? currentUser =
                await db.Users
                    .AsNoTracking()
                    .FirstOrDefaultAsync(
                        u =>
                            u.UserId ==
                            userId.Value,

                        cancellationToken
                    );

            bool requiresAdminPinSetup =
                false;

            if (currentUser != null &&
                string.Equals(
                    currentUser.Position,
                    "Admin",
                    StringComparison.OrdinalIgnoreCase))
            {
                bool hasAdminPin =
                    await db.AdminSecurities
                        .AnyAsync(
                            a =>
                                a.UserId ==
                                currentUser.UserId &&

                                a.PinEnabled,

                            cancellationToken
                        );

                requiresAdminPinSetup =
                    !hasAdminPin;
            }

            return Json(new
            {
                success = true,

                message =
                    requiresAdminPinSetup
                        ? "Passkey registered. " +
                          "Please set your Admin Security PIN."
                        : "Passkey registered successfully.",

                count =
                    newCount,

                maximum =
                    MaximumPasskeys,

                requiresAdminPinSetup,

                redirectUrl =
                    requiresAdminPinSetup
                        ? "/MyProfile/SetAdminPin"
                        : "/MyProfile/Passkey"
            });
        }
        catch (Exception exception)
        {
            return BadRequest(new
            {
                success = false,

                message =
                    "Passkey registration failed. " +
                    GetRealExceptionMessage(
                        exception
                    )
            });
        }
    }

    // ==================================================
    // ADMIN PIN SETUP - GET
    // ==================================================

    [HttpGet]
    public async Task<IActionResult> SetAdminPin()
    {
        int? userId =
            GetCurrentUserId();

        if (userId == null)
        {
            return RedirectToAction(
                "Login",
                "Account"
            );
        }

        User? user =
            await db.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    u =>
                        u.UserId ==
                        userId.Value
                );

        if (user == null)
        {
            return NotFound();
        }

        bool isAdmin =
            string.Equals(
                user.Position,
                "Admin",
                StringComparison.OrdinalIgnoreCase
            );

        if (!isAdmin)
        {
            return Forbid();
        }

        bool hasPasskey =
            await db.UserPasskeys.AnyAsync(
                p =>
                    p.UserId ==
                    user.UserId
            );

        if (!hasPasskey)
        {
            TempData["Error"] =
                "Register at least one passkey " +
                "before setting the Admin PIN.";

            return RedirectToAction(
                nameof(Passkey)
            );
        }

        AdminSecurity? security =
            await db.AdminSecurities
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    a =>
                        a.UserId ==
                        user.UserId
                );

        ViewBag.PinAlreadyEnabled =
            security?.PinEnabled == true;

        ViewBag.Error =
            TempData["Error"];

        ViewBag.Message =
            TempData["Message"];

        return View();
    }

    // ==================================================
    // ADMIN PIN SETUP - POST
    // ==================================================

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetAdminPin(
        string pin,
        string confirmPin)
    {
        int? userId =
            GetCurrentUserId();

        if (userId == null)
        {
            return RedirectToAction(
                "Login",
                "Account"
            );
        }

        User? user =
            await db.Users
                .FirstOrDefaultAsync(
                    u =>
                        u.UserId ==
                        userId.Value
                );

        if (user == null)
        {
            return NotFound();
        }

        bool isAdmin =
            string.Equals(
                user.Position,
                "Admin",
                StringComparison.OrdinalIgnoreCase
            );

        if (!isAdmin)
        {
            return Forbid();
        }

        bool hasPasskey =
            await db.UserPasskeys.AnyAsync(
                p =>
                    p.UserId ==
                    user.UserId
            );

        if (!hasPasskey)
        {
            ViewBag.Error =
                "Register at least one passkey " +
                "before setting the Admin PIN.";

            return View();
        }

        pin =
            (pin ?? "").Trim();

        confirmPin =
            (confirmPin ?? "").Trim();

        bool validPin =
            System.Text.RegularExpressions.Regex
                .IsMatch(
                    pin,
                    @"^\d{6}$"
                );

        if (!validPin)
        {
            ViewBag.Error =
                "Admin PIN must contain " +
                "exactly 6 digits.";

            return View();
        }

        if (pin != confirmPin)
        {
            ViewBag.Error =
                "PIN and Confirm PIN " +
                "do not match.";

            return View();
        }

        if (pin is
            "000000" or
            "111111" or
            "123456" or
            "654321")
        {
            ViewBag.Error =
                "This PIN is too easy to guess.";

            return View();
        }

        AdminSecurity? security =
            await db.AdminSecurities
                .FirstOrDefaultAsync(
                    a =>
                        a.UserId ==
                        user.UserId
                );

        if (security == null)
        {
            security = new AdminSecurity
            {
                UserId =
                    user.UserId,

                CreatedAt =
                    DateTime.UtcNow
            };

            db.AdminSecurities.Add(
                security
            );
        }

        security.PinHash =
            adminPinHasher.HashPassword(
                security,
                pin
            );

        security.PinEnabled = true;
        security.FailedAttempts = 0;
        security.LockoutEnd = null;
        security.UpdatedAt =
            DateTime.UtcNow;

        await db.SaveChangesAsync();

        TempData["Message"] =
            "Admin Security PIN was saved " +
            "successfully.";

        return RedirectToAction(
            nameof(Passkey)
        );
    }

    // ==================================================
    // DELETE PASSKEY
    // ==================================================

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeletePasskey(
        int passkeyId)
    {
        int? userId =
            GetCurrentUserId();

        if (userId == null)
        {
            return Unauthorized(new
            {
                success = false,
                message =
                    "Your login session has expired."
            });
        }

        UserPasskey? passkey =
            await db.UserPasskeys
                .FirstOrDefaultAsync(
                    p =>
                        p.UserPasskeyId ==
                        passkeyId &&

                        p.UserId ==
                        userId.Value
                );

        if (passkey == null)
        {
            return NotFound(new
            {
                success = false,
                message =
                    "The selected passkey could not be found."
            });
        }

        User? currentUser =
            await db.Users
                .FirstOrDefaultAsync(
                    u =>
                        u.UserId ==
                        userId.Value
                );

        if (currentUser == null)
        {
            return NotFound(new
            {
                success = false,
                message =
                    "Your user account could not be found."
            });
        }

        int passkeyCount =
            await db.UserPasskeys
                .CountAsync(
                    p =>
                        p.UserId ==
                        userId.Value
                );

        bool isAdmin =
            string.Equals(
                currentUser.Position,
                "Admin",
                StringComparison.OrdinalIgnoreCase
            );

        bool deletingLastPasskey =
            passkeyCount == 1;

        db.UserPasskeys.Remove(
            passkey
        );

        if (isAdmin &&
            deletingLastPasskey)
        {
            AdminSecurity? security =
                await db.AdminSecurities
                    .FirstOrDefaultAsync(
                        a =>
                            a.UserId ==
                            currentUser.UserId
                    );

            if (security != null)
            {
                db.AdminSecurities.Remove(
                    security
                );
            }
        }

        await db.SaveChangesAsync();

        int remainingCount =
            await db.UserPasskeys
                .CountAsync(
                    p =>
                        p.UserId ==
                        userId.Value
                );

        string message =
            isAdmin && deletingLastPasskey
                ? "The final Admin passkey was deleted. " +
                  "The Admin Security PIN configuration " +
                  "was also removed."
                : "Passkey deleted successfully.";

        return Json(new
        {
            success = true,
            message,
            count = remainingCount,
            maximum = MaximumPasskeys,
            adminSecurityReset =
                isAdmin &&
                deletingLastPasskey
        });
    }

    // ==================================================
    // HELPERS
    // ==================================================

    private int? GetCurrentUserId()
    {
        string? userIdValue =
            User.FindFirstValue(
                ClaimTypes.NameIdentifier
            );

        return int.TryParse(
            userIdValue,
            out int userId
        )
            ? userId
            : null;
    }

    private static string GetDeviceDisplayName(
        string attachment,
        string transports)
    {
        if (string.Equals(
                attachment,
                "Platform",
                StringComparison.OrdinalIgnoreCase))
        {
            return "This device";
        }

        if (transports.Contains(
                "Hybrid",
                StringComparison.OrdinalIgnoreCase))
        {
            return "Phone passkey";
        }

        if (transports.Contains(
                "Usb",
                StringComparison.OrdinalIgnoreCase))
        {
            return "USB security key";
        }

        if (transports.Contains(
                "Nfc",
                StringComparison.OrdinalIgnoreCase))
        {
            return "NFC security key";
        }

        if (transports.Contains(
                "Ble",
                StringComparison.OrdinalIgnoreCase))
        {
            return "Bluetooth security key";
        }

        return "Passkey Device";
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