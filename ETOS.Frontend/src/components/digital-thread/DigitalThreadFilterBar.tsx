"use client";

import type { DigitalThreadSystem } from "@/lib/etos-api";

export type DigitalThreadFilters = {
  systemId: string;
  eventType: string;
  trustState: string;
  windowHours: number;
};

type Props = {
  systems: DigitalThreadSystem[];
  eventTypes: string[];
  filters: DigitalThreadFilters;
  onChange: (next: DigitalThreadFilters) => void;
};

export function DigitalThreadFilterBar({
  systems,
  eventTypes,
  filters,
  onChange,
}: Props) {
  return (
    <div className="flex flex-wrap items-end gap-3 rounded-etos-card border border-etos-ops-border bg-etos-ops-panel p-3">
      <label className="grid gap-1 text-[10px] font-bold uppercase tracking-wide text-etos-ops-ink-muted">
        Time window
        <select
          className="rounded-md border border-etos-ops-border bg-etos-ops-canvas px-2 py-1.5 text-xs text-etos-ops-ink"
          value={filters.windowHours}
          onChange={(event) =>
            onChange({ ...filters, windowHours: Number(event.target.value) })
          }
        >
          <option value={6}>Last 6 hours</option>
          <option value={24}>Last 24 hours</option>
          <option value={72}>Last 72 hours</option>
          <option value={168}>Last 7 days</option>
        </select>
      </label>
      <label className="grid gap-1 text-[10px] font-bold uppercase tracking-wide text-etos-ops-ink-muted">
        System
        <select
          className="rounded-md border border-etos-ops-border bg-etos-ops-canvas px-2 py-1.5 text-xs text-etos-ops-ink"
          value={filters.systemId}
          onChange={(event) =>
            onChange({ ...filters, systemId: event.target.value })
          }
        >
          <option value="">All systems</option>
          {systems.map((system) => (
            <option key={system.systemId} value={system.systemId}>
              {system.displayName}
            </option>
          ))}
        </select>
      </label>
      <label className="grid gap-1 text-[10px] font-bold uppercase tracking-wide text-etos-ops-ink-muted">
        Event type
        <select
          className="rounded-md border border-etos-ops-border bg-etos-ops-canvas px-2 py-1.5 text-xs text-etos-ops-ink"
          value={filters.eventType}
          onChange={(event) =>
            onChange({ ...filters, eventType: event.target.value })
          }
        >
          <option value="">All types</option>
          {eventTypes.map((type) => (
            <option key={type} value={type}>
              {type}
            </option>
          ))}
        </select>
      </label>
      <label className="grid gap-1 text-[10px] font-bold uppercase tracking-wide text-etos-ops-ink-muted">
        Trust
        <select
          className="rounded-md border border-etos-ops-border bg-etos-ops-canvas px-2 py-1.5 text-xs text-etos-ops-ink"
          value={filters.trustState}
          onChange={(event) =>
            onChange({ ...filters, trustState: event.target.value })
          }
        >
          <option value="">All trust states</option>
          <option value="Trusted">Trusted</option>
          <option value="Unverified">Unverified</option>
          <option value="Conflicted">Conflicted</option>
        </select>
      </label>
      <label
        className="grid gap-1 text-[10px] font-bold uppercase tracking-wide text-etos-ops-ink-muted opacity-60"
        title="No site dimension in MVP data"
      >
        Site
        <select
          disabled
          className="cursor-not-allowed rounded-md border border-etos-ops-border bg-etos-ops-canvas px-2 py-1.5 text-xs text-etos-ops-ink-muted"
          value=""
        >
          <option value="">Unavailable in MVP</option>
        </select>
      </label>
      <label
        className="grid gap-1 text-[10px] font-bold uppercase tracking-wide text-etos-ops-ink-muted opacity-60"
        title="No product-line dimension in MVP data"
      >
        Product line
        <select
          disabled
          className="cursor-not-allowed rounded-md border border-etos-ops-border bg-etos-ops-canvas px-2 py-1.5 text-xs text-etos-ops-ink-muted"
          value=""
        >
          <option value="">Unavailable in MVP</option>
        </select>
      </label>
    </div>
  );
}
