# Digital Thread Timeline View — UI/UX and Data Specification

## Purpose

The Digital Thread Timeline View is an interactive, zoomable view of the enterprise digital thread. At the farthest zoom-out level, the entire enterprise appears as one luminous string. As the user zooms in, the string semantically expands into system branches, then into artifact-level lineage events.

This view is designed to answer: **when did each enterprise system become connected, what data flowed between systems, and which artifacts/events created the thread?**

## Screen states and routes

| State | Route | Zoom range | UX goal |
|---|---|---:|---|
| Macro string view | `/digital-thread/timeline?zoom=15` | 5-25% | Show the full enterprise as one string-like timeline with high-level branches and health. |
| System branch view | `/digital-thread/timeline?zoom=100` | 25-200% | Show system endpoints, live connection events, branch counts, and recent flow. |
| Artifact lineage zoom | `/digital-thread/timeline/{assemblyId}/{partId}?zoom=450` | 200-600% | Show object-level lineage, selected event details, confidence, policy status, and evidence links. |

## Real-data sources

The visualization should not be hardcoded. It should be projected from governed backend data that already exists or is planned in the platform:

- Trusted Graph nodes and relationships from graph memory.
- ImportBatch, GraphSnapshot, GraphDiff, identity resolution, and data-quality events from the ingestion pipeline.
- Document link, extraction, vector index, and evidence-reference events from document memory.
- RecommendationArtifact, ReviewTaskArtifact, DecisionArtifact, OutcomeCheckRun, and LearningSignalArtifact relationships.
- ToolRun, AgentRun, WorkflowRun, and AgentTeamRun records from Issues 22-25 once available.
- Connector registry metadata for source system labels, connector health, credential boundary, and read-only/write-disabled status.
- Audit and security events for denied context, export, policy, and permission changes.

## API contract proposal

All reads must go through governed services; the frontend should never read graph storage directly.

| Method | Endpoint | Purpose |
|---|---|---|
| `GET` | `/api/digital-thread/summary` | Connected systems, live links, event rate, trace completeness, active branches, branch health. |
| `GET` | `/api/digital-thread/systems` | System endpoint metadata, connector status, first connected time, last event time, event counts. |
| `GET` | `/api/digital-thread/events` | Time-windowed events used by the timeline and recent-event table. |
| `GET` | `/api/digital-thread/branches` | Aggregated branch geometry/projection grouped by system, relationship type, time bucket, and trust state. |
| `GET` | `/api/digital-thread/lineage/{artifactId}` | Artifact-level lineage rows and selected-path graph segment. |
| `GET` | `/api/digital-thread/events/{eventId}` | Event/connection inspector details and evidence links. |
| `GET` | `/api/digital-thread/minimap` | Lightweight full-thread projection for the minimap. |
| `STREAM` | `/api/digital-thread/events/stream` | Live event updates via SignalR or SSE. |

Recommended implementation: expose a `DigitalThreadProjectionService` that reads governed graph/query/context services and emits UI-ready projections. Use SignalR if the team wants bidirectional live controls; use SSE if MVP only needs server-to-client updates.

## Core data models

### DigitalThreadEvent

```json
{
  "eventId": "EVT-209331",
  "timestampUtc": "2026-06-15T09:42:21Z",
  "sourceSystemId": "solidworks-pdm",
  "sourceSystemName": "SolidWorks PDM",
  "targetSystemId": "neo4j-graph",
  "targetSystemName": "Neo4j Graph",
  "artifactId": "AX-440/D-8821",
  "artifactLabel": "AX-440 / Drawing D-8821",
  "relationshipType": "references",
  "eventType": "ReferenceCreated",
  "direction": "UpstreamToDownstream",
  "count": 64,
  "confidence": 0.98,
  "dataQuality": "High",
  "policyStatus": "Approved",
  "trustState": "Trusted",
  "evidenceLinkCount": 4,
  "classification": "Internal"
}
```

