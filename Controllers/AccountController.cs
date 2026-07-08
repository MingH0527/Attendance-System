using Microsoft.AspNetCore.Mvc;
using WebAndMobile.Models;
using System.Net;
using System.Net.Mail;

namespace WebAndMobile.Controllers;

public class AccountController : Controller
{
    private readonly DB db;
    private readonly IConfiguration config;

    public AccountController(DB db, IConfiguration config)
    {
        this.db = db;
        this.config = config;
    }

    [HttpGet]
    public IActionResult Register()
    {
        return View(new User());
    }

    [HttpPost]
    public async Task<IActionResult> Register(User user)
    {
        if (!ModelState.IsValid)
        {
            return View(user);
        }

        if (db.Users.Any(u => u.Username == user.Username))
        {
            ViewBag.Error = "Username already exists.";
            return View(user);
        }

        if (db.Users.Any(u => u.Email == user.Email))
        {
            ViewBag.Error = "Email already exists.";
            return View(user);
        }

        user.Position = "Student";   // register 默认 student
        user.Status = "Active";
        user.Avatar = "default.png";

        db.Users.Add(user);
        db.SaveChanges();

        await SendRegisterEmail(user.Email, user.Username);

        TempData["Message"] = "Register successful. Please login.";
        return RedirectToAction("Login");
    }

    [HttpGet]
    public IActionResult Login()
    {
        ViewBag.Message = TempData["Message"];
        return View();
    }

    [HttpPost]
    public IActionResult Login(string username, string password)
    {
        var user = db.Users.FirstOrDefault(u =>
            u.Username == username &&
            u.Password == password &&
            u.Status == "Active");

        if (user == null)
        {
            ViewBag.Error = "Invalid username or password.";
            return View();
        }

        if (user.Position == "Admin")
        {
            return RedirectToAction("Index", "Admin");
        }
        else if (user.Position == "Teacher")
        {
            return RedirectToAction("Index", "Teacher");
        }
        else
        {
            return RedirectToAction("Index", "Student");
        }
    }

    private async Task SendRegisterEmail(string toEmail, string username)
    {
        string fromEmail = config["EmailSettings:SenderEmail"]!;
        string appPassword = config["EmailSettings:AppPassword"]!;
        string smtpServer = config["EmailSettings:SmtpServer"]!;
        int port = int.Parse(config["EmailSettings:Port"]!);
        string senderName = config["EmailSettings:SenderName"] ?? "WebAndMobile";

        using var mail = new MailMessage();
        mail.From = new MailAddress(fromEmail, senderName);
        mail.To.Add(toEmail);
        mail.Subject = "Registration Successful";

        mail.Body = $@"
        <h2>Registration Successful</h2>
        <p>Hello <b>{username}</b>,</p>
        <p>Your account has been registered successfully.</p>
        <p>You can now login using your username and password.</p>
        <br>
        <p>Thank you.</p>
        ";

        mail.IsBodyHtml = true;

        using var smtp = new SmtpClient(smtpServer, port)
        {
            Credentials = new NetworkCredential(fromEmail, appPassword),
            EnableSsl = true
        };

        await smtp.SendMailAsync(mail);
    }
}