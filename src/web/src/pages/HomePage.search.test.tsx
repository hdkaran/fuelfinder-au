import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { render, screen, fireEvent, act } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { HelmetProvider } from 'react-helmet-async';
import type { StationDto } from '../types';

// ── Stub geolocation so the component gets coords on mount ────────────────────
const GEO_COORDS = { latitude: -33.8688, longitude: 151.2093 };
beforeEach(() => {
  Object.defineProperty(navigator, 'geolocation', {
    value: {
      getCurrentPosition: (success: PositionCallback) =>
        success({ coords: GEO_COORDS } as GeolocationPosition),
    },
    configurable: true,
  });
  vi.useFakeTimers();
});

afterEach(() => {
  vi.useRealTimers();
  vi.restoreAllMocks();
});

// ── Mock RTK Query hooks (no Redux store / network needed) ────────────────────
vi.mock('../api/fuelFinderApi', () => ({
  useGetNearbyStationsQuery:   vi.fn(),
  useSearchStationsQuery:      vi.fn(),
  useGetStatsSummaryQuery:     vi.fn(),
  useGetTodayReportsQuery:     vi.fn(() => ({ data: undefined, isLoading: false })),
  useGetAffectedStationsQuery: vi.fn(() => ({ data: undefined, isLoading: false })),
  useSubscribePushMutation:    vi.fn(() => [vi.fn()]),
  useUnsubscribePushMutation:  vi.fn(() => [vi.fn()]),
}));

import { useGetNearbyStationsQuery, useSearchStationsQuery, useGetStatsSummaryQuery } from '../api/fuelFinderApi';
import HomePage from './HomePage';

// ── Fixture stations ──────────────────────────────────────────────────────────
const stationA: StationDto = {
  id: 's1', name: 'Shell Bondi', brand: 'Shell', address: '1 Beach Rd',
  suburb: 'Bondi', state: 'NSW', latitude: -33.89, longitude: 151.27,
  distanceMetres: 1200, status: 'available',
  fuelAvailability: [], reportCount: 2, lastReportMinutesAgo: 10,
};
const stationB: StationDto = {
  id: 's2', name: 'BP Newtown', brand: 'BP', address: '5 King St',
  suburb: 'Newtown', state: 'NSW', latitude: -33.9, longitude: 151.18,
  distanceMetres: 3400, status: 'low',
  fuelAvailability: [], reportCount: 1, lastReportMinutesAgo: 30,
};

const noopRefetch = vi.fn();

function setupMocks({
  nearby = [] as StationDto[],
  nearbyLoading = false,
  nearbyError = false,
  search = undefined as StationDto[] | undefined,
  searchLoading = false,
  searchError = false,
} = {}) {
  vi.mocked(useGetNearbyStationsQuery).mockReturnValue({
    data: nearby, isLoading: nearbyLoading, isError: nearbyError,
    isFetching: false, refetch: noopRefetch,
  } as unknown as ReturnType<typeof useGetNearbyStationsQuery>);

  vi.mocked(useSearchStationsQuery).mockReturnValue({
    data: search, isLoading: searchLoading, isError: searchError,
  } as unknown as ReturnType<typeof useSearchStationsQuery>);

  vi.mocked(useGetStatsSummaryQuery).mockReturnValue({
    data: undefined,
  } as unknown as ReturnType<typeof useGetStatsSummaryQuery>);
}

function renderPage() {
  return render(
    <HelmetProvider>
      <MemoryRouter>
        <HomePage />
      </MemoryRouter>
    </HelmetProvider>,
  );
}

/** Type into the search box and advance past the debounce. */
function typeSearch(text: string) {
  fireEvent.change(screen.getByRole('searchbox'), { target: { value: text } });
  act(() => { vi.advanceTimersByTime(400); });
}

// ── Extra fixtures for sort tests ─────────────────────────────────────────────
const stationOut: StationDto = {
  id: 's3', name: 'Caltex Surry Hills', brand: 'Caltex', address: '10 Crown St',
  suburb: 'Surry Hills', state: 'NSW', latitude: -33.88, longitude: 151.21,
  distanceMetres: 2000, status: 'out',
  fuelAvailability: [], reportCount: 0, lastReportMinutesAgo: null,
};

