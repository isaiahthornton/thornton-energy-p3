namespace ThorntonEnergy.Billing.Api.Models;

// One row per change to a bill's status. Rows are written by the trg_Bills_Audit SQL trigger, not by C# code.
public class AuditLog
{
    public int Id { get; set; }
    public string TableName { get; set; } = string.Empty;   // always "Bills" in this service
    public int RecordId { get; set; }                       // Id of the bill that changed
    public string ColumnName { get; set; } = string.Empty;  // "Status"
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public DateTime ChangedAt { get; set; }                 // Eastern time, like the rest of the app
    public string? ChangedBy { get; set; }                  // email of the signed-in user, if known
}

// One row from the vw_BillingSummary SQL view (the billing half of the admin dashboard). EF only reads it.
public class BillingSummary
{
    public decimal UnpaidBalance { get; set; }
    public decimal OverdueBalance { get; set; }
    public decimal RevenueThisMonth { get; set; }
    public decimal AvgKwhLastMonth { get; set; }
}

// One row returned by the usp_GetMonthlyUsage stored procedure (not a table, so not in the DbContext)
public class MonthlyUsageRow
{
    public DateOnly Month { get; set; }   // first day of the month
    public long TotalKwh { get; set; }
    public decimal TotalBilled { get; set; }
}
