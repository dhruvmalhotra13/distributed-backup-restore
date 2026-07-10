import type {
  BackupJob,
  CreateBackupJobRequest,
  CreateRestoreJobRequest,
  CreateScheduleRequest,
  JobEvent,
  RestoreJob,
  Schedule,
} from "./types";

export const API_BASE =
  process.env.NEXT_PUBLIC_API_BASE?.replace(/\/$/, "") ?? "http://localhost:8080";

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const res = await fetch(`${API_BASE}${path}`, {
    ...init,
    headers: {
      "Content-Type": "application/json",
      ...(init?.headers ?? {}),
    },
    cache: "no-store",
  });

  if (!res.ok) {
    let message = `${res.status} ${res.statusText}`;
    try {
      const body = await res.json();
      if (body?.error) message = body.error;
    } catch {
      /* ignore non-JSON bodies */
    }
    throw new Error(message);
  }

  if (res.status === 204) {
    return undefined as T;
  }

  const text = await res.text();
  return (text ? JSON.parse(text) : undefined) as T;
}

export const api = {
  listBackups: () => request<BackupJob[]>("/backup-jobs"),
  getBackup: (id: string) => request<BackupJob>(`/backup-jobs/${id}`),
  createBackup: (body: CreateBackupJobRequest) =>
    request<BackupJob>("/backup-jobs", { method: "POST", body: JSON.stringify(body) }),
  pauseBackup: (id: string) =>
    request<void>(`/backup-jobs/${id}/pause`, { method: "POST" }),
  resumeBackup: (id: string) =>
    request<void>(`/backup-jobs/${id}/resume`, { method: "POST" }),
  cancelBackup: (id: string) =>
    request<void>(`/backup-jobs/${id}/cancel`, { method: "POST" }),
  retryBackup: (id: string) =>
    request<void>(`/backup-jobs/${id}/retry`, { method: "POST" }),

  listRestores: () => request<RestoreJob[]>("/restore-jobs"),
  getRestore: (id: string) => request<RestoreJob>(`/restore-jobs/${id}`),
  createRestore: (body: CreateRestoreJobRequest) =>
    request<RestoreJob>("/restore-jobs", { method: "POST", body: JSON.stringify(body) }),

  getEvents: (jobId: string) => request<JobEvent[]>(`/jobs/${jobId}/events`),

  listSchedules: () => request<Schedule[]>("/schedules"),
  createSchedule: (body: CreateScheduleRequest) =>
    request<Schedule>("/schedules", { method: "POST", body: JSON.stringify(body) }),
  toggleSchedule: (id: string) =>
    request<Schedule>(`/schedules/${id}/toggle`, { method: "POST" }),
  deleteSchedule: (id: string) =>
    request<void>(`/schedules/${id}`, { method: "DELETE" }),
};
