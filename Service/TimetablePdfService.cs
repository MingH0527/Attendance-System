using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using WebAndMobile.ViewModels;

namespace WebAndMobile.Services;

public class TimetablePdfService
{
    public byte[] Generate(
        string termName,
        string className,
        string classType,
        DateTime? weekDate,
        IReadOnlyCollection<TimetableSlotDto> slots)
    {
        DayOfWeek[] days = classType.Equals("Tuition", StringComparison.OrdinalIgnoreCase)
            ? [DayOfWeek.Saturday, DayOfWeek.Sunday]
            : [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday];

        var merged = days.ToDictionary(
            day => day,
            day => Merge(slots.Where(s => s.DayOfWeek == day).ToList()));

        return Document.Create(document =>
        {
            document.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(24);
                page.DefaultTextStyle(x => x.FontFamily("Arial").FontSize(8).FontColor("#243447"));

                page.Header().Column(header =>
                {
                    header.Item().Row(row =>
                    {
                        row.RelativeItem().Column(left =>
                        {
                            left.Item().Text("TIMETABLE").FontSize(22).Bold().FontColor("#123D5A");
                            left.Item().PaddingTop(2)
                                .Text($"{termName}  •  {className}  •  {classType}")
                                .FontSize(10).FontColor("#617285");
                            left.Item().PaddingTop(2)
                                .Text(weekDate.HasValue
                                    ? $"Week of {weekDate.Value:dd MMM yyyy}"
                                    : "Weekly Template")
                                .FontSize(8).FontColor("#7A8794");
                        });

                        row.ConstantItem(245).AlignRight().Column(right =>
                        {
                            right.Item().AlignRight().Text("SMK HLIH Attendance System")
                                .FontSize(10).SemiBold().FontColor("#123D5A");
                            right.Item().PaddingTop(4).AlignRight().Text("Generated electronically")
                                .FontSize(8).FontColor("#7A8794");
                        });
                    });

                    header.Item().PaddingTop(12).LineHorizontal(1).LineColor("#D8E1E8");
                });

                page.Content().PaddingTop(14).Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.ConstantColumn(72);
                        for (int i = 0; i < 8; i++)
                            columns.RelativeColumn();
                    });

                    table.Header(h =>
                    {
                        HeaderCell(h.Cell(), "Day / Time");
                        for (int hour = 9; hour < 17; hour++)
                            HeaderCell(h.Cell(), $"{HourLabel(hour)}\n{HourLabel(hour + 1)}");
                    });

                    foreach (DayOfWeek day in days)
                    {
                        DayCell(table.Cell(), day.ToString());

                        var entries = merged[day];
                        int hour = 9;

                        while (hour < 17)
                        {
                            TimetableSlotDto? entry = entries.FirstOrDefault(s =>
                                TryHour(s.StartTime, out int start) && start == hour);

                            if (entry == null)
                            {
                                EmptyCell(table.Cell());
                                hour++;
                                continue;
                            }

                            int span = GetSpan(entry);
                            span = Math.Clamp(span, 1, 17 - hour);

                            LessonCell(
                                table.Cell().ColumnSpan((uint)span),
                                entry);

                            hour += span;
                        }
                    }
                });

                page.Footer().PaddingTop(10).Row(row =>
                {
                    row.RelativeItem().Text("Normal timetable / replacement / cancellation information is reflected in the exported schedule.")
                        .FontSize(7).FontColor("#7A8794");
                    row.ConstantItem(180).AlignRight()
                        .Text($"Generated {DateTime.Now:dd MMM yyyy, hh:mm tt}")
                        .FontSize(7).FontColor("#7A8794");
                });
            });
        }).GeneratePdf();
    }

    private static void HeaderCell(IContainer container, string text)
    {
        container
            .Background("#123D5A")
            .Border(0.5f).BorderColor("#D6DEE5")
            .PaddingVertical(9).PaddingHorizontal(5)
            .AlignCenter().AlignMiddle()
            .Text(text)
            .SemiBold().FontSize(7).FontColor(Colors.White);
    }

    private static void DayCell(IContainer container, string text)
    {
        container
            .Background("#F1F5F8")
            .Border(0.5f).BorderColor("#D6DEE5")
            .MinHeight(72)
            .Padding(7)
            .AlignMiddle()
            .Text(text)
            .Bold().FontSize(8).FontColor("#123D5A");
    }

    private static void EmptyCell(IContainer container)
    {
        container
            .Background(Colors.White)
            .Border(0.5f).BorderColor("#E1E7EC")
            .MinHeight(72);
    }

    private static void LessonCell(IContainer container, TimetableSlotDto slot)
    {
        bool cancelled = slot.Status.Equals("Cancelled", StringComparison.OrdinalIgnoreCase);
        bool attention = slot.Status.Equals("Attention", StringComparison.OrdinalIgnoreCase);

        string background = cancelled ? "#FFF0F1" : attention ? "#FFF8DC" : "#E9F8FA";
        string border = cancelled ? "#E55A63" : attention ? "#E1A72D" : "#269CB0";

        container
            .Background(background)
            .Border(0.8f).BorderColor(border)
            .MinHeight(72)
            .Padding(7)
            .Column(column =>
            {
                column.Spacing(2);
                column.Item().Text(slot.CourseName)
                    .Bold().FontSize(8).FontColor("#173D4E");
                column.Item().Text($"{slot.StartTime} - {slot.EndTime}")
                    .SemiBold().FontSize(7).FontColor("#506474");

                if (!string.IsNullOrWhiteSpace(slot.TeacherName))
                    column.Item().Text(slot.TeacherName).FontSize(7);

                if (!string.IsNullOrWhiteSpace(slot.RoomName))
                    column.Item().Text(slot.RoomName).FontSize(7).FontColor("#617285");

                if (!slot.Status.Equals("Normal", StringComparison.OrdinalIgnoreCase))
                {
                    column.Item().PaddingTop(2)
                        .Text(slot.Status)
                        .Bold().FontSize(6.5f)
                        .FontColor(cancelled ? "#B42318" : "#8A5B00");
                }
            });
    }

    private static List<TimetableSlotDto> Merge(List<TimetableSlotDto> entries)
    {
        var result = new List<TimetableSlotDto>();

        foreach (TimetableSlotDto entry in entries.OrderBy(x => x.StartTime))
        {
            TimetableSlotDto? previous = result.LastOrDefault();

            if (previous != null &&
                previous.EndTime == entry.StartTime &&
                previous.CourseId == entry.CourseId &&
                previous.TeacherId == entry.TeacherId &&
                (previous.ClassroomId ?? "") == (entry.ClassroomId ?? "") &&
                previous.Status == entry.Status)
            {
                previous.EndTime = entry.EndTime;
                continue;
            }

            result.Add(new TimetableSlotDto
            {
                TermId = entry.TermId,
                TimetableId = entry.TimetableId,
                TeacherId = entry.TeacherId,
                TeacherName = entry.TeacherName,
                CourseId = entry.CourseId,
                CourseName = entry.CourseName,
                ClassId = entry.ClassId,
                ClassName = entry.ClassName,
                ClassroomId = entry.ClassroomId,
                RoomName = entry.RoomName,
                DayOfWeek = entry.DayOfWeek,
                StartTime = entry.StartTime,
                EndTime = entry.EndTime,
                ClassType = entry.ClassType,
                Status = entry.Status,
                StatusReason = entry.StatusReason
            });
        }

        return result;
    }

    private static int GetSpan(TimetableSlotDto slot)
    {
        if (!TimeSpan.TryParse(slot.StartTime, out TimeSpan start) ||
            !TimeSpan.TryParse(slot.EndTime, out TimeSpan end))
            return 1;

        return Math.Max(1, (int)Math.Ceiling((end - start).TotalHours));
    }

    private static bool TryHour(string value, out int hour)
    {
        hour = 0;
        if (!TimeSpan.TryParse(value, out TimeSpan time))
            return false;

        hour = time.Hours;
        return true;
    }

    private static string HourLabel(int hour)
    {
        string suffix = hour >= 12 ? "PM" : "AM";
        int display = hour > 12 ? hour - 12 : hour;
        return $"{display}:00 {suffix}";
    }
}
