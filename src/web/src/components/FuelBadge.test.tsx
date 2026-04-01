import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import FuelBadge from './FuelBadge';

describe('FuelBadge', () => {
  it('renders a check icon when available is true', () => {
    const { container } = render(<FuelBadge fuelType="ULP" available={true} />);
    expect(container.querySelector('svg')).toBeInTheDocument();
    expect(screen.getByText(/ULP/)).toBeInTheDocument();
  });

  it('renders an x icon when available is false', () => {
    const { container } = render(<FuelBadge fuelType="Diesel" available={false} />);
    expect(container.querySelector('svg')).toBeInTheDocument();
    expect(screen.getByText(/Diesel/)).toBeInTheDocument();
  });

  it('renders a minus icon when available is null', () => {
    const { container } = render(<FuelBadge fuelType="E10" available={null} />);
    expect(container.querySelector('svg')).toBeInTheDocument();
    expect(screen.getByText(/E10/)).toBeInTheDocument();
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
