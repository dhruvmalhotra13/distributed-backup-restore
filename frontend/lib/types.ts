export type JobStatus =
  | "Queued"
  | "Running"
  | "Paused"
  | "Cancelled"
  | "Completed"
  | "Failed"
  | "PartiallyFailed";

export interface BackupJob {
  id: string;
  backupId: string;
  backupName: string;
  sourcePath: string;
  status: JobStatus;
  totalBytes: number;
  copiedBytes: number;
  totalFiles: number;
  filesProcessed: number;
  progressPercent: number;
  errorMessage?: string | null;
  createdAt: string;
  updatedAt: string;
}

export interface RestoreJob {
  id: string;
  backupId: string;
  restorePath: string;
  status: JobStatus;
  totalBytes: number;
  restoredBytes: number;
  progressPercent: number;
  errorMessage?: string | null;
  createdAt: string;
  updatedAt: string;
}

export interface JobEvent {
  id: string;
  jobId: string;
  jobType: string;
  eventType: string;
  message: string;
  timestamp: string;
}

export interface ProgressUpdate {
  jobId: string;
  jobType: "Backup" | "Restore";
  status: JobStatus;
  totalBytes: number;
  processedBytes: number;
  progressPercent: number;
  totalFiles: number;
  filesProcessed: number;
  throughputBytesPerSec: number;
  message?: string | null;
  timestamp: string;
}

export interface CreateBackupJobRequest {
  sourcePath: string;
  backupName: string;
  chunkSizeBytes?: number;
}

export interface CreateRestoreJobRequest {
  backupId: string;
  restorePath: string;
}
