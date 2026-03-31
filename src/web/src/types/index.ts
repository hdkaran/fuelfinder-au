// Source of truth for all API types — must stay in sync with CLAUDE.md

export type FuelType = 'Diesel' | 'ULP' | 'E10' | 'Premium';
export type StationStatus = 'available' | 'low' | 'out' | 'unknown';
export type ReportStatus = 'available' | 'low' | 'out' | 'queue';

export interface FuelAvailabilityDto {
  fuelType: FuelType;
  available: boolean | null;
}

export interface StationDto {
  id: string;
  name: string;
  brand: string;
  address: string;
  suburb: string;
  state: string;
  latitude: number;
  longitude: number;
  distanceMetres: number;
  status: StationStatus;
  fuelAvailability: FuelAvailabilityDto[];
  reportCount: number;
  lastReportMinutesAgo: number | null;
}

export interface ReportFuelTypeDto {
  fuelType: FuelType;
  available: boolean;
}

export interface ReportDto {
  id: string;
  status: ReportStatus;
  fuelTypes: ReportFuelTypeDto[];
  createdAt: string; // ISO 8601
  minutesAgo: number;
}

export interface ReportPayload {
  stationId: string;
  status: ReportStatus;
  fuelTypes: { fuelType: FuelType; available: boolean }[];
  latitude: number;
  longitude: number;
}

export interface StatsDto {
  totalReportsToday: number;
  stationsAffected: number;
  lastUpdated: string; // ISO 8601
}

export interface TodayReportDto {
  id: string;
  stationId: string;
  stationName: string;
  stationAddress: string;
  status: ReportStatus;
  minutesAgo: number;
}

export interface AffectedStationDto {
  id: string;
  name: string;
  address: string;
  suburb: string;
  state: string;
  latestStatus: ReportStatus;
  reportCount: number;
}
