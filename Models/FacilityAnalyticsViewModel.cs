namespace WebAndMobile.ViewModels;

public class FacilityAnalyticsPoint
{
    public string Label { get; set; } = "";
    public double Value { get; set; }
    public int Count { get; set; }
}

public class FacilityAnalyticsSeries
{
    public string Name { get; set; } = "";
    public List<double> Values { get; set; } = [];
}

public class FacilityAnalyticsMultiChart
{
    public List<string> Labels { get; set; } = [];
    public List<FacilityAnalyticsSeries> Series { get; set; } = [];
}

public class FacilityAnalyticsRank
{
    public string Label { get; set; } = "";
    public int Count { get; set; }
    public double Hours { get; set; }
}

public class FacilityAnalyticsViewModel
{
    // KPI cards
    public int TotalBookings { get; set; }
    public double TotalBookingHours { get; set; }
    public double CompletionRate { get; set; }
    public double CancellationRate { get; set; }

    // Summary text
    public string PeakBookingSlot { get; set; } = "No data";
    public string MostBookedFacility { get; set; } = "No data";
    public string TopFacilityUser { get; set; } = "No data";
    public int TotalFacilities { get; set; }

    // 16 charts
    public List<FacilityAnalyticsPoint> Last7DaysBookingHours { get; set; } = [];
    public List<FacilityAnalyticsPoint> MonthlyBookingHours { get; set; } = [];
    public List<FacilityAnalyticsPoint> AllTimeUsageTrend { get; set; } = [];
    public List<FacilityAnalyticsPoint> BookingStatusDistribution { get; set; } = [];
    public FacilityAnalyticsMultiChart BookingStatusTrend { get; set; } = new();
    public List<FacilityAnalyticsPoint> CheckInVsNoShow { get; set; } = [];
    public List<FacilityAnalyticsPoint> PeakBookingHours { get; set; } = [];
    public List<FacilityAnalyticsPoint> BookingDurationDistribution { get; set; } = [];
    public List<FacilityAnalyticsPoint> FacilityUsageHours { get; set; } = [];
    public List<FacilityAnalyticsRank> TopFacilities { get; set; } = [];
    public List<FacilityAnalyticsPoint> BookingShareByFacility { get; set; } = [];
    public FacilityAnalyticsMultiChart FacilityLast7DaysComparison { get; set; } = new();
    public FacilityAnalyticsMultiChart FacilityMonthlyComparison { get; set; } = new();
    public FacilityAnalyticsMultiChart FacilityAllTimeTrend { get; set; } = new();
    public List<FacilityAnalyticsRank> TopStudents { get; set; } = [];
    public List<FacilityAnalyticsPoint> BookingLeadTimeDistribution { get; set; } = [];
}
