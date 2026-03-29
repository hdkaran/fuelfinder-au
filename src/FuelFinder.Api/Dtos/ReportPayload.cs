namespace FuelFinder.Api.Dtos;

public record ReportPayload(
    Guid StationId,
    string Status,
    IReadOnlyList<FuelTypePayload> FuelTypes,
    double Latitude,
    double Longitude);

public record FuelTypePayload(string FuelType, bool Available);
