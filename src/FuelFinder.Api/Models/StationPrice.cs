namespace FuelFinder.Api.Models;

public class StationPrice
{
    public int             Id                 { get; set; }
    public Guid            StationId          { get; set; }
    public string          FuelType           { get; set; } = string.Empty;
    public decimal         PricePerLitreCents { get; set; }
    public DateTimeOffset  RecordedAtUtc      { get; set; }
    public string          Source             { get; set; } = string.Empty;
}
