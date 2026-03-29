import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import FuelBadge from './FuelBadge';

describe('FuelBadge', () => {
  it('shows ✓ when available is true', () => {
    render(<FuelBadge fuelType="ULP" available={true} />);
    expect(screen.getByText('✓')).toBeInTheDocument();
  });

  it('shows ✗ when available is false', () => {
    render(<FuelBadge fuelType="Diesel" available={false} />);
    expect(screen.getByText('✗')).toBeInTheDocument();
  });

  it('shows ? when available is null', () => {
    render(<FuelBadge fuelType="E10" available={null} />);
    expect(screen.getByText('?')).toBeInTheDocument();
  });

  it('abbreviates Premium to Prem', () => {
    render(<FuelBadge fuelType="Premium" available={true} />);
    expect(screen.getByText(/Prem/)).toBeInTheDocument();
    expect(screen.queryByText(/Premium/)).not.toBeInTheDocument();
  });

  it('renders other fuel types verbatim', () => {
    render(<FuelBadge fuelType="Diesel" available={null} />);
    expect(screen.getByText(/Diesel/)).toBeInTheDocument();
  });
});
