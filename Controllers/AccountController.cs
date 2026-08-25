using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
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

    private const string PasswordResetDataKey = "PasswordReset.Data";
    private const string PasswordResetOtpKey = "PasswordReset.Otp";
    private const string PasswordResetOtpExpiryKey = "PasswordReset.OtpExpiry";
    private const string PasswordResetOtpAttemptsKey = "PasswordReset.OtpAttempts";
    private const string PasswordResetOtpResendsKey = "PasswordReset.OtpResends";

    private const int MaximumOtpAttempts = 5;
    private const int MaximumOtpResends = 3;

    private static readonly Regex GmailOnlyRegex = new(
        @"^[a-zA-Z0-9._%+\-]+@gmail\.com$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Dictionary<int, string[]> StepFields = new()
    {
        [1] =
        [
            nameof(WebAndMobile.Models.User.Username),
            nameof(WebAndMobile.Models.User.Email),
            nameof(WebAndMobile.Models.User.Phone),
            nameof(WebAndMobile.Models.User.Gender)
        ],
        [2] = [nameof(WebAndMobile.Models.User.Address)],
        [3] = [nameof(WebAndMobile.Models.User.Password), nameof(WebAndMobile.Models.User.ConfirmPassword)]
    };

    public AccountController(
        DB db,
        IPasswordHasher<User> passwordHasher,
        GmailServiceHelper gmailServiceHelper)
    {
        this.db = db;
        this.passwordHasher = passwordHasher;
        this.gmailServiceHelper = gmailServiceHelper;
    }

    // ==================================================
    // REGISTER
    // ==================================================

    [HttpGet]
    public IActionResult Register()
    {
        ClearRegistrationSession();
        ViewBag.Error = TempData["Error"];
        return View(new User());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(
        User model,
        double? latitude,
        double? longitude)
    {
        ModelState.Clear();

        model.Username = (model.Username ?? "").Trim();
        model.Email = (model.Email ?? "").Trim().ToLowerInvariant();
        model.Phone = Regex.Replace(model.Phone ?? "", @"\D", "");
        model.Address = (model.Address ?? "").Trim();
        model.Gender = (model.Gender ?? "").Trim().ToLowerInvariant();

        string password = model.Password ?? "";
        string confirmPassword = model.ConfirmPassword ?? "";

        ValidateRegistration(model, password, confirmPassword, latitude, longitude);

        try
        {
            if (!string.IsNullOrWhiteSpace(model.Username) &&
                await db.Users.AnyAsync(u => u.Username.ToLower() == model.Username.ToLower()))
            {
                ModelState.AddModelError(nameof(model.Username), "Username already exists.");
            }

            if (!string.IsNullOrWhiteSpace(model.Email) &&
                await db.Users.AnyAsync(u => u.Email.ToLower() == model.Email))
            {
                ModelState.AddModelError(nameof(model.Email), "Email already exists.");
            }

            if (!string.IsNullOrWhiteSpace(model.Phone) &&
                await db.Users.AnyAsync(u => u.Phone == model.Phone))
            {
                ModelState.AddModelError(nameof(model.Phone), "Phone number already exists.");
            }
        }
        catch (Exception exception)
        {
            ModelState.AddModelError(
                "",
                "Unable to check existing accounts. " + GetRealExceptionMessage(exception));
        }

        if (!ModelState.IsValid)
        {
            SetRegisterErrorStep();
            SetVisibleRegisterError();
            model.Password = "";
            model.ConfirmPassword = "";
            return View(model);
        }

        User hashingUser = new()
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
            Gender = model.Gender,
            PasswordHash = passwordHasher.HashPassword(hashingUser, password)
        };

        string otp = GenerateOtp();

        try
        {
            await gmailServiceHelper.SendOtpAsync(
                pending.Email,
                pending.Username,
                otp);
        }
        catch (Exception exception)
        {
            ViewBag.Error =
                "The OTP email could not be sent. " +
                GetRealExceptionMessage(exception);

            ViewData["RegisterStep"] = 3;
            model.Password = "";
            model.ConfirmPassword = "";
            return View(model);
        }

        ClearRegistrationSession();
        HttpContext.Session.SetString(
            RegistrationDataKey,
            JsonSerializer.Serialize(pending));
        HttpContext.Session.SetString(RegistrationOtpKey, otp);
        HttpContext.Session.SetString(
            RegistrationOtpExpiryKey,
            DateTime.UtcNow.AddMinutes(1).ToString("O"));
        HttpContext.Session.SetInt32(RegistrationOtpAttemptsKey, 0);
        HttpContext.Session.SetInt32(RegistrationOtpResendsKey, 0);

        TempData["Message"] = "A 6-digit OTP has been sent to your Gmail.";
        return RedirectToAction(nameof(VerifyOtp));
    }

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

        SetRegistrationOtpViewData(pending);
        otp = Regex.Replace(otp ?? "", @"\D", "");

        if (!ValidateSessionOtp(
                otp,
                RegistrationOtpKey,
                RegistrationOtpExpiryKey,
                RegistrationOtpAttemptsKey,
                out string error))
        {
            ViewBag.Error = error;
            return View();
        }

        bool duplicateExists = await db.Users.AnyAsync(u =>
            u.Username.ToLower() == pending.Username.ToLower() ||
            u.Email.ToLower() == pending.Email.ToLower() ||
            u.Phone == pending.Phone);

        if (duplicateExists)
        {
            ClearRegistrationSession();
            TempData["Error"] =
                "This username, Gmail, or phone number has already been registered.";
            return RedirectToAction(nameof(Register));
        }

        User newUser = new()
        {
            Username = pending.Username,
            Email = pending.Email,
            Phone = pending.Phone,
            Address = pending.Address,
            Gender = pending.Gender,
            Password = pending.PasswordHash,
            Position = "Student",
            Avatar = "default.png",
            Status = "Active"
        };

        try
        {
            db.Users.Add(newUser);
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException exception)
        {
            ViewBag.Error =
                "Account could not be created. " +
                GetRealExceptionMessage(exception);
            return View();
        }

        ClearRegistrationSession();
        TempData["Message"] =
            "Email verified and account created successfully. Please log in.";
        return RedirectToAction(nameof(Login));
    }

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

        if (resendCount >= MaximumOtpResends)
        {
            TempData["Error"] =
                "You have used all resend attempts. Please register again.";
            return RedirectToAction(nameof(VerifyOtp));
        }

        DateTime? currentExpiry = GetSessionDateTime(RegistrationOtpExpiryKey);
        if (currentExpiry.HasValue && currentExpiry.Value > DateTime.UtcNow)
        {
            TempData["Error"] = "Please wait until the current OTP expires.";
            return RedirectToAction(nameof(VerifyOtp));
        }

        string otp = GenerateOtp();

        try
        {
            await gmailServiceHelper.SendOtpAsync(
                pending.Email,
                pending.Username,
                otp);
        }
        catch (Exception exception)
        {
            TempData["Error"] =
                "OTP could not be sent. " + GetRealExceptionMessage(exception);
            return RedirectToAction(nameof(VerifyOtp));
        }

        HttpContext.Session.SetString(RegistrationOtpKey, otp);
        HttpContext.Session.SetString(
            RegistrationOtpExpiryKey,
            DateTime.UtcNow.AddMinutes(1).ToString("O"));
        HttpContext.Session.SetInt32(RegistrationOtpAttemptsKey, 0);
        HttpContext.Session.SetInt32(RegistrationOtpResendsKey, resendCount + 1);

        TempData["Message"] = "A new one-minute OTP has been sent.";
        return RedirectToAction(nameof(VerifyOtp));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult CancelOtp()
    {
        ClearRegistrationSession();
        TempData["Error"] =
            "Registration was cancelled. Please check your information and try again.";
        return RedirectToAction(nameof(Register));
    }
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CheckRegistrationAvailability(
    string username,
    string email,
    string phone)
    {
        username = (username ?? "").Trim();
        email = (email ?? "").Trim().ToLowerInvariant();
        phone = Regex.Replace(phone ?? "", @"\D", "");

        if (string.IsNullOrWhiteSpace(username))
        {
            return BadRequest(new
            {
                success = false,
                field = "username",
                message = "Username is required."
            });
        }

        if (string.IsNullOrWhiteSpace(email))
        {
            return BadRequest(new
            {
                success = false,
                field = "email",
                message = "Email is required."
            });
        }

        if (string.IsNullOrWhiteSpace(phone))
        {
            return BadRequest(new
            {
                success = false,
                field = "phone",
                message = "Phone number is required."
            });
        }

        try
        {
            bool usernameExists = await db.Users.AnyAsync(u =>
                u.Username.ToLower() == username.ToLower());

            bool emailExists = await db.Users.AnyAsync(u =>
                u.Email.ToLower() == email);

            bool phoneExists = await db.Users.AnyAsync(u =>
                u.Phone == phone);

            return Json(new
            {
                success = true,

                username = new
                {
                    available = !usernameExists,
                    message = usernameExists
                        ? "Username already exists."
                        : "Username is available."
                },

                email = new
                {
                    available = !emailExists,
                    message = emailExists
                        ? "Gmail address already exists."
                        : "Gmail address is available."
                },

                phone = new
                {
                    available = !phoneExists,
                    message = phoneExists
                        ? "Phone number already exists."
                        : "Phone number is available."
                },

                allAvailable =
                    !usernameExists &&
                    !emailExists &&
                    !phoneExists
            });
        }
        catch (Exception exception)
        {
            return StatusCode(500, new
            {
                success = false,
                message =
                    "Unable to check the database. " +
                    GetRealExceptionMessage(exception)
            });
        }
    }
    // ==================================================
    // FORGOT PASSWORD
    // All OTP/reset temporary data is stored in Session.
    // No OTP columns are required in dbo.Users.
    // ==================================================

    [HttpGet]
    public IActionResult ForgotPassword()
    {
        ViewBag.Error = TempData["Error"];
        ViewBag.Message = TempData["Message"];
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ForgotPassword(
        string username,
        string password,
        string confirmPassword)
    {
        username = (username ?? "").Trim();
        password ??= "";
        confirmPassword ??= "";
        ViewBag.Username = username;

        if (string.IsNullOrWhiteSpace(username))
        {
            ViewBag.Error = "Username is required.";
            return View();
        }

        if (!IsPasswordValid(password))
        {
            ViewBag.Error =
                "Use 8–15 characters with uppercase, lowercase, number and symbol.";
            return View();
        }

        if (password != confirmPassword)
        {
            ViewBag.Error = "Passwords do not match.";
            return View();
        }

        User? user = await db.Users.FirstOrDefaultAsync(u =>
            u.Username.ToLower() == username.ToLower());

        if (user == null)
        {
            ViewBag.Error = "User cannot be found.";
            return View();
        }

        string otp = GenerateOtp();
        string newPasswordHash = passwordHasher.HashPassword(user, password);

        try
        {
            await gmailServiceHelper.SendPasswordResetOtpAsync(
                user.Email,
                user.Username,
                otp);
        }
        catch (Exception exception)
        {
            ViewBag.Error =
                "The password reset OTP could not be sent. " +
                GetRealExceptionMessage(exception);
            return View();
        }

        ClearPasswordResetSession();
        PasswordResetData pending = new()
        {
            UserId = user.UserId,
            MaskedEmail = MaskEmail(user.Email),
            NewPasswordHash = newPasswordHash
        };

        HttpContext.Session.SetString(
            PasswordResetDataKey,
            JsonSerializer.Serialize(pending));
        HttpContext.Session.SetString(PasswordResetOtpKey, otp);
        HttpContext.Session.SetString(
            PasswordResetOtpExpiryKey,
            DateTime.UtcNow.AddMinutes(1).ToString("O"));
        HttpContext.Session.SetInt32(PasswordResetOtpAttemptsKey, 0);
        HttpContext.Session.SetInt32(PasswordResetOtpResendsKey, 0);

        TempData["Message"] = "A password reset OTP has been sent to your Gmail.";
        return RedirectToAction(nameof(ResetPassword));
    }

    // This GET displays your current ResetPassword.cshtml OTP page.
    [HttpGet]
    public IActionResult ResetPassword()
    {
        PasswordResetData? pending = GetPasswordResetData();
        if (pending == null)
        {
            TempData["Error"] = "Your password reset session expired. Please try again.";
            return RedirectToAction(nameof(ForgotPassword));
        }

        SetPasswordResetOtpViewData(pending);
        ViewBag.Message = TempData["Message"];
        ViewBag.Error = TempData["Error"];
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ForgotPasswordOtp(int userId, string otp)
    {
        PasswordResetData? pending = GetPasswordResetData();

        if (pending == null || pending.UserId != userId)
        {
            TempData["Error"] = "Your password reset session expired. Please try again.";
            return RedirectToAction(nameof(ForgotPassword));
        }

        if (!ValidateSessionOtp(
                Regex.Replace(otp ?? "", @"\D", ""),
                PasswordResetOtpKey,
                PasswordResetOtpExpiryKey,
                PasswordResetOtpAttemptsKey,
                out string error))
        {
            TempData["Error"] = error;
            return RedirectToAction(nameof(ResetPassword));
        }

        User? user = await db.Users.FindAsync(pending.UserId);
        if (user == null)
        {
            ClearPasswordResetSession();
            TempData["Error"] = "User cannot be found.";
            return RedirectToAction(nameof(ForgotPassword));
        }

        user.Password = pending.NewPasswordHash;
        await db.SaveChangesAsync();

        ClearPasswordResetSession();
        TempData["Message"] = "Password updated successfully. Please log in.";
        return RedirectToAction(nameof(Login));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResendForgotPasswordOtp(int userId)
    {
        PasswordResetData? pending = GetPasswordResetData();

        if (pending == null || pending.UserId != userId)
        {
            TempData["Error"] = "Your password reset session expired. Please try again.";
            return RedirectToAction(nameof(ForgotPassword));
        }

        int resendCount =
            HttpContext.Session.GetInt32(PasswordResetOtpResendsKey) ?? 0;

        if (resendCount >= MaximumOtpResends)
        {
            TempData["Error"] = "No resend attempts remain.";
            return RedirectToAction(nameof(ResetPassword));
        }

        DateTime? expiry = GetSessionDateTime(PasswordResetOtpExpiryKey);
        if (expiry.HasValue && expiry.Value > DateTime.UtcNow)
        {
            TempData["Error"] = "Please wait until the current OTP expires.";
            return RedirectToAction(nameof(ResetPassword));
        }

        User? user = await db.Users.FindAsync(pending.UserId);
        if (user == null)
        {
            ClearPasswordResetSession();
            TempData["Error"] = "User cannot be found.";
            return RedirectToAction(nameof(ForgotPassword));
        }

        string otp = GenerateOtp();

        try
        {
            await gmailServiceHelper.SendPasswordResetOtpAsync(
                user.Email,
                user.Username,
                otp);
        }
        catch (Exception exception)
        {
            TempData["Error"] =
                "OTP could not be sent. " + GetRealExceptionMessage(exception);
            return RedirectToAction(nameof(ResetPassword));
        }

        HttpContext.Session.SetString(PasswordResetOtpKey, otp);
        HttpContext.Session.SetString(
            PasswordResetOtpExpiryKey,
            DateTime.UtcNow.AddMinutes(1).ToString("O"));
        HttpContext.Session.SetInt32(PasswordResetOtpAttemptsKey, 0);
        HttpContext.Session.SetInt32(PasswordResetOtpResendsKey, resendCount + 1);

        TempData["Message"] = "A new OTP has been sent.";
        return RedirectToAction(nameof(ResetPassword));
    }

    // ==================================================
    // LOGIN
    // Login-attempt data is stored in Session, not dbo.Users.
    // ==================================================

    [HttpGet]
    public IActionResult Login()
    {
        ViewBag.Message = TempData["Message"];
        ViewBag.Error = TempData["Error"];
        return View(new LoginViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        model.Username = (model.Username ?? "").Trim();
        model.Password ??= "";

        if (string.IsNullOrWhiteSpace(model.Username) ||
            string.IsNullOrEmpty(model.Password))
        {
            ViewBag.Error = "Please enter your username and password.";
            return View(model);
        }

        string attemptKey = GetLoginAttemptsKey(model.Username);
        string lockoutKey = GetLoginLockoutKey(model.Username);

        User? user = await db.Users.FirstOrDefaultAsync(u =>
            u.Username.ToLower() == model.Username.ToLower());

        if (user == null)
        {
            ViewBag.Error = "Invalid username or password.";
            return View(model);
        }

        // A permanently blocked account cannot use username/password login.
        if (string.Equals(user.Status, "Blocked", StringComparison.OrdinalIgnoreCase))
        {
            HttpContext.Session.Remove(attemptKey);
            HttpContext.Session.Remove(lockoutKey);

            ViewBag.Error =
                "ACCOUNT PERMANENTLY BLOCKED. This account can no longer sign in.";
            ViewBag.LoginWarningLevel = "locked";
            return View(model);
        }

        // Keep the old Active-status protection for any other inactive status.
        if (!string.Equals(user.Status, "Active", StringComparison.OrdinalIgnoreCase))
        {
            ViewBag.Error = "This account is not active.";
            ViewBag.LoginWarningLevel = "locked";
            return View(model);
        }

        DateTime? lockoutEnd = GetSessionDateTime(lockoutKey);

        if (lockoutEnd.HasValue && lockoutEnd.Value > DateTime.UtcNow)
        {
            ViewBag.Error = "ACCOUNT TEMPORARILY LOCKED.";
            ViewBag.LoginWarningLevel = "locked";
            ViewBag.LockoutEndUtc = lockoutEnd.Value.ToString("O");
            return View(model);
        }

        // The 15-minute lock has expired.
        // IMPORTANT: remove only the timer. Keep Attempts = 4 so the next
        // incorrect password becomes attempt #5 and permanently blocks the account.
        if (lockoutEnd.HasValue)
        {
            HttpContext.Session.Remove(lockoutKey);
        }

        PasswordVerificationResult result =
            passwordHasher.VerifyHashedPassword(user, user.Password, model.Password);

        if (result == PasswordVerificationResult.Failed)
        {
            if (IsLockoutApplicable(user))
            {
                int attempts =
                    (HttpContext.Session.GetInt32(attemptKey) ?? 0) + 1;

                HttpContext.Session.SetInt32(attemptKey, attempts);

                // Fifth incorrect password: permanent database block.
                if (attempts >= 5)
                {
                    user.Status = "Blocked";
                    await db.SaveChangesAsync();

                    HttpContext.Session.Remove(attemptKey);
                    HttpContext.Session.Remove(lockoutKey);

                    ViewBag.Error =
                        "ACCOUNT PERMANENTLY BLOCKED. You used your final login attempt. " +
                        "This account can no longer sign in.";
                    ViewBag.LoginWarningLevel = "locked";
                    return View(model);
                }

                // Fourth incorrect password: 15-minute temporary lock.
                if (attempts == 4)
                {
                    DateTime end = DateTime.UtcNow.AddMinutes(15);
                    HttpContext.Session.SetString(lockoutKey, end.ToString("O"));

                    ViewBag.Error = "ACCOUNT TEMPORARILY LOCKED.";
                    ViewBag.LoginWarningLevel = "locked";
                    ViewBag.LockoutEndUtc = end.ToString("O");
                    return View(model);
                }

                int remainingBeforeTemporaryLock = 4 - attempts;

                ViewBag.Error = remainingBeforeTemporaryLock switch
                {
                    3 => "Password incorrect. You have 3 attempts remaining.",
                    2 => "Password incorrect. You have 2 attempts remaining.",
                    _ => "Password incorrect. You have 1 attempt remaining before a 15-minute lock."
                };

                ViewBag.LoginWarningLevel = remainingBeforeTemporaryLock switch
                {
                    3 => "yellow",
                    2 => "orange",
                    _ => "red"
                };

                return View(model);
            }

            ViewBag.Error = "Invalid username or password.";
            return View(model);
        }

        // Correct password: clear the failed-attempt state.
        HttpContext.Session.Remove(attemptKey);
        HttpContext.Session.Remove(lockoutKey);

        List<Claim> claims =
        [
            new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Role, user.Position)
        ];

        ClaimsIdentity identity = new(
            claims,
            CookieAuthenticationDefaults.AuthenticationScheme);

        // Real ASP.NET Core authentication cookie.
        // Remember Me ON  -> persistent cookie for 30 days.
        // Remember Me OFF -> non-persistent authentication session.
        AuthenticationProperties properties = new()
        {
            IsPersistent = model.RememberMe,
            ExpiresUtc = model.RememberMe
                ? DateTimeOffset.UtcNow.AddDays(30)
                : null,
            AllowRefresh = true
        };

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity),
            properties);

        if (user.Position == "Teacher")
        {
            return RedirectToAction("Index", "Teacher");
        }

        if (user.Position == "Student")
        {
            return RedirectToAction("Index", "Student");
        }

        return RedirectToAction("Index", "Admin");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(
            CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Login));
    }

    // ==================================================
    // HELPERS
    // ==================================================

    private void ValidateRegistration(
        User model,
        string password,
        string confirmPassword,
        double? latitude,
        double? longitude)
    {
        if (string.IsNullOrWhiteSpace(model.Username))
            ModelState.AddModelError(nameof(model.Username), "Username is required.");
        else if (model.Username.Length > 30)
            ModelState.AddModelError(nameof(model.Username), "Username cannot exceed 30 characters.");

        if (string.IsNullOrWhiteSpace(model.Email))
            ModelState.AddModelError(nameof(model.Email), "Email is required.");
        else if (!GmailOnlyRegex.IsMatch(model.Email))
            ModelState.AddModelError(nameof(model.Email), "Use a valid Gmail address ending with @gmail.com.");

        if (string.IsNullOrWhiteSpace(model.Phone))
            ModelState.AddModelError(nameof(model.Phone), "Phone number is required.");
        else if (!IsValidMalaysiaPhone(model.Phone))
            ModelState.AddModelError(nameof(model.Phone), "Enter a valid Malaysian phone number.");

        if (model.Gender is not ("male" or "female"))
            ModelState.AddModelError(nameof(model.Gender), "Please select a gender.");

        if (string.IsNullOrWhiteSpace(model.Address) ||
            latitude == null || longitude == null)
        {
            ModelState.AddModelError(nameof(model.Address), "Please choose your location on the map.");
        }
        else if (latitude < -90 || latitude > 90 ||
                 longitude < -180 || longitude > 180)
        {
            ModelState.AddModelError(nameof(model.Address), "The selected map coordinates are invalid.");
        }

        if (string.IsNullOrEmpty(password))
            ModelState.AddModelError(nameof(model.Password), "Password is required.");
        else if (!IsPasswordValid(password))
            ModelState.AddModelError(nameof(model.Password), "Use 8–15 characters with uppercase, lowercase, number and symbol.");

        if (string.IsNullOrEmpty(confirmPassword))
            ModelState.AddModelError(nameof(model.ConfirmPassword), "Please confirm your password.");
        else if (password != confirmPassword)
            ModelState.AddModelError(nameof(model.ConfirmPassword), "Passwords do not match.");
    }

    private bool ValidateSessionOtp(
        string otp,
        string otpKey,
        string expiryKey,
        string attemptsKey,
        out string error)
    {
        error = "";

        if (otp.Length != 6)
        {
            error = "Please enter the complete 6-digit OTP.";
            return false;
        }

        DateTime? expiry = GetSessionDateTime(expiryKey);
        if (expiry == null || expiry <= DateTime.UtcNow)
        {
            error = "OTP has expired. Please request a new OTP.";
            return false;
        }

        int attempts = HttpContext.Session.GetInt32(attemptsKey) ?? 0;
        if (attempts >= MaximumOtpAttempts)
        {
            error = "Too many incorrect attempts. Please request a new OTP.";
            return false;
        }

        string? savedOtp = HttpContext.Session.GetString(otpKey);
        if (string.IsNullOrWhiteSpace(savedOtp))
        {
            error = "No active OTP was found. Please request a new OTP.";
            return false;
        }

        bool matches = CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(savedOtp),
            Encoding.UTF8.GetBytes(otp));

        if (matches)
            return true;

        attempts++;
        HttpContext.Session.SetInt32(attemptsKey, attempts);
        error = $"Incorrect OTP. {Math.Max(0, MaximumOtpAttempts - attempts)} attempt(s) remaining.";
        return false;
    }

    private void SetRegisterErrorStep()
    {
        if (ModelState.ContainsKey(nameof(WebAndMobile.Models.User.Username)) ||
            ModelState.ContainsKey(nameof(WebAndMobile.Models.User.Email)) ||
            ModelState.ContainsKey(nameof(WebAndMobile.Models.User.Phone)) ||
            ModelState.ContainsKey(nameof(WebAndMobile.Models.User.Gender)))
        {
            ViewData["RegisterStep"] = 1;
        }
        else if (ModelState.ContainsKey(nameof(WebAndMobile.Models.User.Address)))
        {
            ViewData["RegisterStep"] = 2;
        }
        else
        {
            ViewData["RegisterStep"] = 3;
        }
    }

    private void SetVisibleRegisterError()
    {
        int step = (int)(ViewData["RegisterStep"] ?? 1);
        string[] relevantFields = StepFields.GetValueOrDefault(step, []);
        List<string> messages = [];

        foreach ((string key, var value) in ModelState)
        {
            if (!string.IsNullOrEmpty(key) && !relevantFields.Contains(key))
                continue;

            messages.AddRange(value.Errors.Select(e => e.ErrorMessage));
        }

        ViewBag.Error = string.Join(" ", messages);
    }

    private static bool IsLockoutApplicable(User user) =>
        user.Position == "Student" || user.Position == "Teacher";

    private static string GetLoginAttemptsKey(string username) =>
        "Login.Attempts." + username.ToLowerInvariant();

    private static string GetLoginLockoutKey(string username) =>
        "Login.Lockout." + username.ToLowerInvariant();

    private static string GenerateOtp() =>
        RandomNumberGenerator.GetInt32(100000, 1000000).ToString();

    private static bool IsValidMalaysiaPhone(string phone) =>
        Regex.IsMatch(phone, @"^(011\d{8}|01[2-9]\d{7})$");

    private static bool IsPasswordValid(string value) =>
        value.Length is >= 8 and <= 15 &&
        Regex.IsMatch(value, "[A-Z]") &&
        Regex.IsMatch(value, "[a-z]") &&
        Regex.IsMatch(value, @"\d") &&
        Regex.IsMatch(value, "[@$!%*?&]");

    private static string MaskEmail(string email)
    {
        string[] parts = email.Split('@');
        if (parts.Length != 2) return email;

        string name = parts[0];
        string masked = name.Length switch
        {
            <= 1 => name + "***",
            2 => name[0] + "***",
            _ => name[0] + "***" + name[^1]
        };

        return $"{masked}@{parts[1]}";
    }

    private static string GetRealExceptionMessage(Exception exception)
    {
        Exception current = exception;
        while (current.InnerException != null)
            current = current.InnerException;
        return current.Message;
    }

    private DateTime? GetSessionDateTime(string key)
    {
        string? value = HttpContext.Session.GetString(key);
        return DateTime.TryParse(
            value,
            null,
            System.Globalization.DateTimeStyles.RoundtripKind,
            out DateTime date)
            ? date.ToUniversalTime()
            : null;
    }

    private PendingRegistration? GetPendingRegistration() =>
        DeserializeSession<PendingRegistration>(RegistrationDataKey);

    private PasswordResetData? GetPasswordResetData() =>
        DeserializeSession<PasswordResetData>(PasswordResetDataKey);

    private T? DeserializeSession<T>(string key)
    {
        string? json = HttpContext.Session.GetString(key);
        if (string.IsNullOrWhiteSpace(json)) return default;

        try
        {
            return JsonSerializer.Deserialize<T>(json);
        }
        catch (JsonException)
        {
            HttpContext.Session.Remove(key);
            return default;
        }
    }

    private void SetRegistrationOtpViewData(PendingRegistration pending)
    {
        int resendCount =
            HttpContext.Session.GetInt32(RegistrationOtpResendsKey) ?? 0;
        int remaining = Math.Max(0, MaximumOtpResends - resendCount);

        ViewBag.Email = MaskEmail(pending.Email);
        ViewBag.ResendRemaining = remaining;
        ViewBag.OtpExpiresAtUtc =
            GetSessionDateTime(RegistrationOtpExpiryKey)?.ToString("O");
        ViewBag.ResendClass = GetResendClass(remaining);
    }

    private void SetPasswordResetOtpViewData(PasswordResetData pending)
    {
        int resendCount =
            HttpContext.Session.GetInt32(PasswordResetOtpResendsKey) ?? 0;
        int remaining = Math.Max(0, MaximumOtpResends - resendCount);

        ViewBag.UserId = pending.UserId;
        ViewBag.Email = pending.MaskedEmail;
        ViewBag.ResendRemaining = remaining;
        ViewBag.OtpExpiresAtUtc =
            GetSessionDateTime(PasswordResetOtpExpiryKey)?.ToString("O");
        ViewBag.ResendClass = GetResendClass(remaining);
    }

    private static string GetResendClass(int remaining) => remaining switch
    {
        3 => "resend-cyan",
        2 => "resend-yellow",
        1 => "resend-red",
        _ => "resend-gray"
    };

    private void ClearRegistrationSession()
    {
        HttpContext.Session.Remove(RegistrationDataKey);
        HttpContext.Session.Remove(RegistrationOtpKey);
        HttpContext.Session.Remove(RegistrationOtpExpiryKey);
        HttpContext.Session.Remove(RegistrationOtpAttemptsKey);
        HttpContext.Session.Remove(RegistrationOtpResendsKey);
    }

    private void ClearPasswordResetSession()
    {
        HttpContext.Session.Remove(PasswordResetDataKey);
        HttpContext.Session.Remove(PasswordResetOtpKey);
        HttpContext.Session.Remove(PasswordResetOtpExpiryKey);
        HttpContext.Session.Remove(PasswordResetOtpAttemptsKey);
        HttpContext.Session.Remove(PasswordResetOtpResendsKey);
    }

    private sealed class PendingRegistration
    {
        public string Username { get; set; } = "";
        public string Email { get; set; } = "";
        public string Phone { get; set; } = "";
        public string Address { get; set; } = "";
        public string Gender { get; set; } = "";
        public string PasswordHash { get; set; } = "";
    }

    private sealed class PasswordResetData
    {
        public int UserId { get; set; }
        public string MaskedEmail { get; set; } = "";
        public string NewPasswordHash { get; set; } = "";
    }
}