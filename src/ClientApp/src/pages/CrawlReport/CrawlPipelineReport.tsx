import { useMemo, useState } from 'react';
import Stack from '@mui/material/Stack';
import Typography from '@mui/material/Typography';
import Card from '@mui/material/Card';
import CardContent from '@mui/material/CardContent';
import CircularProgress from '@mui/material/CircularProgress';
import Alert from '@mui/material/Alert';
import Box from '@mui/material/Box';
import type { CrawlPipelineName } from '../../api/crawlTypes';
import { useCrawlReport } from './useCrawlReport';
import { DateRangeControl, type DateRangePreset } from './DateRangeControl';
import { defaultCustomRange, resolveDateRange } from './dateRange';
import { StatTile } from './StatTile';
import { DailyStackedChart } from './charts/DailyStackedChart';
import { ProviderBreakdownTable } from './ProviderBreakdownTable';
import { RecentRunsTable } from './RecentRunsTable';
import { useChartColors } from './useChartColorMode';

export function CrawlPipelineReport({ pipeline }: { pipeline: CrawlPipelineName }) {
  const [preset, setPreset] = useState<DateRangePreset>('7');
  const initialCustom = useMemo(defaultCustomRange, []);
  const [customFrom, setCustomFrom] = useState(initialCustom.from);
  const [customTo, setCustomTo] = useState(initialCustom.to);

  const { from, to } = useMemo(() => resolveDateRange(preset, customFrom, customTo), [preset, customFrom, customTo]);

  const { data: report, isLoading, isError, isFetching } = useCrawlReport(pipeline, from, to);
  const colors = useChartColors();

  const dates = report?.timeSeries.map((p) => p.date) ?? [];

  return (
    <Stack gap={2}>
      <DateRangeControl
        preset={preset}
        onPresetChange={setPreset}
        customFrom={customFrom}
        customTo={customTo}
        onCustomFromChange={setCustomFrom}
        onCustomToChange={setCustomTo}
      />

      {isLoading && (
        <Stack alignItems="center" sx={{ py: 6 }}>
          <CircularProgress />
        </Stack>
      )}

      {isError && <Alert severity="error">Failed to load the {pipeline} crawl report.</Alert>}

      {report && (
        <Box sx={{ opacity: isFetching ? 0.7 : 1, transition: 'opacity 0.15s' }}>
          <Stack gap={2}>
            <Stack direction="row" flexWrap="wrap" gap={1.5}>
              <StatTile label="Total runs" value={report.summary.totalRuns} />
              <StatTile label="Success rate" value={report.summary.successRatePercent} suffix="%" color={colors.good} />
              <StatTile label="New articles" value={report.summary.newArticles} color={colors.seriesNew} />
              <StatTile label="Failed feeds" value={report.summary.failedFeeds} color={report.summary.failedFeeds > 0 ? colors.critical : undefined} />
            </Stack>

            <Card variant="outlined">
              <CardContent>
                <Typography variant="subtitle1" fontWeight={600} sx={{ mb: 1 }}>
                  Runs by day
                </Typography>
                <DailyStackedChart
                  variant="bar"
                  ariaLabel={`${pipeline} runs by day, by outcome`}
                  dates={dates}
                  series={[
                    { key: 'success', label: 'Completed', color: colors.good },
                    { key: 'errors', label: 'Completed with errors', color: colors.warning },
                    { key: 'failed', label: 'Failed', color: colors.critical },
                    { key: 'skipped', label: 'Skipped (lock held)', color: colors.inkMuted },
                  ]}
                  values={{
                    success: report.timeSeries.map((p) => p.successfulRuns),
                    errors: report.timeSeries.map((p) => p.runsWithErrors),
                    failed: report.timeSeries.map((p) => p.failedRuns),
                    skipped: report.timeSeries.map((p) => p.skippedRuns),
                  }}
                />
              </CardContent>
            </Card>

            <Card variant="outlined">
              <CardContent>
                <Typography variant="subtitle1" fontWeight={600} sx={{ mb: 1 }}>
                  Articles by day
                </Typography>
                <DailyStackedChart
                  variant="area"
                  ariaLabel={`${pipeline} new articles by day`}
                  dates={dates}
                  series={[{ key: 'new', label: 'New', color: colors.seriesNew }]}
                  values={{
                    new: report.timeSeries.map((p) => p.newArticles),
                  }}
                />
              </CardContent>
            </Card>

            <Card variant="outlined">
              <CardContent>
                <Typography variant="subtitle1" fontWeight={600} sx={{ mb: 1 }}>
                  Providers ({report.providers.length})
                </Typography>
                <ProviderBreakdownTable rows={report.providers} />
              </CardContent>
            </Card>

            <Card variant="outlined">
              <CardContent>
                <Typography variant="subtitle1" fontWeight={600} sx={{ mb: 1 }}>
                  Recent runs
                </Typography>
                <RecentRunsTable pipeline={pipeline} from={from} to={to} />
              </CardContent>
            </Card>
          </Stack>
        </Box>
      )}
    </Stack>
  );
}
