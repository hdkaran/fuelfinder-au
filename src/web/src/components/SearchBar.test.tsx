import { describe, it, expect, vi } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import SearchBar from './SearchBar';

function renderBar(value = '', onChange = vi.fn()) {
  return { onChange, ...render(<SearchBar value={value} onChange={onChange} />) };
}

describe('SearchBar', () => {
  it('renders the input with default placeholder', () => {
    renderBar();
    expect(screen.getByPlaceholderText(/name, brand or suburb/i)).toBeInTheDocument();
  });

  it('renders a custom placeholder when provided', () => {
    render(<SearchBar value="" onChange={vi.fn()} placeholder="Find a station" />);
    expect(screen.getByPlaceholderText('Find a station')).toBeInTheDocument();
  });

  it('calls onChange when the user types', () => {
    const onChange = vi.fn();
    render(<SearchBar value="" onChange={onChange} />);
    fireEvent.change(screen.getByRole('searchbox'), { target: { value: 'Shell' } });
    expect(onChange).toHaveBeenCalledWith('Shell');
  });

  it('does not render the clear button when value is empty', () => {
    renderBar('');
    expect(screen.queryByRole('button', { name: /clear/i })).not.toBeInTheDocument();
  });

  it('renders the clear button when value is non-empty', () => {
    renderBar('Shell');
    expect(screen.getByRole('button', { name: /clear/i })).toBeInTheDocument();
  });

  it('calls onChange with empty string when clear is clicked', () => {
    const onChange = vi.fn();
    render(<SearchBar value="Shell" onChange={onChange} />);
    fireEvent.click(screen.getByRole('button', { name: /clear/i }));
    expect(onChange).toHaveBeenCalledWith('');
  });
});
