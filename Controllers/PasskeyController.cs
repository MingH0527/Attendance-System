using System.Security.Claims;
using Fido2NetLib;
using Fido2NetLib.Objects;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebAndMobile.Models;

namespace WebAndMobile.Controllers;

public class PasskeyController : Controller
{
    private const string AssertionOptionsSessionKey =
        "Passkey.LoginAssertionOptions";

    private readonly DB db;
    private readonly IFido2 fido2;

    public PasskeyController(
        DB db,
        IFido2 fido2)
    {
        this.db = db;
        this.fido2 = fido2;
    }

    // ==================================================
    // CREATE PASSKEY LOGIN OPTIONS
    // POST: /Passkey/CreateLoginOptions
    // ==================================================

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateLoginOptions()
    {
        bool hasAnyPasskey =
            await db.UserPasskeys.AnyAsync();

        if (!hasAnyPasskey)
        {
            return BadRequest(new
            {
                success = false,
                message =
                    "No passkeys have been registered in this system."
            });
        }

        /*
         * Empty AllowedCredentials enables discoverable
         * credential/passkey account selection.
         *
         * The browser, Windows Hello or phone chooses
         * the matching passkey.
         */
        AssertionOptions options =
            fido2.GetAssertionOptions(
                new GetAssertionOptionsParams
                {
                    AllowedCredentials =
                        new List<
                            PublicKeyCredentialDescriptor>(),

                    UserVerification =
                        UserVerificationRequirement.Required
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
    // POST: /Passkey/CompleteLogin
    // ==================================================

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CompleteLogin(
        [FromBody]
        AuthenticatorAssertionRawResponse clientResponse,
        CancellationToken cancellationToken)
    {
        string? optionsJson =
            HttpContext.Session.GetString(
                AssertionOptionsSessionKey
            );

        if (string.IsNullOrWhiteSpace(optionsJson))
        {
            return BadRequest(new
            {
                success = false,
                message =
                    "Passkey login request expired. Please try again."
            });
        }

        // Challenge can only be used once.
        HttpContext.Session.Remove(
            AssertionOptionsSessionKey
        );

        try
        {
            AssertionOptions originalOptions =
                AssertionOptions.FromJson(
                    optionsJson
                );

            /*
             * Locate the passkey selected by Windows Hello,
             * the browser or the user's phone.
             */
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
                        "This passkey is not registered in the system."
                });
            }

            User user = storedPasskey.User;

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

            /*
             * Confirm that the authenticator user handle
             * belongs to the stored credential.
             */
            IsUserHandleOwnerOfCredentialIdAsync
                userHandleCallback =
                    (args, token) =>
                    {
                        bool credentialMatches =
                            args.CredentialId.SequenceEqual(
                                storedPasskey.CredentialId
                            );

                        bool userHandleMatches =
                            storedPasskey.UserHandle != null &&
                            args.UserHandle.SequenceEqual(
                                storedPasskey.UserHandle
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

            /*
             * Save the new signature counter.
             * This helps detect cloned/replayed authenticators.
             */
            storedPasskey.SignCount =
                verificationResult.SignCount;

            storedPasskey.LastUsedAt =
                DateTime.UtcNow;

            await db.SaveChangesAsync(
                cancellationToken
            );

            await SignInUserAsync(user);

            string redirectUrl =
                GetRedirectUrl(user.Position);

            return Json(new
            {
                success = true,

                message =
                    "Passkey login successful.",

                redirectUrl
            });
        }
        catch (Exception exception)
        {
            return BadRequest(new
            {
                success = false,

                message =
                    "Passkey login failed. " +
                    GetRealExceptionMessage(exception)
            });
        }
    }

    // ==================================================
    // HELPERS
    // ==================================================

    private async Task SignInUserAsync(
        User user)
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