import type { JobStatus } from "@/lib/types";

const barColor: Partial<Record<JobStatus, string>> = {
  Completed: "bg-emerald-500",
  Failed: "bg-red-500",
  PartiallyFailed: "bg-orange-500",
  Paused: "bg-amber-500",
  Cancelled: "bg-slate-400",
};

export function ProgressBar({
  percent,
  status,
}: {
  percent: number;
  status: JobStatus;
}) {
  const clamped = Math.max(0, Math.min(100, percent));
  const color = barColor[status] ?? "bg-blue-500";

  return (
    <div className="flex items-center gap-2">
      <div className="h-2 flex-1 overflow-hidden rounded-full bg-slate-200">
        <div
          className={`h-full rounded-full transition-all duration-300 ${color}`}
          style={{ width: `${clamped}%` }}
        />
      </div>
      <span className="w-12 text-right text-xs tabular-nums text-slate-600">
        {clamped.toFixed(0)}%
      </span>
    </div>
  );
}
