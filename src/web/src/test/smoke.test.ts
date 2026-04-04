import { describe, it, expect } from 'vitest';
import type { StationDto, ReportPayload, StatsDto } from '../types';

describe('TypeScript types', () => {
  it('StationDto shape is correct', () => {
    const station: StationDto = {
      id: '123',
      name: 'Test Station',
      brand: 'BP',
      address: '1 Test St',
      suburb: 'Sydney',
      state: 'NSW',
      latitude: -33.8688,
      longitude: 151.2093,
      distanceMetres: 500,
      status: 'available',
      fuelAvailability: [{ fuelType: 'ULP', available: true }],
      reportCount: 3,
      lastReportMinutesAgo: 10,
      latestPrices: [],
    };
    expect(station.status).toBe('available');
  });

  it('ReportPayload shape is correct', () => {
    const payload: ReportPayload = {
      stationId: '123',
      status: 'low',
      fuelTypes: [{ fuelType: 'Diesel', available: true }],
      latitude: -33.8688,
      longitude: 151.2093,
    };
    expect(payload.status).toBe('low');
  });

  it('StatsDto shape is correct', () => {
    const stats: StatsDto = {
      totalReportsToday: 42,
      stationsAffected: 7,
      lastUpdated: new Date().toISOString(),
    };
    expect(stats.totalReportsToday).toBe(42);
  });
});
