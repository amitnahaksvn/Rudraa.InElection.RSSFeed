const compactFormatter = new Intl.NumberFormat(undefined, { notation: 'compact', maximumFractionDigits: 1 });
const fullFormatter = new Intl.NumberFormat(undefined);

export function formatCompactNumber(value: number): string {
  return compactFormatter.format(value);
}

export function formatFullNumber(value: number): string {
  return fullFormatter.format(value);
}
