"use client";

import Image from "next/image";
import { useId, useMemo, useState, type CSSProperties, type ReactNode } from "react";
import {
  Activity,
  Database,
  Factory,
  Filter,
  Globe2,
  PackageCheck,
  Radio,
  Share2,
} from "lucide-react";

export type TimelineSyncStatus = "OK" | "Warning" | "Error";

export type TimelineEventIcon =
  | "database"
  | "share"
  | "globe"
  | "factory"
  | "package"
  | "radio";

export type TimelineEvent = {
  id: string;
  system: string;
  systemTime: string;
  syncStatus: TimelineSyncStatus;
  eventTime: string;
  title: string;
  description?: string;
  color: string;
  logo?: string;
  icon?: TimelineEventIcon;
  cardPosition: "top" | "bottom";
};

/** Future-facing enterprise event shape (Issue 16.1). Visual timeline maps from this. */
export type EnterpriseThreadEvent = {
  id: string;
  timestamp: string;
  sourceSystem: string;
  sourceRecordId: string;
  artifactType: string;
  artifactId?: string;
  eventType: string;
  title: string;
  description: string;
  trustScore?: number;
  confidence?: number;
  syncStatus: "synced" | "pending" | "warning" | "failed";
  severity?: "info" | "low" | "medium" | "high" | "critical";
  traceId?: string;
  relatedObjectIds?: string[];
};

export type DigitalThreadTimelineProps = {
  events: TimelineEvent[];
  activeEventId?: string;
  live?: boolean;
  onEventSelect?: (event: TimelineEvent) => void;
  onSystemFilterChange?: (systemId: string) => void;
  className?: string;
};

const iconMap: Record<TimelineEventIcon, ReactNode> = {
  database: <Database size={18} />,
  share: <Share2 size={18} />,
  globe: <Globe2 size={20} />,
  factory: <Factory size={18} />,
  package: <PackageCheck size={18} />,
  radio: <Radio size={18} />,
};

function syncStatusClass(status: TimelineSyncStatus): string {
  if (status === "OK") return "system-status-ok";
  if (status === "Warning") return "system-status-warn";
  return "system-status-err";
}

/** Matches `.timeline-line` placement in globals.css + SVG viewBox height. */
const THREAD_LINE_TOP_PX = 108;
const THREAD_LINE_HEIGHT_PX = 162;
const THREAD_VIEWBOX_HEIGHT = 210;
const THREAD_VIEWBOX_WIDTH = 1600;
const THREAD_MARKER_SIZE_PX = 24;
const THREAD_CARD_TOP_PX = 240;

/** Absolute cubic segments for the glowing thread path (viewBox coords). */
const THREAD_PATH_SEGMENTS: Array<[
  [number, number],
  [number, number],
  [number, number],
  [number, number],
]> = [
  [
    [0, 112],
    [100, 86],
    [170, 129],
    [265, 105],
  ],
  [
    [265, 105],
    [360, 81],
    [445, 103],
    [535, 103],
  ],
  [
    [535, 103],
    [625, 103],
    [720, 134],
    [815, 102],
  ],
  [
    [815, 102],
    [910, 70],
    [1000, 96],
    [1095, 107],
  ],
  [
    [1095, 107],
    [1190, 118],
    [1270, 93],
    [1360, 104],
  ],
  [
    [1360, 104],
    [1450, 115],
    [1510, 92],
    [1600, 102],
  ],
];

function cubic1d(p0: number, p1: number, p2: number, p3: number, t: number): number {
  const u = 1 - t;
  return u * u * u * p0 + 3 * u * u * t * p1 + 3 * u * t * t * p2 + t * t * t * p3;
}

function threadPathYAtX(targetX: number): number {
  const x = Math.min(THREAD_VIEWBOX_WIDTH, Math.max(0, targetX));

  for (const [p0, p1, p2, p3] of THREAD_PATH_SEGMENTS) {
    if (x < p0[0] - 0.5 || x > p3[0] + 0.5) continue;

    let lo = 0;
    let hi = 1;
    let t = 0.5;
    for (let i = 0; i < 24; i++) {
      t = (lo + hi) / 2;
      const sampleX = cubic1d(p0[0], p1[0], p2[0], p3[0], t);
      if (sampleX < x) lo = t;
      else hi = t;
    }

    return cubic1d(p0[1], p1[1], p2[1], p3[1], t);
  }

  return 105;
}

