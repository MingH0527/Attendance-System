using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebAndMobile.Models;

namespace WebAndMobile.Controllers;

public class AccountController : Controller
{
    private readonly DB db;
    private readonly IPasswordHasher<User> passwordHasher;
    private readonly GmailServiceHelper gmailServiceHelper;

    /*
     * Maps each wizard step to the model fields that live on it.
     * Used both to decide which step to send an invalid submission
     * back to, and to filter the on-screen error summary so it only
     * shows messages relevant to the step being displayed.
     */
    private static readonly Dictionary<int, string[]> StepFields =
        new()
        {
            [1] = new[]
            {
                nameof(WebAndMobile.Models.User.Username),
                nameof(WebAndMobile.Models.User.Email),
                nameof(WebAndMobile.Models.User.Phone),
                nameof(WebAndMobile.Models.User.Gender)
            },

            [2] = new[]
            {
                nameof(WebAndMobile.Models.User.Address),
                nameof(WebAndMobile.Models.User.Latitude),
                nameof(WebAndMobile.Models.User.Longitude)
            },

            [3] = new[]
            {
                nameof(WebAndMobile.Models.User.Password),
                nameof(WebAndMobile.Models.User.ConfirmPassword)
            }
        };

    private static readonly Regex GmailOnlyRegex =
        new(
            @"^[a-zA-Z0-9._%+\-]+@gmail\.com$",
            RegexOptions.IgnoreCase |
            RegexOptions.Compiled
        );

    public AccountController(
        DB db,
        IPasswordHasher<User> passwordHasher,
        GmailServiceHelper gmailServiceHelper
    )
    {
        this.db = db;
        this.passwordHasher = passwordHasher;
        this.gmailServiceHelper = gmailServiceHelper;
    }

    // ==================================================
    // REGISTER GET
    // ==================================================

    [HttpGet]
    public IActionResult Register()
    {
        ViewBag.Error =
            TempData["Error"];

        return View(new User());
    }

