using Microsoft.Extensions.Configuration;

namespace ThorntonEnergy.Shared.Common;

// "Now" and "today" as the utility sees them (Eastern time), not the server's clock (Azure runs in UTC).
// Services use this instead of DateTime.Now so tests can control the time.
public interface IUtilityClock
{
    DateTime Now { get; }
    DateOnly Today { get; }
}

public class UtilityClock : IUtilityClock
{
    private readonly TimeProvider _time;
    private readonly TimeZoneInfo _zone;

    public UtilityClock(TimeProvider time, IConfiguration config)
    {
        _time = time;
        _zone = FindZone(config["App:TimeZone"] ?? "America/New_York");
    }

    public DateTime Now => TimeZoneInfo.ConvertTime(_time.GetUtcNow(), _zone).DateTime;

    public DateOnly Today => DateOnly.FromDateTime(Now);

    // Windows and Linux name time zones differently, so try the configured name, then the Windows one
    private static TimeZoneInfo FindZone(string id)
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
        catch (TimeZoneNotFoundException) { return TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time"); }
    }
}
