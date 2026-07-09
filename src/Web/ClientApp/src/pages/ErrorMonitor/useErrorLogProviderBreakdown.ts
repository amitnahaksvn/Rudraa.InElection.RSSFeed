import { useQuery } from '@tanstack/react-query';
import { fetchErrorLogProviderBreakdown } from '../../api/errorLogs';
import type { ErrorLogFilters } from '../../api/types';

export function useErrorLogProviderBreakdown(filters: ErrorLogFilters) {
  return useQuery({
    queryKey: ['errorLogProviderBreakdown', filters.provider, filters.country, filters.source, filters.search],
    queryFn: () => fetchErrorLogProviderBreakdown(filters),
  });
}
