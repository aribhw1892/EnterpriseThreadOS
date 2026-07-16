/**
 * Preview fixtures for `/digital-thread/timeline` when
 * `DigitalThread:UseLiveProjection` is false in appsettings.
 *
 * Layout metaphor (Option A): NOW orb on the right; enterprise systems
 * and history converge into the live string. Governance/security are
 * overlay signals on the thread — not peer ERP nodes.
 */

import type {
  DigitalThreadBranch,
  DigitalThreadEvent,
  DigitalThreadEventDetail,
  DigitalThreadMinimap,
  DigitalThreadSummary,
  DigitalThreadSystem,
} from "@/lib/etos-api";

const now = Date.now();
const hoursAgo = (h: number) => new Date(now - h * 60 * 60 * 1000).toISOString();

export const digitalThreadPreviewSystems: DigitalThreadSystem[] = [
  {
    systemId: "solidworks-pdm",
    displayName: "SolidWorks PDM",
    systemType: "PDM",
    connectionStatus: "Healthy",
    lastEventAtUtc: hoursAgo(0.2),
    eventCount24h: 42,
    syncStatus: "OK",
  },
  {
    systemId: "teamcenter",
    displayName: "Teamcenter",
    systemType: "PLM",
    connectionStatus: "Healthy",
    lastEventAtUtc: hoursAgo(0.4),
    eventCount24h: 31,
    syncStatus: "OK",
  },
  {
    systemId: "odoo-erp",
    displayName: "ODOO-ERP",
    systemType: "ERP",
    connectionStatus: "Healthy",
    lastEventAtUtc: hoursAgo(0.5),
    eventCount24h: 58,
    syncStatus: "OK",
  },
  {
    systemId: "sap-s4hana",
    displayName: "SAP S/4HANA",
    systemType: "ERP",
    connectionStatus: "Healthy",
    lastEventAtUtc: hoursAgo(1.1),
    eventCount24h: 27,
    syncStatus: "OK",
  },
  {
    systemId: "mes",
    displayName: "MES",
    systemType: "MES",
    connectionStatus: "Healthy",
    lastEventAtUtc: hoursAgo(0.8),
    eventCount24h: 64,
    syncStatus: "OK",
  },
  {
    systemId: "qms",
    displayName: "QMS",
    systemType: "QMS",
    connectionStatus: "Warning",
    lastEventAtUtc: hoursAgo(1.5),
    eventCount24h: 12,
    syncStatus: "Warning",
  },
  {
    systemId: "sharepoint",
    displayName: "SharePoint",
    systemType: "Docs",
    connectionStatus: "Healthy",
    lastEventAtUtc: hoursAgo(2),
    eventCount24h: 19,
    syncStatus: "OK",
  },
  {
    systemId: "iot-scada",
    displayName: "IoT / SCADA",
    systemType: "IoT",
    connectionStatus: "Warning",
    lastEventAtUtc: hoursAgo(0.3),
    eventCount24h: 88,
    syncStatus: "Warning",
  },
];

