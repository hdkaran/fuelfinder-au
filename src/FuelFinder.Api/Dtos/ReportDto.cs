namespace FuelFinder.Api.Dtos;

record ReportDto(
    Guid Id,
    string Status,
    IReadOnlyList<ReportFuelTypeDto> FuelTypes,
    DateTimeOffset CreatedAt,
    int MinutesAgo);

record ReportFuelTypeDto(string FuelType, bool Available);
