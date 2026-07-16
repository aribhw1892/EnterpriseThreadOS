# Graph Report - .  (2026-07-16)

## Corpus Check
- cluster-only mode — file stats not available

## Summary
- 1329 nodes · 3890 edges · 63 communities (58 shown, 5 thin omitted)
- Extraction: 99% EXTRACTED · 1% INFERRED · 0% AMBIGUOUS · INFERRED: 27 edges (avg confidence: 0.8)
- Token cost: 0 input · 0 output

## Graph Freshness
- Built from commit: `e70ad111`
- Run `git rev-parse HEAD` and compare to check if the graph is stale.
- Run `graphify update .` after code changes (no API cost).

## Community Hubs (Navigation)
- [[_COMMUNITY_Community 0|Community 0]]
- [[_COMMUNITY_Community 1|Community 1]]
- [[_COMMUNITY_Community 2|Community 2]]
- [[_COMMUNITY_Community 3|Community 3]]
- [[_COMMUNITY_Community 4|Community 4]]
- [[_COMMUNITY_Community 5|Community 5]]
- [[_COMMUNITY_Community 6|Community 6]]
- [[_COMMUNITY_Community 7|Community 7]]
- [[_COMMUNITY_Community 8|Community 8]]
- [[_COMMUNITY_Community 9|Community 9]]
- [[_COMMUNITY_Community 10|Community 10]]
- [[_COMMUNITY_Community 11|Community 11]]
- [[_COMMUNITY_Community 12|Community 12]]
- [[_COMMUNITY_Community 13|Community 13]]
- [[_COMMUNITY_Community 14|Community 14]]
- [[_COMMUNITY_Community 15|Community 15]]
- [[_COMMUNITY_Community 16|Community 16]]
- [[_COMMUNITY_Community 17|Community 17]]
- [[_COMMUNITY_Community 18|Community 18]]
- [[_COMMUNITY_Community 19|Community 19]]
- [[_COMMUNITY_Community 20|Community 20]]
- [[_COMMUNITY_Community 21|Community 21]]
- [[_COMMUNITY_Community 22|Community 22]]
- [[_COMMUNITY_Community 23|Community 23]]
- [[_COMMUNITY_Community 24|Community 24]]
- [[_COMMUNITY_Community 25|Community 25]]
- [[_COMMUNITY_Community 26|Community 26]]
- [[_COMMUNITY_Community 27|Community 27]]
- [[_COMMUNITY_Community 28|Community 28]]
- [[_COMMUNITY_Community 29|Community 29]]
- [[_COMMUNITY_Community 30|Community 30]]
- [[_COMMUNITY_Community 31|Community 31]]
- [[_COMMUNITY_Community 32|Community 32]]
- [[_COMMUNITY_Community 33|Community 33]]
- [[_COMMUNITY_Community 34|Community 34]]
- [[_COMMUNITY_Community 35|Community 35]]
- [[_COMMUNITY_Community 36|Community 36]]
- [[_COMMUNITY_Community 37|Community 37]]
- [[_COMMUNITY_Community 38|Community 38]]
- [[_COMMUNITY_Community 39|Community 39]]
- [[_COMMUNITY_Community 40|Community 40]]
- [[_COMMUNITY_Community 41|Community 41]]
- [[_COMMUNITY_Community 42|Community 42]]
- [[_COMMUNITY_Community 43|Community 43]]
- [[_COMMUNITY_Community 44|Community 44]]
- [[_COMMUNITY_Community 45|Community 45]]
- [[_COMMUNITY_Community 46|Community 46]]
- [[_COMMUNITY_Community 47|Community 47]]
- [[_COMMUNITY_Community 48|Community 48]]
- [[_COMMUNITY_Community 49|Community 49]]
- [[_COMMUNITY_Community 50|Community 50]]
- [[_COMMUNITY_Community 51|Community 51]]
- [[_COMMUNITY_Community 52|Community 52]]
- [[_COMMUNITY_Community 53|Community 53]]
- [[_COMMUNITY_Community 54|Community 54]]
- [[_COMMUNITY_Community 55|Community 55]]
- [[_COMMUNITY_Community 56|Community 56]]
- [[_COMMUNITY_Community 57|Community 57]]
- [[_COMMUNITY_Community 60|Community 60]]
- [[_COMMUNITY_Community 62|Community 62]]

