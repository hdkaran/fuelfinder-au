import { createApi, fetchBaseQuery } from '@reduxjs/toolkit/query/react';
import type { StationDto, ReportDto, ReportPayload, StatsDto } from '../types';

interface NearbyStationsParams {
  lat: number;
  lng: number;
  radius: number;
  fuelType?: string;
}

export const fuelFinderApi = createApi({
  reducerPath: 'fuelFinderApi',
  baseQuery: fetchBaseQuery({ baseUrl: '/api' }),
  endpoints: (builder) => ({
    getNearbyStations: builder.query<StationDto[], NearbyStationsParams>({
      query: ({ lat, lng, radius, fuelType }) => ({
        url: '/stations/nearby',
        params: { lat, lng, radius, ...(fuelType ? { fuelType } : {}) },
      }),
    }),
    getStation: builder.query<StationDto, string>({
      query: (id) => `/stations/${id}`,
    }),
    submitReport: builder.mutation<void, ReportPayload>({
      query: (payload) => ({
        url: '/reports',
        method: 'POST',
        body: payload,
      }),
    }),
    getRecentReports: builder.query<ReportDto[], string>({
      query: (stationId) => ({
        url: '/reports/recent',
        params: { stationId },
      }),
    }),
    getStatsSummary: builder.query<StatsDto, void>({
      query: () => '/stats/summary',
    }),
  }),
});

export const {
  useGetNearbyStationsQuery,
  useGetStationQuery,
  useSubmitReportMutation,
  useGetRecentReportsQuery,
  useGetStatsSummaryQuery,
} = fuelFinderApi;
