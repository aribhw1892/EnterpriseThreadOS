"use client";

import { useMemo, useState, useTransition } from "react";
import {
  getDigitalThreadBranches,
  getDigitalThreadEvents,
  getDigitalThreadMinimap,
  type DigitalThreadBranch,
  type DigitalThreadEvent,
  type DigitalThreadMinimap as MinimapModel,
  type DigitalThreadSummary,
  type DigitalThreadSystem,
} from "@/lib/etos-api";
import { buildCanvasScene } from "@/lib/digital-thread-map";
import { buildPreviewEventDetail } from "@/lib/ui-fixtures/digital-thread-timeline";
import { EmptyState } from "@/components/ui/EmptyState";
import { ErrorState } from "@/components/ui/ErrorState";
import { KpiCard } from "@/components/ui/KpiCard";
import { DigitalThreadCanvas } from "@/components/digital-thread/DigitalThreadCanvas";
import {
  DigitalThreadFilterBar,
  type DigitalThreadFilters,
} from "@/components/digital-thread/DigitalThreadFilterBar";
import { DigitalThreadEventInspector } from "@/components/digital-thread/DigitalThreadEventInspector";
import { DigitalThreadLiveClient } from "@/components/digital-thread/DigitalThreadLiveClient";
import { DigitalThreadMinimap } from "@/components/digital-thread/DigitalThreadMinimap";
import { DigitalThreadScrubber } from "@/components/digital-thread/DigitalThreadScrubber";

type Props = {
  summary: DigitalThreadSummary | null;
  systems: DigitalThreadSystem[];
  events: DigitalThreadEvent[];
  branches: DigitalThreadBranch[];
  minimap: MinimapModel | null;
  loadError: string | null;
  useLiveProjection: boolean;
  auth: { userId: string; tenantId: string };
};

