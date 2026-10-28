using System.Text.Json.Serialization;

namespace ThorntonEnergy.Metering.Api.Models;

public class Meter
{
    public int Id { get; set; }

    // The customer lives in the monolith's database, so this is just their id: no foreign key, no Customer property.
    // Ownership checks compare it with the customerId in the caller's JWT.
    public int CustomerId { get; set; }

    public string MeterNumber { get; set; } = string.Empty;
    public DateOnly InstallationDate { get; set; }
    public MeterStatus Status { get; set; }

    [JsonIgnore] public List<MeterReading> Readings { get; set; } = [];
}

public enum MeterStatus { Active, Inactive, Removed }
