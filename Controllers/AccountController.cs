using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
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

    private const string RegistrationDataKey = "Registration.Data";
    private const string RegistrationOtpKey = "Registration.Otp";
    private const string RegistrationOtpExpiryKey = "Registration.OtpExpiry";
    private const string RegistrationOtpAttemptsKey = "Registration.OtpAttempts";
    private const string RegistrationOtpResendsKey = "Registration.OtpResends";

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
        ClearRegistrationSession();

        ViewBag.Error = TempData["Error"];
        return View(new User());
    }

    // ==================================================
    // REGISTER POST
    // ==================================================

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(User model)
    {
        ModelState.Clear();

        model.Username = (model.Username ?? "").Trim();
        model.Email = (model.Email ?? "").Trim().ToLowerInvariant();
        model.Phone = Regex.Replace(model.Phone ?? "", @"\D", "");
        model.Address = (model.Address ?? "").Trim();
        model.Gender = (model.Gender ?? "").Trim().ToLowerInvariant();

        string password = model.Password ?? "";
        string confirmPassword = model.ConfirmPassword ?? "";

        if (string.IsNullOrWhiteSpace(model.Username))
        {
            ModelState.AddModelError(nameof(model.Username), "Username is required.");
        }
        else if (model.Username.Length > 30)
        {
            ModelState.AddModelError(nameof(model.Username), "Username cannot exceed 30 characters.");
        }

        if (string.IsNullOrWhiteSpace(model.Email))
        {
            ModelState.AddModelError(nameof(model.Email), "Email is required.");
        }
        else if (!GmailOnlyRegex.IsMatch(model.Email))
        {
            ModelState.AddModelError(nameof(model.Email), "Use a valid Gmail address ending with @gmail.com.");
        }

        if (string.IsNullOrWhiteSpace(model.Phone))
        {
            ModelState.AddModelError(nameof(model.Phone), "Phone number is required.");
        }
        else if (!IsValidMalaysiaPhone(model.Phone))
        {
            ModelState.AddModelError(nameof(model.Phone), "Enter a valid Malaysian phone number.");
        }

        if (model.Gender is not ("male" or "female"))
        {
            ModelState.AddModelError(nameof(model.Gender), "Please select a gender.");
        }

        if (string.IsNullOrWhiteSpace(model.Address) ||
            model.Latitude == null ||
            model.Longitude == null)
        {
            ModelState.AddModelError(nameof(model.Address), "Please choose your location on the map.");
        }
        else if (model.Latitude < -90 || model.Latitude > 90 ||
                 model.Longitude < -180 || model.Longitude > 180)
        {
            ModelState.AddModelError(nameof(model.Address), "The selected map coordinates are invalid.");
        }

        if (string.IsNullOrEmpty(password))
        {
            ModelState.AddModelError(nameof(model.Password), "Password is required.");
        }
        else if (!IsPasswordValid(password))
        {
            ModelState.AddModelError(nameof(model.Password), "Use 8–15 characters with uppercase, lowercase, number and symbol.");
        }

        if (string.IsNullOrEmpty(confirmPassword))
        {
            ModelState.AddModelError(nameof(model.ConfirmPassword), "Please confirm your password.");
        }
        else if (password != confirmPassword)
        {
            ModelState.AddModelError(nameof(model.ConfirmPassword), "Passwords do not match.");
        }

        try
        {
            if (!string.IsNullOrWhiteSpace(model.Username) &&
                await db.Users.AnyAsync(user => user.Username.ToLower() == model.Username.ToLower()))
            {
                ModelState.AddModelError(nameof(model.Username), "Username already exists.");
            }

            if (!string.IsNullOrWhiteSpace(model.Email) &&
                await db.Users.AnyAsync(user => user.Email.ToLower() == model.Email))
            {
                ModelState.AddModelError(nameof(model.Email), "Email already exists.");
            }

            if (!string.IsNullOrWhiteSpace(model.Phone) &&
                await db.Users.AnyAsync(user => user.Phone == model.Phone))
            {
                ModelState.AddModelError(nameof(model.Phone), "Phone number already exists.");
            }
        }
        catch (Exception exception)
        {
            string message = "Unable to check existing accounts. " + GetRealExceptionMessage(exception);
            ModelState.AddModelError("", message);
            ViewBag.Error = message;
        }

        if (!ModelState.IsValid)
        {
            SetRegisterErrorStep();

            int visibleStep = (int)ViewData["RegisterStep"]!;
            string[] relevantFields =
                StepFields.TryGetValue(visibleStep, out var fields)
                    ? fields
                    : Array.Empty<string>();

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

            ViewBag.Error = string.Join(" ", visibleErrorMessages);
            model.Password = "";
            model.ConfirmPassword = "";
            return View(model);
        }

        ClearRegistrationSession();

        User passwordHashUser = new()
        {
            Username = model.Username,
            Email = model.Email
        };

        PendingRegistration pending = new()
        {
            Username = model.Username,
            Email = model.Email,
            Phone = model.Phone,
            Address = model.Address,
            Latitude = model.Latitude,
            Longitude = model.Longitude,
            Gender = model.Gender,
            PasswordHash = passwordHasher.HashPassword(passwordHashUser, password)
        };

        string otp = GenerateOtp();

        try
        {
            await gmailServiceHelper.SendOtpAsync(
                pending.Email,
                pending.Username,
                otp
            );
        }
        catch (Exception exception)
        {
            string message =
                "The OTP email could not be sent. " +
                GetRealExceptionMessage(exception);

            ViewBag.Error = message;
            ViewData["RegisterStep"] = 3;
            model.Password = "";
            model.ConfirmPassword = "";
            return View(model);
        }

        HttpContext.Session.SetString(
            RegistrationDataKey,
            JsonSerializer.Serialize(pending)
        );

        HttpContext.Session.SetString(RegistrationOtpKey, otp);
        HttpContext.Session.SetString(
            RegistrationOtpExpiryKey,
            DateTime.UtcNow.AddMinutes(1).ToString("O")
        );
        HttpContext.Session.SetInt32(RegistrationOtpAttemptsKey, 0);
        HttpContext.Session.SetInt32(RegistrationOtpResendsKey, 0);

        TempData["Message"] = "A 6-digit OTP has been sent to your Gmail.";
        return RedirectToAction(nameof(VerifyOtp));
    }

    // ==================================================
    // VERIFY REGISTER OTP GET
    // ==================================================

    [HttpGet]
    public IActionResult VerifyOtp()
    {
        PendingRegistration? pending = GetPendingRegistration();

        if (pending == null)
        {
            TempData["Error"] = "Your registration session expired. Please register again.";
            return RedirectToAction(nameof(Register));
        }

        SetRegistrationOtpViewData(pending);
        ViewBag.Message = TempData["Message"];
        ViewBag.Error = TempData["Error"];
        return View();
    }

    // ==================================================
    // VERIFY REGISTER OTP POST
    // ==================================================

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> VerifyOtp(string otp)
    {
        PendingRegistration? pending = GetPendingRegistration();

        if (pending == null)
        {
            TempData["Error"] = "Your registration session expired. Please register again.";
            return RedirectToAction(nameof(Register));
        }

        otp = Regex.Replace(otp ?? "", @"\D", "");
        SetRegistrationOtpViewData(pending);

        if (otp.Length != 6)
        {
            ViewBag.Error = "Please enter the complete 6-digit OTP.";
            return View();
        }

        DateTime? expiresAt = GetRegistrationOtpExpiry();

        if (expiresAt == null || expiresAt <= DateTime.UtcNow)
        {
            ViewBag.Error = "OTP has expired. Please request a new OTP.";
            return View();
        }

        int attempts =
            HttpContext.Session.GetInt32(RegistrationOtpAttemptsKey) ?? 0;

        if (attempts >= 5)
        {
            ViewBag.Error = "Too many incorrect attempts. Please request a new OTP.";
            return View();
        }

        string? savedOtp =
            HttpContext.Session.GetString(RegistrationOtpKey);

        if (string.IsNullOrWhiteSpace(savedOtp))
        {
            ViewBag.Error = "No active OTP was found. Please request a new OTP.";
            return View();
        }

        bool otpMatches = CryptographicOperations.FixedTimeEquals(
            System.Text.Encoding.UTF8.GetBytes(savedOtp),
            System.Text.Encoding.UTF8.GetBytes(otp)
        );

        if (!otpMatches)
        {
            attempts++;
            HttpContext.Session.SetInt32(RegistrationOtpAttemptsKey, attempts);

            int remainingAttempts = Math.Max(0, 5 - attempts);
            ViewBag.Error = $"Incorrect OTP. {remainingAttempts} attempt(s) remaining.";
            return View();
        }

        bool duplicateExists = await db.Users.AnyAsync(user =>
            user.Username.ToLower() == pending.Username.ToLower() ||
            user.Email.ToLower() == pending.Email.ToLower() ||
            user.Phone == pending.Phone
        );

        if (duplicateExists)
        {
            ClearRegistrationSession();
            TempData["Error"] = "This username, Gmail, or phone number has already been registered.";
            return RedirectToAction(nameof(Register));
        }

        User newUser = new()
        {
            Username = pending.Username,
            Email = pending.Email,
            Phone = pending.Phone,
            Address = pending.Address,
            Latitude = pending.Latitude,
            Longitude = pending.Longitude,
            Gender = pending.Gender,
            Password = pending.PasswordHash,
            Position = "Student",
            Avatar = "default.png",
            Status = "Active",
            IsEmailVerified = true,
            CreatedDate = DateTime.UtcNow
        };

        try
        {
            db.Users.Add(newUser);
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException exception)
        {
            ViewBag.Error = "Account could not be created. " + GetRealExceptionMessage(exception);
            return View();
        }

        ClearRegistrationSession();
        TempData["Message"] = "Email verified and account created successfully. Please log in.";
        return RedirectToAction(nameof(Login));
    }

    // ==================================================
    // RESEND REGISTER OTP
    // ==================================================

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResendOtp()
    {
        PendingRegistration? pending = GetPendingRegistration();

        if (pending == null)
        {
            TempData["Error"] = "Your registration session expired. Please register again.";
            return RedirectToAction(nameof(Register));
        }

        int resendCount =
            HttpContext.Session.GetInt32(RegistrationOtpResendsKey) ?? 0;

        if (resendCount >= 3)
        {
            TempData["Error"] =
                "You have used all resend attempts. Please return to Register and check your Gmail address.";
            return RedirectToAction(nameof(VerifyOtp));
        }

        DateTime? currentExpiry = GetRegistrationOtpExpiry();

        if (currentExpiry.HasValue && currentExpiry.Value > DateTime.UtcNow)
        {
            TempData["Error"] = "Please wait until the current one-minute OTP expires.";
            return RedirectToAction(nameof(VerifyOtp));
        }

        string newOtp = GenerateOtp();

        try
        {
            await gmailServiceHelper.SendOtpAsync(
                pending.Email,
                pending.Username,
                newOtp
            );
        }
        catch (Exception exception)
        {
            TempData["Error"] = "OTP could not be sent. " + GetRealExceptionMessage(exception);
            return RedirectToAction(nameof(VerifyOtp));
        }

        resendCount++;
        HttpContext.Session.SetString(RegistrationOtpKey, newOtp);
        HttpContext.Session.SetString(
            RegistrationOtpExpiryKey,
            DateTime.UtcNow.AddMinutes(1).ToString("O")
        );
        HttpContext.Session.SetInt32(RegistrationOtpAttemptsKey, 0);
        HttpContext.Session.SetInt32(RegistrationOtpResendsKey, resendCount);

        TempData["Message"] = "A new one-minute OTP has been sent.";
        return RedirectToAction(nameof(VerifyOtp));
    }

    // ==================================================
    // CANCEL REGISTER OTP
    // ==================================================

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult CancelOtp()
    {
        ClearRegistrationSession();
        TempData["Error"] =
            "Registration was cancelled. Please check or change your Gmail address.";
        return RedirectToAction(nameof(Register));
    }


    // ==================================================
    // FORGOT PASSWORD
    // ==================================================

    [HttpGet]
    public IActionResult ForgotPassword()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ForgotPassword(string username, string email)
    {
        username = (username ?? "").Trim();
        email = (email ?? "").Trim().ToLowerInvariant();

        User? user = await db.Users.FirstOrDefaultAsync(u =>
            u.Username.ToLower() == username.ToLower() &&
            u.Email.ToLower() == email);

        if (user == null)
        {
            ViewBag.Error = "The username or registered Gmail address is incorrect. User cannot be found.";
            return View();
        }

        string otp = GenerateOtp();
        user.OtpHash = passwordHasher.HashPassword(user, otp);
        user.OtpExpiresAt = DateTime.UtcNow.AddMinutes(1);
        user.OtpLastSentAt = DateTime.UtcNow;
        user.OtpAttemptCount = 0;
        user.OtpResendCount = 0;
        await gmailServiceHelper.SendPasswordResetOtpAsync(user.Email, user.Username, otp);
        await db.SaveChangesAsync();

        return RedirectToAction(nameof(ForgotPasswordOtp), new { userId = user.UserId });
    }

    [HttpGet]
    public async Task<IActionResult> ForgotPasswordOtp(int userId)
    {
        User? user = await db.Users.FindAsync(userId);
        if (user == null) return RedirectToAction(nameof(ForgotPassword));
        ViewBag.UserId = user.UserId;
        ViewBag.Email = MaskEmail(user.Email);
        ViewBag.Error = TempData["Error"];
        ViewBag.Message = TempData["Message"];
        SetOtpViewData(user);
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ForgotPasswordOtp(int userId, string otp)
    {
        User? user = await db.Users.FindAsync(userId);
        if (user == null) return RedirectToAction(nameof(ForgotPassword));
        otp = Regex.Replace(otp ?? "", @"\D", "");

        if (user.OtpExpiresAt == null || user.OtpExpiresAt < DateTime.UtcNow)
        {
            TempData["Error"] = "OTP has expired.";
            return RedirectToAction(nameof(ForgotPasswordOtp), new { userId });
        }

        if (string.IsNullOrWhiteSpace(user.OtpHash) ||
            passwordHasher.VerifyHashedPassword(user, user.OtpHash, otp) == PasswordVerificationResult.Failed)
        {
            TempData["Error"] = "Incorrect OTP.";
            return RedirectToAction(nameof(ForgotPasswordOtp), new { userId });
        }

        HttpContext.Session.SetInt32("PasswordResetUserId", user.UserId);
        user.OtpHash = null;
        user.OtpExpiresAt = null;
        await db.SaveChangesAsync();
        return RedirectToAction(nameof(ResetPassword));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResendForgotPasswordOtp(int userId)
    {
        User? user = await db.Users.FindAsync(userId);
        if (user == null) return RedirectToAction(nameof(ForgotPassword));

        if (user.OtpResendCount >= 3)
        {
            TempData["Error"] = "No resend attempts remain. Please check the username and use the registered Gmail address.";
            return RedirectToAction(nameof(ForgotPasswordOtp), new { userId });
        }

        if (user.OtpExpiresAt.HasValue && user.OtpExpiresAt > DateTime.UtcNow)
        {
            TempData["Error"] = "Please wait until the current OTP expires.";
            return RedirectToAction(nameof(ForgotPasswordOtp), new { userId });
        }

        string otp = GenerateOtp();
        user.OtpHash = passwordHasher.HashPassword(user, otp);
        user.OtpExpiresAt = DateTime.UtcNow.AddMinutes(1);
        user.OtpLastSentAt = DateTime.UtcNow;
        user.OtpResendCount++;
        await gmailServiceHelper.SendPasswordResetOtpAsync(user.Email, user.Username, otp);
        await db.SaveChangesAsync();
        TempData["Message"] = "A new OTP has been sent.";
        return RedirectToAction(nameof(ForgotPasswordOtp), new { userId });
    }

    [HttpGet]
    public IActionResult ResetPassword()
    {
        if (HttpContext.Session.GetInt32("PasswordResetUserId") == null)
            return RedirectToAction(nameof(ForgotPassword));
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(string newPassword, string confirmPassword)
    {
        int? userId = HttpContext.Session.GetInt32("PasswordResetUserId");
        if (userId == null) return RedirectToAction(nameof(ForgotPassword));

        if (!IsPasswordValid(newPassword ?? ""))
        {
            ViewBag.Error = "Use 8–15 characters with uppercase, lowercase, number and symbol.";
            return View();
        }
        if (newPassword != confirmPassword)
        {
            ViewBag.Error = "Passwords do not match.";
            return View();
        }

        User? user = await db.Users.FindAsync(userId.Value);
        if (user == null)
        {
            ViewBag.Error = "User cannot be found.";
            return View();
        }

        user.Password = passwordHasher.HashPassword(user, newPassword);
        user.OtpHash = null;
        user.OtpExpiresAt = null;
        user.OtpResendCount = 0;
        await db.SaveChangesAsync();
        HttpContext.Session.Remove("PasswordResetUserId");
        TempData["Message"] = "Password updated successfully. Please log in.";
        return RedirectToAction(nameof(Login));
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

        return View(new LoginViewModel());
    }

    // ==================================================
    // LOGIN POST
    // ==================================================
    //
    // Failed-attempt lockout (Student/Teacher only, never Admin):
    //   - FailedLoginAttempts and LockoutEnd are persisted on the User
    //     row in the database, NOT in Session/TempData/ViewBag/hidden
    //     fields, so it survives refresh, browser change, or a
    //     disabled-JavaScript client.
    //   - 4 total attempts. After the 4th wrong password, LockoutEnd is
    //     set to UtcNow + 15 minutes and further attempts are rejected
    //     until it expires.

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(
        LoginViewModel model
    )
    {
        model.Username =
            (model.Username ?? "")
            .Trim();

        model.Password ??= "";

        if (
            string.IsNullOrWhiteSpace(model.Username) ||
            string.IsNullOrEmpty(model.Password)
        )
        {
            ViewBag.Error =
                "Please enter your username and password.";

            return View(model);
        }

        User? user =
            await db.Users.FirstOrDefaultAsync(
                dbUser =>
                    dbUser.Username.ToLower() ==
                    model.Username.ToLower()
            );

        // ==================================================
        // LOCKOUT CHECK (only applies if the account exists)
        // ==================================================

        if (
            user != null &&
            IsLockoutApplicable(user) &&
            user.LockoutEnd.HasValue
        )
        {
            if (user.LockoutEnd.Value > DateTime.UtcNow)
            {
                TimeSpan remaining =
                    user.LockoutEnd.Value - DateTime.UtcNow;

                ViewBag.Error =
                    "Login attempts have been used up!!!! " +
                    $"Please try again after {remaining.Minutes} minutes and {remaining.Seconds} seconds.";

                ViewBag.LoginWarningLevel = "locked";

                return View(model);
            }

            /*
             * Lockout window has expired. Reset the counter and
             * lockout, then continue on to normal password checking
             * below.
             */
            user.FailedLoginAttempts = 0;
            user.LockoutEnd = null;

            await db.SaveChangesAsync();
        }

        if (user == null)
        {
            /*
             * Do not reveal whether the username exists.
             */
            ViewBag.Error =
                "Invalid username or password.";

            return View(model);
        }

        PasswordVerificationResult result =
            passwordHasher.VerifyHashedPassword(
                user,
                user.Password,
                model.Password
            );

        if (result == PasswordVerificationResult.Failed)
        {
            if (IsLockoutApplicable(user))
            {
                user.FailedLoginAttempts++;

                if (user.FailedLoginAttempts >= 4)
                {
                    user.LockoutEnd =
                        DateTime.UtcNow.AddMinutes(15);

                    await db.SaveChangesAsync();

                    ViewBag.Error =
                        "Login attempts have been used up!!!!";

                    ViewBag.LoginWarningLevel = "locked";

                    return View(model);
                }

                await db.SaveChangesAsync();

                int remainingAttempts =
                    4 - user.FailedLoginAttempts;

                ViewBag.Error =
                    $"Password incorrect. You have {remainingAttempts} attempt(s) remaining.";

                ViewBag.LoginWarningLevel = remainingAttempts switch
                {
                    3 => "yellow",
                    2 => "orange",
                    _ => "red"
                };

                return View(model);
            }

            /*
             * Admin accounts: no lockout, generic message only.
             */
            ViewBag.Error =
                "Invalid username or password.";

            return View(model);
        }

        // ==================================================
        // PASSWORD CORRECT -> RESET LOCKOUT STATE
        // ==================================================

        if (
            IsLockoutApplicable(user) &&
            (user.FailedLoginAttempts != 0 || user.LockoutEnd != null)
        )
        {
            user.FailedLoginAttempts = 0;
            user.LockoutEnd = null;

            await db.SaveChangesAsync();
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

            return View(model);
        }

        // ==================================================
        // SIGN IN (COOKIE AUTHENTICATION)
        // ==================================================

        List<Claim> claims = new()
        {
            new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Role, user.Position)
        };

        ClaimsIdentity identity =
            new(claims, CookieAuthenticationDefaults.AuthenticationScheme);

        ClaimsPrincipal principal =
            new(identity);

        AuthenticationProperties authProperties = new()
        {
            IsPersistent = model.RememberMe,

            ExpiresUtc =
                model.RememberMe
                    ? DateTimeOffset.UtcNow.AddDays(30)
                    : null,

            AllowRefresh = true
        };

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            authProperties
        );

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
    // LOGOUT
    // ==================================================

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(
            CookieAuthenticationDefaults.AuthenticationScheme
        );

        return RedirectToAction(
            nameof(Login)
        );
    }

    // ==================================================
    // LOCKOUT HELPER
    // ==================================================

    /*
     * The failed-attempt lockout only applies to Student and Teacher
     * accounts. Admin accounts are never locked by this feature.
     */
    private static bool IsLockoutApplicable(User user)
    {
        return
            user.Position == "Student" ||
            user.Position == "Teacher";
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

    private PendingRegistration? GetPendingRegistration()
    {
        string? json = HttpContext.Session.GetString(RegistrationDataKey);

        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<PendingRegistration>(json);
        }
        catch (JsonException)
        {
            ClearRegistrationSession();
            return null;
        }
    }

    private DateTime? GetRegistrationOtpExpiry()
    {
        string? text =
            HttpContext.Session.GetString(RegistrationOtpExpiryKey);

        if (DateTime.TryParse(
            text,
            null,
            System.Globalization.DateTimeStyles.RoundtripKind,
            out DateTime value))
        {
            return value.ToUniversalTime();
        }

        return null;
    }

    private void SetRegistrationOtpViewData(PendingRegistration pending)
    {
        int resendCount =
            HttpContext.Session.GetInt32(RegistrationOtpResendsKey) ?? 0;

        int remaining = Math.Max(0, 3 - resendCount);
        DateTime? expiry = GetRegistrationOtpExpiry();

        ViewBag.Email = MaskEmail(pending.Email);
        ViewBag.ResendRemaining = remaining;
        ViewBag.OtpExpiresAtUtc = expiry?.ToString("O");
        ViewBag.ResendClass = remaining switch
        {
            3 => "resend-cyan",
            2 => "resend-yellow",
            1 => "resend-red",
            _ => "resend-gray"
        };
    }

    private void ClearRegistrationSession()
    {
        HttpContext.Session.Remove(RegistrationDataKey);
        HttpContext.Session.Remove(RegistrationOtpKey);
        HttpContext.Session.Remove(RegistrationOtpExpiryKey);
        HttpContext.Session.Remove(RegistrationOtpAttemptsKey);
        HttpContext.Session.Remove(RegistrationOtpResendsKey);
    }

    private sealed class PendingRegistration
    {
        public string Username { get; set; } = "";
        public string Email { get; set; } = "";
        public string Phone { get; set; } = "";
        public string Address { get; set; } = "";
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public string Gender { get; set; } = "";
        public string PasswordHash { get; set; } = "";
    }

    private void SetOtpViewData(User user)
    {
        int remaining = Math.Max(0, 3 - user.OtpResendCount);
        ViewBag.ResendRemaining = remaining;
        ViewBag.OtpExpiresAtUtc = user.OtpExpiresAt?.ToString("O");
        ViewBag.ResendClass = remaining switch
        {
            3 => "resend-cyan",
            2 => "resend-yellow",
            1 => "resend-red",
            _ => "resend-gray"
        };
    }
}