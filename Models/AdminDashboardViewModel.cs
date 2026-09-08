namespace WebAndMobile.ViewModels;

public class AdminPaymentRow
{
    public string PaymentTransactionId { get; set; } = "";

    public string UserId { get; set; } = "";

    public string Username { get; set; } = "";

    public string FeeName { get; set; } = "";

    public string PaymentMethod { get; set; } = "";

    public decimal Amount { get; set; }

    public string Status { get; set; } = "";

    public DateTime CreatedAt { get; set; }

    public DateTime? PaidAt { get; set; }
}

public class AdminFeeBreakdownRow
{
    public string FeeName { get; set; } = "";

    public int PaidCount { get; set; }

    public decimal TotalCollected { get; set; }
}

public class AdminChartSlice
{
    public string Label { get; set; } = "";

    public int Count { get; set; }

    public decimal Amount { get; set; }
}

public class AdminMonthPoint
{
    public string Label { get; set; } = "";

    public int Count { get; set; }

    public decimal Amount { get; set; }
}

public class AdminBucket
{
    public string Label { get; set; } = "";

    public int Count { get; set; }

    public decimal Amount { get; set; }
}

public class AdminDashboardViewModel
{
    // Money totals

    public decimal TotalCollected { get; set; }

    public decimal PendingAmount { get; set; }

    public decimal CollectedThisMonth { get; set; }

    // Transaction counts

    public int TotalTransactions { get; set; }

    public int PaidCount { get; set; }

    public int PendingCount { get; set; }

    public int ExpiredCount { get; set; }

    // Student coverage

    public int PayingStudentCount { get; set; }

    public int TotalStudentCount { get; set; }

    // Tables

    public List<AdminFeeBreakdownRow> FeeBreakdown { get; set; } = [];

    public List<AdminPaymentRow> Payments { get; set; } = [];

    // Charts

    /// Collection per calendar month for the last 12 months.
    public List<AdminMonthPoint> MonthlyCollection { get; set; } = [];

    /// Paid transactions grouped by payment method.
    public List<AdminChartSlice> MethodBreakdown { get; set; } = [];

    /// Students who have paid the most, highest first.
    public List<AdminChartSlice> TopStudents { get; set; } = [];

    /// Paid transactions by day of the week (Monday first).
    public List<AdminBucket> WeekdayBuckets { get; set; } = [];

    /// Paid transactions by four-hour block of the day.
    public List<AdminBucket> HourBuckets { get; set; } = [];

    public string BusiestMonth { get; set; } = "";

    public string BusiestDay { get; set; } = "";

    public string BusiestHour { get; set; } = "";

    public string TopMethod { get; set; } = "";

    // Paging for the Overall Student Payments table

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 10;

    /// Row count after filters, before paging.
    public int TotalPayments { get; set; }

    public int TotalPages { get; set; } = 1;

    /// 1-based index of the first row on this page (0 when empty).
    public int FirstRowOnPage =>
        TotalPayments == 0
            ? 0
            : ((Page - 1) * PageSize) + 1;

    public int LastRowOnPage =>
        Math.Min(Page * PageSize, TotalPayments);

    // Active filters (echoed back to the form)

    public string? Search { get; set; }

    public string StatusFilter { get; set; } = "All";

    public string MethodFilter { get; set; } = "All";

    /// Inclusive: payments made on this local date or later.
    public DateTime? FromDate { get; set; }

    /// Inclusive: payments made on this local date or earlier.
    public DateTime? ToDate { get; set; }

    /// Every payment method that appears in the data, for the dropdown.
    public List<string> AvailableMethods { get; set; } = [];

    /// True when anything other than the defaults is applied.
    public bool HasActiveFilters =>
        !string.IsNullOrWhiteSpace(Search) ||
        StatusFilter != "All" ||
        MethodFilter != "All" ||
        FromDate.HasValue ||
        ToDate.HasValue;
}
