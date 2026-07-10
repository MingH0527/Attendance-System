using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebAndMobile.Models;

namespace WebAndMobile.Controllers;

public class AccountController : Controller
{
    private readonly DB db;

    private readonly IPasswordHasher<User>
        passwordHasher;

    private static readonly Regex
        GmailOnlyRegex =
            new(
                @"^[a-zA-Z0-9._%+\-]+@gmail\.com$",
                RegexOptions.IgnoreCase |
                RegexOptions.Compiled
            );

    public AccountController(
        DB db,
        IPasswordHasher<User> passwordHasher
    )
    {
        this.db = db;

        this.passwordHasher =
            passwordHasher;
    }

    [HttpGet]
    public IActionResult Register()
    {
        return View(new User());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(
        User model
    )
    {
        ModelState.Clear();

        model.Username =
            (model.Username ?? string.Empty)
                .Trim();

        model.Email =
            (model.Email ?? string.Empty)
                .Trim()
                .ToLowerInvariant();

        model.Phone =
            Regex.Replace(
                model.Phone ?? string.Empty,
                @"\D",
                string.Empty
            );

        model.Address =
            (model.Address ?? string.Empty)
                .Trim();

        model.Gender =
            (model.Gender ?? string.Empty)
                .Trim()
                .ToLowerInvariant();

        string password =
            model.Password ?? string.Empty;

        string confirmPassword =
            model.ConfirmPassword ??
            string.Empty;

        // Username validation
        if (
            string.IsNullOrWhiteSpace(
                model.Username
            )
        )
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

        // Email validation
        if (
            string.IsNullOrWhiteSpace(
                model.Email
            )
        )
        {
            ModelState.AddModelError(
                nameof(model.Email),
                "Email is required."
            );
        }
        else if (
            !GmailOnlyRegex.IsMatch(
                model.Email
            )
        )
        {
            ModelState.AddModelError(
                nameof(model.Email),
                "Use a valid Gmail address ending with @gmail.com."
            );
        }

        // Phone validation
        if (
            string.IsNullOrWhiteSpace(
                model.Phone
            )
        )
        {
            ModelState.AddModelError(
                nameof(model.Phone),
                "Phone number is required."
            );
        }
        else if (
            !IsValidMalaysiaPhone(
                model.Phone
            )
        )
        {
            ModelState.AddModelError(
                nameof(model.Phone),
                "Enter a valid Malaysian number: 012–019 uses 10 digits; 011 uses 11 digits."
            );
        }

        // Gender validation
        if (
            model.Gender is not
            ("male" or "female")
        )
        {
            ModelState.AddModelError(
                nameof(model.Gender),
                "Please select a gender."
            );
        }

        // Address and map validation
        if (
            string.IsNullOrWhiteSpace(
                model.Address
            ) ||
            model.Latitude is null ||
            model.Longitude is null
        )
        {
            ModelState.AddModelError(
                nameof(model.Address),
                "Please choose your location on the map."
            );
        }
        else if (
            model.Latitude is < -90 or > 90 ||
            model.Longitude is < -180 or > 180
        )
        {
            ModelState.AddModelError(
                nameof(model.Address),
                "The selected map coordinates are invalid."
            );
        }

        // Password validation
        if (string.IsNullOrEmpty(password))
        {
            ModelState.AddModelError(
                nameof(model.Password),
                "Password is required."
            );
        }
        else if (
            !IsPasswordValid(password)
        )
        {
            ModelState.AddModelError(
                nameof(model.Password),
                "Use 8–15 characters with uppercase, lowercase, number and symbol."
            );
        }

        // Confirm password validation
        if (
            string.IsNullOrEmpty(
                confirmPassword
            )
        )
        {
            ModelState.AddModelError(
                nameof(model.ConfirmPassword),
                "Please confirm your password."
            );
        }
        else if (
            !string.Equals(
                password,
                confirmPassword,
                StringComparison.Ordinal
            )
        )
        {
            ModelState.AddModelError(
                nameof(model.ConfirmPassword),
                "Passwords do not match."
            );
        }

        // Duplicate username
        if (
            !string.IsNullOrWhiteSpace(
                model.Username
            ) &&
            await db.Users.AnyAsync(
                u =>
                    u.Username.ToLower() ==
                    model.Username.ToLower()
            )
        )
        {
            ModelState.AddModelError(
                nameof(model.Username),
                "Username already exists."
            );
        }

        // Duplicate email
        if (
            !string.IsNullOrWhiteSpace(
                model.Email
            ) &&
            await db.Users.AnyAsync(
                u =>
                    u.Email.ToLower() ==
                    model.Email
            )
        )
        {
            ModelState.AddModelError(
                nameof(model.Email),
                "Email already exists."
            );
        }

        // Duplicate phone
        if (
            !string.IsNullOrWhiteSpace(
                model.Phone
            ) &&
            await db.Users.AnyAsync(
                u => u.Phone == model.Phone
            )
        )
        {
            ModelState.AddModelError(
                nameof(model.Phone),
                "Phone number already exists."
            );
        }

        if (!ModelState.IsValid)
        {
            model.Password =
                string.Empty;

            model.ConfirmPassword =
                string.Empty;

            return View(model);
        }

        var newUser = new User
        {
            Username =
                model.Username,

            Email =
                model.Email,

            Phone =
                model.Phone,

            Gender =
                model.Gender,

            Address =
                model.Address,

            Latitude =
                model.Latitude,

            Longitude =
                model.Longitude,

            Position =
                "Student",

            Status =
                "Active",

            Avatar =
                "default.png",

            CreatedDate =
                DateTime.UtcNow
        };

        newUser.Password =
            passwordHasher.HashPassword(
                newUser,
                password
            );

        try
        {
            db.Users.Add(newUser);

            await db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            ModelState.AddModelError(
                string.Empty,
                "Registration could not be completed because one of the details is already in use."
            );

            model.Password =
                string.Empty;

            model.ConfirmPassword =
                string.Empty;

            return View(model);
        }

        TempData["Message"] =
            "Registration successful. Please log in.";

        return RedirectToAction(
            nameof(Login)
        );
    }

    private static bool
        IsValidMalaysiaPhone(
            string phone
        )
    {
        return Regex.IsMatch(
            phone,
            @"^(011\d{8}|01[2-9]\d{7})$"
        );
    }

    private static bool
        IsPasswordValid(
            string value
        )
    {
        return
            value.Length is >= 8 and <= 15 &&

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

    [HttpGet]
    public IActionResult Login()
    {
        ViewBag.Message =
            TempData["Message"];

        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(
        string username,
        string password
    )
    {
        username =
            (username ?? string.Empty)
                .Trim();

        password ??=
            string.Empty;

        var user =
            await db.Users
                .FirstOrDefaultAsync(
                    u =>
                        u.Username.ToLower() ==
                        username.ToLower() &&

                        u.Status == "Active"
                );

        if (
            user == null ||
            passwordHasher
                .VerifyHashedPassword(
                    user,
                    user.Password,
                    password
                ) ==
                PasswordVerificationResult.Failed
        )
        {
            ViewBag.Error =
                "Invalid username or password.";

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
}