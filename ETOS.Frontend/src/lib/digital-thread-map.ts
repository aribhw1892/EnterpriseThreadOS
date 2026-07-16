import type {
  TimelineEvent,
  TimelineEventIcon,
  TimelineSyncStatus,
} from "@/components/mission-control/DigitalThreadTimeline";
import type {
  DigitalThreadBranch,
  DigitalThreadEvent,
  DigitalThreadHeatmapBucket,
  DigitalThreadMinimap,
  DigitalThreadSystem,
} from "@/lib/etos-api";

const SYSTEM_VISUALS: Record<
  string,
  { color: string; icon: TimelineEventIcon; logo?: string }
> = {
  "solidworks-pdm": { color: "#ef233c", icon: "database", logo: "/logos/solidworks.svg" },
  pdm: { color: "#ef233c", icon: "database", logo: "/logos/solidworks.svg" },
  teamcenter: { color: "#21d07a", icon: "share", logo: "/logos/teamcenter.svg" },
  "sap-s4hana": { color: "#119df5", icon: "database", logo: "/logos/sap.svg" },
  sap: { color: "#119df5", icon: "database", logo: "/logos/sap.svg" },
  "odoo-erp": { color: "#714b67", icon: "database" },
  odoo: { color: "#714b67", icon: "database" },
  mes: { color: "#f97316", icon: "radio" },
  qms: { color: "#eab308", icon: "package" },
  sharepoint: { color: "#0369a1", icon: "share" },
  "iot-scada": { color: "#14b8a6", icon: "radio" },
  iot: { color: "#14b8a6", icon: "radio" },
  "tool-runtime": { color: "#0ea5e9", icon: "radio" },
  "agent-runtime": { color: "#8b5cf6", icon: "globe" },
  "workflow-runtime": { color: "#6366f1", icon: "share" },
  "data-quality": { color: "#f59e0b", icon: "package" },
  recommendations: { color: "#14b8a6", icon: "globe" },
  governance: { color: "#64748b", icon: "share" },
  security: { color: "#ef4444", icon: "radio" },
};

const FALLBACK_COLORS = [
  "#28a8ff",
  "#55d94c",
  "#f97316",
  "#a855f7",
  "#06b6d4",
  "#eab308",
];

function hashSystemId(systemId: string): number {
  let hash = 0;
  for (let i = 0; i < systemId.length; i += 1) {
    hash = (hash * 31 + systemId.charCodeAt(i)) >>> 0;
  }
  return hash;
}

export function visualForSystem(systemId: string) {
  const key = systemId.toLowerCase();
  if (SYSTEM_VISUALS[key]) {
    return SYSTEM_VISUALS[key];
  }
  for (const [prefix, visual] of Object.entries(SYSTEM_VISUALS)) {
    if (key.includes(prefix) || prefix.includes(key)) {
      return visual;
    }
  }
  return {
    color: FALLBACK_COLORS[hashSystemId(systemId) % FALLBACK_COLORS.length],
    icon: "database" as TimelineEventIcon,
  };
}

function mapSyncStatus(value: string): TimelineSyncStatus {
  const normalized = value.trim().toLowerCase();
  if (normalized === "ok" || normalized === "healthy" || normalized === "synced") {
    return "OK";
  }
  if (normalized === "error" || normalized === "failed" || normalized === "down") {
    return "Error";
  }
  return "Warning";
}

function formatEventTime(timestampUtc: string): string {
  const date = new Date(timestampUtc);
  if (Number.isNaN(date.getTime())) {
    return "—";
  }
  return date.toLocaleTimeString(undefined, {
    hour: "numeric",
    minute: "2-digit",
  });
}

export function mapDigitalThreadEventToTimeline(
  event: DigitalThreadEvent,
  index: number,
): TimelineEvent {
  const visual = visualForSystem(event.sourceSystemId);
  return {
    id: event.eventId,
    system: event.sourceSystemName.toUpperCase(),
    systemTime: formatEventTime(event.timestampUtc),
    syncStatus: mapSyncStatus(event.syncStatus),
    eventTime: formatEventTime(event.timestampUtc),
    title: event.title,
    description: event.description,
    color: visual.color,
    logo: visual.logo,
    icon: visual.icon,
    cardPosition: index % 2 === 0 ? "bottom" : "top",
  };
}

