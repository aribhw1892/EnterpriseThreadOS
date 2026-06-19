# EnterpriseThreadOS UI Mockup Pack
Generated enterprise UI/UX mockups for the full flow through Issue 18.5, Issues 22-25, and the interactive Digital Thread Timeline view addendum.
## Screens
- 01. **Enterprise command center** — `/` — `images/01-command-center.png`
- 02. **Model package & reference seed** — `/model-artifacts` — `images/02-reference-model-package.png`
- 03. **Ontology & semantic layer detail** — `/model-artifacts/ontology` — `images/03-ontology-semantic-layer.png`
- 04. **Capability definitions** — `/capabilities` — `images/04-capability-definitions.png`
- 05. **Business policy definitions** — `/business-policies` — `images/05-business-policy-definitions.png`
- 06. **Optimization model definitions** — `/optimization-models` — `images/06-optimization-models.png`
- 07. **Agent template library** — `/agent-templates` — `images/07-agent-templates.png`
- 08. **Import hub** — `/imports` — `images/08-import-hub.png`
- 09. **Import wizard — upload** — `/imports/new` — `images/09-import-wizard-upload.png`
- 10. **Mapping review & AI suggestions** — `/imports/demo-cad-pdm/mapping` — `images/10-mapping-review.png`
- 11. **Staging graph validation** — `/imports/demo-cad-pdm/staging` — `images/11-staging-graph-validation.png`
- 12. **Identity resolution review** — `/imports/demo-erp/identity` — `images/12-identity-resolution-review.png`
- 13. **Data quality issue triage** — `/imports/data-quality` — `images/13-data-quality-triage.png`
- 14. **Trusted graph promotion & snapshot diff** — `/graph/promote` — `images/14-trusted-graph-promotion.png`
- 15. **Document memory explorer** — `/documents` — `images/15-document-memory-explorer.png`
- 16. **Graph explorer & 360° context** — `/explorers/360/P-1001` — `images/16-graph-explorer-360-view.png`
- 17. **Governed chat over digital thread** — `/chat` — `images/17-governed-chat.png`
- 18. **AI Trace detail** — `/ai-traces/91` — `images/18-ai-trace-detail.png`
- 19. **Dashboard builder preview** — `/dashboards/draft-31` — `images/19-dashboard-builder-preview.png`
- 20. **Report builder preview** — `/reports/draft-08` — `images/20-report-builder-preview.png`
- 21. **Recommendation inbox** — `/recommendations` — `images/21-recommendation-inbox.png`
- 22. **Recommendation detail & evidence** — `/recommendations/REC-221` — `images/22-recommendation-detail.png`
- 23. **Artifact explorer** — `/explorers/artifacts` — `images/23-artifact-explorer.png`
- 24. **Tool, skill & connector registry** — `/tools` — `images/24-tool-registry.png`
- 25. **Tool definition editor** — `/tools/graph-query-tool/edit` — `images/25-tool-definition-editor.png`
- 26. **Connector detail & credential boundary** — `/connectors/mock-erp-read` — `images/26-connector-credential-boundary.png`
- 27. **Tool run & dry-run trace** — `/tool-runs/TR-204` — `images/27-tool-run-trace.png`
- 28. **Agent builder — create from prompt or template** — `/agents/new` — `images/28-agent-builder.png`
- 29. **Agent advanced configuration** — `/agents/bom-analyzer/configure` — `images/29-agent-advanced-configuration.png`
- 30. **Agent test run & preview** — `/agents/bom-analyzer/test-run` — `images/30-agent-test-run.png`
- 31. **Agent runs explorer** — `/agent-runs` — `images/31-agent-runs-explorer.png`
- 32. **Workflow builder canvas** — `/workflows/new` — `images/32-workflow-builder-canvas.png`
- 33. **Workflow publish risk review** — `/workflows/bom-impact/publish` — `images/33-workflow-publish-review.png`
- 34. **Workflow run & safe mode trace** — `/workflow-runs/WFR-40` — `images/34-workflow-run-safe-mode.png`
- 35. **Agent team builder** — `/agent-teams/new` — `images/35-agent-team-builder.png`
- 36. **Agent team run — delegation & consensus** — `/agent-team-runs/ATR-5` — `images/36-agent-team-run-consensus.png`
- 37. **Governance & audit dashboard** — `/governance` — `images/37-governance-audit-dashboard.png`

- 38. **Digital thread timeline — macro string view** — `/digital-thread/timeline?zoom=15` — `images/38-digital-thread-macro-string-view.png`
- 39. **Digital thread timeline — live system branch view** — `/digital-thread/timeline?zoom=100` — `images/39-digital-thread-live-system-branch-view.png`
- 40. **Digital thread timeline — artifact lineage zoom** — `/digital-thread/timeline/AX-440/P-1842?zoom=450` — `images/40-digital-thread-artifact-lineage-zoom.png`

## UX principles applied
- Enterprise shell with persistent navigation, breadcrumb, search, tenant context, and read-only MVP boundary.
- Wizard/stepper for imports; tables with filters-ready structure; detail side panels for evidence/governance.
- All AI/agent/workflow screens expose trace, confidence, schema, policy, and audit state.
- Source-system writes are visibly disabled; platform-owned overlays are actionable.
- Issues 22-25 screens emphasize schema compatibility, dry-run, safe mode, run traceability, and explicit delegation/consensus.

## Digital Thread Timeline addendum

The pack now includes an implementation-ready interactive timeline concept. The screen family uses semantic zoom:

- **Macro string view:** the whole enterprise appears as one luminous string, with high-level branches for connected systems.
- **System branch view:** zooming in reveals system endpoints, live connection pulses, recent connection events, and branch KPIs.
- **Artifact lineage zoom:** deeper zoom reveals exact artifact relationships, selected event details, confidence, data quality, policy status, and evidence links.

The supporting data/API contract is documented in `DIGITAL_THREAD_TIMELINE_SPEC.md` and `docs/DIGITAL_THREAD_TIMELINE_SPEC.md`.
