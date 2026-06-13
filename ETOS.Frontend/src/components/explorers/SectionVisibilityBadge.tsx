import type { ContextViewSectionVisibility } from "@/lib/etos-api";

export function SectionVisibilityBadge({ visibility }: { visibility: ContextViewSectionVisibility }) {
  const normalized = visibility.toLowerCase();
  const className =
    normalized === "visible"
      ? "bg-emerald-100 text-emerald-800 dark:bg-emerald-950 dark:text-emerald-200"
      : normalized === "denied"
        ? "bg-rose-100 text-rose-800 dark:bg-rose-950 dark:text-rose-200"
        : "bg-slate-700 text-slate-200";

  return (
    <span className={`rounded-full px-3 py-1 text-xs font-semibold uppercase tracking-wide ${className}`}>
      {visibility}
    </span>
  );
}
