"use client";

import { useCallback, useEffect, useState } from "react";
import { api } from "@/lib/api";
import type { Schedule } from "@/lib/types";
import { formatDate } from "@/lib/format";

const CRON_PRESETS = [
  { label: "Every minute (demo)", value: "* * * * *" },
  { label: "Every 5 minutes", value: "*/5 * * * *" },
  { label: "Hourly", value: "0 * * * *" },
  { label: "Daily 02:00 UTC", value: "0 2 * * *" },
];

export function SchedulesPanel() {
  const [schedules, setSchedules] = useState<Schedule[]>([]);
  const [name, setName] = useState("");
  const [sourcePath, setSourcePath] = useState("");
  const [cron, setCron] = useState(CRON_PRESETS[2].value);
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

  const refresh = useCallback(async () => {
    try {
      setSchedules(await api.listSchedules());
    } catch {
      /* API may be starting */
    }
  }, []);

  useEffect(() => {
    refresh();
    const id = setInterval(refresh, 5000);
    return () => clearInterval(id);
  }, [refresh]);

  async function submit(e: React.FormEvent) {
    e.preventDefault();
    setError(null);
    setSubmitting(true);
    try {
      await api.createSchedule({ name, sourcePath, cronExpression: cron });
      setName("");
      setSourcePath("");
      refresh();
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to create schedule");
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <section className="rounded-xl border border-slate-200 bg-white p-5 shadow-sm">
      <h2 className="mb-4 text-sm font-semibold uppercase tracking-wide text-slate-500">
        Scheduled backups
      </h2>

      <form onSubmit={submit} className="space-y-3">
        <input
          value={name}
          onChange={(e) => setName(e.target.value)}
          required
          placeholder="Schedule name (backup set)"
          className="w-full rounded-md border border-slate-300 px-3 py-2 text-sm outline-none focus:border-blue-500 focus:ring-1 focus:ring-blue-500"
        />
        <input
          value={sourcePath}
          onChange={(e) => setSourcePath(e.target.value)}
          required
          placeholder="C:\Users\you\Desktop\MyProject"
          className="w-full rounded-md border border-slate-300 px-3 py-2 font-mono text-sm outline-none focus:border-blue-500 focus:ring-1 focus:ring-blue-500"
        />
        <div className="flex gap-2">
          <select
            value={cron}
            onChange={(e) => setCron(e.target.value)}
            className="rounded-md border border-slate-300 px-2 py-2 text-sm outline-none focus:border-blue-500"
          >
            {CRON_PRESETS.map((p) => (
              <option key={p.value} value={p.value}>
                {p.label}
              </option>
            ))}
          </select>
          <input
            value={cron}
            onChange={(e) => setCron(e.target.value)}
            className="w-full rounded-md border border-slate-300 px-3 py-2 font-mono text-sm outline-none focus:border-blue-500 focus:ring-1 focus:ring-blue-500"
          />
        </div>
        {error && <p className="text-sm text-red-600">{error}</p>}
        <button
          type="submit"
          disabled={submitting}
          className="w-full rounded-md bg-indigo-600 px-4 py-2 text-sm font-medium text-white transition hover:bg-indigo-700 disabled:opacity-50"
        >
          {submitting ? "Adding…" : "Add schedule"}
        </button>
      </form>

      {schedules.length > 0 && (
        <ul className="mt-4 space-y-2">
          {schedules.map((s) => (
            <li
              key={s.id}
              className="flex items-center justify-between gap-2 rounded-lg border border-slate-200 px-3 py-2 text-xs"
            >
              <div className="min-w-0">
                <div className="flex items-center gap-2">
                  <span className="font-semibold text-slate-800">{s.name}</span>
                  <code className="rounded bg-slate-100 px-1 font-mono">{s.cronExpression}</code>
                  <span className={s.enabled ? "text-emerald-600" : "text-slate-400"}>
                    {s.enabled ? "● on" : "○ off"}
                  </span>
                </div>
                <p className="truncate text-slate-500">
                  next: {s.nextRunAt ? formatDate(s.nextRunAt) : "—"}
                  {s.lastRunAt ? ` · last: ${formatDate(s.lastRunAt)}` : ""}
                </p>
              </div>
              <div className="flex shrink-0 gap-1">
                <button
                  onClick={async () => {
                    await api.toggleSchedule(s.id);
                    refresh();
                  }}
                  className="rounded border border-slate-200 px-2 py-1 text-slate-600 hover:bg-slate-50"
                >
                  {s.enabled ? "Disable" : "Enable"}
                </button>
                <button
                  onClick={async () => {
                    await api.deleteSchedule(s.id);
                    refresh();
                  }}
                  className="rounded border border-red-200 px-2 py-1 text-red-600 hover:bg-red-50"
                >
                  Delete
                </button>
              </div>
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}