    // ==================================================
    // REGISTER POST
    // ==================================================

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(User model)
    {
        Console.WriteLine("========== REGISTER POST REACHED ==========");

        Console.WriteLine("---- RAW Request.Form (what the browser actually sent) ----");

        if (Request.HasFormContentType)
        {
            foreach (var pair in Request.Form)
            {
                Console.WriteLine($"  {pair.Key} = '{pair.Value}'");
            }
        }
        else
        {
            Console.WriteLine(
                $"  Request.HasFormContentType is FALSE. Content-Type was: '{Request.ContentType}'"
            );
        }

        Console.WriteLine("-------------------------------------------------------------");

        Console.WriteLine(
            $"Bound values -> Username='{model.Username}', Email='{model.Email}', " +
            $"Phone='{model.Phone}', Gender='{model.Gender}', Address='{model.Address}', " +
            $"Latitude={model.Latitude}, Longitude={model.Longitude}, " +
            $"PasswordLength={(model.Password ?? "").Length}, ConfirmPasswordLength={(model.ConfirmPassword ?? "").Length}"
        );

        /*
         * Clear automatic ModelState validation because
         * this controller performs its own validation below.
         */
        ModelState.Clear();

        model.Username =
            (model.Username ?? "")
            .Trim();

        model.Email =
            (model.Email ?? "")
            .Trim()
            .ToLowerInvariant();

        model.Phone =
            Regex.Replace(
                model.Phone ?? "",
                @"\D",
                ""
            );

        model.Address =
            (model.Address ?? "")
            .Trim();

        model.Gender =
            (model.Gender ?? "")
            .Trim()
            .ToLowerInvariant();

        string password =
            model.Password ?? "";

        string confirmPassword =
            model.ConfirmPassword ?? "";

        // ==================================================
        // USERNAME VALIDATION
        // ==================================================

        if (string.IsNullOrWhiteSpace(model.Username))
        {
            ModelState.AddModelError(
                nameof(model.Username),
                "Username is required."
            );
        }
        else if (model.Username.Length > 30)
        {
            ModelState.AddModelError(
                nameof(model.Username),
                "Username cannot exceed 30 characters."
            );
        }

        // ==================================================
        // EMAIL VALIDATION
        // ==================================================

        if (string.IsNullOrWhiteSpace(model.Email))
        {
            ModelState.AddModelError(
                nameof(model.Email),
                "Email is required."
            );
        }
        else if (!GmailOnlyRegex.IsMatch(model.Email))
        {
            ModelState.AddModelError(
                nameof(model.Email),
                "Use a valid Gmail address ending with @gmail.com."
            );
        }

        // ==================================================
        // PHONE VALIDATION
        // ==================================================

        if (string.IsNullOrWhiteSpace(model.Phone))
        {
            ModelState.AddModelError(
                nameof(model.Phone),
                "Phone number is required."
            );
        }
        else if (!IsValidMalaysiaPhone(model.Phone))
        {
            ModelState.AddModelError(
                nameof(model.Phone),
                "Enter a valid Malaysian phone number."
            );
        }

        // ==================================================
        // GENDER VALIDATION
        // ==================================================

        if (model.Gender is not ("male" or "female"))
        {
            ModelState.AddModelError(
                nameof(model.Gender),
                "Please select a gender."
            );
        }

        // ==================================================
        // LOCATION VALIDATION
        // ==================================================

        if (
            string.IsNullOrWhiteSpace(model.Address) ||
            model.Latitude == null ||
            model.Longitude == null
        )
        {
            ModelState.AddModelError(
                nameof(model.Address),
                "Please choose your location on the map."
            );
        }
        else if (
            model.Latitude < -90 ||
            model.Latitude > 90 ||
            model.Longitude < -180 ||
            model.Longitude > 180
        )
        {
            ModelState.AddModelError(
                nameof(model.Address),
                "The selected map coordinates are invalid."
            );
        }

        // ==================================================
        // PASSWORD VALIDATION
        // ==================================================

        if (string.IsNullOrEmpty(password))
        {
            ModelState.AddModelError(
                nameof(model.Password),
                "Password is required."
            );
        }
        else if (!IsPasswordValid(password))
        {
            ModelState.AddModelError(
                nameof(model.Password),
                "Use 8–15 characters with uppercase, lowercase, number and symbol."
            );
        }

        // ==================================================
        // CONFIRM PASSWORD VALIDATION
        // ==================================================

        if (string.IsNullOrEmpty(confirmPassword))
        {
            ModelState.AddModelError(
                nameof(model.ConfirmPassword),
                "Please confirm your password."
            );
        }
        else if (password != confirmPassword)
        {
            ModelState.AddModelError(
                nameof(model.ConfirmPassword),
                "Passwords do not match."
            );
        }

        // ==================================================
        // DUPLICATE CHECK
        // ==================================================

        try
        {
            if (
                !string.IsNullOrWhiteSpace(model.Username) &&
                await db.Users.AnyAsync(
                    user =>
                        user.Username.ToLower() ==
                        model.Username.ToLower()
                )
            )
            {
                ModelState.AddModelError(
                    nameof(model.Username),
                    "Username already exists."
                );
            }

            if (
                !string.IsNullOrWhiteSpace(model.Email) &&
                await db.Users.AnyAsync(
                    user =>
                        user.Email.ToLower() ==
                        model.Email
                )
            )
            {
                ModelState.AddModelError(
                    nameof(model.Email),
                    "Email already exists."
                );
            }

            if (
                !string.IsNullOrWhiteSpace(model.Phone) &&
                await db.Users.AnyAsync(
                    user =>
                        user.Phone == model.Phone
                )
            )
            {
                ModelState.AddModelError(
                    nameof(model.Phone),
                    "Phone number already exists."
                );
            }
        }
        catch (Exception exception)
        {
            Console.WriteLine(
                "========== DATABASE CHECK ERROR =========="
            );

            Console.WriteLine(exception.ToString());

            Console.WriteLine(
                "=========================================="
            );

            string message =
                "Unable to check existing accounts. " +
                GetRealExceptionMessage(exception);

            ModelState.AddModelError("", message);

            /*
             * Redundant, guaranteed-visible fallback in case the
             * asp-validation-summary tag helper does not render.
             */
            ViewBag.Error = message;
        }

        // ==================================================
        // RETURN VALIDATION ERROR
        // ==================================================

        if (!ModelState.IsValid)
        {
            Console.WriteLine("========== REGISTER MODELSTATE INVALID ==========");

            foreach (var entry in ModelState)
            {
                foreach (var error in entry.Value.Errors)
                {
                    string key =
                        string.IsNullOrEmpty(entry.Key)
                            ? "(form-level)"
                            : entry.Key;

                    Console.WriteLine(
                        $"  Field '{key}': {error.ErrorMessage}"
                    );
                }
            }

            Console.WriteLine("==================================================");

            /*
             * Send the user back to the EARLIEST step that has a
             * problem (not the step they submitted from). This must
             * run before building the on-screen summary below, since
             * the summary is filtered down to only that step's fields.
             */
            SetRegisterErrorStep();

            int visibleStep = (int)ViewData["RegisterStep"]!;

            string[] relevantFields =
                StepFields.TryGetValue(visibleStep, out var fields)
                    ? fields
                    : Array.Empty<string>();

            /*
             * Only surface errors that belong to the step the user is
             * actually looking at. Form-level errors (empty key, e.g.
             * the duplicate-check database exception above) are always
             * shown, since there is no field to attach them to.
             */
            List<string> visibleErrorMessages = new();

            foreach (var entry in ModelState)
            {
                bool belongsToVisibleStep =
                    string.IsNullOrEmpty(entry.Key) ||
                    relevantFields.Contains(entry.Key);

                if (!belongsToVisibleStep)
                {
                    continue;
                }

                foreach (var error in entry.Value.Errors)
                {
                    visibleErrorMessages.Add(error.ErrorMessage);
                }
            }

            /*
             * Guaranteed-visible summary banner for the current step.
             * (The view no longer also renders
             * asp-validation-summary="All" for this, since having both
             * produced a duplicate/empty box.)
             */
            ViewBag.Error =
                string.Join(" ", visibleErrorMessages);

            /*
             * Do not send passwords back to the browser.
             * Other fields remain inside model.
             */
            model.Password = "";
            model.ConfirmPassword = "";

            return View(model);
        }

        Console.WriteLine("ModelState is valid. Proceeding with registration.");

        // ==================================================
        // CREATE OTP
        // ==================================================

        string otp = GenerateOtp();

        User newUser =
            new()
            {
                Username = model.Username,
                Email = model.Email,
                Phone = model.Phone,
                Address = model.Address,

                Latitude = model.Latitude,
                Longitude = model.Longitude,

                Gender = model.Gender,

                Position = "Student",
                Avatar = "default.png",

                Status = "Pending",
                IsEmailVerified = false,

                CreatedDate = DateTime.UtcNow,

                OtpExpiresAt =
                    DateTime.UtcNow.AddMinutes(5),

                OtpLastSentAt =
                    DateTime.UtcNow,

                OtpAttemptCount = 0
            };

        newUser.Password =
            passwordHasher.HashPassword(
                newUser,
                password
            );

        newUser.OtpHash =
            passwordHasher.HashPassword(
                newUser,
                otp
            );

        // ==================================================
        // SAVE USER FIRST
        // ==================================================

        Console.WriteLine("Attempting to save pending user to the database...");

        try
        {
            db.Users.Add(newUser);

            await db.SaveChangesAsync();

            Console.WriteLine(
                $"User saved. UserId: {newUser.UserId}"
            );
        }
        catch (DbUpdateException exception)
        {
            Console.WriteLine(
                "========== DATABASE SAVE ERROR =========="
            );

            Console.WriteLine(exception.ToString());

            Console.WriteLine(
                "========================================="
            );

            /*
             * Remove entity tracking so another submit
             * does not try to insert it again.
             */
            db.Entry(newUser).State =
                EntityState.Detached;

            string message =
                "Account could not be saved. " +
                GetRealExceptionMessage(exception);

            ModelState.AddModelError("", message);

            /*
             * Redundant, guaranteed-visible fallback in case the
             * asp-validation-summary tag helper does not render.
             */
            ViewBag.Error = message;

            ViewData["RegisterStep"] = 3;

            model.Password = "";
            model.ConfirmPassword = "";

            return View(model);
        }
        catch (Exception exception)
        {
            Console.WriteLine(
                "========== GENERAL SAVE ERROR =========="
            );

            Console.WriteLine(exception.ToString());

            Console.WriteLine(
                "========================================"
            );

            db.Entry(newUser).State =
                EntityState.Detached;

            string message =
                "An error occurred while creating the account. " +
                GetRealExceptionMessage(exception);

            ModelState.AddModelError("", message);

            ViewBag.Error = message;

            ViewData["RegisterStep"] = 3;

            model.Password = "";
            model.ConfirmPassword = "";

            return View(model);
        }

        // ==================================================
        // SEND OTP EMAIL
        // ==================================================

        Console.WriteLine($"OTP generated. Starting OTP email send to {newUser.Email}...");

        try
        {
            await gmailServiceHelper.SendOtpAsync(
                newUser.Email,
                newUser.Username,
                otp
            );

            Console.WriteLine(
                $"OTP email sent successfully to {newUser.Email}"
            );
        }
        catch (Exception exception)
        {
            Console.WriteLine(
                "========== OTP EMAIL ERROR =========="
            );

            Console.WriteLine(exception.ToString());

            Console.WriteLine(
                "====================================="
            );

            /*
             * The user cannot complete registration without
             * receiving an OTP, so remove the pending account.
             */
            try
            {
                User? savedUser =
                    await db.Users.FindAsync(
                        newUser.UserId
                    );

                if (savedUser != null)
                {
                    db.Users.Remove(savedUser);

                    await db.SaveChangesAsync();
                }
            }
            catch (Exception deleteException)
            {
                Console.WriteLine(
                    "Unable to remove failed pending user:"
                );

                Console.WriteLine(
                    deleteException.ToString()
                );
            }

            string message =
                "The account was not completed because the OTP email could not be sent. " +
                GetRealExceptionMessage(exception);

            ModelState.AddModelError("", message);

            /*
             * Redundant, guaranteed-visible fallback in case the
             * asp-validation-summary tag helper does not render.
             */
            ViewBag.Error = message;

            ViewData["RegisterStep"] = 3;

            model.Password = "";
            model.ConfirmPassword = "";

            return View(model);
        }

        // ==================================================
        // REDIRECT TO VERIFY OTP
        // ==================================================

        Console.WriteLine($"Redirecting to VerifyOtp for UserId: {newUser.UserId}");

        TempData["Message"] =
            "A 6-digit OTP has been sent to your Gmail.";

        return RedirectToAction(
            nameof(VerifyOtp),
            new
            {
                userId = newUser.UserId
            }
        );
    }

