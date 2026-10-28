namespace ThorntonEnergy.Api.Models;

// History only: changes recorded here before customers, bills and outages moved to their own services.
// No trigger writes to this table any more; each service now logs its own changes in its own database.
// Rows are written by SQL triggers (see the AdminInsight migration), not by C# code.
public class AuditLog
{
    public int Id { get; set; }
    public string TableName { get; set; } = string.Empty;   // "Customers", "Bills" or "Outages"
    public int RecordId { get; set; }                       // Id of the row that changed
    public string ColumnName { get; set; } = string.Empty;  // "Status" or "RatePlanId"
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public DateTime ChangedAt { get; set; }                 // Eastern time, like the rest of the app
    public string? ChangedBy { get; set; }                  // email of the signed-in user, if known
}
