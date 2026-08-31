using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using WebAndMobile.Models;

namespace WebAndMobile.Services;

public class ReceiptPdfService
{
    public byte[] Generate(PaymentTransaction payment)
    {
        DateTime paymentDate = (payment.PaidAt ?? payment.CreatedAt).ToLocalTime();

        return Document.Create(document =>
        {
            document.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(45);
                page.DefaultTextStyle(style => style.FontSize(11).FontFamily("Arial").FontColor(Colors.Grey.Darken3));

                page.Header().Column(header =>
                {
                    header.Item().Text("SMK HLIH").FontSize(25).Bold().FontColor("#1E3A5C");
                    header.Item().PaddingTop(4).Text("Official Payment Receipt").FontSize(15).FontColor("#C89B3C");
                    header.Item().PaddingTop(15).LineHorizontal(1).LineColor("#DCE3EC");
                });

                page.Content().PaddingVertical(25).Column(content =>
                {
                    content.Spacing(14);
                    content.Item().Background("#F4F7FB").Padding(18).Column(summary =>
                    {
                        summary.Spacing(8);
                        AddRow(summary, "Receipt Number", payment.PaymentTransactionId);
                        AddRow(summary, "Payment Status", payment.Status);
                        AddRow(summary, "Payment Date", paymentDate.ToString("dd MMM yyyy, hh:mm tt"));
                    });

                    content.Item().PaddingTop(10).Text("Student Information").FontSize(15).Bold().FontColor("#1E3A5C");
                    AddRow(content, "Student ID", payment.User.UserId);
                    AddRow(content, "Student Name", payment.User.Username);
                    AddRow(content, "Email", payment.User.Email);

                    content.Item().PaddingTop(10).Text("Payment Information").FontSize(15).Bold().FontColor("#1E3A5C");
                    AddRow(content, "Fee", payment.FeeName);
                    AddRow(content, "Payment Method", payment.PaymentMethod);

                    content.Item().PaddingTop(15).BorderTop(1).BorderColor("#DCE3EC").PaddingTop(15).Row(row =>
                    {
                        row.RelativeItem().Text("Total Paid").FontSize(16).Bold().FontColor("#1E3A5C");
                        row.RelativeItem().AlignRight().Text($"RM {payment.Amount:N2}").FontSize(20).Bold().FontColor("#15803D");
                    });

                    content.Item().PaddingTop(20).Background("#FFF8E6").Padding(14)
                        .Text("This receipt was generated electronically. No signature is required.")
                        .FontSize(10).FontColor("#7C4A03");
                });

                page.Footer().AlignCenter().Text(text =>
                {
                    text.Span($"Generated on {DateTime.Now:dd MMM yyyy, hh:mm tt} • ");
                    text.Span("SMK HLIH Attendance System").Bold();
                });
            });
        }).GeneratePdf();
    }

    private static void AddRow(ColumnDescriptor column, string label, string value)
    {
        column.Item().Row(row =>
        {
            row.ConstantItem(145).Text(label).SemiBold().FontColor("#64748B");
            row.RelativeItem().Text(value ?? "-").FontColor("#1E293B");
        });
    }
}
