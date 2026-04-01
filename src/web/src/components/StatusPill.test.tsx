import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import StatusPill from './StatusPill';

describe('StatusPill', () => {
  it('renders "Available" for available status', () => {
    render(<StatusPill status="available" />);
    expect(screen.getByText('Available')).toBeInTheDocument();
  });

  it('renders "Low" for low status', () => {
    render(<StatusPill status="low" />);
    expect(screen.getByText('Low')).toBeInTheDocument();
  });

  it('renders "Out of fuel" for out status', () => {
    render(<StatusPill status="out" />);
    expect(screen.getByText('Out of fuel')).toBeInTheDocument();
  });

  it('renders "Unknown" for unknown status', () => {
    render(<StatusPill status="unknown" />);
    expect(screen.getByText('Unknown')).toBeInTheDocument();
  });
});