export const digitalThreadPreviewEvents: DigitalThreadEvent[] = [
  {
    eventId: "preview:pdm-1",
    timestampUtc: hoursAgo(18),
    sourceSystemId: "solidworks-pdm",
    sourceSystemName: "SolidWorks PDM",
    eventType: "Create",
    title: "Part P-1842 created",
    description: "New part checked into PDM vault.",
    artifactId: null,
    trustState: "Trusted",
    syncStatus: "OK",
    severity: "low",
    traceId: null,
    recommendationId: null,
  },
  {
    eventId: "preview:tc-1",
    timestampUtc: hoursAgo(14),
    sourceSystemId: "teamcenter",
    sourceSystemName: "Teamcenter",
    eventType: "Release",
    title: "Item Revision A released",
    description: "PLM release gate passed for AX-440.",
    artifactId: null,
    trustState: "Trusted",
    syncStatus: "OK",
    severity: "low",
    traceId: null,
    recommendationId: null,
  },
  {
    eventId: "preview:odoo-1",
    timestampUtc: hoursAgo(10),
    sourceSystemId: "odoo-erp",
    sourceSystemName: "ODOO-ERP",
    eventType: "Sync",
    title: "BOM sync to ERP",
    description: "Engineering BOM mirrored into Odoo.",
    artifactId: null,
    trustState: "Trusted",
    syncStatus: "OK",
    severity: "info",
    traceId: null,
    recommendationId: null,
  },
  {
    eventId: "preview:sap-1",
    timestampUtc: hoursAgo(8),
    sourceSystemId: "sap-s4hana",
    sourceSystemName: "SAP S/4HANA",
    eventType: "Update",
    title: "Material master updated",
    description: "Material MM-4402 attributes refreshed.",
    artifactId: null,
    trustState: "Trusted",
    syncStatus: "OK",
    severity: "low",
    traceId: null,
    recommendationId: null,
  },
  {
    eventId: "preview:mes-1",
    timestampUtc: hoursAgo(5),
    sourceSystemId: "mes",
    sourceSystemName: "MES",
    eventType: "Start",
    title: "Work order WO-784512 started",
    description: "Shop-floor execution started for AX-440.",
    artifactId: null,
    trustState: "Trusted",
    syncStatus: "OK",
    severity: "info",
    traceId: null,
    recommendationId: null,
  },
  {
    eventId: "preview:qms-1",
    timestampUtc: hoursAgo(3),
    sourceSystemId: "qms",
    sourceSystemName: "QMS",
    eventType: "Inspect",
    title: "Inspection plan triggered",
    description: "Incoming inspection queued for lot L-992.",
    artifactId: null,
    trustState: "Unverified",
    syncStatus: "Warning",
    severity: "medium",
    traceId: null,
    recommendationId: null,
  },
  {
    eventId: "preview:iot-1",
    timestampUtc: hoursAgo(1.2),
    sourceSystemId: "iot-scada",
    sourceSystemName: "IoT / SCADA",
    eventType: "Telemetry",
    title: "Temperature alert raised",
    description: "Line 3 chamber exceeded band.",
    artifactId: null,
    trustState: "Conflicted",
    syncStatus: "Warning",
    severity: "high",
    traceId: null,
    recommendationId: null,
  },
  {
    eventId: "preview:gov-1",
    timestampUtc: hoursAgo(2.5),
    sourceSystemId: "governance",
    sourceSystemName: "Governance",
    eventType: "AuditSignal",
    title: "Policy check recorded",
    description: "Restricted-context deny audited for export attempt.",
    artifactId: null,
    trustState: "Unverified",
    syncStatus: "Warning",
    severity: "medium",
    traceId: null,
    recommendationId: null,
  },
  {
    eventId: "preview:sec-1",
    timestampUtc: hoursAgo(0.6),
    sourceSystemId: "security",
    sourceSystemName: "Security",
    eventType: "SecurityEvent",
    title: "Access anomaly flagged",
    description: "Elevated permission probe detected.",
    artifactId: null,
    trustState: "Conflicted",
    syncStatus: "Error",
    severity: "high",
    traceId: null,
    recommendationId: null,
  },
  {
    eventId: "preview:share-1",
    timestampUtc: hoursAgo(0.4),
    sourceSystemId: "sharepoint",
    sourceSystemName: "SharePoint",
    eventType: "Update",
    title: "Spec document updated",
    description: "AX-440 assembly spec revision B published.",
    artifactId: null,
    trustState: "Trusted",
    syncStatus: "OK",
    severity: "low",
    traceId: null,
    recommendationId: null,
  },
  {
    eventId: "preview:mes-2",
    timestampUtc: hoursAgo(0.15),
    sourceSystemId: "mes",
    sourceSystemName: "MES",
    eventType: "Update",
    title: "Production order update",
    description: "PO-742339 quantity confirmed.",
    artifactId: null,
    trustState: "Trusted",
    syncStatus: "OK",
    severity: "info",
    traceId: null,
    recommendationId: null,
  },
];