/** Marker `top` so the core sits on the glowing curve for column `index` of `count`. */
function markerTopForColumn(index: number, count: number): number {
  const columns = Math.max(count, 1);
  const viewX = ((index + 0.5) / columns) * THREAD_VIEWBOX_WIDTH;
  const pathY = threadPathYAtX(viewX);
  const screenY =
    THREAD_LINE_TOP_PX + (pathY / THREAD_VIEWBOX_HEIGHT) * THREAD_LINE_HEIGHT_PX;
  return Math.round(screenY - THREAD_MARKER_SIZE_PX / 2);
}

export function DigitalThreadTimeline({
  events,
  activeEventId,
  live = false,
  onEventSelect,
  onSystemFilterChange,
  className,
}: DigitalThreadTimelineProps) {
  const reactId = useId().replace(/:/g, "");
  const lineGlowId = `lineGlow-${reactId}`;
  const threadGradientId = `threadGradient-${reactId}`;

  const [systemFilter, setSystemFilter] = useState("all");
  const [selectedId, setSelectedId] = useState<string | null>(
    activeEventId ?? events.find((event) => event.id === "thread")?.id ?? events[0]?.id ?? null,
  );

  const visibleEvents = useMemo(() => {
    if (systemFilter === "all") return events;
    return events.filter((item) => item.id === systemFilter);
  }, [events, systemFilter]);

  function handleFilterChange(value: string) {
    setSystemFilter(value);
    onSystemFilterChange?.(value);
  }

  function handleSelect(event: TimelineEvent) {
    setSelectedId(event.id);
    onEventSelect?.(event);
  }

  return (
    <section
      className={["thread-shell", className].filter(Boolean).join(" ")}
      data-ui-preview="true"
    >
      <div className="thread-header">
        <div>
          <p className="thread-eyebrow">
            CONNECTED ENTERPRISE
            {live ? (
              <span className="thread-live-pill" aria-label="Live feed active">
                LIVE
              </span>
            ) : (
              <span className="thread-preview-pill">Preview — backend Issue 16.1</span>
            )}
          </p>
          <h2 className="thread-title">DIGITAL THREAD TIMELINE — LIVE VIEW</h2>
        </div>

        <div className="thread-actions">
          <select
            value={systemFilter}
            onChange={(event) => handleFilterChange(event.target.value)}
            className="thread-select"
            aria-label="Filter timeline systems"
          >
            <option value="all">View All Systems</option>
            {events.map((item) => (
              <option key={item.id} value={item.id}>
                {item.system}
              </option>
            ))}
          </select>

          <button
            className="thread-filter-button"
            type="button"
            disabled
            title="Advanced filters require Issue 16.1 digital-thread APIs"
          >
            <Filter size={15} />
            Filters
          </button>
        </div>
      </div>

      <div className="thread-viewport">
        <div className="ambient-grid" />
        <div className="ambient-light ambient-light-one" />
        <div className="ambient-light ambient-light-two" />

        <DigitalGlobe />

        <svg
          className="timeline-line"
          viewBox="0 0 1600 210"
          preserveAspectRatio="none"
          aria-hidden="true"
        >
          <defs>
            <filter id={lineGlowId} x="-30%" y="-100%" width="160%" height="300%">
              <feGaussianBlur stdDeviation="6" result="blur" />
              <feMerge>
                <feMergeNode in="blur" />
                <feMergeNode in="SourceGraphic" />
              </feMerge>
            </filter>

            <linearGradient id={threadGradientId} x1="0%" x2="100%">
              <stop offset="0%" stopColor="#087eff" />
              <stop offset="28%" stopColor="#1da4ff" />
              <stop offset="53%" stopColor="#3d8fff" />
              <stop offset="76%" stopColor="#00d9ff" />
              <stop offset="100%" stopColor="#1685ff" />
            </linearGradient>
          </defs>

          <path
            className="timeline-line-soft"
            d="
              M 0 112
              C 100 86, 170 129, 265 105
              S 445 103, 535 103
              S 720 134, 815 102
              S 1000 96, 1095 107
              S 1270 93, 1360 104
              S 1510 92, 1600 102
            "
            fill="none"
            strokeWidth="14"
            filter={`url(#${lineGlowId})`}
          />

          <path
            d="
              M 0 112
              C 100 86, 170 129, 265 105
              S 445 103, 535 103
              S 720 134, 815 102
              S 1000 96, 1095 107
              S 1270 93, 1360 104
              S 1510 92, 1600 102
            "
            fill="none"
            stroke={`url(#${threadGradientId})`}
            strokeWidth="3.5"
            filter={`url(#${lineGlowId})`}
          />

          <path
            className="timeline-line-dash"
            d="
              M 0 112
              C 100 86, 170 129, 265 105
              S 445 103, 535 103
              S 720 134, 815 102
              S 1000 96, 1095 107
              S 1270 93, 1360 104
              S 1510 92, 1600 102
            "
            fill="none"
            strokeWidth="0.8"
            strokeDasharray="4 18"
            opacity="0.85"
          />
        </svg>

        <div
          className="timeline-content"
          style={{
            gridTemplateColumns: `repeat(${Math.max(visibleEvents.length, 1)}, minmax(140px, 1fr))`,
          }}
        >
          {visibleEvents.map((event, index) => {
            const selected = selectedId === event.id;
            const markerTop = markerTopForColumn(index, visibleEvents.length);
            const connectorTop = markerTop + THREAD_MARKER_SIZE_PX - 4;
            const connectorHeight = Math.max(
              THREAD_CARD_TOP_PX - connectorTop,
              24,
            );

            return (
              <article
                key={event.id}
                className={`timeline-column ${
                  event.id === "thread" ? "timeline-column-center" : ""
                }`}
              >
                <SystemHeading event={event} />

                <button
                  type="button"
                  className={`timeline-marker ${selected ? "is-selected" : ""}`}
                  style={
                    {
                      "--event-color": event.color,
                      top: markerTop,
                    } as CSSProperties
                  }
                  onClick={() => handleSelect(event)}
                  aria-label={`Open ${event.system} timeline event`}
                  aria-pressed={selected}
                >
                  <span className="marker-pulse" />
                  <span className="marker-core" />
                </button>

                <span
                  className="timeline-connector"
                  style={
                    {
                      "--event-color": event.color,
                      top: connectorTop,
                      height: connectorHeight,
                    } as CSSProperties
                  }
                >
                  <span className="connector-dot" />
                </span>

                <EventCard
                  event={event}
                  selected={selected}
                  onSelect={() => handleSelect(event)}
                />
              </article>
            );
          })}
        </div>
      </div>
    </section>
  );
}

