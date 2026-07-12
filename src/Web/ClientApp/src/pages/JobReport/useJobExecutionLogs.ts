import { useQuery } from '@tanstack/react-query';
import { fetchJobExecutionLogs } from '../../api/jobReports';
import type { JobExecutionStatusName } from '../../api/jobReportTypes';

export function useJobExecutionLogs(
  jobId: string | null,
  status: JobExecutionStatusName | null,
  page: number,
  pageSize: number,
) {
  return useQuery({
    queryKey: ['jobExecutionLogs', jobId, status, page, pageSize],
    queryFn: () =>
      fetchJobExecutionLogs({
        count: pageSize,
        skip: page * pageSize,
        jobId: jobId ?? undefined,
        status: status ?? undefined,
      }),
    placeholderData: (previous) => previous,
  });
}
