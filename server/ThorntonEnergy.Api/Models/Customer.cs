using System.Text.Json.Serialization;

namespace ThorntonEnergy.Api.Models;

public class Customer
{
    public int Id { get; set; }
    public string AccountNumber { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string ServiceAddress { get; set; } = string.Empty;
    public int RatePlanId { get; set; }
    public AccountStatus Status { get; set; }
    public decimal? Latitude { get; set; }   // service location, used by the outage map (null = not mapped yet)
    public decimal? Longitude { get; set; }

    [JsonIgnore] public RatePlan RatePlan { get; set; } = null!;
}