### SystemConnection

```json
{
  "systemId": "sap-s4hana",
  "displayName": "SAP S/4HANA",
  "systemType": "ERP",
  "connectorId": "mock-erp-read",
  "connectionStatus": "Healthy",
  "firstConnectedAtUtc": "2022-05-12T08:14:32Z",
  "lastEventAtUtc": "2026-06-15T09:43:02Z",
  "liveLinkCount": 8,
  "eventCount24h": 8913,
  "readOnly": true,
  "writesEnabled": false
}
```

### ThreadBranch

```json
{
  "branchId": "branch-sap-s4hana-mes-qms",
  "systemIds": ["sap-s4hana", "mes", "qms"],
  "timeStartUtc": "2024-01-10T00:00:00Z",
  "timeEndUtc": "2026-06-15T09:43:02Z",
  "eventCount": 18624,
  "health": "Healthy",
  "trustScore": 0.94,
  "traceCompleteness": 0.91,
  "projectionPoints": [[120, 410], [360, 388], [620, 320]]
}
```

## Interactions

- Mouse wheel / trackpad pinch zooms from macro string to system branch to artifact lineage.
- Drag pans the canvas; minimap viewport mirrors pan/zoom state.
- Fit to view resets to the full enterprise thread.
- Time scrubber moves the visible time window and can replay historical connection growth.
- Live mode animates new `DigitalThreadEvent` pulses without forcing the user to lose viewport position.
- Selecting a system endpoint filters branches and the event table.
- Selecting a pulse/event opens the right-side event inspector.
- Selecting an artifact opens 360° Context View or AI Trace using existing explorer routes.
- Governance filters hide restricted events before rendering and show aggregate counts only when allowed.

## Semantic zoom behavior

| Zoom | Visual behavior | Data density |
|---:|---|---|
| 5-25% | One string-like core thread, high-level branches, annual/monthly time markers. | Aggregated by system and large time buckets. |
| 25-200% | System branches, endpoints, live pulses, event clusters, branch health. | Aggregated by system, event type, relationship type, and smaller time buckets. |
| 200-600% | Individual objects, exact event nodes, selected path, tooltips, event inspector. | Raw governed events and lineage records. |

## UI components

- Filter bar: time range, site, product line, system, event type, trust state.
- Canvas: zoomable/pannable WebGL or Canvas renderer for performance.
- Minimap: full-thread projection with selected viewport.
- Timeline scrubber: historical replay and live/pause mode.
- Right summary panel: connected systems, live links, events/min, trace completeness, active branches, branch health, thread age.
- Event inspector: selected event details, source/target system, relationship, confidence, policy status, data quality, graph path, evidence links.
- Bottom table: recent connection events or lineage details depending on zoom state.

## Governance and safety requirements

- The screen is read-only in MVP.
- All data is tenant-scoped and permission-filtered before it reaches the UI.
- Restricted data is filtered before visualization; do not rely on UI-only redaction.
- Events with low trust/conflicted identity links should be visible as warning/degraded aggregates and excluded from trusted recommendations unless approved by policy.
- Every drill-through to evidence, AI Trace, artifact, or graph record must reuse existing permission checks.
- Future write-capable connector states may be shown as disabled placeholders only until action governance is implemented.

## Performance requirements

- Use aggregated projections at macro zoom to avoid rendering every graph event.
- Virtualize tables and event lists.
- Use request cancellation/debouncing while users scrub or zoom.
- Stream incremental deltas, not full graph reloads, during live mode.
- Cache minimap and aggregate branch projections by tenant, graph snapshot, filter hash, and time bucket.

## Mockup assets

| Screen | Image |
|---|---|
| Macro string view | `images/38-digital-thread-macro-string-view.png` |
| Live system branch view | `images/39-digital-thread-live-system-branch-view.png` |
| Artifact lineage zoom | `images/40-digital-thread-artifact-lineage-zoom.png` |
