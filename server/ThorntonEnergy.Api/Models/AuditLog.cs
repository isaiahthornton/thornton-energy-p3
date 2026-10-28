namespace ThorntonEnergy.Api.Models;

// One row per change to a tracked column (customer status / rate plan).
// Bill and outage status changes are logged by the Billing and Outages services in their own databases.
// Rows are written by SQL triggers (see the AdminInsight migration), not by C# code.
public class AuditLog
{
    public int Id { get; set; }
    public string TableName { get; set; } = string.Empty;   // "Customers" ("Bills" or "Outages" in older rows)
    public int RecordId { get; set; }                       // Id of the row that changed
    public string ColumnName { get; set; } = string.Empty;  // "Status" or "RatePlanId"
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public DateTime ChangedAt { get; set; }                 // Eastern time, like the rest of the app
    public string? ChangedBy { get; set; }                  // email of the signed-in user, if known
}
