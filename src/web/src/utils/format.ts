export function formatDistance(metres: number): string {
  if (metres >= 1000) return `${(metres / 1000).toFixed(1)} km`;
  return `${Math.round(metres)} m`;
}

export function formatMinutesAgo(minutes: number | null): string {
  if (minutes === null) return 'No reports yet';
  if (minutes < 1) return 'Just now';
  if (minutes < 60) return `${minutes} min ago`;
  if (minutes >= 1440) {
    const days = Math.floor(minutes / 1440);
    return `${days} day${days !== 1 ? 's' : ''} ago`;
  }
  const hours = Math.floor(minutes / 60);
  return `${hours} hr${hours !== 1 ? 's' : ''} ago`;
}

export function pluralise(count: number, singular: string): string {
  return `${count} ${singular}${count !== 1 ? 's' : ''}`;
}
