"use client";

import { useState } from "react";
import { api } from "@/lib/api";
import type { BackupJob } from "@/lib/types";

export function CreateRestoreForm({
  completedBackups,
  onCreated,
}: {
  completedBackups: BackupJob[];
  onCreated: () => void;
}) {
  const [backupId, setBackupId] = useState("");
  const [restorePath, setRestorePath] = useState("");
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function submit(e: React.FormEvent) {
    e.preventDefault();
    setError(null);
    setSubmitting(true);
    try {
      await api.createRestore({ backupId, restorePath });
      onCreated();
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to create restore");
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <form onSubmit={submit} className="space-y-3">
      <div>
        <label className="mb-1 block text-sm font-medium text-slate-700">
          Backup version
        </label>
        <select
          value={backupId}
          onChange={(e) => setBackupId(e.target.value)}
          required
          className="w-full rounded-md border border-slate-300 px-3 py-2 text-sm outline-none focus:border-blue-500 focus:ring-1 focus:ring-blue-500"
        >
          <option value="" disabled>
            Select a completed backup…
          </option>
          {completedBackups.map((b) => (
            <option key={b.id} value={b.backupId}>
              {b.backupName} ({b.backupId})
            </option>
          ))}
        </select>
      </div>
      <div>
        <label className="mb-1 block text-sm font-medium text-slate-700">
          Restore target path
        </label>
        <input
          value={restorePath}
          onChange={(e) => setRestorePath(e.target.value)}
          required
          placeholder="C:\Users\you\Desktop\Restored"
          className="w-full rounded-md border border-slate-300 px-3 py-2 font-mono text-sm outline-none focus:border-blue-500 focus:ring-1 focus:ring-blue-500"
        />
        <p className="mt-1 text-xs text-slate-500">
          Any folder inside your user home; it will be created if missing.
        </p>
      </div>
      {error && <p className="text-sm text-red-600">{error}</p>}
      <button
        type="submit"
        disabled={submitting || completedBackups.length === 0}
        className="w-full rounded-md bg-emerald-600 px-4 py-2 text-sm font-medium text-white transition hover:bg-emerald-700 disabled:opacity-50"
      >
        {submitting ? "Creating…" : "Start restore"}
      </button>
      {completedBackups.length === 0 && (
        <p className="text-xs text-slate-500">
          No completed backups yet — run a backup first.
        </p>
      )}
    </form>
  );
}
