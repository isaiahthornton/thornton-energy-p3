namespace ThorntonEnergy.Outages.Api.Models;

// One row per change to an outage's status. Rows are written by the trg_Outages_Audit SQL trigger, not by C# code.
public class AuditLog
{
    public int Id { get; set; }
    public string TableName { get; set; } = string.Empty;   // always "Outages" in this service
    public int RecordId { get; set; }                       // Id of the outage that changed
    public string ColumnName { get; set; } = string.Empty;  // "Status"
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public DateTime ChangedAt { get; set; }                 // Eastern time, like the rest of the app
    public string? ChangedBy { get; set; }                  // email of the signed-in user, if known
}

// One row from the vw_OutageSummary SQL view (the outage part of the admin dashboard). EF only reads it.
public class OutageSummary
{
    public int OpenOutages { get; set; }
}
