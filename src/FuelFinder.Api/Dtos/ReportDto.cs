namespace FuelFinder.Api.Dtos;

public record ReportDto(
    Guid Id,
    string Status,
    IReadOnlyList<ReportFuelTypeDto> FuelTypes,
    DateTimeOffset CreatedAt,
    int MinutesAgo);

public record ReportFuelTypeDto(string FuelType, bool Available);
