namespace ThorntonEnergy.Outages.Api.Models;

public class Outage
{
    public int Id { get; set; }

    // The customer lives in another service's database, so this is just their id: no foreign key, no Customer property.
    // Ownership checks compare it with the customerId in the caller's JWT.
    public int CustomerId { get; set; }

    public string Address { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime TimeReported { get; set; }
    public DateTime? TimeResolved { get; set; }
    public OutageStatus Status { get; set; }
}

public enum OutageStatus { Reported, InProgress, Resolved }

// What the outage map needs about a customer (another service owns customers)
public record CustomerLocation(string Name, decimal Latitude, decimal Longitude);
