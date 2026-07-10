"use client";

import { useEffect, useState } from "react";
import { api } from "@/lib/api";
import type { JobEvent } from "@/lib/types";
import { formatDate } from "@/lib/format";

export function EventsModal({
  jobId,
  title,
  onClose,
}: {
  jobId: string;
  title: string;
  onClose: () => void;
}) {
  const [events, setEvents] = useState<JobEvent[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let active = true;
    api
      .getEvents(jobId)
      .then((data) => active && setEvents(data))
      .catch((e) => active && setError(e instanceof Error ? e.message : "Failed to load"))
      .finally(() => active && setLoading(false));
    return () => {
      active = false;
    };
  }, [jobId]);

  return (
    <div
      className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4"
      onClick={onClose}
    >
      <div
        className="max-h-[80vh] w-full max-w-2xl overflow-hidden rounded-xl bg-white shadow-xl"
        onClick={(e) => e.stopPropagation()}
      >
        <div className="flex items-center justify-between border-b border-slate-200 px-5 py-4">
          <h3 className="text-sm font-semibold text-slate-800">Timeline — {title}</h3>
          <button
            onClick={onClose}
            className="rounded-md px-2 py-1 text-slate-500 hover:bg-slate-100"
          >
            ✕
          </button>
        </div>
        <div className="max-h-[65vh] overflow-y-auto p-5">
          {loading && <p className="text-sm text-slate-500">Loading…</p>}
          {error && <p className="text-sm text-red-600">{error}</p>}
          {!loading && !error && events.length === 0 && (
            <p className="text-sm text-slate-500">No events yet.</p>
          )}
          <ol className="relative space-y-4 border-l border-slate-200 pl-5">
            {events.map((e) => (
              <li key={e.id} className="relative">
                <span className="absolute -left-[23px] top-1 h-2.5 w-2.5 rounded-full bg-blue-500" />
                <div className="flex items-baseline justify-between gap-3">
                  <span className="text-sm font-medium text-slate-800">{e.eventType}</span>
                  <span className="text-xs text-slate-400">{formatDate(e.timestamp)}</span>
                </div>
                <p className="text-sm text-slate-600">{e.message}</p>
              </li>
            ))}
          </ol>
        </div>
      </div>
    </div>
  );
}
