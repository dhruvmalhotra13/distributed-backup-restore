"use client";

import { useState } from "react";
import type { JobStatus, ProgressUpdate, RestoreJob } from "@/lib/types";
import { ProgressBar } from "./ProgressBar";
import { StatusBadge } from "./StatusBadge";
import { EventsModal } from "./EventsModal";
import { formatBytes, formatDate } from "@/lib/format";

export function RestoreJobCard({
  job,
  live,
}: {
  job: RestoreJob;
  live?: ProgressUpdate;
}) {
  const [showEvents, setShowEvents] = useState(false);

  const status = (live?.status ?? job.status) as JobStatus;
  const percent = live?.progressPercent ?? job.progressPercent;
  const processed = live?.processedBytes ?? job.restoredBytes;
  const total = live?.totalBytes ?? job.totalBytes;

  return (
    <div className="rounded-xl border border-slate-200 bg-white p-4 shadow-sm">
      <div className="mb-2 flex items-start justify-between gap-3">
        <div>
          <div className="flex items-center gap-2">
            <h3 className="font-mono text-sm font-semibold text-slate-800">{job.backupId}</h3>
            <StatusBadge status={status} />
          </div>
          <p className="mt-0.5 truncate font-mono text-xs text-slate-500">→ {job.restorePath}</p>
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
        <span className="ml-auto">{formatDate(job.updatedAt)}</span>
      </div>

      {(live?.message || job.errorMessage) && (
        <p
          className={`mt-2 text-xs ${status === "Failed" ? "text-red-600" : "text-slate-500"}`}
        >
          {job.errorMessage ?? live?.message}
        </p>
      )}

      {showEvents && (
        <EventsModal jobId={job.id} title={`Restore ${job.backupId}`} onClose={() => setShowEvents(false)} />
      )}
    </div>
  );
}
