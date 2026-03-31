namespace FuelFinder.Api.Dtos;

public record TodayReportDto(
    Guid Id,
    Guid StationId,
    string StationName,
    string StationAddress,
    string Status,
    int MinutesAgo);
