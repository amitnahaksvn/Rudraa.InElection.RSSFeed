import { useId, useMemo, useState } from 'react';
import Box from '@mui/material/Box';
import Stack from '@mui/material/Stack';
import Typography from '@mui/material/Typography';
import { useChartColors } from '../useChartColorMode';
import { formatCompactNumber, formatFullNumber } from '../../../utils/formatNumber';

export interface ChartSeries {
  key: string;
  label: string;
  color: string;
}

export interface DailyStackedChartProps {
  /** ISO "YYYY-MM-DD" dates, oldest first - the x-axis. */
  dates: string[];
  series: ChartSeries[];
  /** series.key -> one value per date, aligned to `dates`. */
  values: Record<string, number[]>;
  variant: 'bar' | 'area';
  height?: number;
  ariaLabel: string;
}

const VIEW_WIDTH = 960;
const MARGIN = { top: 12, right: 12, bottom: 28, left: 44 };
const GRID_STEPS = 4;

function niceMax(value: number): number {
  if (value <= 0) {
    return 4;
  }
  const magnitude = Math.pow(10, Math.floor(Math.log10(value)));
  const residual = value / magnitude;
  const niceResidual = residual <= 1 ? 1 : residual <= 2 ? 2 : residual <= 5 ? 5 : 10;
  return niceResidual * magnitude;
}

function formatDateLabel(iso: string): string {
  const date = new Date(`${iso}T00:00:00Z`);
  return date.toLocaleDateString(undefined, { month: 'short', day: 'numeric', timeZone: 'UTC' });
}

/** A rect with rounded top corners, square base - the "4px rounded data-end, square at the baseline" bar spec. */
function roundedTopRectPath(x: number, y: number, w: number, h: number, r: number): string {
  const radius = Math.min(r, w / 2, h);
  if (h <= 0 || radius <= 0) {
    return `M ${x} ${y + h} L ${x} ${y} L ${x + w} ${y} L ${x + w} ${y + h} Z`;
  }
  return [
    `M ${x} ${y + h}`,
    `L ${x} ${y + radius}`,
    `Q ${x} ${y} ${x + radius} ${y}`,
    `L ${x + w - radius} ${y}`,
    `Q ${x + w} ${y} ${x + w} ${y + radius}`,
    `L ${x + w} ${y + h}`,
    'Z',
  ].join(' ');
}

