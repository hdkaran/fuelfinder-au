namespace FuelFinder.Api.Dtos;

public record StationDto(
    Guid Id,
    string Name,
    string Brand,
    string Address,
    string Suburb,
    string State,
    double Latitude,
    double Longitude,
    double DistanceMetres,
    string Status,
    IReadOnlyList<FuelAvailabilityDto> FuelAvailability,
    int ReportCount,
    int? LastReportMinutesAgo,
    IReadOnlyList<PriceDto> LatestPrices);

public record FuelAvailabilityDto(string FuelType, bool? Available);
