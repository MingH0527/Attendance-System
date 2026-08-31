using System.Net;
using System.Net.Mail;
using System.Net.Mime;
using System.Text.Encodings.Web;

namespace WebAndMobile;

public class GmailServiceHelper
{
    private readonly IConfiguration configuration;
    private readonly IWebHostEnvironment webHostEnvironment;

    public GmailServiceHelper(
        IConfiguration configuration,
        IWebHostEnvironment webHostEnvironment
    )
    {
        this.configuration = configuration;
        this.webHostEnvironment = webHostEnvironment;
    }

    public async Task SendOtpAsync(
        string receiverEmail,
        string username,
        string otp
    )
    {
        // ==================================================
        // READ EMAIL SETTINGS
        // ==================================================

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
            ?? "HLIH Attendance System";

        string portText =
            configuration["EmailSettings:Port"]
            ?? "587";

        if (!int.TryParse(portText, out int port))
        {
            port = 587;
        }

        appPassword =
            appPassword.Replace(" ", "");

        // Prevent HTML injection from user input.
        string safeUsername =
            HtmlEncoder.Default.Encode(
                username ?? ""
            );

        string safeOtp =
            HtmlEncoder.Default.Encode(
                otp ?? ""
            );

        // ==================================================
        // LOGO FILE PATH
        // ==================================================

        string logoPath =
            Path.Combine(
                webHostEnvironment.WebRootPath,
                "image",
                "Logo.png"
            );

        if (!File.Exists(logoPath))
        {
            throw new FileNotFoundException(
                "The email logo could not be found. " +
                "Expected location: wwwroot/image/Logo.png",
                logoPath
            );
        }

        // ==================================================
        // CREATE EMAIL
        // ==================================================

        using MailMessage mail =
            new MailMessage();

        mail.From =
            new MailAddress(
                senderEmail,
                senderName
            );

        mail.To.Add(receiverEmail);

        mail.Subject =
            "SMK HLIH Email Verification Code";

        mail.Priority =
            MailPriority.High;

        // ==================================================
        // HTML EMAIL BODY
        // ==================================================

        string htmlBody = $"""
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <meta name="viewport"
          content="width=device-width, initial-scale=1.0">

    <title>HLIH Email Verification</title>
</head>

<body style="
    margin: 0;
    padding: 0;
    background-color: #f3f6fb;
    font-family: Arial, Helvetica, sans-serif;
">

    <table role="presentation"
           width="100%"
           cellspacing="0"
           cellpadding="0"
           border="0"
           style="
               width: 100%;
               background-color: #f3f6fb;
               padding: 35px 15px;
           ">

        <tr>
            <td align="center">

                <table role="presentation"
                       width="600"
                       cellspacing="0"
                       cellpadding="0"
                       border="0"
                       style="
                           width: 100%;
                           max-width: 600px;
                           background-color: #ffffff;
                           border-radius: 14px;
                           overflow: hidden;
                           box-shadow: 0 8px 24px rgba(0, 0, 0, 0.08);
                       ">

                    <!-- HEADER -->
                    <tr>
                        <td align="center"
                            style="
                                background-color: #163d7a;
                                padding: 30px 25px 24px 25px;
                            ">

                            <img src="cid:HLIHLogo"
                                 alt="HLIH Logo"
                                 width="100"
                                 style="
                                     display: block;
                                     width: 100px;
                                     max-width: 100px;
                                     height: auto;
                                     margin: 0 auto 14px auto;
                                     border: 0;
                                 " />

                            <h1 style="
                                margin: 0;
                                color: #ffffff;
                                font-size: 25px;
                                line-height: 1.3;
                                font-weight: 700;
                            ">
                                HLIH Attendance System
                            </h1>

                            <p style="
                                margin: 8px 0 0 0;
                                color: #dbeafe;
                                font-size: 14px;
                                line-height: 1.5;
                            ">
                                Official Email Verification
                            </p>

                        </td>
                    </tr>

                    <!-- CONTENT -->
                    <tr>
                        <td style="
                            padding: 36px 40px 30px 40px;
                            color: #263238;
                        ">

                            <p style="
                                margin: 0 0 18px 0;
                                font-size: 16px;
                                line-height: 1.7;
                            ">
                                Hello <strong>{safeUsername}</strong>,
                            </p>

                            <p style="
                                margin: 0 0 24px 0;
                                font-size: 16px;
                                line-height: 1.7;
                                color: #4b5563;
                            ">
                                Thank you for registering with the
                                HLIH Attendance System. Please use the
                                verification code below to verify your
                                Gmail address.
                            </p>

                            <!-- OTP BOX -->
                            <table role="presentation"
                                   width="100%"
                                   cellspacing="0"
                                   cellpadding="0"
                                   border="0"
                                   style="
                                       margin: 0 0 25px 0;
                                   ">

                                <tr>
                                    <td align="center"
                                        style="
                                            background-color: #edf4ff;
                                            border: 2px dashed #2563eb;
                                            border-radius: 12px;
                                            padding: 24px 15px;
                                        ">

                                        <p style="
                                            margin: 0 0 10px 0;
                                            color: #475569;
                                            font-size: 13px;
                                            font-weight: 700;
                                            letter-spacing: 1px;
                                            text-transform: uppercase;
                                        ">
                                            Your verification code
                                        </p>

                                        <p style="
                                            margin: 0;
                                            color: #163d7a;
                                            font-size: 36px;
                                            line-height: 1.2;
                                            font-weight: 700;
                                            letter-spacing: 9px;
                                        ">
                                            {safeOtp}
                                        </p>

                                    </td>
                                </tr>

                            </table>

                            <p style="
                                margin: 0 0 12px 0;
                                font-size: 15px;
                                line-height: 1.7;
                                color: #4b5563;
                            ">
                                This verification code will expire in
                                <strong>1 minute</strong>.
                            </p>

                            <p style="
                                margin: 0 0 24px 0;
                                font-size: 15px;
                                line-height: 1.7;
                                color: #b42318;
                            ">
                                For your security, do not share this
                                verification code with anyone.
                            </p>

                            <!-- SECURITY NOTICE -->
                            <table role="presentation"
                                   width="100%"
                                   cellspacing="0"
                                   cellpadding="0"
                                   border="0">

                                <tr>
                                    <td style="
                                        background-color: #fff8e6;
                                        border-left: 4px solid #f59e0b;
                                        border-radius: 6px;
                                        padding: 15px 17px;
                                    ">

                                        <p style="
                                            margin: 0;
                                            color: #7c4a03;
                                            font-size: 13px;
                                            line-height: 1.6;
                                        ">
                                            If you did not create an
                                            HLIH account, you may safely
                                            ignore this email.
                                        </p>

                                    </td>
                                </tr>

                            </table>

                        </td>
                    </tr>

                    <!-- FOOTER -->
                    <tr>
                        <td align="center"
                            style="
                                background-color: #f8fafc;
                                border-top: 1px solid #e5e7eb;
                                padding: 22px 30px;
                            ">

                            <p style="
                                margin: 0 0 6px 0;
                                color: #475569;
                                font-size: 13px;
                                line-height: 1.5;
                                font-weight: 700;
                            ">
                                HLIH Attendance System
                            </p>

                            <p style="
                                margin: 0 0 6px 0;
                                color: #64748b;
                                font-size: 12px;
                                line-height: 1.5;
                            ">
                                This is an automated verification email.
                                Please do not reply.
                            </p>

                            <p style="
                                margin: 0;
                                color: #94a3b8;
                                font-size: 11px;
                                line-height: 1.5;
                            ">
                                © {DateTime.UtcNow.Year}
                                HLIH. All rights reserved.
                            </p>

                        </td>
                    </tr>

                </table>

            </td>
        </tr>

    </table>

</body>
</html>
""";

        // ==================================================
        // EMBED LOGO INTO EMAIL
        // ==================================================

        AlternateView htmlView =
            AlternateView.CreateAlternateViewFromString(
                htmlBody,
                null,
                MediaTypeNames.Text.Html
            );

        LinkedResource logo =
     new LinkedResource(
         logoPath,
         MediaTypeNames.Image.Png
     );

        logo.ContentId =
            "HLIHLogo";

        logo.TransferEncoding =
            TransferEncoding.Base64;

        logo.ContentType.Name =
            "Logo.png";

        htmlView.LinkedResources.Add(
            logo
        );

        mail.AlternateViews.Add(
            htmlView
        );

        mail.IsBodyHtml =
            true;

        // Plain-text fallback for email clients
        // that cannot display HTML.
        mail.Body = $"""
Hello {username},

Your SMK HLIH email verification code is:

{otp}

This verification code will expire in 5 minutes.

Do not share this verification code with anyone.

If you did not create this account, please ignore this email.

SMK HLIH 
""";

        // ==================================================
        // SEND EMAIL
        // ==================================================

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

        smtp.EnableSsl =
            true;

        smtp.UseDefaultCredentials =
            false;

        smtp.DeliveryMethod =
            SmtpDeliveryMethod.Network;

        smtp.Timeout =
            15000;

        await smtp.SendMailAsync(
            mail
        );
    }

