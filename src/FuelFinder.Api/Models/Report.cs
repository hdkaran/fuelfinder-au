namespace FuelFinder.Api.Models;

/// <summary>
/// A crowdsourced report submitted by a driver at a fuel station.
/// Status values: "available" | "low" | "out" | "queue"
/// </summary>
public class Report
{
    public Guid Id { get; set; }
    public Guid StationId { get; set; }
    public Station Station { get; set; } = null!;

    /// <summary>"available" | "low" | "out" | "queue"</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>Reporter's GPS latitude at time of submission.</summary>
    public double Latitude { get; set; }

    /// <summary>Reporter's GPS longitude at time of submission.</summary>
    public double Longitude { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public ICollection<ReportFuelType> FuelTypes { get; set; } = new List<ReportFuelType>();
}
