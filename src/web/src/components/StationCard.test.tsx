import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import StationCard from './StationCard';
import type { StationDto } from '../types';

const station: StationDto = {
  id: 'abc-123',
  name: 'Sydney BP',
  brand: 'BP',
  address: '1 George St',
  suburb: 'Sydney',
  state: 'NSW',
  latitude: -33.8688,
  longitude: 151.2093,
  distanceMetres: 350,
  status: 'available',
  fuelAvailability: [
    { fuelType: 'ULP', available: true },
    { fuelType: 'Diesel', available: false },
    { fuelType: 'E10', available: null },
    { fuelType: 'Premium', available: true },
  ],
  reportCount: 3,
  lastReportMinutesAgo: 5,
  latestPrices: [],
};

function renderCard(s: StationDto = station) {
  return render(
    <MemoryRouter>
      <StationCard station={s} />
    </MemoryRouter>,
  );
}

describe('StationCard', () => {
  it('renders station name and brand', () => {
    renderCard();
    expect(screen.getByText('Sydney BP')).toBeInTheDocument();
    expect(screen.getByText('BP')).toBeInTheDocument();
  });

  it('renders the status pill', () => {
    renderCard();
    expect(screen.getByText('Available')).toBeInTheDocument();
  });

  it('renders formatted distance', () => {
    renderCard();
    expect(screen.getByText(/350 m/)).toBeInTheDocument();
  });

  it('renders fuel badges for all four types', () => {
    renderCard();
    expect(screen.getByText(/ULP/)).toBeInTheDocument();
    expect(screen.getByText(/Diesel/)).toBeInTheDocument();
    expect(screen.getByText(/E10/)).toBeInTheDocument();
    expect(screen.getByText(/Prem/)).toBeInTheDocument();
  });

  it('renders last report time in footer', () => {
    renderCard();
    expect(screen.getByText(/5 min ago/)).toBeInTheDocument();
  });

  it('renders "No reports yet" in footer when lastReportMinutesAgo is null', () => {
    renderCard({ ...station, lastReportMinutesAgo: null, reportCount: 0 });
    expect(screen.getByText(/No reports yet/)).toBeInTheDocument();
  });

  it('links to the station detail page', () => {
    renderCard();
    const links = screen.getAllByRole('link');
    const detailLink = links.find((l) => l.getAttribute('href') === '/stations/abc-123');
    expect(detailLink).toBeInTheDocument();
  });

  it('renders a directions link pointing to Google Maps with the station coordinates', () => {
    renderCard();
    const link = screen.getByRole('link', { name: /directions to Sydney BP/i });
    expect(link).toHaveAttribute(
      'href',
      'https://www.google.com/maps/dir/?api=1&destination=-33.8688,151.2093',
    );
    expect(link).toHaveAttribute('target', '_blank');
    expect(link).toHaveAttribute('rel', 'noopener noreferrer');
  });
});
