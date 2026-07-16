"use client";

import { useMemo, useState } from "react";
import {
  CartesianGrid,
  Line,
  LineChart,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from "recharts";
import { EmptyState } from "@/components/ui/EmptyState";
import { Notice } from "@/components/ui/Notice";

export type GovernanceTrendSeries = {
  kpiKey: string;
  title: string;
  points: { bucketStart: string; value: number }[];
  error?: string | null;
};

const STROKE = "var(--etos-accent-cyan)";
const GRID = "var(--etos-border-soft, #e2e8f0)";
const TICK = "var(--etos-ink-muted, #64748b)";

function formatBucket(iso: string) {
  const date = new Date(iso);
  if (Number.isNaN(date.getTime())) {
    return iso;
  }
  return date.toLocaleDateString(undefined, { month: "short", day: "numeric" });
}

export function GovernanceTrendCharts({ series }: { series: GovernanceTrendSeries[] }) {
  const firstKey = series[0]?.kpiKey ?? "";
  const [activeKey, setActiveKey] = useState(firstKey);
  const active = series.find((item) => item.kpiKey === activeKey) ?? series[0];

  const chartData = useMemo(() => {
    if (!active?.points?.length) {
      return [];
    }
    return active.points.map((point) => ({
      label: formatBucket(point.bucketStart),
      value: point.value,
      bucketStart: point.bucketStart,
    }));
  }, [active]);

  if (series.length === 0) {
    return <EmptyState message="No trend KPIs available for this window." />;
  }

  return (
    <div className="space-y-4">
      <div
        role="tablist"
        aria-label="Governance KPI trends"
        className="flex flex-wrap gap-2"
      >
        {series.map((item) => {
          const selected = item.kpiKey === active?.kpiKey;
          return (
            <button
              key={item.kpiKey}
              type="button"
              role="tab"
              aria-selected={selected}
              onClick={() => setActiveKey(item.kpiKey)}
              className={
                selected
                  ? "rounded-full border border-etos-ink bg-etos-ink px-3 py-1.5 text-xs font-extrabold text-white"
                  : "rounded-full border border-etos-border bg-etos-panel px-3 py-1.5 text-xs font-extrabold text-etos-ink-muted hover:bg-etos-panel-muted hover:text-etos-ink"
              }
            >
              {item.title}
            </button>
          );
        })}
      </div>

      {active?.error ? (
        <Notice variant="danger">{active.error}</Notice>
      ) : chartData.length === 0 ? (
        <EmptyState message={`No trend points for ${active?.title ?? "this KPI"} in the selected window.`} />
      ) : (
        <div className="h-64 w-full" role="img" aria-label={`${active?.title ?? "KPI"} trend chart`}>
          <ResponsiveContainer width="100%" height="100%">
            <LineChart data={chartData} margin={{ top: 8, right: 12, left: 0, bottom: 0 }}>
              <CartesianGrid stroke={GRID} strokeDasharray="3 3" vertical={false} />
              <XAxis
                dataKey="label"
                tick={{ fill: TICK, fontSize: 11 }}
                axisLine={{ stroke: GRID }}
                tickLine={false}
              />
              <YAxis
                tick={{ fill: TICK, fontSize: 11 }}
                axisLine={false}
                tickLine={false}
                width={40}
                allowDecimals={false}
              />
              <Tooltip
                contentStyle={{
                  background: "var(--etos-panel, #fff)",
                  border: "1px solid var(--etos-border, #e2e8f0)",
                  borderRadius: 12,
                  color: "var(--etos-ink, #0f172a)",
                  fontSize: 12,
                }}
                labelStyle={{ color: "var(--etos-ink-muted, #64748b)" }}
              />
              <Line
                type="monotone"
                dataKey="value"
                name={active?.title ?? "Value"}
                stroke={STROKE}
                strokeWidth={2.5}
                dot={{ r: 3, fill: STROKE }}
                activeDot={{ r: 5 }}
              />
            </LineChart>
          </ResponsiveContainer>
        </div>
      )}
    </div>
  );
}
