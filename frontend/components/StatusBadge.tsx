import type { JobStatus } from "@/lib/types";

const styles: Record<JobStatus, string> = {
  Queued: "bg-slate-200 text-slate-700",
  Running: "bg-blue-100 text-blue-700",
  Paused: "bg-amber-100 text-amber-700",
  Cancelled: "bg-slate-300 text-slate-700",
  Completed: "bg-emerald-100 text-emerald-700",
  Failed: "bg-red-100 text-red-700",
  PartiallyFailed: "bg-orange-100 text-orange-700",
};

export function StatusBadge({ status }: { status: JobStatus }) {
  return (
    <span
      className={`inline-flex items-center rounded-full px-2.5 py-0.5 text-xs font-medium ${styles[status] ?? "bg-slate-200 text-slate-700"}`}
    >
      {status}
    </span>
  );
}
