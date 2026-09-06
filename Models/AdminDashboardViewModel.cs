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

    // Active filters (echoed back to the form)

    public string? Search { get; set; }

    public string StatusFilter { get; set; } = "All";
}