## God Nodes (most connected - your core abstractions)
1. `resolveTenantHeaders()` - 174 edges
2. `missingContext()` - 171 edges
3. `Button()` - 51 edges
4. `Card()` - 49 edges
5. `CardHeader()` - 49 edges
6. `CardTitle()` - 49 edges
7. `CardContent()` - 49 edges
8. `ErrorState()` - 47 edges
9. `PageHeader()` - 39 edges
10. `getArtifactVersions()` - 38 edges

## Surprising Connections (you probably didn't know these)
- `AgentTemplateDefinitionDetailPage()` --calls--> `loadAgentTemplateDefinitionDetail()`  [INFERRED]
  src/app/(shell)/agent-templates/[artifactId]/page.tsx → src/components/agent-templates/AgentTemplateDefinitionDetailView.tsx
- `NewAgentPage()` --calls--> `getAgentTypeDefinitionArtifacts()`  [INFERRED]
  src/app/(shell)/agents/new/page.tsx → src/lib/etos-api.ts
- `BusinessPolicyDefinitionDetailPage()` --calls--> `loadBusinessPolicyDefinitionDetail()`  [INFERRED]
  src/app/(shell)/business-policies/[artifactId]/page.tsx → src/components/business-policies/BusinessPolicyDefinitionDetailView.tsx
- `CapabilityDefinitionDetailPage()` --calls--> `loadCapabilityDefinitionDetail()`  [INFERRED]
  src/app/(shell)/capabilities/[artifactId]/page.tsx → src/components/capabilities/CapabilityDefinitionDetailView.tsx
- `DashboardDetailPage()` --calls--> `loadDashboardReportDetail()`  [INFERRED]
  src/app/(shell)/dashboards/[artifactId]/page.tsx → src/components/dashboards/DashboardReportDetailView.tsx

## Import Cycles
- None detected.

## Communities (63 total, 5 thin omitted)

### Community 0 - "Community 0"
Cohesion: 0.01
Nodes (152): AgentArtifactVersionReference, AgentDerivedCapabilityRisk, AgentExecutionRequest, AgentExecutionResponse, AgentRunDetail, AgentRunSummary, AgentSkillReference, AgentTemplateArtifactVersionReference (+144 more)

### Community 1 - "Community 1"
Cohesion: 0.09
Nodes (29): ANCHOR_KINDS, Explorer360AliasPage(), PageProps, ArtifactDetailPage(), DocumentDetailPage(), loadGraphNeighborhood(), ContextView360(), ExplorerErrorState() (+21 more)

### Community 2 - "Community 2"
Cohesion: 0.08
Nodes (53): ImportDataQualityPage(), approveDraftMapping(), approveIdentityCandidate(), captureTrustedSnapshot(), createBomRecommendation(), createComparisonImport(), createDemoImport(), createManualDataQualityIssue() (+45 more)

### Community 3 - "Community 3"
Cohesion: 0.07
Nodes (66): LearningSignalDetailPage(), ArtifactsExplorerPage(), CapabilitiesPage(), LearningSignalsPage(), addDecisionComment(), addReviewTaskComment(), assignReviewTask(), castDecisionVote() (+58 more)

### Community 4 - "Community 4"
Cohesion: 0.10
Nodes (26): DecisionDetailPage(), DecisionDetailPageProps, TaskDetailPage(), TaskDetailPageProps, DecisionDetailPanel(), DecisionDetailPanelProps, DecisionDetail, DecisionVoteKind (+18 more)

### Community 5 - "Community 5"
Cohesion: 0.07
Nodes (10): cleanDemoDatasetAction(), Artifact, ArtifactDependency, ArtifactRelationship, ArtifactVersion, ClassificationScheme, cleanDevelopmentDemoData(), PolicyImpact (+2 more)

