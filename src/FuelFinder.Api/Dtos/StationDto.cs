namespace FuelFinder.Api.Dtos;

record StationDto(
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
    int? LastReportMinutesAgo);

record FuelAvailabilityDto(string FuelType, bool? Available);