export function mapDigitalThreadEventsToTimeline(
  events: DigitalThreadEvent[],
): TimelineEvent[] {
  return [...events]
    .sort(
      (left, right) =>
        new Date(left.timestampUtc).getTime() - new Date(right.timestampUtc).getTime(),
    )
    .slice(-12)
    .map((event, index) => mapDigitalThreadEventToTimeline(event, index));
}

export type LiveStreamItem = {
  id: string;
  system: string;
  time: string;
  summary: string;
  severity: "success" | "info" | "warning" | "danger";
};

function severityFromEvent(event: DigitalThreadEvent): LiveStreamItem["severity"] {
  const sync = mapSyncStatus(event.syncStatus);
  if (sync === "Error") return "danger";
  if (sync === "Warning") return "warning";
  const severity = (event.severity ?? "").toLowerCase();
  if (severity === "high" || severity === "critical") return "danger";
  if (severity === "medium") return "warning";
  if (severity === "info" || severity === "low") return "info";
  return "success";
}

export function mapDigitalThreadEventsToStream(
  events: DigitalThreadEvent[],
  limit = 8,
): LiveStreamItem[] {
  return events.slice(0, limit).map((event) => ({
    id: event.eventId,
    system: event.sourceSystemName
      .split(/\s+/)
      .map((part) => part[0] ?? "")
      .join("")
      .slice(0, 3)
      .toUpperCase(),
    time: formatEventTime(event.timestampUtc),
    summary: `${event.title} — ${event.description}`,
    severity: severityFromEvent(event),
  }));
}

export type HeatmapGrid = {
  systemLabels: string[];
  rows: number[][];
};

export function buildHeatmapGrid(
  buckets: DigitalThreadHeatmapBucket[],
  systems: DigitalThreadSystem[],
  windowHours = 24,
): HeatmapGrid {
  const bucketCount = Math.max(1, Math.ceil(windowHours / 2));
  const now = Date.now();
  const windowStart = now - windowHours * 60 * 60 * 1000;

  const systemIds =
    systems.length > 0
      ? systems
          .slice()
          .sort((a, b) => b.eventCount24h - a.eventCount24h)
          .slice(0, 8)
          .map((system) => system.systemId)
      : [...new Set(buckets.map((bucket) => bucket.systemId))].slice(0, 8);

  if (systemIds.length === 0) {
    return { systemLabels: [], rows: [] };
  }

  const labelById = new Map(
    systems.map((system) => [system.systemId, system.displayName] as const),
  );

  const counts = new Map<string, number>();
  let maxCount = 1;
  for (const bucket of buckets) {
    const start = new Date(bucket.bucketStartUtc).getTime();
    if (Number.isNaN(start) || start < windowStart) continue;
    const index = Math.min(
      bucketCount - 1,
      Math.max(0, Math.floor((start - windowStart) / (2 * 60 * 60 * 1000))),
    );
    const key = `${bucket.systemId}:${index}`;
    const next = (counts.get(key) ?? 0) + bucket.eventCount;
    counts.set(key, next);
    maxCount = Math.max(maxCount, next);
  }

  const rows = systemIds.map((systemId) =>
    Array.from({ length: bucketCount }, (_, index) => {
      const value = counts.get(`${systemId}:${index}`) ?? 0;
      if (value <= 0) return 0;
      const ratio = value / maxCount;
      if (ratio >= 0.8) return 4;
      if (ratio >= 0.6) return 3;
      if (ratio >= 0.35) return 2;
      return 1;
    }),
  );

  return {
    systemLabels: systemIds.map(
      (id) => labelById.get(id) ?? id.replace(/-/g, " "),
    ),
    rows,
  };
}

export function systemsConnectedLabel(systems: DigitalThreadSystem[]): string {
  const total = systems.length;
  if (total === 0) return "0";
  const healthy = systems.filter((system) =>
    system.connectionStatus.toLowerCase() === "healthy",
  ).length;
  return `${healthy} / ${total}`;
}

/** Synthetic projection sources — render as thread overlays, not ERP endpoints. */
const OVERLAY_SYSTEM_IDS = new Set([
  "governance",
  "security",
  "tool-runtime",
  "agent-runtime",
  "workflow-runtime",
  "recommendations",
  "data-quality",
]);