    // ==================================================
    // VERIFY OTP GET
    // ==================================================

    [HttpGet]
    public async Task<IActionResult> VerifyOtp(int userId)
    {
        User? user =
            await db.Users.FindAsync(userId);

        if (user == null)
        {
            TempData["Error"] =
                "Account not found.";

            return RedirectToAction(
                nameof(Register)
            );
        }

        if (user.IsEmailVerified)
        {
            TempData["Message"] =
                "Email has already been verified.";

            return RedirectToAction(
                nameof(Login)
            );
        }

        ViewBag.UserId = user.UserId;

        ViewBag.Email =
            MaskEmail(user.Email);

        ViewBag.Message =
            TempData["Message"];

        ViewBag.Error =
            TempData["Error"];

        return View();
    }

    // ==================================================
    // VERIFY OTP POST
    // ==================================================

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> VerifyOtp(
        int userId,
        string otp
    )
    {
        otp =
            Regex.Replace(
                otp ?? "",
                @"\D",
                ""
            );

        User? user =
            await db.Users.FindAsync(userId);

        if (user == null)
        {
            TempData["Error"] =
                "Account not found.";

            return RedirectToAction(
                nameof(Register)
            );
        }

        ViewBag.UserId = user.UserId;

        ViewBag.Email =
            MaskEmail(user.Email);

        if (user.IsEmailVerified)
        {
            TempData["Message"] =
                "Email has already been verified.";

            return RedirectToAction(
                nameof(Login)
            );
        }

        if (otp.Length != 6)
        {
            ViewBag.Error =
                "Please enter the complete 6-digit OTP.";

            return View();
        }

        if (
            user.OtpExpiresAt == null ||
            user.OtpExpiresAt < DateTime.UtcNow
        )
        {
            ViewBag.Error =
                "OTP has expired. Please request a new OTP.";

            return View();
        }

        if (user.OtpAttemptCount >= 5)
        {
            ViewBag.Error =
                "Too many incorrect attempts. Please request a new OTP.";

            return View();
        }

        if (string.IsNullOrWhiteSpace(user.OtpHash))
        {
            ViewBag.Error =
                "No active OTP was found.";

            return View();
        }

        PasswordVerificationResult result =
            passwordHasher.VerifyHashedPassword(
                user,
                user.OtpHash,
                otp
            );

        if (result == PasswordVerificationResult.Failed)
        {
            user.OtpAttemptCount++;

            await db.SaveChangesAsync();

            int remainingAttempts =
                Math.Max(
                    0,
                    5 - user.OtpAttemptCount
                );

            ViewBag.Error =
                $"Incorrect OTP. {remainingAttempts} attempt(s) remaining.";

            return View();
        }

        user.IsEmailVerified = true;
        user.Status = "Active";

        user.OtpHash = null;
        user.OtpExpiresAt = null;
        user.OtpLastSentAt = null;
        user.OtpAttemptCount = 0;

        await db.SaveChangesAsync();

        TempData["Message"] =
            "Email verified successfully. Please log in.";

        return RedirectToAction(
            nameof(Login)
        );
    }

