import { createApi, fetchBaseQuery } from '@reduxjs/toolkit/query/react';
import type { StationDto, ReportDto, ReportPayload, StatsDto } from '../types';

interface NearbyStationsParams {
  lat: number;
  lng: number;
  radius: number;
  fuelType?: string;
}

interface SearchStationsParams {
  q: string;
  lat?: number;
  lng?: number;
}

export interface PushSubscribePayload {
  endpoint: string;
  p256dh: string;
  auth: string;
  latitude: number;
  longitude: number;
}

export const fuelFinderApi = createApi({
  reducerPath: 'fuelFinderApi',
  baseQuery: fetchBaseQuery({
    baseUrl: import.meta.env.VITE_API_BASE_URL
      ? `${import.meta.env.VITE_API_BASE_URL}/api`
      : '/api',
  }),
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
    searchStations: builder.query<StationDto[], SearchStationsParams>({
      query: ({ q, lat, lng }) => ({
        url: '/stations/search',
        params: { q, ...(lat != null && lng != null ? { lat, lng } : {}) },
      }),
    }),
    subscribePush: builder.mutation<void, PushSubscribePayload>({
      query: (body) => ({ url: '/push/subscribe', method: 'POST', body }),
    }),
    unsubscribePush: builder.mutation<void, { endpoint: string }>({
      query: ({ endpoint }) => ({
        url: `/push/subscribe?endpoint=${encodeURIComponent(endpoint)}`,
        method: 'DELETE',
      }),
    }),
  }),
});

export const {
  useGetNearbyStationsQuery,
  useGetStationQuery,
  useSubmitReportMutation,
  useGetRecentReportsQuery,
  useGetStatsSummaryQuery,
  useSearchStationsQuery,
  useSubscribePushMutation,
  useUnsubscribePushMutation,
} = fuelFinderApi;
