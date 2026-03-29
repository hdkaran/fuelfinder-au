namespace FuelFinder.Api.Dtos;

record ReportPayload(
    Guid StationId,
    string Status,
    IReadOnlyList<FuelTypePayload> FuelTypes,
    double Latitude,
    double Longitude);

record FuelTypePayload(string FuelType, bool Available);