### Community 6 - "Community 6"
Cohesion: 0.06
Nodes (23): BatchCard(), BatchDetailPanels(), ButtonGroup(), DataQualityPanel(), formatStatus(), IdentityCandidateCard(), IdentityResolutionPanel(), IMPORT_STEPS (+15 more)

### Community 7 - "Community 7"
Cohesion: 0.05
Nodes (107): AgentModelConfigPanel(), AgentModelConfigPanelProps, AgentRow, PageProps, AiTracesPage(), PageProps, renderApiError(), buildCapabilityRows() (+99 more)

### Community 8 - "Community 8"
Cohesion: 0.15
Nodes (18): configurePath(), ensureMappingAgentSeedAction(), markAgentReadyAction(), parseFallbackModels(), publishAgentAction(), saveAgentModelConfigAction(), AgentModelConfigForm(), AgentModelConfigFormProps (+10 more)

### Community 9 - "Community 9"
Cohesion: 0.11
Nodes (23): DashboardDetailPage(), ReportDetailPage(), DashboardReportDetailProps, DashboardReportDetailView(), DashboardReportKind, exportAction(), loadDashboardReportDetail(), markReadyAction() (+15 more)

### Community 10 - "Community 10"
Cohesion: 0.06
Nodes (32): dependencies, graphology, graphology-layout-forceatlas2, lucide-react, next, next-themes, react, react-dom (+24 more)

### Community 11 - "Community 11"
Cohesion: 0.12
Nodes (16): GrantsPanel(), MembershipsPanel(), RolesPanel(), TenantsPanel(), UsersPanel(), TenantSwitcher(), TAB_ITEMS, TabId (+8 more)

### Community 12 - "Community 12"
Cohesion: 0.13
Nodes (16): approveAllPdmIdentityCandidatesAction(), approvePdmIdentityCandidateAction(), approvePdmMappingAction(), conflictPdmIdentityCandidateAction(), generatePdmIdentityCandidatesAction(), loadPdmAiPreviewAction(), loadPdmBatchStates(), promotePdmBatchesAction() (+8 more)

### Community 13 - "Community 13"
Cohesion: 0.19
Nodes (10): AdminIdentityPage(), resolveTab(), getIdentityLists(), AppShell(), initialsFromName(), ShellLayout(), Sidebar(), ThemeToggle() (+2 more)

### Community 14 - "Community 14"
Cohesion: 0.13
Nodes (16): ImportWizardActions, GuidedBatchPanel(), buildWizardCopy(), getImportSourceConfig(), getImportWizardBasePath(), ImportSourceManifestEntry, ImportSourceWizardCopy, loadPackageManifest() (+8 more)

### Community 15 - "Community 15"
Cohesion: 0.13
Nodes (15): GuidedBatchPanelProps, WizardServerAction, ImportBatchResult, ImportColumnMapping, ImportFileProfile, ImportLifecycleMapping, ImportManifest, ImportMappingsDocument (+7 more)

### Community 16 - "Community 16"
Cohesion: 0.13
Nodes (23): AgentRunsPage(), AgentsPage(), buildRows(), DashboardsPage(), buildHeatmapGrid(), mapDigitalThreadEventsToStream(), mapDigitalThreadEventsToTimeline(), systemsConnectedLabel() (+15 more)

### Community 17 - "Community 17"
Cohesion: 0.13
Nodes (21): postWorkflowDefinition(), postWorkflowExecute(), postWorkflowMarkReady(), postWorkflowPublish(), postWorkflowTestRun(), WorkflowStepDefinition, WorkflowVersionDetail, createWorkflowAction() (+13 more)

### Community 18 - "Community 18"
Cohesion: 0.32
Nodes (11): agentExecuteAction(), agentPreviewAction(), agentTestRunAction(), agentTestRunPath(), createAgentFromPromptAction(), createAgentFromTemplateAction(), redirectNewError(), redirectToConfigure() (+3 more)