const OVERLAY_SYSTEM_TYPES = new Set([
  "governance",
  "security",
  "runtime",
]);

export function isOverlaySystem(
  systemId: string,
  systemType?: string,
): boolean {
  if (OVERLAY_SYSTEM_IDS.has(systemId.toLowerCase())) return true;
  if (systemType && OVERLAY_SYSTEM_TYPES.has(systemType.toLowerCase())) {
    return true;
  }
  return false;
}

export type CanvasPulse = {
  id: string;
  x: number;
  y: number;
  color: string;
  title: string;
  eventType: string;
  trustState: string;
  timestampUtc: string;
  kind: "enterprise" | "overlay";
};

export type CanvasBranchPath = {
  id: string;
  d: string;
  health: string;
  eventCount: number;
  systemIds: string[];
  color: string;
};

export type CanvasSystemNode = {
  id: string;
  label: string;
  x: number;
  y: number;
  color: string;
  status: string;
  logo?: string;
};

export type CanvasScene = {
  width: number;
  height: number;
  /** Glowing core string path ending at NOW (right). */
  corePath: string;
  now: { x: number; y: number };
  branches: CanvasBranchPath[];
  pulses: CanvasPulse[];
  systemNodes: CanvasSystemNode[];
};

function cubicBranchPath(
  fromX: number,
  fromY: number,
  toX: number,
  toY: number,
  bend: number,
): string {
  const c1x = fromX + (toX - fromX) * 0.28;
  const c1y = fromY + bend * 0.35;
  const c2x = fromX + (toX - fromX) * 0.55;
  const c2y = toY + bend * 0.55;
  return `M ${fromX} ${fromY} C ${c1x} ${c1y}, ${c2x} ${c2y}, ${toX} ${toY}`;
}

function buildCorePath(width: number, height: number, nowX: number, nowY: number): string {
  // Stronger undulation — closer to Mission Control / mockup fiber curve.
  const startY = nowY + 6;
  return [
    `M 40 ${startY}`,
    `C ${width * 0.1} ${startY - 42}, ${width * 0.18} ${startY + 48}, ${width * 0.26} ${startY - 10}`,
    `S ${width * 0.4} ${startY + 8}, ${width * 0.48} ${startY - 2}`,
    `S ${width * 0.62} ${startY + 38}, ${width * 0.72} ${startY - 8}`,
    `S ${width * 0.86} ${startY + 16}, ${nowX} ${nowY}`,
  ].join(" ");
}

function coreYAtProgress(progress: number, height: number): number {
  const mid = height / 2;
  const wave = Math.sin(progress * Math.PI * 2.2) * 18;
  return mid + wave * (1 - progress * 0.35);
}

/**
 * Option A layout: PAST on the left, NOW orb on the right.
 * Enterprise systems sit as left/mid branch tips; strands converge into NOW.
 * Governance/security/runtime events become overlay pulses on the core string.
 */