    // ==================================================
    // RESEND OTP
    // ==================================================

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResendOtp(
        int userId
    )
    {
        User? user =
            await db.Users.FindAsync(userId);

        if (user == null)
        {
            TempData["Error"] =
                "Account not found.";

            return RedirectToAction(
                nameof(Register)
            );
        }

        if (user.IsEmailVerified)
        {
            TempData["Message"] =
                "Email has already been verified.";

            return RedirectToAction(
                nameof(Login)
            );
        }

        if (
            user.OtpLastSentAt.HasValue &&
            user.OtpLastSentAt.Value
                .AddSeconds(60) >
            DateTime.UtcNow
        )
        {
            int secondsRemaining =
                (int)Math.Ceiling(
                    (
                        user.OtpLastSentAt.Value
                            .AddSeconds(60) -
                        DateTime.UtcNow
                    ).TotalSeconds
                );

            TempData["Error"] =
                $"Please wait {secondsRemaining} seconds before requesting another OTP.";

            return RedirectToAction(
                nameof(VerifyOtp),
                new
                {
                    userId
                }
            );
        }

        string newOtp = GenerateOtp();

        user.OtpHash =
            passwordHasher.HashPassword(
                user,
                newOtp
            );

        user.OtpExpiresAt =
            DateTime.UtcNow.AddMinutes(5);

        user.OtpLastSentAt =
            DateTime.UtcNow;

        user.OtpAttemptCount = 0;

        try
        {
            await db.SaveChangesAsync();

            await gmailServiceHelper.SendOtpAsync(
                user.Email,
                user.Username,
                newOtp
            );
        }
        catch (Exception exception)
        {
            Console.WriteLine(
                "========== RESEND OTP ERROR =========="
            );

            Console.WriteLine(exception.ToString());

            Console.WriteLine(
                "======================================"
            );

            TempData["Error"] =
                "OTP could not be sent. " +
                GetRealExceptionMessage(exception);

            return RedirectToAction(
                nameof(VerifyOtp),
                new
                {
                    userId
                }
            );
        }

        TempData["Message"] =
            "A new OTP has been sent.";

        return RedirectToAction(
            nameof(VerifyOtp),
            new
            {
                userId
            }
        );
    }

