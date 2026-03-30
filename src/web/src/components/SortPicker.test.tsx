import { describe, it, expect, vi } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import SortPicker, { SORT_OPTIONS, type SortValue } from './SortPicker';

function renderPicker(value: SortValue = 'distance', onChange = vi.fn()) {
  return { onChange, ...render(<SortPicker value={value} onChange={onChange} />) };
}

describe('SortPicker', () => {
  it('renders all three sort options', () => {
    renderPicker();
    for (const opt of SORT_OPTIONS) {
      expect(screen.getByRole('button', { name: opt.label })).toBeInTheDocument();
    }
  });

  it('marks the active option as pressed', () => {
    renderPicker('status');
    expect(screen.getByRole('button', { name: 'Available' })).toHaveAttribute('aria-pressed', 'true');
    expect(screen.getByRole('button', { name: 'Nearest' })).toHaveAttribute('aria-pressed', 'false');
  });

  it('calls onChange with the selected value', () => {
    const onChange = vi.fn();
    renderPicker('distance', onChange);
    fireEvent.click(screen.getByRole('button', { name: 'Freshest' }));
    expect(onChange).toHaveBeenCalledWith('freshness');
  });
});
