import { configureStore } from '@reduxjs/toolkit';
import { fuelFinderApi } from '../api/fuelFinderApi';

export const store = configureStore({
  reducer: {
    [fuelFinderApi.reducerPath]: fuelFinderApi.reducer,
  },
  middleware: (getDefaultMiddleware) =>
    getDefaultMiddleware().concat(fuelFinderApi.middleware),
});

export type RootState = ReturnType<typeof store.getState>;
export type AppDispatch = typeof store.dispatch;
