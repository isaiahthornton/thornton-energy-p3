using System.Text.Json.Serialization;

namespace ThorntonEnergy.Metering.Api.Models;

public class MeterReading
{
    public int Id { get; set; }
    public int MeterId { get; set; }
    public DateOnly ReadingDate { get; set; }
    public int ReadingValue { get; set; }

    [JsonIgnore] public Meter Meter { get; set; } = null!;
}
