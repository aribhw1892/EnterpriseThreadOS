"use client";

import {
  createContext,
  useContext,
  useMemo,
  useState,
  useTransition,
  type ReactNode,
} from "react";
import {
  getDigitalThreadEvents,
  type DigitalThreadEvent,
} from "@/lib/etos-api";
import {
  mapDigitalThreadEventsToStream,
  type LiveStreamItem,
} from "@/lib/digital-thread-map";
import { DigitalThreadLiveClient } from "@/components/digital-thread/DigitalThreadLiveClient";
import { DigitalThreadScrubber } from "@/components/digital-thread/DigitalThreadScrubber";

const severityDotClasses: Record<string, string> = {
  success: "bg-emerald-400",
  info: "bg-sky-400",
  warning: "bg-amber-400",
  danger: "bg-rose-400",
};

type LiveContextValue = {
  live: boolean;
  setLive: (value: boolean) => void;
  status: string;
  streamItems: LiveStreamItem[];
  streamError: string | null;
  scrubRatio: number;
  setScrubRatio: (ratio: number) => void;
  pending: boolean;
};

const MissionControlLiveContext = createContext<LiveContextValue | null>(null);

function useMissionControlLive() {
  const value = useContext(MissionControlLiveContext);
  if (!value) {
    throw new Error("MissionControlLiveProvider required");
  }
  return value;
}

export function MissionControlLiveProvider({
  initialEvents,
  auth,
  streamError,
  children,
}: {
  initialEvents: DigitalThreadEvent[];
  auth: { userId: string; tenantId: string };
  streamError: string | null;
  children: ReactNode;
}) {
  const [events, setEvents] = useState(initialEvents);
  const [live, setLive] = useState(false);
  const [scrubRatio, setScrubRatio] = useState(1);
  const [status, setStatus] = useState("idle");
  const [error, setError] = useState<string | null>(streamError);
  const [pending, startTransition] = useTransition();
  const [sinceIso] = useState(() => new Date(Date.now() - 60_000).toISOString());

  const streamItems = useMemo(
    () => mapDigitalThreadEventsToStream(events, 8),
    [events],
  );

  const value = useMemo<LiveContextValue>(
    () => ({
      live,
      setLive,
      status,
      streamItems,
      streamError: error,
      scrubRatio,
      setScrubRatio: (ratio: number) => {
        setScrubRatio(ratio);
        const to = new Date();
        const from = new Date(
          to.getTime() - 24 * 60 * 60 * 1000 * Math.max(0.05, ratio),
        );
        startTransition(() => {
          void getDigitalThreadEvents({
            from: from.toISOString(),
            to: to.toISOString(),
            limit: 50,
          }).then((result) => {
            if (result.error) {
              setError(result.error);
              return;
            }
            setError(null);
            setEvents(result.data ?? []);
          });
        });
      },
      pending,
    }),
    [live, status, streamItems, error, scrubRatio, pending],
  );

  return (
    <MissionControlLiveContext.Provider value={value}>
      <DigitalThreadLiveClient
        enabled={live}
        auth={auth}
        since={sinceIso}
        onEvent={(event) => {
          setEvents((current) => {
            if (current.some((item) => item.eventId === event.eventId)) {
              return current;
            }
            return [event, ...current].slice(0, 100);
          });
        }}
        onStatus={(next, detail) =>
          setStatus(detail ? `${next}: ${detail}` : next)
        }
      />
      {children}
    </MissionControlLiveContext.Provider>
  );
}

export function MissionControlLiveButton() {
  const { live, setLive, status } = useMissionControlLive();
  return (
    <div className="flex flex-wrap items-center gap-2">
      <button
        type="button"
        onClick={() => setLive(!live)}
        title={live ? "Pause live stream" : "Start live SSE stream"}
        className={`inline-flex items-center gap-2 rounded-full border px-4 py-1.5 text-xs font-bold uppercase tracking-wide ${
          live
            ? "border-emerald-400/40 text-emerald-300"
            : "border-etos-ops-border text-etos-ops-ink-muted"
        }`}
      >
        <span
          aria-hidden
          className={`h-2 w-2 rounded-full ${live ? "bg-emerald-400" : "bg-slate-500"}`}
        />
        Live
      </button>
      <span className="text-[10px] uppercase tracking-wide text-etos-ops-ink-muted">
        {status}
      </span>
    </div>
  );
}

export function MissionControlLiveStreamPanel() {
  const { streamItems, streamError } = useMissionControlLive();
  return (
    <section className="flex flex-col rounded-etos-card border border-etos-ops-border bg-etos-ops-panel p-4">
      <div className="mb-3 flex items-center justify-between gap-2">
        <h2 className="text-xs font-bold uppercase tracking-[0.15em] text-etos-ops-ink-muted">
          Live event stream
        </h2>
      </div>
      {streamError ? (
        <p className="text-xs text-etos-warning-fg">{streamError}</p>
      ) : streamItems.length > 0 ? (
        <ul className="grid gap-3">
          {streamItems.map((event) => (
            <li key={event.id} className="flex items-start gap-2">
              <span className="flex h-8 w-8 shrink-0 items-center justify-center rounded-lg border border-etos-ops-border bg-etos-ops-panel-elevated text-[9px] font-black">
                {event.system}
              </span>
              <div className="min-w-0">
                <p className="text-[10px] font-mono text-etos-ops-ink-muted">
                  {event.time}
                </p>
                <p className="text-xs leading-5">{event.summary}</p>
              </div>
              <span
                aria-hidden
                className={`ml-auto mt-1 h-2 w-2 shrink-0 rounded-full ${severityDotClasses[event.severity]}`}
              />
            </li>
          ))}
        </ul>
      ) : (
        <p className="text-xs text-etos-ops-ink-muted">
          No recent digital-thread events.
        </p>
      )}
    </section>
  );
}

export function MissionControlMasterScrubber() {
  const { live, setLive, scrubRatio, setScrubRatio, pending } =
    useMissionControlLive();
  return (
    <div className="rounded-etos-card border border-etos-ops-border bg-etos-ops-panel p-4">
      <div className="mb-3 flex items-center justify-between gap-2">
        <h2 className="text-xs font-bold uppercase tracking-[0.15em] text-etos-ops-ink-muted">
          Master timeline
        </h2>
        {pending ? (
          <span className="text-[10px] uppercase tracking-wide text-etos-ops-ink-muted">
            Refreshing
          </span>
        ) : null}
      </div>
      <DigitalThreadScrubber
        windowHours={24}
        scrubRatio={scrubRatio}
        live={live}
        onLiveChange={setLive}
        onScrubRatioChange={setScrubRatio}
      />
    </div>
  );
}