### Community 19 - "Community 19"
Cohesion: 0.12
Nodes (23): PdmWizardBatchState, redirectOnError(), redirectWithWizardParams(), requireProfile(), approveAllIdentityCandidatesForBatch(), approveIdentityCandidate(), approveImportMapping(), buildImportMappingPayloadFromPreview() (+15 more)

### Community 20 - "Community 20"
Cohesion: 0.11
Nodes (11): createDemoDocument(), DocumentsPage(), requestVectorIndex(), CadParsingStatus, createDemoDocumentFlow(), DataQualityIssue, DocumentArtifact, DocumentArtifactDetail (+3 more)

### Community 21 - "Community 21"
Cohesion: 0.10
Nodes (19): compilerOptions, allowJs, esModuleInterop, incremental, isolatedModules, jsx, lib, module (+11 more)

### Community 22 - "Community 22"
Cohesion: 0.18
Nodes (14): IdentityFormClient(), IdentityFormState, createGrantAction(), createMembershipAction(), createRoleAction(), createTenantAction(), createUserAction(), switchTenantAction() (+6 more)

### Community 23 - "Community 23"
Cohesion: 0.20
Nodes (14): createImportWizardActions(), approveAllOdooIdentityCandidatesAction(), approveOdooIdentityCandidateAction(), approveOdooMappingAction(), conflictOdooIdentityCandidateAction(), generateOdooIdentityCandidatesAction(), loadOdooAiPreviewAction(), loadOdooBatchStates() (+6 more)

### Community 24 - "Community 24"
Cohesion: 0.21
Nodes (13): RecommendationDetailPage(), AgentConfigurePage(), WorkflowEditPage(), executePublishedWorkflowByKey(), getArtifactReadiness(), getArtifactVersions(), loadAgentVersionByKey(), loadWorkflowVersionByKey() (+5 more)

### Community 25 - "Community 25"
Cohesion: 0.19
Nodes (11): BusinessPolicyDefinitionDetailPage(), BusinessPolicyDefinitionDetailProps, BusinessPolicyDefinitionDetailView(), loadBusinessPolicyDefinitionDetail(), markReadyAction(), publishAction(), BusinessPoliciesPage(), BusinessPolicyDefinitionDetail (+3 more)

### Community 26 - "Community 26"
Cohesion: 0.21
Nodes (9): CapabilityDefinitionDetailPage(), CapabilityDefinitionDetailProps, CapabilityDefinitionDetailView(), loadCapabilityDefinitionDetail(), markReadyAction(), publishAction(), ApiResult, CapabilityDefinitionDetail (+1 more)

### Community 27 - "Community 27"
Cohesion: 0.20
Nodes (11): OptimizationModelDefinitionDetailPage(), getOptimizationModelDefinitionArtifacts(), getOptimizationModelDefinitionDetail(), OptimizationModelDefinitionDetail, loadOptimizationModelDefinitionDetail(), markReadyAction(), OptimizationModelDefinitionDetailProps, OptimizationModelDefinitionDetailView() (+3 more)

### Community 28 - "Community 28"
Cohesion: 0.17
Nodes (12): AgentTemplateDefinitionDetailProps, AgentTemplateDefinitionDetailView(), loadAgentTemplateDefinitionDetail(), markReadyAction(), publishAction(), AgentTemplatesPage(), AgentTemplateDefinitionDetailPage(), AgentTemplateDefinitionDetail (+4 more)

### Community 29 - "Community 29"
Cohesion: 0.21
Nodes (16): loadToolDefinitionDetail(), ToolDefinitionEditorPage(), compatibilityScanToolDefinition(), dryRunToolDefinition(), executeToolDefinition(), getToolDefinitionArtifacts(), getToolDefinitionDetail(), resolveLinkedTool() (+8 more)

