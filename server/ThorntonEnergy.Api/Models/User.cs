using System.Text.Json.Serialization;
using ThorntonEnergy.Shared.Auth;

namespace ThorntonEnergy.Api.Models;

public class User
{
    public int Id { get; set; }
    public string Email { get; set; } = string.Empty;
    [JsonIgnore] public string PasswordHash { get; set; } = string.Empty;
    public UserRole Role { get; set; }
    public int? CustomerId { get; set; }

    [JsonIgnore] public Customer? Customer { get; set; }
}