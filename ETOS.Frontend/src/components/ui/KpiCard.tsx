import type { ReactNode } from "react";

export type KpiTrend = "up" | "warn" | "bad" | "flat";

const trendClasses: Record<KpiTrend, string> = {
  up: "text-etos-success-fg",
  warn: "text-etos-warning-fg",
  bad: "text-etos-danger-fg",
  flat: "text-etos-ink-muted",
};

export function KpiCard({
  label,
  value,
  trend,
  trendLabel,
  hint,
  ops = false,
}: {
  label: string;
  value: ReactNode;
  trend?: KpiTrend;
  trendLabel?: string;
  hint?: string;
  /** Ops-canvas styling for Mission Control / digital-thread surfaces. */
  ops?: boolean;
}) {
  return (
    <div
      className={
        ops
          ? "rounded-etos-card border border-etos-ops-border bg-etos-ops-panel-elevated p-4"
          : "rounded-etos-card border border-etos-border-panel bg-etos-panel-elevated p-4 shadow-etos"
      }
    >
      <p
        className={`text-xs font-semibold uppercase tracking-wide ${
          ops ? "text-etos-ops-ink-muted" : "text-etos-ink-muted"
        }`}
      >
        {label}
      </p>
      <p
        className={`mt-2 text-[28px] font-black leading-none ${
          ops ? "text-etos-ops-ink" : "text-etos-ink"
        }`}
      >
        {value}
      </p>
      {trend && trendLabel ? (
        <p className={`mt-2 text-xs font-semibold ${trendClasses[trend]}`}>
          {trendLabel}
        </p>
      ) : null}
      {hint ? (
        <p
          className={`mt-1 text-xs ${
            ops ? "text-etos-ops-ink-muted" : "text-etos-ink-subtle"
          }`}
        >
          {hint}
        </p>
      ) : null}
    </div>
  );
}
