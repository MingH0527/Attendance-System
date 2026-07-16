using System.Security.Claims;
using System.Text;
using Fido2NetLib;
using Fido2NetLib.Objects;
using Microsoft.AspNetCore.Authorization;
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

    public MyProfileController(
        DB db,
        IFido2 fido2)
    {
        this.db = db;
        this.fido2 = fido2;
    }

    // GET: /MyProfile
    [HttpGet]
    public IActionResult Index()
    {
        return View();
    }

    // GET: /MyProfile/Passkey
    [HttpGet]
    public async Task<IActionResult> Passkey()
    {
        int? userId = GetCurrentUserId();

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
                .Where(p => p.UserId == userId.Value)
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();

        ViewBag.PasskeyCount = passkeys.Count;
        ViewBag.MaximumPasskeys = MaximumPasskeys;

        return View(passkeys);
    }

    // POST: /MyProfile/CreatePasskeyOptions
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreatePasskeyOptions()
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
                u => u.UserId == userId.Value
            );

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

        Fido2User fidoUser = new()
        {
            Id = Encoding.UTF8.GetBytes(
                user.UserId.ToString()
            ),

            Name = user.Username,

            DisplayName = user.Username
        };

        List<PublicKeyCredentialDescriptor> excludedCredentials =
            existingPasskeys
                .Select(p =>
                    new PublicKeyCredentialDescriptor(
                        p.CredentialId
                    ))
                .ToList();

        AuthenticatorSelection authenticatorSelection = new()
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
                }
            );

        HttpContext.Session.SetString(
            PasskeyOptionsSessionKey,
            options.ToJson()
        );

        return Json(options);
    }

    // POST: /MyProfile/CompletePasskeyRegistration
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult>
        CompletePasskeyRegistration(
            [FromBody]
            AuthenticatorAttestationRawResponse attestationResponse,
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

        string? optionsJson =
            HttpContext.Session.GetString(
                PasskeyOptionsSessionKey
            );

        if (string.IsNullOrWhiteSpace(optionsJson))
        {
            return BadRequest(new
            {
                success = false,
                message =
                    "Passkey registration expired. Please try again."
            });
        }

        // Challenge must only be used once.
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
        cancellationToken: cancellationToken
    );

            int currentCount =
                await db.UserPasskeys.CountAsync(
                    p => p.UserId == userId.Value,
                    cancellationToken
                );

            if (currentCount >= MaximumPasskeys)
            {
                return BadRequest(new
                {
                    success = false,
                    message =
                        "This account already has the maximum of 3 passkeys."
                });
            }

            bool duplicateExists =
                await db.UserPasskeys.AnyAsync(
                    p => p.CredentialId == result.Id,
                    cancellationToken
                );

            if (duplicateExists)
            {
                return Conflict(new
                {
                    success = false,
                    message =
                        "This passkey has already been registered."
                });
            }

            UserPasskey passkey = new()
            {
                UserId = userId.Value,

                CredentialId = result.Id,

                PublicKey = result.PublicKey,

                SignCount = result.SignCount,

                AaGuid = result.AaGuid,

                UserHandle = originalOptions.User.Id,

                CredentialType = "public-key",

                DeviceName =
                    "Passkey " +
                    DateTime.Now.ToString(
                        "dd MMM yyyy HH:mm"
                    ),

                CreatedAt = DateTime.UtcNow
            };

            db.UserPasskeys.Add(passkey);

            await db.SaveChangesAsync(
                cancellationToken
            );

            int newCount =
                await db.UserPasskeys.CountAsync(
                    p => p.UserId == userId.Value,
                    cancellationToken
                );

            return Json(new
            {
                success = true,

                message =
                    "Passkey registered successfully.",

                count = newCount,

                maximum = MaximumPasskeys
            });
        }
        catch (Exception exception)
        {
            return BadRequest(new
            {
                success = false,
                message =
                    "Passkey registration failed. " +
                    GetRealExceptionMessage(exception)
            });
        }
    }

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

    private static string GetRealExceptionMessage(
        Exception exception)
    {
        Exception current = exception;

        while (current.InnerException != null)
        {
            current = current.InnerException;
        }

        return current.Message;
    }
}