function SystemHeading({ event }: { event: TimelineEvent }) {
  const icon = event.icon ? iconMap[event.icon] : null;

  return (
    <div className="system-heading">
      <div
        className="system-logo"
        style={{
          borderColor: `${event.color}55`,
          boxShadow: `0 0 20px ${event.color}22`,
        }}
      >
        {event.logo ? (
          <Image
            src={event.logo}
            alt=""
            width={28}
            height={28}
            className="system-logo-image"
            unoptimized
          />
        ) : (
          <span style={{ color: event.color }}>{icon}</span>
        )}
      </div>

      <p className="system-name">{event.system}</p>
      <p className="system-time">{event.systemTime}</p>

      <div className="system-status">
        <Activity size={10} />
        Sync{" "}
        <span className={syncStatusClass(event.syncStatus)}>
          {event.syncStatus}
        </span>
      </div>
    </div>
  );
}

function EventCard({
  event,
  selected,
  onSelect,
}: {
  event: TimelineEvent;
  selected: boolean;
  onSelect: () => void;
}) {
  return (
    <button
      type="button"
      className={`event-card ${selected ? "is-selected" : ""}`}
      style={{ "--event-color": event.color } as CSSProperties}
      onClick={onSelect}
      aria-pressed={selected}
    >
      <span className="event-card-accent" />

      <span className="event-time">{event.eventTime}</span>
      <span className="event-title">{event.title}</span>

      {event.description ? (
        <span className="event-description">{event.description}</span>
      ) : null}
    </button>
  );
}

function DigitalGlobe() {
  return (
    <div className="digital-globe" aria-hidden="true">
      <div className="globe-halo globe-halo-one" />
      <div className="globe-halo globe-halo-two" />

      <div className="globe-sphere">
        <div className="globe-grid globe-grid-horizontal" />
        <div className="globe-grid globe-grid-vertical" />
        <div className="globe-continent" />
        <div className="globe-shine" />

        {Array.from({ length: 12 }).map((_, index) => (
          <span key={index} className={`globe-node globe-node-${index + 1}`} />
        ))}
      </div>

      <div className="globe-orbit globe-orbit-one" />
      <div className="globe-orbit globe-orbit-two" />
      <div className="globe-orbit globe-orbit-three" />
    </div>
  );
}