    // ==================================================
    // LOGIN GET
    // ==================================================

    [HttpGet]
    public IActionResult Login()
    {
        ViewBag.Message =
            TempData["Message"];

        ViewBag.Error =
            TempData["Error"];

        return View();
    }

    // ==================================================
    // LOGIN POST
    // ==================================================

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(
        string username,
        string password
    )
    {
        username =
            (username ?? "")
            .Trim();

        password ??= "";

        User? user =
            await db.Users.FirstOrDefaultAsync(
                user =>
                    user.Username.ToLower() ==
                    username.ToLower()
            );

        if (user == null)
        {
            ViewBag.Error =
                "Invalid username or password.";

            return View();
        }

        PasswordVerificationResult result =
            passwordHasher.VerifyHashedPassword(
                user,
                user.Password,
                password
            );

        if (result == PasswordVerificationResult.Failed)
        {
            ViewBag.Error =
                "Invalid username or password.";

            return View();
        }

        if (
            !user.IsEmailVerified ||
            user.Status == "Pending"
        )
        {
            TempData["Error"] =
                "Please verify your Gmail before logging in.";

            return RedirectToAction(
                nameof(VerifyOtp),
                new
                {
                    userId = user.UserId
                }
            );
        }

        if (user.Status != "Active")
        {
            ViewBag.Error =
                "This account is not active.";

            return View();
        }

        return user.Position switch
        {
            "Admin" =>
                RedirectToAction(
                    "Index",
                    "Admin"
                ),

            "Teacher" =>
                RedirectToAction(
                    "Index",
                    "Teacher"
                ),

            _ =>
                RedirectToAction(
                    "Index",
                    "Student"
                )
        };
    }

