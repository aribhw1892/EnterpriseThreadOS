/**
 * Static preview fixtures still used by Mission Control after Issue 16.1:
 * AI insights only (no insights API). Timeline/stream/heatmap/alerts are live.
 *
 * Live button + master scrubber use Issue 16.1b SSE (`events/stream`).
 * Do not reintroduce fixture fallbacks for wired digital-thread widgets.
 */

import type { TimelineEvent } from "@/components/mission-control/DigitalThreadTimeline";

/** @deprecated Kept for reference / visual QA only — Mission Control no longer mounts these. */
export type ThreadSystemFixture = {
  key: string;
  name: string;
  shortCode: string;
  syncState: "ok" | "warning" | "down";
  lastEvent: string;
  lastEventAt: string;
};

export const threadSystemsFixture: ThreadSystemFixture[] = [
  {
    key: "pdm",
    name: "SolidWorks PDM",
    shortCode: "SW",
    syncState: "ok",
    lastEvent: "Part P-1842 created in PDM",
    lastEventAt: "10:14 AM",
  },
  {
    key: "teamcenter",
    name: "Teamcenter",
    shortCode: "TC",
    syncState: "ok",
    lastEvent: "Item Revision A released",
    lastEventAt: "10:15 AM",
  },
  {
    key: "erp",
    name: "SAP S/4HANA",
    shortCode: "SAP",
    syncState: "ok",
    lastEvent: "Material master created in SAP",
    lastEventAt: "10:17 AM",
  },
  {
    key: "mes",
    name: "MES (Werum)",
    shortCode: "MES",
    syncState: "ok",
    lastEvent: "Work order WO-784512 started",
    lastEventAt: "10:21 AM",
  },
  {
    key: "qms",
    name: "QMS",
    shortCode: "QMS",
    syncState: "warning",
    lastEvent: "Inspection plan triggered",
    lastEventAt: "10:22 AM",
  },
  {
    key: "supplier",
    name: "Supplier Portal",
    shortCode: "SUP",
    syncState: "ok",
    lastEvent: "Shipment ASN-78432 created",
    lastEventAt: "10:23 AM",
  },
  {
    key: "iot",
    name: "IoT / SCADA",
    shortCode: "IOT",
    syncState: "down",
    lastEvent: "Temperature alert raised",
    lastEventAt: "10:24 AM",
  },
  {
    key: "sharepoint",
    name: "SharePoint",
    shortCode: "DOC",
    syncState: "ok",
    lastEvent: "Spec updated in SharePoint",
    lastEventAt: "10:24 AM",
  },
];

/** Visual timeline events for DigitalThreadTimeline (preview until Issue 16.1). */
export const digitalThreadTimelineFixture: TimelineEvent[] = [
  {
    id: "pdm",
    system: "SOLIDWORKS PDM",
    systemTime: "10:14 AM",
    syncStatus: "OK",
    eventTime: "10:14 AM",
    title: "Part P-1842",
    description: "Updated in PDM",
    color: "#ef233c",
    logo: "/logos/solidworks.svg",
    icon: "database",
    cardPosition: "bottom",
  },
  {
    id: "teamcenter",
    system: "TEAMCENTER",
    systemTime: "10:20 AM",
    syncStatus: "OK",
    eventTime: "10:15 AM",
    title: "Item Revision",
    description: "A Released",
    color: "#21d07a",
    logo: "/logos/teamcenter.svg",
    icon: "share",
    cardPosition: "bottom",
  },
  {
    id: "sap",
    system: "SAP S/4HANA",
    systemTime: "10:21 AM",
    syncStatus: "OK",
    eventTime: "10:17 AM",
    title: "Material Master",
    description: "Created in SAP",
    color: "#119df5",
    logo: "/logos/sap.svg",
    icon: "database",
    cardPosition: "bottom",
  },
  {
    id: "thread",
    system: "DIGITAL THREAD",
    systemTime: "10:19 AM",
    syncStatus: "OK",
    eventTime: "10:19 AM",
    title: "BOM Linked",
    description: "Across Systems",
    color: "#28a8ff",
    icon: "globe",
    cardPosition: "bottom",
  },
  {
    id: "mes",
    system: "MES (WERUM)",
    systemTime: "10:22 AM",
    syncStatus: "OK",
    eventTime: "10:21 AM",
    title: "Work Order",
    description: "WO-764512 Started",
    color: "#8b5cf6",
    icon: "factory",
    cardPosition: "bottom",
  },
  {
    id: "qms",
    system: "QMS",
    systemTime: "10:23 AM",
    syncStatus: "OK",
    eventTime: "10:22 AM",
    title: "Inspection Plan",
    description: "Triggered",
    color: "#55d94c",
    icon: "package",
    cardPosition: "bottom",
  },
  {
    id: "supplier",
    system: "SUPPLIER PORTAL",
    systemTime: "10:23 AM",
    syncStatus: "OK",
    eventTime: "10:23 AM",
    title: "Shipment",
    description: "ASN-774322 Created",
    color: "#ff921f",
    icon: "package",
    cardPosition: "bottom",
  },
  {
    id: "iot",
    system: "IOT / SCADA",
    systemTime: "10:24 AM",
    syncStatus: "OK",
    eventTime: "10:24 AM",
    title: "Temperature",
    description: "Alert Raised",
    color: "#1de7f2",
    icon: "radio",
    cardPosition: "bottom",
  },
  {
    id: "sharepoint",
    system: "SHAREPOINT",
    systemTime: "10:24 AM",
    syncStatus: "OK",
    eventTime: "10:24 AM",
    title: "Specification",
    description: "Updated",
    color: "#25d5d9",
    icon: "share",
    cardPosition: "bottom",
  },
];