export function DigitalThreadTimelineWorkspace({
  summary,
  systems,
  events: initialEvents,
  branches: initialBranches,
  minimap: initialMinimap,
  loadError,
  useLiveProjection,
  auth,
}: Props) {
  const [events, setEvents] = useState(initialEvents);
  const [branches, setBranches] = useState(initialBranches);
  const [minimap, setMinimap] = useState(initialMinimap);
  const [filters, setFilters] = useState<DigitalThreadFilters>({
    systemId: "",
    eventType: "",
    trustState: "",
    windowHours: summary?.windowHours ?? 24,
  });
  const [zoomPercent, setZoomPercent] = useState(100);
  const [pan, setPan] = useState({ x: 0, y: 0 });
  const [selectedEventId, setSelectedEventId] = useState<string | null>(null);
  const [live, setLive] = useState(useLiveProjection);
  const [scrubRatio, setScrubRatio] = useState(1);
  const [streamStatus, setStreamStatus] = useState<string>(
    useLiveProjection ? "stopped" : "preview",
  );
  const [refetchError, setRefetchError] = useState<string | null>(null);
  const [pending, startTransition] = useTransition();

  const eventTypes = useMemo(
    () => [...new Set(events.map((event) => event.eventType))].sort(),
    [events],
  );

  const filteredEvents = useMemo(() => {
    return events.filter((event) => {
      if (filters.systemId && event.sourceSystemId !== filters.systemId) return false;
      if (filters.eventType && event.eventType !== filters.eventType) return false;
      if (
        filters.trustState &&
        !event.trustState.toLowerCase().includes(filters.trustState.toLowerCase())
      ) {
        return false;
      }
      return true;
    });
  }, [events, filters]);

  const scene = useMemo(
    () => buildCanvasScene(branches, filteredEvents, systems),
    [branches, filteredEvents, systems],
  );

  const previewDetail = useMemo(() => {
    if (useLiveProjection || !selectedEventId) return null;
    return buildPreviewEventDetail(selectedEventId);
  }, [selectedEventId, useLiveProjection]);

  const [sinceIso] = useState(() => new Date(Date.now() - 60_000).toISOString());

  const refetchWindow = (windowHours: number, ratio: number) => {
    if (!useLiveProjection) return;
    const to = new Date();
    const from = new Date(to.getTime() - windowHours * 60 * 60 * 1000 * Math.max(0.05, ratio));
    startTransition(() => {
      void (async () => {
        const [nextEvents, nextBranches, nextMinimap] = await Promise.all([
          getDigitalThreadEvents({
            from: from.toISOString(),
            to: to.toISOString(),
            systemId: filters.systemId || undefined,
            limit: 100,
          }),
          getDigitalThreadBranches({
            from: from.toISOString(),
            to: to.toISOString(),
          }),
          getDigitalThreadMinimap(windowHours),
        ]);

        if (nextEvents.error) {
          setRefetchError(nextEvents.error);
          return;
        }
        setRefetchError(null);
        setEvents(nextEvents.data ?? []);
        if (!nextBranches.error) setBranches(nextBranches.data ?? []);
        if (!nextMinimap.error) setMinimap(nextMinimap.data);
      })();
    });
  };

  if (loadError) {
    return <ErrorState error={loadError} />;
  }

  const empty =
    systems.length === 0 && events.length === 0 && branches.length === 0;

  return (
    <div className="grid gap-4 text-etos-ops-ink">
      {useLiveProjection ? (
        <DigitalThreadLiveClient
          enabled={live}
          auth={auth}
          since={sinceIso}
          onEvent={(event) => {
            setEvents((current) => {
              if (current.some((item) => item.eventId === event.eventId)) {
                return current;
              }
              return [event, ...current].slice(0, 200);
            });
          }}
          onStatus={(status, detail) =>
            setStreamStatus(detail ? `${status}: ${detail}` : status)
          }
        />
      ) : null}

      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <h1 className="text-2xl font-black tracking-tight">Digital Thread Timeline</h1>
          <p className="text-sm text-etos-ops-ink-muted">
            Reverse string canvas — history converges into NOW. Enterprise systems on
            strands; governance/security as overlays.
          </p>
        </div>
        <div className="flex flex-wrap items-center gap-2">
          {!useLiveProjection ? (
            <p className="rounded-full border border-etos-ops-border px-3 py-1 text-[10px] font-bold uppercase tracking-wide text-sky-300">
              Preview · fixtures
            </p>
          ) : null}
          <p className="rounded-full border border-etos-ops-border px-3 py-1 text-[10px] font-bold uppercase tracking-wide text-etos-ops-ink-muted">
            Stream · {streamStatus}
            {pending ? " · refreshing" : ""}
          </p>
        </div>
      </div>

      <section className="grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
        <KpiCard
          ops
          label="Connected systems"
          value={summary?.connectedSystemCount ?? systems.length}
        />
        <KpiCard
          ops
          label="Events / min"
          value={summary?.eventsLastMinute?.toFixed(2) ?? "—"}
        />
        <KpiCard ops label="Branches" value={branches.length} />
        <KpiCard
          ops
          label="Open alerts"
          value={summary?.openAlertCounts.total ?? "—"}
        />
      </section>

      <DigitalThreadFilterBar
        systems={systems.filter(
          (system) =>
            !["governance", "security", "tool-runtime", "agent-runtime"].includes(
              system.systemId,
            ),
        )}
        eventTypes={eventTypes}
        filters={filters}
        onChange={(next) => {
          setFilters(next);
          refetchWindow(next.windowHours, scrubRatio);
        }}
      />

      {refetchError ? <ErrorState error={refetchError} /> : null}

      {empty ? (
        <EmptyState message="No digital-thread systems or events for this tenant in the selected window." />
      ) : (
        <div className="grid gap-4 xl:grid-cols-[minmax(0,1fr)_320px]">
          <div className="grid gap-3">
            <DigitalThreadCanvas
              scene={scene}
              zoomPercent={zoomPercent}
              pan={pan}
              selectedEventId={selectedEventId}
              onSelectEvent={setSelectedEventId}
              onPanChange={setPan}
              onZoomChange={setZoomPercent}
            />
            <div className="grid gap-3 md:grid-cols-[220px_minmax(0,1fr)]">
              <DigitalThreadMinimap
                minimap={minimap}
                view={{
                  x: pan.x,
                  y: pan.y,
                  scale: zoomPercent / 100,
                  width: 320,
                  height: 180,
                }}
              />
              <DigitalThreadScrubber
                windowHours={filters.windowHours}
                scrubRatio={scrubRatio}
                live={live}
                onLiveChange={(next) => {
                  if (!useLiveProjection) return;
                  setLive(next);
                }}
                onScrubRatioChange={(ratio) => {
                  setScrubRatio(ratio);
                  refetchWindow(filters.windowHours, ratio);
                }}
              />
            </div>
          </div>
          <DigitalThreadEventInspector
            eventId={selectedEventId}
            previewDetail={previewDetail}
            onClose={() => setSelectedEventId(null)}
          />
        </div>
      )}
    </div>
  );
}
