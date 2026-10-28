using System.Text.Json.Serialization;
using ThorntonEnergy.Shared.Auth;

namespace ThorntonEnergy.Api.Models;

public class User
{
    public int Id { get; set; }
    public string Email { get; set; } = string.Empty;
    [JsonIgnore] public string PasswordHash { get; set; } = string.Empty;
    public UserRole Role { get; set; }
    // The customer this login belongs to (null for admins). Customers live in the Customers service,
    // so this is just their id: no foreign key, no Customer property.
    public int? CustomerId { get; set; }
}