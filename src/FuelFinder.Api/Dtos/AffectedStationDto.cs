namespace FuelFinder.Api.Dtos;

public record AffectedStationDto(
    Guid Id,
    string Name,
    string Address,
    string Suburb,
    string State,
    string LatestStatus,
    int ReportCount);