export const digitalThreadPreviewBranches: DigitalThreadBranch[] = [
  {
    branchId: "preview-branch-plm",
    systemIds: ["solidworks-pdm", "teamcenter"],
    timeStartUtc: hoursAgo(20),
    timeEndUtc: hoursAgo(0.2),
    eventCount: 73,
    health: "Healthy",
    trustScore: 0.94,
    projectionPoints: [],
  },
  {
    branchId: "preview-branch-erp",
    systemIds: ["odoo-erp", "sap-s4hana"],
    timeStartUtc: hoursAgo(16),
    timeEndUtc: hoursAgo(0.5),
    eventCount: 85,
    health: "Healthy",
    trustScore: 0.91,
    projectionPoints: [],
  },
  {
    branchId: "preview-branch-ops",
    systemIds: ["mes", "qms", "iot-scada", "sharepoint"],
    timeStartUtc: hoursAgo(12),
    timeEndUtc: hoursAgo(0.15),
    eventCount: 183,
    health: "Warning",
    trustScore: 0.78,
    projectionPoints: [],
  },
];

export const digitalThreadPreviewSummary: DigitalThreadSummary = {
  connectedSystemCount: digitalThreadPreviewSystems.length,
  healthySystemCount: digitalThreadPreviewSystems.filter(
    (s) => s.connectionStatus === "Healthy",
  ).length,
  warningSystemCount: digitalThreadPreviewSystems.filter(
    (s) => s.connectionStatus === "Warning",
  ).length,
  downSystemCount: 0,
  eventsLastMinute: 2.4,
  openAlertCounts: {
    dataQualityOpen: 1,
    securityHighOrCritical: 1,
    failedRuns: 0,
    total: 2,
  },
  topThreads: [
    { id: "preview-branch-ops", label: "Ops / MES lane", eventCount: 183 },
    { id: "preview-branch-erp", label: "ERP lane", eventCount: 85 },
    { id: "preview-branch-plm", label: "PLM lane", eventCount: 73 },
  ],
  heatmapBuckets: [],
  windowHours: 24,
  generatedAtUtc: new Date(now).toISOString(),
};

export const digitalThreadPreviewMinimap: DigitalThreadMinimap = {
  windowHours: 24,
  windowStartUtc: hoursAgo(24),
  windowEndUtc: new Date(now).toISOString(),
  systems: digitalThreadPreviewSystems.map((system, index) => ({
    systemId: system.systemId,
    displayName: system.displayName,
    connectionStatus: system.connectionStatus,
    x: 80 + (index % 4) * 90,
    y: 60 + Math.floor(index / 4) * 70,
  })),
  coarsePoints: [
    { x: 40, y: 120 },
    { x: 180, y: 110 },
    { x: 360, y: 125 },
    { x: 540, y: 118 },
    { x: 720, y: 122 },
    { x: 900, y: 120 },
  ],
};

export function buildPreviewEventDetail(
  eventId: string,
): DigitalThreadEventDetail | null {
  const event = digitalThreadPreviewEvents.find((item) => item.eventId === eventId);
  if (!event) return null;

  const isOverlay =
    event.sourceSystemId === "governance" || event.sourceSystemId === "security";

  return {
    ...event,
    policySafeSummary: isOverlay
      ? "Governance/security signal projected onto the live thread (not a source-system endpoint)."
      : "Preview policy summary — live detail requires UseLiveProjection.",
    dataQualitySafeSummary: null,
    evidenceLinks: [
      {
        linkType: "preview",
        label: "Preview evidence",
        href: null,
        safeSummary: "Fixture evidence for visual QA of the reverse-thread canvas.",
      },
    ],
    drillRoutes: [
      {
        routeType: "imports",
        label: "Open Import Hub",
        href: "/imports",
      },
      {
        routeType: "governance",
        label: "Open Governance",
        href: "/governance",
      },
    ],
  };
}
