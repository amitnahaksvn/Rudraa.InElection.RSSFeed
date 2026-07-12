// Mirrors Application/JobReports/Dtos/JobExecutionLogDto.cs - keep in sync by hand.

export type JobExecutionStatusName = 'Running' | 'Succeeded' | 'Failed';

export interface JobExecutionLog {
  id: string;
  jobId: string;
  jobName: string;
  hangfireJobId: string | null;
  startedAt: string;
  completedAt: string | null;
  duration: string | null; // .NET TimeSpan JSON, e.g. "00:00:01.2340000"
  status: JobExecutionStatusName;
  errorMessage: string | null;
}
