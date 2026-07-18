import type { JobExecutionLog, JobExecutionStatusName } from './jobReportTypes';
import { throwIfNotOk } from './httpUtils';

export interface JobExecutionLogQuery {
  count?: number;
  skip?: number;
  jobId?: string;
  status?: JobExecutionStatusName;
}

export async function fetchJobExecutionLogs(query: JobExecutionLogQuery): Promise<JobExecutionLog[]> {
  const params = new URLSearchParams({
    count: String(query.count ?? 20),
    skip: String(query.skip ?? 0),
  });
  if (query.jobId) {
    params.set('jobId', query.jobId);
  }
  if (query.status) {
    params.set('status', query.status);
  }
  const response = await fetch(`/api/job-reports?${params}`);
  await throwIfNotOk(response);
  return response.json();
}