### Community 30 - "Community 30"
Cohesion: 0.18
Nodes (18): anchorHint(), askTurnAction(), ChatPage(), createSessionAction(), loadLatestTurn(), PageProps, renderApiError(), askGovernedChatTurn() (+10 more)

### Community 31 - "Community 31"
Cohesion: 0.13
Nodes (17): DigitalThreadEventInspector(), LoadState, Props, DigitalThreadFilterBar(), DigitalThreadFilters, Props, DigitalThreadMinimap(), Props (+9 more)

### Community 32 - "Community 32"
Cohesion: 0.18
Nodes (9): ContextPackagesExplorerPage(), buildFilters(), DecisionsExplorerPage(), DecisionsExplorerPageProps, ExplorerListShell(), ContextPackageExplorerSummary, DecisionExplorerFilters, DecisionExplorerItem (+1 more)

### Community 33 - "Community 33"
Cohesion: 0.21
Nodes (11): DigitalThreadScrubber(), Props, LiveStreamItem, LiveContextValue, MissionControlLiveButton(), MissionControlLiveContext, MissionControlLiveProvider(), MissionControlLiveStreamPanel() (+3 more)

### Community 34 - "Community 34"
Cohesion: 0.14
Nodes (17): exportLatestTrace(), AdminFoundationPage(), createDataQualityIssueFromLatestSecurityEvent(), createExtractionIssueForLatestDocument(), emptyObject(), emptyResult(), exportAiTrace(), fetchApi() (+9 more)

### Community 35 - "Community 35"
Cohesion: 0.47
Nodes (6): parseManifestJson(), readDemoCsv(), readHelpersManifest(), resolveManufacturingReferenceRoot(), resolveRepoRoot(), readPdmDemoManifest()

### Community 37 - "Community 37"
Cohesion: 0.40
Nodes (3): ArtifactReadiness, ToolDefinitionDetail, PageProps

### Community 38 - "Community 38"
Cohesion: 0.54
Nodes (6): getImportProfileByKey(), getImportProfiles(), loadImportMappings(), getPdmImportProfileByKey(), getPdmImportProfiles(), loadPdmImportMappings()

### Community 39 - "Community 39"
Cohesion: 0.25
Nodes (9): DigitalThreadLiveClient(), Props, DigitalThreadStreamAuth, DigitalThreadStreamHandle, DigitalThreadStreamHandlers, resolveDigitalThreadStreamAuth(), startDigitalThreadEventStream(), StreamEnvelope (+1 more)

### Community 40 - "Community 40"
Cohesion: 0.25
Nodes (7): Backend Configuration, Current App, ETOS Frontend, Local Development, More Documentation, Scripts, Stack

### Community 41 - "Community 41"
Cohesion: 0.29
Nodes (6): Conventions, ETOS Frontend — Agent Guide, Stack, This is NOT the Next.js you know, UI program (active), Verify

### Community 42 - "Community 42"
Cohesion: 0.22
Nodes (11): ImportIdentityPage(), getIdentityCandidatesForBatch(), getImportBatchDetail(), getTrustScoresForBatch(), hasBlockingValidationIssues(), hasUnresolvedIdentityCandidates(), promoteImportBatch(), promoteImportBatches() (+3 more)

### Community 43 - "Community 43"
Cohesion: 0.33
Nodes (4): ImportWizardShell(), ImportWizardShellProps, STEPS, PdmImportWizardProps

### Community 44 - "Community 44"
Cohesion: 0.16
Nodes (16): DigitalThreadCanvas(), Props, CanvasBranchPath, CanvasPulse, CanvasScene, FALLBACK_COLORS, formatEventTime(), hashSystemId() (+8 more)

### Community 45 - "Community 45"
Cohesion: 0.40
Nodes (3): inter, metadata, ThemeProvider()

### Community 46 - "Community 46"
Cohesion: 0.15
Nodes (13): cubic1d(), DigitalThreadTimeline(), DigitalThreadTimelineProps, EnterpriseThreadEvent, iconMap, markerTopForColumn(), syncStatusClass(), SystemHeading() (+5 more)