    // ==================================================
    // REGISTER ERROR STEP
    // ==================================================

    private void SetRegisterErrorStep()
    {
        /*
         * Check Step 1 first, then 2, then 3, so the user lands on the
         * EARLIEST step that actually has a problem. The previous order
         * checked Step 3 first, so a submission with errors on both
         * Step 1 and Step 3 would land on Step 3 while the summary still
         * listed the Step 1 errors too — showing "Email is required"
         * etc. on a screen with no email field on it.
         */
        if (
            ModelState.ContainsKey(nameof(WebAndMobile.Models.User.Username)) ||
            ModelState.ContainsKey(nameof(WebAndMobile.Models.User.Email)) ||
            ModelState.ContainsKey(nameof(WebAndMobile.Models.User.Phone)) ||
            ModelState.ContainsKey(nameof(WebAndMobile.Models.User.Gender))
        )
        {
            ViewData["RegisterStep"] = 1;
        }
        else if (
            ModelState.ContainsKey(nameof(WebAndMobile.Models.User.Address)) ||
            ModelState.ContainsKey(nameof(WebAndMobile.Models.User.Latitude)) ||
            ModelState.ContainsKey(nameof(WebAndMobile.Models.User.Longitude))
        )
        {
            ViewData["RegisterStep"] = 2;
        }
        else if (
            ModelState.ContainsKey(nameof(WebAndMobile.Models.User.Password)) ||
            ModelState.ContainsKey(nameof(WebAndMobile.Models.User.ConfirmPassword))
        )
        {
            ViewData["RegisterStep"] = 3;
        }
        else
        {
            /*
             * No field-specific error was added (e.g. the duplicate-check
             * database call threw and its message was added under the
             * empty "" key). Stay on Step 3, where Create Account was
             * clicked from, since nothing the user entered was actually
             * invalid.
             */
            ViewData["RegisterStep"] = 3;
        }
    }

    // ==================================================
    // HELPERS
    // ==================================================

    private static string GenerateOtp()
    {
        int number =
            RandomNumberGenerator.GetInt32(
                100000,
                1000000
            );

        return number.ToString();
    }

    private static string MaskEmail(
        string email
    )
    {
        string[] parts =
            email.Split('@');

        if (parts.Length != 2)
        {
            return email;
        }

        string name = parts[0];

        string maskedName;

        if (name.Length <= 1)
        {
            maskedName =
                name + "***";
        }
        else if (name.Length == 2)
        {
            maskedName =
                name[0] + "***";
        }
        else
        {
            maskedName =
                name[0] +
                "***" +
                name[^1];
        }

        return
            $"{maskedName}@{parts[1]}";
    }

    private static bool IsValidMalaysiaPhone(
        string phone
    )
    {
        return Regex.IsMatch(
            phone,
            @"^(011\d{8}|01[2-9]\d{7})$"
        );
    }

    private static bool IsPasswordValid(
        string value
    )
    {
        return
            value.Length >= 8 &&
            value.Length <= 15 &&

            Regex.IsMatch(
                value,
                "[A-Z]"
            ) &&

            Regex.IsMatch(
                value,
                "[a-z]"
            ) &&

            Regex.IsMatch(
                value,
                @"\d"
            ) &&

            Regex.IsMatch(
                value,
                "[@$!%*?&]"
            );
    }

    private static string GetRealExceptionMessage(
        Exception exception
    )
    {
        Exception current = exception;

        while (current.InnerException != null)
        {
            current =
                current.InnerException;
        }

        return current.Message;
    }
}