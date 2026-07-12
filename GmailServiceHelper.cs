using System.Net;
using System.Net.Mail;

namespace WebAndMobile;

public class GmailServiceHelper
{
    private readonly IConfiguration configuration;

    public GmailServiceHelper(
        IConfiguration configuration
    )
    {
        this.configuration = configuration;
    }

    public async Task SendOtpAsync(
        string receiverEmail,
        string username,
        string otp
    )
    {
        string smtpServer =
            configuration["EmailSettings:SmtpServer"]
            ?? throw new InvalidOperationException(
                "SMTP server is missing."
            );

        string senderEmail =
            configuration["EmailSettings:SenderEmail"]
            ?? throw new InvalidOperationException(
                "Sender email is missing."
            );

        string appPassword =
            configuration["EmailSettings:AppPassword"]
            ?? throw new InvalidOperationException(
                "Gmail App Password is missing."
            );

        string senderName =
            configuration["EmailSettings:SenderName"]
            ?? "WebAndMobile";

        string portText =
            configuration["EmailSettings:Port"]
            ?? "587";

        if (!int.TryParse(portText, out int port))
        {
            port = 587;
        }

        appPassword =
            appPassword.Replace(" ", "");

        using MailMessage mail =
            new MailMessage();

        mail.From =
            new MailAddress(
                senderEmail,
                senderName
            );

        mail.To.Add(receiverEmail);

        mail.Subject =
            "HLIH OTP Verification";

        mail.Body = $"""
Hello {username},

Your OTP verification code is:

{otp}

This OTP will expire in 5 minutes.

Do not share this OTP with anyone.

HLIH Attendance System
""";

        mail.IsBodyHtml = false;

        using SmtpClient smtp =
            new SmtpClient(
                smtpServer,
                port
            );

        smtp.Credentials =
            new NetworkCredential(
                senderEmail,
                appPassword
            );

        smtp.EnableSsl = true;
        smtp.Timeout = 15000;

        await smtp.SendMailAsync(mail);
    }
}