    public Task SendPasswordResetOtpAsync(
        string receiverEmail,
        string username,
        string otp
    )
    {
        return SendOtpAsync(receiverEmail, username, otp);
    }

    public async Task SendReceiptAsync(
        string receiverEmail,
        string studentName,
        string transactionId,
        byte[] pdfBytes)
    {
        if (string.IsNullOrWhiteSpace(receiverEmail))
            throw new ArgumentException("The receiver email is missing.");
        if (pdfBytes == null || pdfBytes.Length == 0)
            throw new ArgumentException("The receipt PDF is empty.");

        string smtpServer = configuration["EmailSettings:SmtpServer"]
            ?? throw new InvalidOperationException("SMTP server is missing.");
        string senderEmail = configuration["EmailSettings:SenderEmail"]
            ?? throw new InvalidOperationException("Sender email is missing.");
        string appPassword = configuration["EmailSettings:AppPassword"]
            ?? throw new InvalidOperationException("Gmail App Password is missing.");
        string senderName = configuration["EmailSettings:SenderName"]
            ?? "HLIH Attendance System";
        string portText = configuration["EmailSettings:Port"] ?? "587";
        if (!int.TryParse(portText, out int port)) port = 587;
        appPassword = appPassword.Replace(" ", "");

        string safeStudentName = HtmlEncoder.Default.Encode(studentName ?? "");
        string safeTransactionId = HtmlEncoder.Default.Encode(transactionId ?? "");

        using MailMessage mail = new();
        mail.From = new MailAddress(senderEmail, senderName);
        mail.To.Add(receiverEmail);
        mail.Subject = $"Payment Receipt #{transactionId}";
        mail.IsBodyHtml = true;
        mail.Body = $"""
<!DOCTYPE html>
<html>
<body style="margin:0;padding:30px;background:#f3f6fb;font-family:Arial,Helvetica,sans-serif;">
  <div style="max-width:600px;margin:auto;padding:32px;background:#fff;border-radius:14px;box-shadow:0 8px 24px rgba(0,0,0,.08);">
    <h1 style="margin:0 0 10px;color:#1e3a5c;font-size:24px;">Payment Receipt</h1>
    <p style="color:#475569;line-height:1.7;">Hello <strong>{safeStudentName}</strong>,</p>
    <p style="color:#475569;line-height:1.7;">Your payment was completed successfully. Your official electronic receipt is attached to this email as a PDF.</p>
    <div style="margin:24px 0;padding:16px;background:#f4f7fb;border-radius:10px;">
      <strong style="color:#1e3a5c;">Transaction ID:</strong>
      <span style="color:#334155;">{safeTransactionId}</span>
    </div>
    <p style="color:#64748b;font-size:13px;line-height:1.6;">Please keep this receipt for your records. This is an automated email. Please do not reply.</p>
    <p style="margin-top:30px;color:#1e3a5c;font-weight:bold;">SMK HLIH Attendance System</p>
  </div>
</body>
</html>
""";

        MemoryStream pdfStream = new(pdfBytes);
        Attachment attachment = new(
            pdfStream,
            $"Receipt-{transactionId}.pdf",
            MediaTypeNames.Application.Pdf);
        mail.Attachments.Add(attachment);

        using SmtpClient smtp = new(smtpServer, port);
        smtp.Credentials = new NetworkCredential(senderEmail, appPassword);
        smtp.EnableSsl = true;
        smtp.UseDefaultCredentials = false;
        smtp.DeliveryMethod = SmtpDeliveryMethod.Network;
        smtp.Timeout = 30000;
        await smtp.SendMailAsync(mail);
    }
}
