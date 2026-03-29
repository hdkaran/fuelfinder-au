namespace FuelFinder.Api.Models;

/// <summary>
/// Records the availability of a specific fuel type within a Report.
/// FuelType values: "Diesel" | "ULP" | "E10" | "Premium"
/// </summary>
public class ReportFuelType
{
    public int Id { get; set; }
    public Guid ReportId { get; set; }
    public Report Report { get; set; } = null!;

    /// <summary>"Diesel" | "ULP" | "E10" | "Premium"</summary>
    public string FuelType { get; set; } = string.Empty;

    public bool Available { get; set; }
}
