// Validated data-viz palette (see the "dataviz" skill's references/palette.md) - swap these hex
// values in one place if the brand palette ever changes. Categorical slots passed the
// CVD-separation/lightness/chroma-floor validator for both this app's light and dark chart
// surfaces; status colors (good/warning/critical) are the fixed, never-themed status set, always
// paired with an icon+label rather than color alone.
export const chartPalette = {
  light: {
    surface: '#fcfcfb',
    ink: '#0b0b0b',
    inkSecondary: '#52514e',
    inkMuted: '#898781',
    gridline: '#e1e0d9',
    baseline: '#c3c2b7',
    seriesNew: '#2a78d6',
    good: '#0ca30c',
    warning: '#fab219',
    critical: '#d03b3b',
    meterTrack: '#e1e0d9',
    meterFill: '#2a78d6',
  },
  dark: {
    surface: '#1a1a19',
    ink: '#ffffff',
    inkSecondary: '#c3c2b7',
    inkMuted: '#898781',
    gridline: '#2c2c2a',
    baseline: '#383835',
    seriesNew: '#3987e5',
    good: '#0ca30c',
    warning: '#fab219',
    critical: '#d03b3b',
    meterTrack: '#383835',
    meterFill: '#3987e5',
  },
} as const;

export type ChartColorMode = keyof typeof chartPalette;