### Community 47 - "Community 47"
Cohesion: 0.40
Nodes (3): BatchPipeline(), BatchPipelineProps, ImportWizardBatchState

### Community 48 - "Community 48"
Cohesion: 0.36
Nodes (5): NavGroup, navGroupLabels, navGroupOrder, NavItem, navItems

### Community 49 - "Community 49"
Cohesion: 0.50
Nodes (3): Error details, Instructions, Test info

### Community 50 - "Community 50"
Cohesion: 0.50
Nodes (3): Error details, Instructions, Test info

### Community 51 - "Community 51"
Cohesion: 0.50
Nodes (3): Error details, Instructions, Test info

### Community 52 - "Community 52"
Cohesion: 0.50
Nodes (3): Error details, Instructions, Test info

### Community 53 - "Community 53"
Cohesion: 0.15
Nodes (12): activityHeatmapFixture, aiInsightsFixture, digitalThreadTimelineFixture, heatmapSystemLabels, LiveEventFixture, liveEventStreamFixture, ThreadAlertFixture, threadAlertsFixture (+4 more)

### Community 55 - "Community 55"
Cohesion: 0.33
Nodes (6): runDemoGovernedQuery(), GraphExplorerPage(), getGraphExplorerNodes(), runDemoGovernedQueryFlow(), runGovernedQueryForDocument(), runGovernedQueryForGraphNode()

### Community 62 - "Community 62"
Cohesion: 0.13
Nodes (15): loadConnectorDefinitionDetail(), GovernanceDashboardPage(), getAgentRunDetail(), getConnectorDefinitionArtifacts(), getConnectorDefinitionDetail(), getGovernanceDashboard(), getGovernanceKpiTrends(), getSkillDefinitionArtifacts() (+7 more)

## Knowledge Gaps
- **358 isolated node(s):** `eslintConfig`, `nextConfig`, `name`, `version`, `private` (+353 more)
  These have ≤1 connection - possible missing edges or undocumented components.
- **5 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `resolveTenantHeaders()` connect `Community 3` to `Community 0`, `Community 1`, `Community 2`, `Community 4`, `Community 5`, `Community 7`, `Community 8`, `Community 9`, `Community 16`, `Community 17`, `Community 18`, `Community 19`, `Community 20`, `Community 22`, `Community 24`, `Community 25`, `Community 27`, `Community 28`, `Community 29`, `Community 30`, `Community 32`, `Community 34`, `Community 42`, `Community 55`, `Community 62`?**
  _High betweenness centrality (0.013) - this node is a cross-community bridge._
- **Why does `missingContext()` connect `Community 3` to `Community 0`, `Community 1`, `Community 2`, `Community 4`, `Community 5`, `Community 7`, `Community 8`, `Community 9`, `Community 13`, `Community 16`, `Community 17`, `Community 18`, `Community 19`, `Community 20`, `Community 22`, `Community 24`, `Community 25`, `Community 27`, `Community 28`, `Community 29`, `Community 30`, `Community 32`, `Community 34`, `Community 42`, `Community 55`, `Community 62`?**
  _High betweenness centrality (0.013) - this node is a cross-community bridge._
- **Why does `Button()` connect `Community 7` to `Community 1`, `Community 2`, `Community 36`, `Community 6`, `Community 8`, `Community 11`, `Community 17`, `Community 20`, `Community 29`, `Community 30`?**
  _High betweenness centrality (0.012) - this node is a cross-community bridge._
- **What connects `eslintConfig`, `nextConfig`, `name` to the rest of the system?**
  _358 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `Community 0` be split into smaller, more focused modules?**
  _Cohesion score 0.012987012987012988 - nodes in this community are weakly interconnected._
- **Should `Community 1` be split into smaller, more focused modules?**
  _Cohesion score 0.08888888888888889 - nodes in this community are weakly interconnected._
- **Should `Community 2` be split into smaller, more focused modules?**
  _Cohesion score 0.08045977011494253 - nodes in this community are weakly interconnected._