export function DailyStackedChart({ dates, series, values, variant, height = 220, ariaLabel }: DailyStackedChartProps) {
  const colors = useChartColors();
  const gradientId = useId();
  const [hoverIndex, setHoverIndex] = useState<number | null>(null);

  const plotWidth = VIEW_WIDTH - MARGIN.left - MARGIN.right;
  const plotHeight = height - MARGIN.top - MARGIN.bottom;

  const totals = useMemo(
    () => dates.map((_, i) => series.reduce((sum, s) => sum + (values[s.key]?.[i] ?? 0), 0)),
    [dates, series, values],
  );
  const maxTotal = niceMax(Math.max(...totals, 0));

  const slot = dates.length > 0 ? plotWidth / dates.length : plotWidth;
  const barWidth = Math.min(24, slot * 0.55);
  const yScale = (v: number) => plotHeight - (v / maxTotal) * plotHeight;

  // Thin the x-axis labels so they never collide - show at most ~8 across the whole width.
  const labelStride = Math.max(1, Math.ceil(dates.length / 8));

  const cumulative = useMemo(() => {
    // cumulative[seriesIndex][dayIndex] = running total up to and including that series (bottom-up stack).
    const result: number[][] = [];
    let running = dates.map(() => 0);
    for (const s of series) {
      const next = dates.map((_, i) => running[i] + (values[s.key]?.[i] ?? 0));
      result.push(next);
      running = next;
    }
    return result;
  }, [dates, series, values]);

  return (
    <Box>
      <Box sx={{ position: 'relative' }}>
        <svg
          viewBox={`0 0 ${VIEW_WIDTH} ${height}`}
          width="100%"
          height={height}
          role="img"
          aria-label={ariaLabel}
          style={{ display: 'block', overflow: 'visible' }}
        >
          <defs>
            {series.map((s) => (
              <linearGradient key={s.key} id={`${gradientId}-${s.key}`} x1="0" y1="0" x2="0" y2="1">
                <stop offset="0%" stopColor={s.color} stopOpacity={0.28} />
                <stop offset="100%" stopColor={s.color} stopOpacity={0.05} />
              </linearGradient>
            ))}
          </defs>

          <g transform={`translate(${MARGIN.left}, ${MARGIN.top})`}>
            {/* Gridlines + y-axis ticks */}
            {Array.from({ length: GRID_STEPS + 1 }, (_, step) => {
              const value = (maxTotal / GRID_STEPS) * step;
              const y = yScale(value);
              return (
                <g key={step}>
                  <line x1={0} x2={plotWidth} y1={y} y2={y} stroke={colors.gridline} strokeWidth={1} />
                  <text x={-8} y={y} textAnchor="end" dominantBaseline="middle" fontSize={11} fill={colors.inkMuted}>
                    {formatCompactNumber(value)}
                  </text>
                </g>
              );
            })}

            {/* Baseline */}
            <line x1={0} x2={plotWidth} y1={plotHeight} y2={plotHeight} stroke={colors.baseline} strokeWidth={1} />

            {variant === 'bar'
              ? dates.map((date, dayIndex) => {
                  const cx = slot * dayIndex + slot / 2;
                  const barX = cx - barWidth / 2;
                  const topSegmentIndex = series.findLastIndex((_, si) => (values[series[si].key]?.[dayIndex] ?? 0) > 0);
                  return (
                    <g key={date}>
                      {series.map((s, seriesIndex) => {
                        const value = values[s.key]?.[dayIndex] ?? 0;
                        if (value <= 0) {
                          return null;
                        }
                        const segBottom = seriesIndex === 0 ? plotHeight : yScale(cumulative[seriesIndex - 1][dayIndex]);
                        const segTop = yScale(cumulative[seriesIndex][dayIndex]);
                        const segHeight = segBottom - segTop;
                        const isTopSegment = seriesIndex === topSegmentIndex;
                        const path = isTopSegment
                          ? roundedTopRectPath(barX, segTop, barWidth, segHeight, 4)
                          : `M ${barX} ${segBottom} L ${barX} ${segTop} L ${barX + barWidth} ${segTop} L ${barX + barWidth} ${segBottom} Z`;
                        return (
                          <path
                            key={s.key}
                            d={path}
                            fill={s.color}
                            opacity={hoverIndex === null || hoverIndex === dayIndex ? 1 : 0.35}
                          />
                        );
                      })}
                    </g>
                  );
                })
              : series.map((s, seriesIndex) => {
                  const topLine = dates.map((_, i) => {
                    const x = slot * i + slot / 2;
                    const y = yScale(cumulative[seriesIndex][i]);
                    return `${x},${y}`;
                  });
                  const bottomLine = dates
                    .map((_, i) => {
                      const x = slot * i + slot / 2;
                      const y = yScale(seriesIndex === 0 ? 0 : cumulative[seriesIndex - 1][i]);
                      return `${x},${y}`;
                    })
                    .reverse();
                  const areaPoints = [...topLine, ...bottomLine].join(' ');
                  const topLinePath = `M ${topLine.join(' L ')}`;
                  return (
                    <g key={s.key}>
                      <polygon points={areaPoints} fill={`url(#${gradientId}-${s.key})`} />
                      <path d={topLinePath} fill="none" stroke={s.color} strokeWidth={2} strokeLinejoin="round" strokeLinecap="round" />
                    </g>
                  );
                })}

            {/* Crosshair */}
            {hoverIndex !== null && (
              <line
                x1={slot * hoverIndex + slot / 2}
                x2={slot * hoverIndex + slot / 2}
                y1={0}
                y2={plotHeight}
                stroke={colors.baseline}
                strokeWidth={1}
                strokeDasharray="3,3"
              />
            )}

            {/* X-axis labels */}
            {dates.map((date, i) =>
              i % labelStride === 0 ? (
                <text
                  key={date}
                  x={slot * i + slot / 2}
                  y={plotHeight + 18}
                  textAnchor="middle"
                  fontSize={11}
                  fill={colors.inkMuted}
                >
                  {formatDateLabel(date)}
                </text>
              ) : null,
            )}

            {/* Hover hit targets - the whole day-column, bigger than the mark itself */}
            {dates.map((date, i) => (
              <rect
                key={date}
                x={slot * i}
                y={0}
                width={slot}
                height={plotHeight}
                fill="transparent"
                tabIndex={0}
                aria-label={`${formatDateLabel(date)}: ${formatFullNumber(totals[i])} total`}
                onPointerEnter={() => setHoverIndex(i)}
                onPointerMove={() => setHoverIndex(i)}
                onPointerLeave={() => setHoverIndex(null)}
                onFocus={() => setHoverIndex(i)}
                onBlur={() => setHoverIndex(null)}
              />
            ))}
          </g>
        </svg>

        {hoverIndex !== null && (
          <Box
            sx={{
              position: 'absolute',
              top: MARGIN.top,
              left: `${((slot * hoverIndex + slot / 2) / VIEW_WIDTH) * 100}%`,
              transform: dates.length > 1 && hoverIndex > dates.length / 2 ? 'translateX(-105%)' : 'translateX(8px)',
              bgcolor: 'background.paper',
              border: 1,
              borderColor: 'divider',
              borderRadius: 1,
              boxShadow: 3,
              p: 1,
              pointerEvents: 'none',
              minWidth: 150,
              zIndex: 1,
            }}
          >
            <Typography variant="caption" color="text.secondary" fontWeight={600}>
              {formatDateLabel(dates[hoverIndex])}
            </Typography>
            <Stack gap={0.25} sx={{ mt: 0.5 }}>
              {series.map((s) => (
                <Stack key={s.key} direction="row" alignItems="center" justifyContent="space-between" gap={2}>
                  <Stack direction="row" alignItems="center" gap={0.75}>
                    <Box sx={{ width: 10, height: 10, borderRadius: '2px', bgcolor: s.color, flexShrink: 0 }} />
                    <Typography variant="caption" color="text.secondary">
                      {s.label}
                    </Typography>
                  </Stack>
                  <Typography variant="caption" fontWeight={700} sx={{ fontVariantNumeric: 'tabular-nums' }}>
                    {formatFullNumber(values[s.key]?.[hoverIndex] ?? 0)}
                  </Typography>
                </Stack>
              ))}
            </Stack>
          </Box>
        )}
      </Box>

      {/* Legend - always present for 2+ series */}
      <Stack direction="row" flexWrap="wrap" gap={2} sx={{ mt: 1, px: `${MARGIN.left}px` }}>
        {series.map((s) => (
          <Stack key={s.key} direction="row" alignItems="center" gap={0.75}>
            <Box sx={{ width: 12, height: 12, borderRadius: '2px', bgcolor: s.color }} />
            <Typography variant="caption" color="text.secondary">
              {s.label}
            </Typography>
          </Stack>
        ))}
      </Stack>
    </Box>
  );
}
