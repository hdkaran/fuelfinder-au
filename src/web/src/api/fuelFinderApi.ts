import { createApi, fetchBaseQuery } from '@reduxjs/toolkit/query/react';
import type { StationDto, ReportDto, ReportPayload, StatsDto, TodayReportDto, AffectedStationDto, PriceDto } from '../types';

interface NearbyStationsParams {
  lat: number;
  lng: number;
  radius: number;
  fuelType?: string;
}

interface NearbyPricesParams {
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
  tagTypes: ['NearbyStations', 'SearchStations', 'Station', 'RecentReports', 'Stats', 'NearbyPrices'],
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
      providesTags: ['NearbyStations'],
    }),
    getStation: builder.query<StationDto, string>({
      query: (id) => `/stations/${id}`,
      providesTags: (_result, _error, id) => [{ type: 'Station', id }],
    }),
    submitReport: builder.mutation<{ id: string }, ReportPayload>({
      query: (payload) => ({
        url: '/reports',
        method: 'POST',
        body: payload,
      }),
      invalidatesTags: (_result, _error, arg) => [
        'NearbyStations',
        'SearchStations',
        { type: 'Station',        id: arg.stationId },
        { type: 'RecentReports',  id: arg.stationId },
        'Stats',
      ],
    }),
    getRecentReports: builder.query<ReportDto[], string>({
      query: (stationId) => ({
        url: '/reports/recent',
        params: { stationId },
      }),
      providesTags: (_result, _error, stationId) => [{ type: 'RecentReports', id: stationId }],
    }),
    getStatsSummary: builder.query<StatsDto, void>({
      query: () => '/stats/summary',
      providesTags: ['Stats'],
    }),
    searchStations: builder.query<StationDto[], SearchStationsParams>({
      query: ({ q, lat, lng }) => ({
        url: '/stations/search',
        params: { q, ...(lat != null && lng != null ? { lat, lng } : {}) },
      }),
      providesTags: ['SearchStations'],
    }),
    getTodayReports: builder.query<TodayReportDto[], void>({
      query: () => '/stats/reports-today',
      providesTags: ['Stats'],
    }),
    getAffectedStations: builder.query<AffectedStationDto[], void>({
      query: () => '/stats/affected-stations',
      providesTags: ['Stats'],
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
    getNearbyPrices: builder.query<PriceDto[], NearbyPricesParams>({
      query: ({ lat, lng, radius, fuelType }) => ({
        url: '/prices/nearby',
        params: { lat, lng, radius, ...(fuelType ? { fuelType } : {}) },
      }),
      providesTags: ['NearbyPrices'],
    }),
  }),
});

export const {
  useGetNearbyStationsQuery,
  useGetStationQuery,
  useSubmitReportMutation,
  useGetRecentReportsQuery,
  useGetStatsSummaryQuery,
  useGetTodayReportsQuery,
  useGetAffectedStationsQuery,
  useSearchStationsQuery,
  useSubscribePushMutation,
  useUnsubscribePushMutation,
  useGetNearbyPricesQuery,
} = fuelFinderApi;
