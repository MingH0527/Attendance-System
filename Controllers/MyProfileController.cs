using System.Security.Claims;
using System.Text;
using Fido2NetLib;
using Fido2NetLib.Objects;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebAndMobile.Models;
using WebAndMobile.Services;

namespace WebAndMobile.Controllers;

[Authorize]
public class MyProfileController : Controller
{
    private const string PasskeyOptionsSessionKey =
        "Passkey.RegistrationOptions";

    private const string PasskeyDeviceSessionKey =
        "Passkey.RegistrationDeviceIdentifier";

    private const string VerifiedBindingSessionKey =
        "Passkey.VerifiedPhoneDeviceIdentifier";

    private const int MaximumPasskeys = 3;
    private const int MaximumAccountsPerDevice = 4;
    private const string DeviceCookieName = "WebAndMobile.PasskeyDevice";

    private readonly DB db;
    private readonly PasskeyFido2Factory passkeyFido2Factory;

    private readonly IPasswordHasher<AdminSecurity>
        adminPinHasher;

    public MyProfileController(
        DB db,
        PasskeyFido2Factory passkeyFido2Factory,
        IPasswordHasher<AdminSecurity> adminPinHasher)
    {
        this.db = db;
        this.passkeyFido2Factory = passkeyFido2Factory;
        this.adminPinHasher = adminPinHasher;
    }

    // ==================================================
    // MY PROFILE
    // ==================================================

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        int? userId = GetCurrentUserId();

        if (userId == null)
        {
            return RedirectToAction("Login", "Account");
        }

