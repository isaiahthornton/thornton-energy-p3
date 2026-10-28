namespace ThorntonEnergy.Customers.Api.Models;

// One row per change to a customer's status or rate plan. Rows are written by the trg_Customers_Audit SQL trigger,
// not by C# code.
public class AuditLog
{
    public int Id { get; set; }
    public string TableName { get; set; } = string.Empty;   // always "Customers" in this service
    public int RecordId { get; set; }                       // Id of the customer that changed
    public string ColumnName { get; set; } = string.Empty;  // "Status" or "RatePlanId"
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public DateTime ChangedAt { get; set; }                 // Eastern time, like the rest of the app
    public string? ChangedBy { get; set; }                  // email of the signed-in user, if known
}

// One row from the vw_CustomerSummary SQL view (the customer part of the admin dashboard). EF only reads it.
public class CustomerSummary
{
    public int ActiveCustomers { get; set; }
}
