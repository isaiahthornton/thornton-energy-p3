namespace ThorntonEnergy.Shared.Auth;

// Bound from the "Jwt" configuration section (appsettings.json + user secrets locally / app settings in Azure)
public class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = string.Empty;   // who created the token (this API)
    public string Audience { get; set; } = string.Empty; // who the token is for (the Angular app)
    public string Key { get; set; } = string.Empty;      // secret signing key: never in appsettings.json or git
    public int ExpiresMinutes { get; set; } = 60;
}