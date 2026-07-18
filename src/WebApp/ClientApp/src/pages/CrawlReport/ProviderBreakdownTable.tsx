import { useMemo, useState } from 'react';
import Box from '@mui/material/Box';
import Table from '@mui/material/Table';
import TableBody from '@mui/material/TableBody';
import TableCell from '@mui/material/TableCell';
import TableContainer from '@mui/material/TableContainer';
import TableHead from '@mui/material/TableHead';
import TableRow from '@mui/material/TableRow';
import TableSortLabel from '@mui/material/TableSortLabel';
import TextField from '@mui/material/TextField';
import Typography from '@mui/material/Typography';
import Tooltip from '@mui/material/Tooltip';
import InputAdornment from '@mui/material/InputAdornment';
import SearchIcon from '@mui/icons-material/Search';
import type { CrawlReportProviderRow } from '../../api/crawlTypes';
import { formatRelativeTime, formatAbsoluteTime } from '../../utils/formatDate';
import { formatFullNumber } from '../../utils/formatNumber';
import { SuccessRateMeter } from './SuccessRateMeter';

type SortKey =
  | 'provider'
  | 'country'
  | 'nextExecution'
  | 'lastExecution'
  | 'totalRuns'
  | 'successRatePercent'
  | 'newArticles'
  | 'failedFeeds';

const COLUMNS: { key: SortKey; label: string; align?: 'right' }[] = [
  { key: 'provider', label: 'Provider' },
  { key: 'country', label: 'Country' },
  { key: 'nextExecution', label: 'Next run' },
  { key: 'lastExecution', label: 'Last run' },
  { key: 'totalRuns', label: 'Runs', align: 'right' },
  { key: 'successRatePercent', label: 'Success rate' },
  { key: 'newArticles', label: 'New', align: 'right' },
  { key: 'failedFeeds', label: 'Failed', align: 'right' },
];

function compareValues(a: CrawlReportProviderRow, b: CrawlReportProviderRow, key: SortKey): number {
  const av = a[key];
  const bv = b[key];
  if (av === null || av === undefined) return bv === null || bv === undefined ? 0 : 1;
  if (bv === null || bv === undefined) return -1;
  if (typeof av === 'number' && typeof bv === 'number') return av - bv;
  return String(av).localeCompare(String(bv));
}

export function ProviderBreakdownTable({ rows }: { rows: CrawlReportProviderRow[] }) {
  const [search, setSearch] = useState('');
  const [sortKey, setSortKey] = useState<SortKey>('provider');
  const [sortDir, setSortDir] = useState<'asc' | 'desc'>('asc');

  const filteredSorted = useMemo(() => {
    const term = search.trim().toLowerCase();
    const filtered = term
      ? rows.filter((r) => r.provider.toLowerCase().includes(term) || r.country.toLowerCase().includes(term))
      : rows;
    const sorted = [...filtered].sort((a, b) => compareValues(a, b, sortKey) * (sortDir === 'asc' ? 1 : -1));
    return sorted;
  }, [rows, search, sortKey, sortDir]);

  const handleSort = (key: SortKey) => {
    if (key === sortKey) {
      setSortDir((d) => (d === 'asc' ? 'desc' : 'asc'));
    } else {
      setSortKey(key);
      setSortDir('asc');
    }
  };

  return (
    <Box>
      <TextField
        size="small"
        placeholder="Search provider or country..."
        value={search}
        onChange={(e) => setSearch(e.target.value)}
        sx={{ mb: 1, width: 280 }}
        slotProps={{ input: { startAdornment: <InputAdornment position="start"><SearchIcon fontSize="small" /></InputAdornment> } }}
      />
      <TableContainer sx={{ maxHeight: 520 }}>
        <Table size="small" stickyHeader>
          <TableHead>
            <TableRow>
              {COLUMNS.map((col) => (
                <TableCell key={col.key} align={col.align} sortDirection={sortKey === col.key ? sortDir : false}>
                  <TableSortLabel active={sortKey === col.key} direction={sortDir} onClick={() => handleSort(col.key)}>
                    {col.label}
                  </TableSortLabel>
                </TableCell>
              ))}
            </TableRow>
          </TableHead>
          <TableBody>
            {filteredSorted.map((row) => (
              <TableRow key={`${row.country}-${row.provider}`} hover>
                <TableCell>
                  <Typography variant="body2" fontWeight={600}>
                    {row.provider}
                  </Typography>
                </TableCell>
                <TableCell>{row.country}</TableCell>
                <TableCell>
                  {row.cron ? (
                    <Tooltip title={`cron: ${row.cron} (${row.timeZone ?? 'UTC'})`}>
                      <span>{row.nextExecution ? formatRelativeTime(row.nextExecution) : '—'}</span>
                    </Tooltip>
                  ) : (
                    '—'
                  )}
                </TableCell>
                <TableCell>
                  {row.lastExecution ? (
                    <Tooltip title={formatAbsoluteTime(row.lastExecution)}>
                      <span>{formatRelativeTime(row.lastExecution)}</span>
                    </Tooltip>
                  ) : (
                    'Never'
                  )}
                </TableCell>
                <TableCell align="right" sx={{ fontVariantNumeric: 'tabular-nums' }}>
                  {formatFullNumber(row.totalRuns)}
                </TableCell>
                <TableCell>{row.hasRun ? <SuccessRateMeter percent={row.successRatePercent} /> : '—'}</TableCell>
                <TableCell align="right" sx={{ fontVariantNumeric: 'tabular-nums' }}>
                  {formatFullNumber(row.newArticles)}
                </TableCell>
                <TableCell align="right" sx={{ fontVariantNumeric: 'tabular-nums', color: row.failedFeeds > 0 ? 'error.main' : undefined }}>
                  {formatFullNumber(row.failedFeeds)}
                </TableCell>
              </TableRow>
            ))}
            {filteredSorted.length === 0 && (
              <TableRow>
                <TableCell colSpan={COLUMNS.length} align="center">
                  <Typography variant="body2" color="text.secondary" sx={{ py: 3 }}>
                    No providers match your search.
                  </Typography>
                </TableCell>
              </TableRow>
            )}
          </TableBody>
        </Table>
      </TableContainer>
    </Box>
  );
}
