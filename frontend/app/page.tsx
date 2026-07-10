"use client";

import { useCallback, useEffect, useState } from "react";
import { api } from "@/lib/api";
import type { BackupJob, RestoreJob } from "@/lib/types";
import { useProgress } from "@/lib/useProgress";
import { CreateBackupForm } from "@/components/CreateBackupForm";
import { CreateRestoreForm } from "@/components/CreateRestoreForm";
import { BackupJobCard } from "@/components/BackupJobCard";
import { RestoreJobCard } from "@/components/RestoreJobCard";
import { SchedulesPanel } from "@/components/SchedulesPanel";

export default function Dashboard() {
  const { progress, connected } = useProgress();
  const [backups, setBackups] = useState<BackupJob[]>([]);
  const [restores, setRestores] = useState<RestoreJob[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [loaded, setLoaded] = useState(false);

  const refresh = useCallback(async () => {
    try {
      const [b, r] = await Promise.all([api.listBackups(), api.listRestores()]);
      setBackups(b);
      setRestores(r);
      setError(null);
    } catch (e) {
      setError(e instanceof Error ? e.message : "Failed to reach the API");
    } finally {
      setLoaded(true);
    }
  }, []);

  useEffect(() => {
    refresh();
    const id = setInterval(refresh, 4000);
    return () => clearInterval(id);
  }, [refresh]);

  const completedBackups = backups.filter((b) => b.status === "Completed");

  return (
    <main className="mx-auto w-full max-w-6xl px-4 py-8">
      <header className="mb-8 flex flex-wrap items-center justify-between gap-3">
        <div>
          <h1 className="text-2xl font-bold text-slate-900">
            Distributed Backup &amp; Restore
          </h1>
          <p className="text-sm text-slate-500">
            Local-first backup platform · live progress via SignalR
          </p>
        </div>
        <div className="flex items-center gap-2 rounded-full border border-slate-200 bg-white px-3 py-1.5 text-xs">
          <span
            className={`h-2 w-2 rounded-full ${connected ? "bg-emerald-500" : "bg-slate-300"}`}
          />
          {connected ? "Live" : "Offline"}
        </div>
      </header>

      {error && (
        <div className="mb-6 rounded-lg border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">
          {error} — is the API running at{" "}
          <code className="font-mono">
            {process.env.NEXT_PUBLIC_API_BASE ?? "http://localhost:8080"}
          </code>
          ?
        </div>
      )}

      <div className="grid grid-cols-1 gap-6 lg:grid-cols-3">
        <div className="space-y-6">
          <section className="rounded-xl border border-slate-200 bg-white p-5 shadow-sm">
            <h2 className="mb-4 text-sm font-semibold uppercase tracking-wide text-slate-500">
              New backup
            </h2>
            <CreateBackupForm onCreated={refresh} />
          </section>

          <section className="rounded-xl border border-slate-200 bg-white p-5 shadow-sm">
            <h2 className="mb-4 text-sm font-semibold uppercase tracking-wide text-slate-500">
              New restore
            </h2>
            <CreateRestoreForm completedBackups={completedBackups} onCreated={refresh} />
          </section>

          <SchedulesPanel />
        </div>

        <div className="space-y-6 lg:col-span-2">
          <section>
            <h2 className="mb-3 text-sm font-semibold uppercase tracking-wide text-slate-500">
              Backup jobs ({backups.length})
            </h2>
            <div className="space-y-3">
              {loaded && backups.length === 0 && (
                <p className="rounded-lg border border-dashed border-slate-300 p-6 text-center text-sm text-slate-400">
                  No backups yet. Create one to get started.
                </p>
              )}
              {backups.map((job) => (
                <BackupJobCard
                  key={job.id}
                  job={job}
                  live={progress[job.id]}
                  onChanged={refresh}
                />
              ))}
            </div>
          </section>

          {restores.length > 0 && (
            <section>
              <h2 className="mb-3 text-sm font-semibold uppercase tracking-wide text-slate-500">
                Restore jobs ({restores.length})
              </h2>
              <div className="space-y-3">
                {restores.map((job) => (
                  <RestoreJobCard key={job.id} job={job} live={progress[job.id]} />
                ))}
              </div>
            </section>
          )}
        </div>
      </div>
    </main>
  );
}