        User? user = await db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.UserId == userId.Value);

        if (user != null)
        {
            bool hasCompletedRegistration = user.Position switch
            {
                "Student" => await db.StudentClasses.AnyAsync(sc => sc.UserId == userId.Value),
                "Teacher" => await db.TeacherCourses.AnyAsync(tc => tc.UserId == userId.Value),
                _ => true
            };

            ViewBag.ShowRegisterCourseCard = !hasCompletedRegistration;
        }

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

        ViewBag.PasskeyEnvironment =
            passkeyFido2Factory.GetMode(Request);

        ViewBag.PasskeyRpId =
            passkeyFido2Factory.GetCurrentRpId(Request);

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
        int? userId = GetCurrentUserId();

        if (userId == null)
        {
            return Unauthorized(new
            {
                success = false,
                message = "Your login session has expired."
            });
        }

        User? user = await db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(
                u => u.UserId == userId.Value);

        if (user == null)
        {
            return NotFound(new
            {
                success = false,
                message = "User account could not be found."
            });
        }

        List<UserPasskey> existingPasskeys =
            await db.UserPasskeys
                .AsNoTracking()
                .Where(p => p.UserId == userId.Value)
                .ToListAsync();

        if (existingPasskeys.Count >= MaximumPasskeys)
        {
            return BadRequest(new
            {
                success = false,
                message =
                    "This account already has the maximum of 3 passkeys."
            });
        }

        string? verifiedPhoneDeviceIdentifier =
            HttpContext.Session.GetString(
                VerifiedBindingSessionKey
            );

        string deviceIdentifier =
            NormaliseDeviceIdentifier(
                verifiedPhoneDeviceIdentifier
            );

        if (string.IsNullOrWhiteSpace(
                deviceIdentifier))
        {
            return StatusCode(
                StatusCodes.Status428PreconditionRequired,
                new
                {
                    success = false,
                    code = "PHONE_BINDING_REQUIRED",
                    message =
                        "Verify the phone or tablet that will own this Passkey first."
                });
        }

        HttpContext.Session.SetString(
            PasskeyDeviceSessionKey,
            deviceIdentifier
        );

        bool sameDeviceAlreadyRegistered =
            existingPasskeys.Any(
                p =>
                    !string.IsNullOrWhiteSpace(
                        p.DeviceIdentifier) &&
                    string.Equals(
                        p.DeviceIdentifier,
                        deviceIdentifier,
                        StringComparison.Ordinal));

        if (sameDeviceAlreadyRegistered)
        {
            return Conflict(new
            {
                success = false,
                code = "DEVICE_ALREADY_REGISTERED",
                message =
                    "This phone or tablet is already registered for this account. " +
                    "Use a different device."
            });
        }

        int deviceAccountCount =
            await GetDeviceAccountCountAsync(
                deviceIdentifier);

        if (deviceAccountCount >=
            MaximumAccountsPerDevice)
        {
            HttpContext.Session.Remove(
                VerifiedBindingSessionKey
            );

            return StatusCode(
                StatusCodes.Status429TooManyRequests,
                new
                {
                    success = false,
                    code = "DEVICE_ACCOUNT_LIMIT_REACHED",
                    deviceAccountCount,
                    deviceMaximumAccounts =
                        MaximumAccountsPerDevice,
                    message =
                        "Passkey registration blocked. " +
                        "This phone or tablet is already linked to the maximum of 4 accounts. " +
                        "Delete a Passkey association from an account you no longer want linked " +
                        "to this device, then try again."
                });
        }

        string? warningMessage = null;
        string warningLevel = "none";

        if (deviceAccountCount == 2)
        {
            warningLevel = "warning";
            warningMessage =
                "This device is currently linked to 2 accounts. " +
                "After registering this account, only 1 Passkey account slot " +
                "will remain on this device.";
        }
        else if (deviceAccountCount == 3)
        {
            warningLevel = "final";
            warningMessage =
                "This is the final available Passkey account slot on this device. " +
                "After registering this account, this device will not accept " +
                "Passkey registration for any additional accounts.";
        }

        Fido2User fidoUser = new()
        {
            Id =
                Encoding.UTF8.GetBytes(
                    user.UserId.ToString()),
            Name = user.Username,
            DisplayName = user.Username
        };

        List<PublicKeyCredentialDescriptor>
            excludedCredentials =
                existingPasskeys
                    .Select(
                        p =>
                            new PublicKeyCredentialDescriptor(
                                p.CredentialId))
                    .ToList();

        AuthenticatorSelection
            authenticatorSelection = new()
            {
                ResidentKey =
                    ResidentKeyRequirement.Required,
                UserVerification =
                    UserVerificationRequirement.Required
            };

        IFido2 fido2 =
            passkeyFido2Factory.Create(Request);

        CredentialCreateOptions options =
            fido2.RequestNewCredential(
                new RequestNewCredentialParams
                {
                    User = fidoUser,
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
                });

        HttpContext.Session.SetString(
            PasskeyOptionsSessionKey,
            options.ToJson());

        return Json(new
        {
            success = true,
            options,
            environment =
                passkeyFido2Factory.GetMode(Request),
            rpId =
                passkeyFido2Factory.GetCurrentRpId(Request),
            deviceAccountCount,
            deviceMaximumAccounts =
                MaximumAccountsPerDevice,
            warningLevel,
            warningMessage
        });
    }

    // ==================================================
    // COMPLETE PASSKEY REGISTRATION
    // ==================================================

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult>
        CompletePasskeyRegistration(
            [FromBody]
            PasskeyRegistrationRequest request,

            CancellationToken cancellationToken)
    {
        int? userId = GetCurrentUserId();

        if (userId == null)
        {
            return Unauthorized(new
            {
                success = false,
                message = "Your login session has expired."
            });
        }

        if (request?.AttestationResponse == null)
        {
            return BadRequest(new
            {
                success = false,
                message =
                    "Invalid Passkey registration response."
            });
        }

        string? optionsJson =
            HttpContext.Session.GetString(
                PasskeyOptionsSessionKey);

        if (string.IsNullOrWhiteSpace(optionsJson))
        {
            return BadRequest(new
            {
                success = false,
                message =
                    "Passkey registration expired. Please try again."
            });
        }

        string? registrationDeviceIdentifier =
            HttpContext.Session.GetString(
                PasskeyDeviceSessionKey
            );

        HttpContext.Session.Remove(
            PasskeyOptionsSessionKey);

        HttpContext.Session.Remove(
            PasskeyDeviceSessionKey);

        if (string.IsNullOrWhiteSpace(
                registrationDeviceIdentifier))
        {
            return BadRequest(new
            {
                success = false,
                code = "DEVICE_SESSION_MISSING",
                message =
                    "The device registration session expired. " +
                    "Please start Passkey registration again."
            });
        }

        string deviceIdentifier =
            NormaliseDeviceIdentifier(
                registrationDeviceIdentifier
            );

        if (string.IsNullOrWhiteSpace(
                deviceIdentifier))
        {
            return BadRequest(new
            {
                success = false,
                code = "DEVICE_IDENTIFIER_INVALID",
                message =
                    "The verified phone or tablet identity is invalid. " +
                    "Please restart Passkey registration."
            });
        }

        HttpContext.Session.Remove(
            VerifiedBindingSessionKey
        );

        try
        {
            int currentCount =
                await db.UserPasskeys.CountAsync(
                    p => p.UserId == userId.Value,
                    cancellationToken);

            if (currentCount >= MaximumPasskeys)
            {
                return BadRequest(new
                {
                    success = false,
                    message =
                        "This account already has the maximum of 3 passkeys."
                });
            }

            bool sameDeviceAlreadyRegistered =
                await db.UserPasskeys.AnyAsync(
                    p =>
                        p.UserId == userId.Value &&
                        p.DeviceIdentifier ==
                            deviceIdentifier,
                    cancellationToken);

            if (sameDeviceAlreadyRegistered)
            {
                return Conflict(new
                {
                    success = false,
                    code = "DEVICE_ALREADY_REGISTERED",
                    message =
                        "This device is already registered for this account."
                });
            }

            int deviceAccountCount =
                await GetDeviceAccountCountAsync(
                    deviceIdentifier,
                    cancellationToken);

            if (deviceAccountCount >=
                MaximumAccountsPerDevice)
            {
                return StatusCode(
                    StatusCodes.Status429TooManyRequests,
                    new
                    {
                        success = false,
                        code =
                            "DEVICE_ACCOUNT_LIMIT_REACHED",
                        message =
                            "Passkey registration blocked. " +
                            "This device is already linked to 4 accounts. " +
                            "Delete an unwanted account association first."
                    });
            }

            CredentialCreateOptions originalOptions =
                CredentialCreateOptions.FromJson(
                    optionsJson);

            IsCredentialIdUniqueToUserAsyncDelegate
                uniqueCredentialCallback =
                    async (args, token) =>
                    {
                        bool credentialExists =
                            await db.UserPasskeys.AnyAsync(
                                p =>
                                    p.CredentialId ==
                                    args.CredentialId,
                                token);

                        return !credentialExists;
                    };

            IFido2 fido2 =
                passkeyFido2Factory.Create(Request);

            var result =
                await fido2.MakeNewCredentialAsync(
                    new MakeNewCredentialParams
                    {
                        AttestationResponse =
                            request.AttestationResponse,
                        OriginalOptions =
                            originalOptions,
                        IsCredentialIdUniqueToUserCallback =
                            uniqueCredentialCallback
                    },
                    cancellationToken:
                        cancellationToken);

            bool duplicateExists =
                await db.UserPasskeys.AnyAsync(
                    p => p.CredentialId == result.Id,
                    cancellationToken);

            if (duplicateExists)
            {
                return Conflict(new
                {
                    success = false,
                    message =
                        "This passkey has already been registered."
                });
            }

            string attachment =
                CleanDeviceValue(
                    request.DeviceInfo
                        .AuthenticatorAttachment,
                    50,
                    "Unknown");

            string transports =
                request.DeviceInfo.Transports == null
                    ? ""
                    : string.Join(
                        ",",
                        request.DeviceInfo.Transports
                            .Where(
                                x =>
                                    !string.IsNullOrWhiteSpace(
                                        x))
                            .Take(10));

            string deviceType =
                NormaliseDeviceType(
                    request.DeviceInfo.DeviceType);

            string deviceName =
                CleanDeviceValue(
                    request.DeviceInfo.DeviceName,
                    100,
                    GetDeviceDisplayName(
                        attachment,
                        transports));

            string operatingSystem =
                CleanDeviceValue(
                    request.DeviceInfo.OperatingSystem,
                    100,
                    "Unknown OS");

            string browser =
                CleanDeviceValue(
                    request.DeviceInfo.Browser,
                    100,
                    "Unknown Browser");

            UserPasskey passkey = new()
            {
                UserId = userId.Value,
                CredentialId = result.Id,
                PublicKey = result.PublicKey,
                SignCount = result.SignCount,
                AaGuid = result.AaGuid,
                UserHandle =
                    originalOptions.User.Id,
                CredentialType = "public-key",
                DeviceIdentifier =
                    deviceIdentifier,
                RelyingPartyId =
                    passkeyFido2Factory
                        .GetCurrentRpId(Request),
                DeviceName = deviceName,
                DeviceType = deviceType,
                OperatingSystem =
                    operatingSystem,
                Browser = browser,
                AuthenticatorAttachment =
                    attachment,
                AuthenticatorTransports =
                    CleanDeviceValue(
                        transports,
                        200,
                        ""),
                UserAgent =
                    CleanDeviceValue(
                        request.DeviceInfo.UserAgent,
                        300,
                        ""),
                CreatedAt = DateTime.UtcNow
            };

            db.UserPasskeys.Add(passkey);

            await db.SaveChangesAsync(
                cancellationToken);

            passkey.PasskeyCode =
                $"USRPK{passkey.UserPasskeyId:D4}";

            await db.SaveChangesAsync(
                cancellationToken);

            int newCount =
                await db.UserPasskeys.CountAsync(
                    p => p.UserId == userId.Value,
                    cancellationToken);

            int newDeviceAccountCount =
                await GetDeviceAccountCountAsync(
                    deviceIdentifier,
                    cancellationToken);

            User? currentUser =
                await db.Users
                    .AsNoTracking()
                    .FirstOrDefaultAsync(
                        u =>
                            u.UserId ==
                            userId.Value,
                        cancellationToken);

            bool requiresAdminPinSetup = false;

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
                            cancellationToken);

                requiresAdminPinSetup =
                    !hasAdminPin;
            }

            string successMessage =
                newDeviceAccountCount >=
                MaximumAccountsPerDevice
                    ? "Passkey registered successfully. " +
                      "This device has now reached its 4-account limit."
                    : requiresAdminPinSetup
                        ? "Passkey registered. " +
                          "Please set your Admin Security PIN."
                        : "Passkey registered successfully.";

            return Json(new
            {
                success = true,
                message = successMessage,
                passkeyCode =
                    passkey.PasskeyCode,
                count = newCount,
                maximum = MaximumPasskeys,
                deviceAccountCount =
                    newDeviceAccountCount,
                deviceMaximumAccounts =
                    MaximumAccountsPerDevice,
                requiresAdminPinSetup,
                environment =
                    passkeyFido2Factory.GetMode(Request),
                redirectUrl =
                    requiresAdminPinSetup
                        ? "/MyProfile/SetAdminPin"
                        : "/MyProfile/Passkey"
            });
        }
        catch (DbUpdateException)
        {
            return Conflict(new
            {
                success = false,
                message =
                    "This account/device Passkey association already exists."
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
                        exception)
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

    private string GetRequestDeviceIdentifier()
    {
        string? headerValue =
            Request.Headers[
                "X-Passkey-Device-Id"
            ].FirstOrDefault();

        string normalised =
            NormaliseDeviceIdentifier(
                headerValue
            );

        if (!string.IsNullOrWhiteSpace(
                normalised))
        {
            SetDeviceCookie(normalised);
            return normalised;
        }

        if (
            Request.Cookies.TryGetValue(
                DeviceCookieName,
                out string? existing)
        )
        {
            normalised =
                NormaliseDeviceIdentifier(
                    existing
                );

            if (!string.IsNullOrWhiteSpace(
                    normalised))
            {
                return normalised;
            }
        }

        string generated =
            Guid.NewGuid().ToString("N");

        SetDeviceCookie(generated);

        return generated;
    }

    private static string NormaliseDeviceIdentifier(
        string? value)
    {
        string cleaned =
            (value ?? "")
                .Trim()
                .Replace("-", "")
                .ToLowerInvariant();

        if (
            cleaned.Length != 32 ||
            !cleaned.All(
                character =>
                    char.IsAsciiHexDigit(
                        character))
        )
        {
            return "";
        }

        return cleaned;
    }

    private void SetDeviceCookie(
        string deviceIdentifier)
    {
        Response.Cookies.Append(
            DeviceCookieName,
            deviceIdentifier,
            new CookieOptions
            {
                HttpOnly = true,
                Secure = Request.IsHttps,
                SameSite = SameSiteMode.Lax,
                IsEssential = true,
                Expires =
                    DateTimeOffset.UtcNow
                        .AddYears(2)
            });
    }

    private Task<int> GetDeviceAccountCountAsync(
        string deviceIdentifier,
        CancellationToken cancellationToken =
            default)
    {
        return db.UserPasskeys
            .AsNoTracking()
            .Where(
                p =>
                    p.DeviceIdentifier ==
                    deviceIdentifier)
            .Select(p => p.UserId)
            .Distinct()
            .CountAsync(cancellationToken);
    }

    private static string NormaliseDeviceType(
        string? value)
    {
        string type =
            (value ?? "").Trim();

        if (type.Equals(
                "Mobile",
                StringComparison.OrdinalIgnoreCase))
        {
            return "Mobile";
        }

        if (type.Equals(
                "Tablet",
                StringComparison.OrdinalIgnoreCase))
        {
            return "Tablet";
        }

        if (type.Equals(
                "Desktop",
                StringComparison.OrdinalIgnoreCase))
        {
            return "Desktop";
        }

        return "Unknown";
    }

    private static string CleanDeviceValue(
        string? value,
        int maximumLength,
        string fallback)
    {
        string cleaned =
            (value ?? "").Trim();

        if (string.IsNullOrWhiteSpace(cleaned))
        {
            cleaned = fallback;
        }

        return cleaned.Length <= maximumLength
            ? cleaned
            : cleaned[..maximumLength];
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