// ── Tests ─────────────────────────────────────────────────────────────────────
describe('HomePage — search integration', () => {
  it('renders the search bar on load', () => {
    setupMocks();
    renderPage();
    expect(screen.getByRole('searchbox')).toBeInTheDocument();
  });

  it('shows the radius picker when search input is empty', () => {
    setupMocks();
    renderPage();
    // RadiusPicker renders one button labelled exactly "5 km"
    expect(screen.getByRole('button', { name: '5 km' })).toBeInTheDocument();
  });

  it('hides the radius picker while a search is active (≥ 2 chars)', () => {
    setupMocks({ search: [stationA] });
    renderPage();
    typeSearch('Shell');
    expect(screen.queryByRole('button', { name: '5 km' })).not.toBeInTheDocument();
  });

  it('shows search results after debounce when query is ≥ 2 chars', () => {
    setupMocks({ search: [stationA, stationB] });
    renderPage();
    typeSearch('sh');
    expect(screen.getByText('Shell Bondi')).toBeInTheDocument();
    expect(screen.getByText('BP Newtown')).toBeInTheDocument();
  });

  it('does not show search results when query is only 1 char', () => {
    setupMocks({ nearby: [stationB], search: [stationA] });
    renderPage();
    // Type only 1 char — debounce fires but isSearching stays false
    fireEvent.change(screen.getByRole('searchbox'), { target: { value: 'S' } });
    act(() => { vi.advanceTimersByTime(400); });
    // Nearby list shows, search result does not
    expect(screen.getByText('BP Newtown')).toBeInTheDocument();
    expect(screen.queryByText('Shell Bondi')).not.toBeInTheDocument();
  });

  it('shows empty state when search returns no results', () => {
    setupMocks({ search: [] });
    renderPage();
    typeSearch('xyzzy');
    expect(screen.getByText(/no stations found for/i)).toBeInTheDocument();
    expect(screen.getByText(/xyzzy/i)).toBeInTheDocument();
  });

  it('shows skeleton loaders while search is loading', () => {
    setupMocks({ searchLoading: true });
    renderPage();
    typeSearch('BP');
    expect(screen.getByRole('list')).toBeInTheDocument();
  });

  it('restores the nearby list after clearing the search', () => {
    setupMocks({ nearby: [stationB], search: [stationA] });
    renderPage();

    // Activate search
    typeSearch('Shell');
    expect(screen.getByText('Shell Bondi')).toBeInTheDocument();

    // Clear via the ✕ button — search input empties, debounce fires
    fireEvent.click(screen.getByRole('button', { name: /clear/i }));
    act(() => { vi.advanceTimersByTime(400); });

    // Nearby station is back, search result is gone
    expect(screen.getByText('BP Newtown')).toBeInTheDocument();
    expect(screen.queryByText('Shell Bondi')).not.toBeInTheDocument();
  });

  it('hides the sort picker while a search is active', () => {
    setupMocks({ nearby: [stationA, stationB], search: [stationA] });
    renderPage();
    expect(screen.getByRole('button', { name: 'Nearest' })).toBeInTheDocument();
    typeSearch('Shell');
    expect(screen.queryByRole('button', { name: 'Nearest' })).not.toBeInTheDocument();
  });

  it('restores the radius picker after clearing the search', () => {
    setupMocks({ search: [stationA] });
    renderPage();

    typeSearch('Shell');
    expect(screen.queryByRole('button', { name: '5 km' })).not.toBeInTheDocument();

    fireEvent.click(screen.getByRole('button', { name: /clear/i }));
    act(() => { vi.advanceTimersByTime(400); });

    expect(screen.getByRole('button', { name: '5 km' })).toBeInTheDocument();
  });
});

describe('HomePage — sort integration', () => {
  it('sorts by status: available before low before out', () => {
    // Provide stations in reverse order to confirm sorting reorders them
    setupMocks({ nearby: [stationOut, stationB, stationA] });
    renderPage();

    fireEvent.click(screen.getByRole('button', { name: 'Available' }));

    const items = screen.getAllByRole('listitem');
    const texts = items.map((el) => el.textContent ?? '');
    const aIdx = texts.findIndex((t) => t.includes('Shell Bondi'));
    const bIdx = texts.findIndex((t) => t.includes('BP Newtown'));
    const cIdx = texts.findIndex((t) => t.includes('Caltex Surry Hills'));
    expect(aIdx).toBeLessThan(bIdx);
    expect(bIdx).toBeLessThan(cIdx);
  });

  it('sorts by freshness: station with lower lastReportMinutesAgo appears first', () => {
    setupMocks({ nearby: [stationB, stationA] }); // B=30 min, A=10 min
    renderPage();

    fireEvent.click(screen.getByRole('button', { name: 'Freshest' }));

    const items = screen.getAllByRole('listitem');
    const texts = items.map((el) => el.textContent ?? '');
    const aIdx = texts.findIndex((t) => t.includes('Shell Bondi'));
    const bIdx = texts.findIndex((t) => t.includes('BP Newtown'));
    expect(aIdx).toBeLessThan(bIdx); // A (10 min ago) before B (30 min ago)
  });

  it('places null lastReportMinutesAgo last when sorting by freshness', () => {
    setupMocks({ nearby: [stationOut, stationA] }); // Out has null, A has 10
    renderPage();

    fireEvent.click(screen.getByRole('button', { name: 'Freshest' }));

    const items = screen.getAllByRole('listitem');
    const texts = items.map((el) => el.textContent ?? '');
    const aIdx = texts.findIndex((t) => t.includes('Shell Bondi'));
    const outIdx = texts.findIndex((t) => t.includes('Caltex Surry Hills'));
    expect(aIdx).toBeLessThan(outIdx);
  });
});
