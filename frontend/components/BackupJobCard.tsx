"use client";

import { useState } from "react";
import type { BackupJob, JobStatus, ProgressUpdate } from "@/lib/types";
import { api } from "@/lib/api";
import { ProgressBar } from "./ProgressBar";
import { StatusBadge } from "./StatusBadge";
import { EventsModal } from "./EventsModal";
import { formatBytes, formatDate, formatThroughput } from "@/lib/format";

type Action = "pause" | "resume" | "cancel" | "retry";

const actionsFor: Record<JobStatus, Action[]> = {
  Queued: ["cancel"],
  Running: ["pause", "cancel"],
  Paused: ["resume", "cancel"],
  Failed: ["resume", "retry"],
  PartiallyFailed: ["retry"],
  Cancelled: ["retry"],
  Completed: [],
};

const actionStyles: Record<Action, string> = {
  pause: "bg-amber-500 hover:bg-amber-600",
  resume: "bg-blue-500 hover:bg-blue-600",
  cancel: "bg-slate-500 hover:bg-slate-600",
  retry: "bg-indigo-500 hover:bg-indigo-600",
};

export function BackupJobCard({
  job,
  live,
  onChanged,
}: {
  job: BackupJob;
  live?: ProgressUpdate;
  onChanged: () => void;
}) {
  const [busy, setBusy] = useState<Action | null>(null);
  const [showEvents, setShowEvents] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const status = (live?.status ?? job.status) as JobStatus;
  const percent = live?.progressPercent ?? job.progressPercent;
  const processed = live?.processedBytes ?? job.copiedBytes;
  const total = live?.totalBytes ?? job.totalBytes;
  const throughput = live?.throughputBytesPerSec ?? 0;

  async function run(action: Action) {
    setBusy(action);
    setError(null);
    try {
      if (action === "pause") await api.pauseBackup(job.id);
      if (action === "resume") await api.resumeBackup(job.id);
      if (action === "cancel") await api.cancelBackup(job.id);
      if (action === "retry") await api.retryBackup(job.id);
      onChanged();
    } catch (e) {
      setError(e instanceof Error ? e.message : "Action failed");
    } finally {
      setBusy(null);
    }
  }

  return (
    <div className="rounded-xl border border-slate-200 bg-white p-4 shadow-sm">
      <div className="mb-2 flex items-start justify-between gap-3">
        <div>
          <div className="flex items-center gap-2">
            <h3 className="font-semibold text-slate-800">{job.backupName}</h3>
            <StatusBadge status={status} />
          </div>
          <p className="mt-0.5 font-mono text-xs text-slate-500">{job.backupId}</p>
        </div>
        <button
          onClick={() => setShowEvents(true)}
          className="rounded-md border border-slate-200 px-2.5 py-1 text-xs text-slate-600 hover:bg-slate-50"
        >
          Timeline
        </button>
      </div>

      <ProgressBar percent={percent} status={status} />

      <div className="mt-2 flex flex-wrap gap-x-4 gap-y-1 text-xs text-slate-500">
        <span>
          {formatBytes(processed)} / {formatBytes(total)}
        </span>
        <span>
          {live?.filesProcessed ?? job.filesProcessed}/{live?.totalFiles ?? job.totalFiles} files
        </span>
        {status === "Running" && <span>{formatThroughput(throughput)}</span>}
        <span className="ml-auto">{formatDate(job.updatedAt)}</span>
      </div>

      {(live?.message || job.errorMessage) && (
        <p
          className={`mt-2 text-xs ${status === "Failed" ? "text-red-600" : "text-slate-500"}`}
        >
          {job.errorMessage ?? live?.message}
        </p>
      )}

      {error && <p className="mt-2 text-xs text-red-600">{error}</p>}

      <div className="mt-3 flex flex-wrap gap-2">
        {actionsFor[status].map((action) => (
          <button
            key={action}
            onClick={() => run(action)}
            disabled={busy !== null}
            className={`rounded-md px-3 py-1 text-xs font-medium capitalize text-white transition disabled:opacity-50 ${actionStyles[action]}`}
          >
            {busy === action ? "…" : action}
          </button>
        ))}
      </div>

      {showEvents && (
        <EventsModal jobId={job.id} title={job.backupName} onClose={() => setShowEvents(false)} />
      )}
    </div>
  );
}