export type LiveEventFixture = {
  id: string;
  system: string;
  time: string;
  summary: string;
  severity: "info" | "success" | "warning" | "danger";
};

export const liveEventStreamFixture: LiveEventFixture[] = [
  {
    id: "evt-1",
    system: "SAP",
    time: "10:24:31",
    summary: "Material MAT-551001 updated in SAP",
    severity: "success",
  },
  {
    id: "evt-2",
    system: "MES",
    time: "10:23:47",
    summary: "WO-784512 started in MES — Assembly AX-1001",
    severity: "info",
  },
  {
    id: "evt-3",
    system: "QMS",
    time: "10:22:18",
    summary: "NCR-2026-113 raised — severity high",
    severity: "danger",
  },
  {
    id: "evt-4",
    system: "PDM",
    time: "10:21:35",
    summary: "Drawing DRW-5512 released rev B",
    severity: "success",
  },
  {
    id: "evt-5",
    system: "IOT",
    time: "10:21:02",
    summary: "Temperature alert — machine CNC-04",
    severity: "warning",
  },
  {
    id: "evt-6",
    system: "DOC",
    time: "10:20:11",
    summary: "Spec_Sheet_RevB.pdf linked in SharePoint",
    severity: "info",
  },
];

export type ThreadAlertFixture = {
  id: string;
  level: "high" | "medium" | "info";
  label: string;
  count: number;
};

export const threadAlertsFixture: ThreadAlertFixture[] = [
  { id: "alert-high", level: "high", label: "Require immediate action", count: 3 },
  { id: "alert-medium", level: "medium", label: "Require attention", count: 7 },
  { id: "alert-info", level: "info", label: "Informational", count: 12 },
];

export const aiInsightsFixture: string[] = [
  "3 systems have sync latency above 10 minutes",
  "12 objects missing ERP mapping",
  "Data trust improved 4.2% in the last 24 hours",
  "Rework risk increased 18% in the last 24 hours",
];

export type TopThreadFixture = {
  id: string;
  name: string;
  events: number;
};

export const topActiveThreadsFixture: TopThreadFixture[] = [
  { id: "thread-1", name: "P-1842 Hydraulic Pump", events: 512 },
  { id: "thread-2", name: "Generator Program", events: 418 },
  { id: "thread-3", name: "Compressor Program", events: 376 },
  { id: "thread-4", name: "Valve Assembly VA-200", events: 284 },
  { id: "thread-5", name: "Bearing Housing BH-101", events: 193 },
];

/**
 * 8 systems x 12 buckets (2h each over 24h); values 0-4 map to intensity
 * classes in the heatmap widget.
 */
export const activityHeatmapFixture: number[][] = [
  [1, 2, 3, 4, 3, 2, 3, 4, 4, 3, 2, 1],
  [0, 1, 2, 3, 4, 3, 2, 2, 3, 4, 3, 2],
  [2, 3, 4, 4, 3, 3, 4, 3, 2, 3, 4, 3],
  [1, 1, 2, 3, 3, 4, 4, 4, 3, 2, 2, 1],
  [0, 0, 1, 2, 2, 3, 3, 2, 4, 3, 1, 1],
  [1, 2, 2, 1, 2, 3, 2, 3, 3, 2, 2, 2],
  [0, 1, 1, 2, 3, 2, 4, 3, 2, 1, 0, 1],
  [1, 0, 1, 1, 2, 2, 3, 2, 1, 2, 1, 0],
];

export const heatmapSystemLabels: string[] = [
  "SolidWorks PDM",
  "Teamcenter",
  "SAP S/4HANA",
  "MES (Werum)",
  "QMS",
  "Supplier Portal",
  "IoT / SCADA",
  "SharePoint",
];
