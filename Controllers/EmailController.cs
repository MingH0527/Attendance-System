using Microsoft.AspNetCore.Mvc;
using WebAndMobile.Models;
using System.Net;
using System.Net.Mail;

namespace WebAndMobile.Controllers;

public class EmailController : Controller
{
    private readonly IConfiguration _config;

    public EmailController(IConfiguration config)
    {
        _config = config;
    }

    [HttpGet]
    public IActionResult SendEmail()
    {
        Console.WriteLine("GET SendEmail called");
        return View(new EmailModel());
    }

    [HttpPost]
    public async Task<IActionResult> SendEmail(EmailModel model)
    {
        Console.WriteLine("POST START");
        Console.WriteLine($"Raw model -> Email='{model.Email}', Message='{model.Message}'");

        if (!ModelState.IsValid)
        {
            // THIS IS LIKELY WHERE YOUR EMAIL IS SILENTLY DYING.
            // Print every validation error so we can see exactly what failed.
            Console.WriteLine("ModelState INVALID. Errors:");
            foreach (var kvp in ModelState)
            {
                foreach (var error in kvp.Value.Errors)
                {
                    Console.WriteLine($"  Field '{kvp.Key}': {error.ErrorMessage}");
                }
            }

            // Surface it in the UI too, not just the console,
            // so it's impossible to miss in the browser.
            ViewBag.Error = "Validation failed. Check the fields below.";
            return View(model);
        }

        Console.WriteLine("ModelState OK");

        try
        {
            string fromEmail = _config["EmailSettings:SenderEmail"]!;
            string appPassword = _config["EmailSettings:AppPassword"]!;
            string smtpServer = _config["EmailSettings:SmtpServer"]!;
            int port = int.Parse(_config["EmailSettings:Port"]!);
            string senderName = _config["EmailSettings:SenderName"] ?? "WebAndMobile";

            Console.WriteLine("Configuration Loaded");
            Console.WriteLine($"  SmtpServer={smtpServer}, Port={port}, SenderName={senderName}, SenderEmail={fromEmail}");
            Console.WriteLine($"  AppPassword length={appPassword?.Length ?? 0} (not printing the value itself)");

            if (string.IsNullOrWhiteSpace(fromEmail) ||
                string.IsNullOrWhiteSpace(appPassword))
            {
                Console.WriteLine("EmailSettings missing SenderEmail or AppPassword.");
                ViewBag.Error = "EmailSettings is not configured correctly.";
                return View(model);
            }

            Console.WriteLine("Creating MailMessage");
            using var mail = new MailMessage();
            mail.From = new MailAddress(fromEmail, senderName);
            mail.To.Add(model.Email);
            mail.Subject = "Your OTP Verification Code";
            mail.Body = model.Message;
            mail.IsBodyHtml = false;

            Console.WriteLine("Creating SMTP Client");
            using var smtp = new SmtpClient(smtpServer, port)
            {
                Credentials = new NetworkCredential(fromEmail, appPassword),
                EnableSsl = true,
                Timeout = 10000
            };

            Console.WriteLine("Before SendMailAsync");
            await smtp.SendMailAsync(mail);
            Console.WriteLine("After SendMailAsync");

            ViewBag.Message = "Email sent successfully!";
            ModelState.Clear();

            Console.WriteLine("Returning Success View");
            return View(new EmailModel());
        }
        catch (SmtpException ex)
        {
            Console.WriteLine("SMTP EXCEPTION:");
            Console.WriteLine(ex.ToString());
            ViewBag.Error = $"SMTP Error: {ex.StatusCode} - {ex.Message}";
        }
        catch (Exception ex)
        {
            Console.WriteLine("GENERAL EXCEPTION:");
            Console.WriteLine(ex.ToString());
            ViewBag.Error = $"General Error: {ex.Message}";
        }

        Console.WriteLine("Returning fallback View with model + error");
        return View(model);
    }
}