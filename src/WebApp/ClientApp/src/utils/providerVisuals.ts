const AVATAR_COLORS = [
  '#1976d2',
  '#9c27b0',
  '#2e7d32',
  '#ed6c02',
  '#0288d1',
  '#c2185b',
  '#5d4037',
  '#455a64',
  '#00796b',
  '#7b1fa2',
];

export function getDomainFromUrl(url?: string | null): string | undefined {
  if (!url) return undefined;
  try {
    return new URL(url).hostname.replace(/^www\./, '');
  } catch {
    return undefined;
  }
}

export function getFaviconUrl(domain: string): string {
  return `https://www.google.com/s2/favicons?sz=64&domain=${encodeURIComponent(domain)}`;
}

// Deterministic (not random) so the same provider always gets the same color across reloads.
export function getAvatarColor(name: string): string {
  let hash = 0;
  for (let i = 0; i < name.length; i++) {
    hash = name.charCodeAt(i) + ((hash << 5) - hash);
  }
  return AVATAR_COLORS[Math.abs(hash) % AVATAR_COLORS.length];
}

export function getInitials(name: string): string {
  const letters = name.replace(/[^A-Za-z0-9]/g, '');
  return (letters.slice(0, 2) || '?').toUpperCase();
}