export function buildCanvasScene(
  branches: DigitalThreadBranch[],
  events: DigitalThreadEvent[],
  systems: DigitalThreadSystem[],
): CanvasScene {
  const width = 1200;
  const height = 480;
  const nowX = width - 72;
  const nowY = height / 2;
  const corePath = buildCorePath(width, height, nowX, nowY);

  const enterpriseSystems = systems
    .filter((system) => !isOverlaySystem(system.systemId, system.systemType))
    .slice(0, 12);

  const laneCount = Math.max(1, enterpriseSystems.length);
  const systemNodes: CanvasSystemNode[] = enterpriseSystems.map((system, index) => {
    const t = laneCount === 1 ? 0.5 : index / (laneCount - 1);
    const y = 56 + t * (height - 112);
    const x = 110 + (index % 2) * 36;
    const visual = visualForSystem(system.systemId);
    return {
      id: system.systemId,
      label: system.displayName,
      x,
      y,
      color: visual.color,
      status: system.connectionStatus,
      logo: visual.logo,
    };
  });

  const nodeBySystem = new Map(systemNodes.map((node) => [node.id, node]));

  const branchPaths: CanvasBranchPath[] = [];
  if (branches.length > 0) {
    for (const [index, branch] of branches.entries()) {
      const primaryId = branch.systemIds.find((id) => nodeBySystem.has(id)) ?? branch.systemIds[0];
      const node = primaryId ? nodeBySystem.get(primaryId) : undefined;
      const points = branch.projectionPoints;
      let d = "";
      if (points.length >= 2) {
        // Re-anchor server points toward NOW (right) so live geometry matches Option A.
        const maxX = Math.max(...points.map((p) => p.x), 1);
        const remapped = points.map((point) => {
          const progress = point.x / maxX;
          return {
            x: 80 + progress * (nowX - 80),
            y: point.y,
          };
        });
        remapped.push({ x: nowX, y: nowY });
        d = remapped
          .map((point, pointIndex) =>
            `${pointIndex === 0 ? "M" : "L"} ${point.x.toFixed(1)} ${point.y.toFixed(1)}`,
          )
          .join(" ");
      } else if (node) {
        const bend = (index % 2 === 0 ? -1 : 1) * (28 + (index % 3) * 12);
        d = cubicBranchPath(node.x, node.y, nowX, nowY, bend);
      } else {
        continue;
      }

      branchPaths.push({
        id: branch.branchId,
        d,
        health: branch.health,
        eventCount: branch.eventCount,
        systemIds: branch.systemIds,
        color: visualForSystem(primaryId ?? `branch-${index}`).color,
      });
    }
  }

  // Ensure every enterprise system has a converging strand even without branch APIs.
  for (const [index, node] of systemNodes.entries()) {
    const already = branchPaths.some((branch) => branch.systemIds.includes(node.id));
    if (already) continue;
    const bend = (index % 2 === 0 ? -1 : 1) * (32 + (index % 4) * 10);
    branchPaths.push({
      id: `system-strand-${node.id}`,
      d: cubicBranchPath(node.x, node.y, nowX, nowY, bend),
      health: node.status === "Healthy" ? "Healthy" : "Warning",
      eventCount: 0,
      systemIds: [node.id],
      color: node.color,
    });
  }

  const timestamps = events
    .map((event) => new Date(event.timestampUtc).getTime())
    .filter((value) => !Number.isNaN(value));
  const minTs = timestamps.length ? Math.min(...timestamps) : Date.now() - 86_400_000;
  const maxTs = timestamps.length ? Math.max(...timestamps) : Date.now();
  const span = Math.max(1, maxTs - minTs);

  const pulses: CanvasPulse[] = events.slice(0, 48).map((event, index) => {
    const ts = new Date(event.timestampUtc).getTime();
    const progress = Number.isNaN(ts)
      ? index / Math.max(1, events.length - 1)
      : (ts - minTs) / span;
    const overlay = isOverlaySystem(event.sourceSystemId);
    const node = nodeBySystem.get(event.sourceSystemId);
    const coreY = coreYAtProgress(progress, height);
    const x = 64 + progress * (nowX - 96);
    const y = overlay || !node
      ? coreY + (overlay ? (index % 2 === 0 ? -14 : 14) : 0)
      : node.y + (coreY - node.y) * Math.min(1, 0.35 + progress * 0.65);

    return {
      id: event.eventId,
      x,
      y,
      color: visualForSystem(event.sourceSystemId).color,
      title: event.title,
      eventType: event.eventType,
      trustState: event.trustState,
      timestampUtc: event.timestampUtc,
      kind: overlay ? "overlay" : "enterprise",
    };
  });

  return { width, height, corePath, now: { x: nowX, y: nowY }, branches: branchPaths, pulses, systemNodes };
}

export function buildMinimapViewport(
  minimap: DigitalThreadMinimap | null,
  view: { x: number; y: number; scale: number; width: number; height: number },
) {
  if (!minimap) {
    return {
      points: [] as Array<{ x: number; y: number }>,
      systems: [] as DigitalThreadMinimap["systems"],
      viewport: { x: 0, y: 0, width: 40, height: 24 },
    };
  }

  const scale = Math.max(0.05, view.scale);
  return {
    points: minimap.coarsePoints,
    systems: minimap.systems,
    viewport: {
      x: Math.max(0, -view.x / scale),
      y: Math.max(0, -view.y / scale),
      width: view.width / scale,
      height: view.height / scale,
    },
  };
}

export function zoomBandLabel(zoomPercent: number): string {
  if (zoomPercent < 25) return "Macro string";
  if (zoomPercent < 200) return "System branches";
  return "Artifact lineage";
}
