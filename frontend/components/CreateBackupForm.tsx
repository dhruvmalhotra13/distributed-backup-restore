"use client";

import { useState } from "react";
import { api } from "@/lib/api";

export function CreateBackupForm({ onCreated }: { onCreated: () => void }) {
  const [sourcePath, setSourcePath] = useState("");
  const [backupName, setBackupName] = useState("");
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function submit(e: React.FormEvent) {
    e.preventDefault();
    setError(null);
    setSubmitting(true);
    try {
      await api.createBackup({ sourcePath, backupName });
      setBackupName("");
      onCreated();
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to create backup");
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <form onSubmit={submit} className="space-y-3">
      <div>
        <label className="mb-1 block text-sm font-medium text-slate-700">
          Backup name
        </label>
        <input
          value={backupName}
          onChange={(e) => setBackupName(e.target.value)}
          required
          placeholder="e.g. VacationBackup"
          className="w-full rounded-md border border-slate-300 px-3 py-2 text-sm outline-none focus:border-blue-500 focus:ring-1 focus:ring-blue-500"
        />
      </div>
      <div>
        <label className="mb-1 block text-sm font-medium text-slate-700">
          Source path
        </label>
        <input
          value={sourcePath}
          onChange={(e) => setSourcePath(e.target.value)}
          required
          placeholder="C:\Users\you\Desktop\MyProject"
          className="w-full rounded-md border border-slate-300 px-3 py-2 font-mono text-sm outline-none focus:border-blue-500 focus:ring-1 focus:ring-blue-500"
        />
        <p className="mt-1 text-xs text-slate-500">
          The real folder on your machine (any folder inside your user home).
        </p>
      </div>
      {error && <p className="text-sm text-red-600">{error}</p>}
      <button
        type="submit"
        disabled={submitting}
        className="w-full rounded-md bg-blue-600 px-4 py-2 text-sm font-medium text-white transition hover:bg-blue-700 disabled:opacity-50"
      >
        {submitting ? "Creating…" : "Start backup"}
      </button>
    </form>
  );
}
