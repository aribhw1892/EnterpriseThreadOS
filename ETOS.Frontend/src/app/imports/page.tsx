import {
  ApiResult,
  DataQualityIssue,
  IdentityCandidateLink,
  ImportBatch,
  ImportBatchDetail,
  ImportColumnMapping,
  ImportFileEvidence,
  ImportMappingVersion,
  ImportStagingGraphRun,
  ImportValidationIssue,
  TrustScoreRecord,
  approveLatestIdentityCandidate,
  adminUserId,
  approveLatestImportMapping,
  createDataQualityIssueFromLatestSecurityEvent,
  createDemoComparisonImportFlow,
  createDemoImportFlow,
  createManualDataQualityIssueForLatestBatch,
  generateDataQualityIssuesForLatestImport,
  generateLatestIdentityCandidates,
  getImportLists,
  MonitoringIssueTypeDefinition,
  markLatestIdentityCandidateConflicted,
  runIdentityResolutionDemoFlow,
  selectedTenantId,
  stageLatestImportBatch,
  validateLatestImportBatch,
} from "@/lib/etos-api";
import { revalidatePath } from "next/cache";
import type { ReactNode } from "react";

export const dynamic = "force-dynamic";

async function createDemoImport() {
  "use server";

  await createDemoImportFlow();
  revalidatePath("/imports");
}

async function createComparisonImport() {
  "use server";

  await createDemoComparisonImportFlow();
  revalidatePath("/imports");
}

async function runIdentityDemo() {
  "use server";

  await runIdentityResolutionDemoFlow();
  revalidatePath("/imports");
}

async function approveDraftMapping() {
  "use server";

  await approveLatestImportMapping();
  revalidatePath("/imports");
}

async function validateBatch() {
  "use server";

  await validateLatestImportBatch();
  revalidatePath("/imports");
}

async function stageBatch() {
  "use server";

  await stageLatestImportBatch();
  revalidatePath("/imports");
}

async function generateIdentityCandidates() {
  "use server";

  await generateLatestIdentityCandidates();
  revalidatePath("/imports");
}

async function approveIdentityCandidate() {
  "use server";

  await approveLatestIdentityCandidate();
  revalidatePath("/imports");
}

async function markIdentityCandidateConflicted() {
  "use server";

  await markLatestIdentityCandidateConflicted();
  revalidatePath("/imports");
}

async function generateDataQualityIssues() {
  "use server";

  await generateDataQualityIssuesForLatestImport();
  revalidatePath("/imports");
}

async function createManualDataQualityIssue() {
  "use server";

  await createManualDataQualityIssueForLatestBatch();
  revalidatePath("/imports");
}

async function createSecurityEventDataQualityIssue() {
  "use server";

  await createDataQualityIssueFromLatestSecurityEvent();
  revalidatePath("/imports");
}

function formatStatus(status: string | number) {
  if (typeof status === "number") {
    return (
      {
        0: "Unverified",
        1: "Provisional",
        2: "Trusted",
        3: "Conflicted",
      }[status] ?? String(status)
    );
  }

  return status;
}

function StatusBadge({ status }: { status: string | number }) {
  const displayStatus = formatStatus(status);
  const normalized = displayStatus.toLowerCase();
  const className =
    normalized === "staged" || normalized === "completed" || normalized === "approved" || normalized === "trusted"
      ? "bg-emerald-100 text-emerald-800 dark:bg-emerald-950 dark:text-emerald-200"
      : normalized === "failed" || normalized === "error" || normalized === "conflicted" || normalized === "critical"
        ? "bg-rose-100 text-rose-800 dark:bg-rose-950 dark:text-rose-200"
        : normalized === "high" || normalized === "medium"
          ? "bg-amber-100 text-amber-800 dark:bg-amber-950 dark:text-amber-200"
        : "bg-cyan-100 text-cyan-800 dark:bg-cyan-950 dark:text-cyan-200";

  return (
    <span className={`rounded-full px-3 py-1 text-xs font-semibold uppercase tracking-wide ${className}`}>
      {displayStatus}
    </span>
  );
}

function ErrorState({ error }: { error: string }) {
  return (
    <div className="rounded-2xl border border-amber-500/30 bg-amber-500/10 p-4 text-sm text-amber-100">
      {error}
    </div>
  );
}

function EmptyState({ message }: { message: string }) {
  return (
    <div className="rounded-2xl border border-slate-800 bg-slate-950 p-4 text-sm text-slate-400">
      {message}
    </div>
  );
}

function ActionButton({ action, children }: { action: () => Promise<void>; children: ReactNode }) {
  return (
    <form action={action}>
      <button
        type="submit"
        className="rounded-2xl bg-cyan-300 px-5 py-3 text-sm font-semibold text-slate-950 transition hover:bg-cyan-200"
      >
        {children}
      </button>
    </form>
  );
}

function ButtonGroup({
  title,
  description,
  children,
}: {
  title: string;
  description: string;
  children: ReactNode;
}) {
  return (
    <div className="rounded-2xl border border-slate-800 bg-slate-950/60 p-4">
      <h2 className="text-sm font-semibold uppercase tracking-[0.25em] text-cyan-300">{title}</h2>
      <p className="mt-2 text-xs text-slate-400">{description}</p>
      <div className="mt-4 flex flex-wrap gap-3">{children}</div>
    </div>
  );
}

function ListSection<T>({
  title,
  description,
  items,
  emptyMessage,
  renderItem,
}: {
  title: string;
  description: string;
  items: T[];
  emptyMessage: string;
  renderItem: (item: T) => ReactNode;
}) {
  return (
    <section className="rounded-3xl border border-slate-800 bg-slate-900 p-6">
      <div className="mb-5">
        <h2 className="text-2xl font-semibold">{title}</h2>
        <p className="mt-1 text-sm text-slate-400">{description}</p>
      </div>
      {items.length > 0 ? <div className="grid gap-3">{items.map(renderItem)}</div> : <EmptyState message={emptyMessage} />}
    </section>
  );
}

function BatchCard(batch: ImportBatch) {
  return (
    <article key={batch.id} className="rounded-2xl border border-slate-800 bg-slate-950 p-4">
      <div className="flex items-center justify-between gap-3">
        <div>
          <h3 className="font-semibold">{batch.sourceSystem}</h3>
          <p className="mt-1 text-sm text-slate-400">{batch.description ?? "No description."}</p>
        </div>
        <StatusBadge status={batch.status} />
      </div>
      <div className="mt-3 grid gap-1 text-xs text-slate-500 md:grid-cols-2">
        <p>Model: {batch.activeModelPackageKey ?? batch.activeModelPackageVersionId}</p>
        <p>Version: {batch.activeModelPackageVersionLabel ?? "unknown"}</p>
        <p>Evidence: {batch.evidenceCount}</p>
        <p>Mappings: {batch.mappingVersionCount}</p>
        <p>Validation issues: {batch.validationIssueCount}</p>
        <p>Staging runs: {batch.stagingRunCount}</p>
      </div>
    </article>
  );
}

function EvidenceCard(evidence: ImportFileEvidence) {
  return (
    <article key={evidence.id} className="rounded-2xl border border-slate-800 bg-slate-950 p-4">
      <h3 className="font-semibold">{evidence.originalFileName}</h3>
      <div className="mt-3 grid gap-1 text-xs text-slate-500">
        <p>Checksum: {evidence.sha256Checksum}</p>
        <p>Size: {evidence.sizeBytes} bytes</p>
        <p>Content type: {evidence.contentType}</p>
        <p>Audit: {evidence.auditRecordId ?? "not linked"}</p>
      </div>
    </article>
  );
}

function formatColumnMapping(mapping: ImportColumnMapping) {
  const target = mapping.canonicalAttributeKey
    ? `${mapping.canonicalObjectType}.${mapping.canonicalAttributeKey}`
    : mapping.isIdentityField
      ? `${mapping.canonicalObjectType} identity`
      : `${mapping.canonicalObjectType} unmapped`;

  return `${mapping.sourceColumn} -> ${target}`;
}

function MappingCard(mapping: ImportMappingVersion) {
  return (
    <article key={mapping.id} className="rounded-2xl border border-slate-800 bg-slate-950 p-4">
      <div className="flex items-center justify-between gap-3">
        <div>
          <h3 className="font-semibold">{mapping.versionLabel}</h3>
          <p className="mt-1 text-sm text-slate-400">{mapping.summary ?? "No summary."}</p>
        </div>
        <StatusBadge status={mapping.state} />
      </div>
      <div className="mt-3 grid gap-1 text-xs text-slate-500">
        <p>Suggestion provider: {mapping.suggestionProvider}</p>
        <p>{mapping.columnMappingCount} column mappings, {mapping.lifecycleMappingCount} lifecycle mappings</p>
        <p>
          Columns:{" "}
          {mapping.columnMappings
            .map(formatColumnMapping)
            .join(", ")}
        </p>
      </div>
    </article>
  );
}

function IssueCard(issue: ImportValidationIssue) {
  return (
    <article key={issue.id} className="rounded-2xl border border-slate-800 bg-slate-950 p-4">
      <div className="flex items-center justify-between gap-3">
        <h3 className="font-semibold">{issue.issueCode}</h3>
        <StatusBadge status={issue.severity} />
      </div>
      <p className="mt-2 text-sm text-slate-300">{issue.message}</p>
      <p className="mt-2 text-xs text-slate-500">
        Row {issue.rowNumber ?? "n/a"} {issue.sourceColumn ? `- ${issue.sourceColumn}` : ""}
      </p>
    </article>
  );
}

function StagingRunCard(run: ImportStagingGraphRun) {
  return (
    <article key={run.id} className="rounded-2xl border border-slate-800 bg-slate-950 p-4">
      <div className="flex items-center justify-between gap-3">
        <h3 className="font-semibold">Run {run.id.slice(0, 8)}</h3>
        <StatusBadge status={run.status} />
      </div>
      <div className="mt-3 grid gap-1 text-xs text-slate-500">
        <p>Nodes: {run.nodeCount}</p>
        <p>Relationships: {run.relationshipCount}</p>
        <p>Failure: {run.failureSummary ?? "none"}</p>
      </div>
    </article>
  );
}

function IdentityCandidateCard(candidate: IdentityCandidateLink) {
  return (
    <article key={candidate.id} className="rounded-2xl border border-slate-800 bg-slate-950 p-4">
      <div className="flex items-center justify-between gap-3">
        <div>
          <h3 className="font-semibold">{candidate.objectType} identity link</h3>
          <p className="mt-1 text-sm text-slate-400">
            {candidate.sourceSystem} {candidate.sourceRecordId} {"->"} {candidate.targetSystem} {candidate.targetRecordId}
          </p>
        </div>
        <StatusBadge status={candidate.state} />
      </div>
      <div className="mt-3 grid gap-1 text-xs text-slate-500">
        <p>Confidence: {(candidate.confidenceScore * 100).toFixed(1)}%</p>
        <p>Trust: {formatStatus(candidate.trustState)}</p>
        <p>Excluded from trusted recommendations: {candidate.excludedFromTrustedRecommendations ? "yes" : "no"}</p>
        <p>Graph relationship: {candidate.graphRelationshipId ?? "not created"}</p>
        <p>{candidate.evidenceSummary}</p>
      </div>
    </article>
  );
}

function TrustScoreCard(score: TrustScoreRecord) {
  const breakdown = Object.entries(score.breakdown)
    .map(([key, value]) => `${key}: ${value}`)
    .join(", ");

  return (
    <article key={score.id} className="rounded-2xl border border-slate-800 bg-slate-950 p-4">
      <div className="flex items-center justify-between gap-3">
        <div>
          <h3 className="font-semibold">{score.entityType}</h3>
          <p className="mt-1 text-sm text-slate-400">Score {(score.score * 100).toFixed(1)}%</p>
        </div>
        <StatusBadge status={score.trustState} />
      </div>
      <div className="mt-3 grid gap-1 text-xs text-slate-500">
        <p>Candidate: {score.identityCandidateLinkId ?? "n/a"}</p>
        <p>Relationship: {score.graphRelationshipId ?? "not linked"}</p>
        <p>Breakdown: {breakdown || "none"}</p>
      </div>
    </article>
  );
}

function DataQualityIssueCard(issue: DataQualityIssue) {
  const trustBreakdown = issue.trustImpacts
    .flatMap((impact) => Object.entries(impact.breakdown).map(([key, value]) => `${key}: ${value}`))
    .join(", ");
  const sourceLinks = issue.sourceLinks
    .map((link) => `${link.sourceType}${link.label ? ` (${link.label})` : ""}`)
    .join(", ");

  return (
    <article key={issue.id} className="rounded-2xl border border-slate-800 bg-slate-950 p-4">
      <div className="flex items-center justify-between gap-3">
        <div>
          <h3 className="font-semibold">{issue.title}</h3>
          <p className="mt-1 text-sm text-slate-400">
            {issue.origin} issue on {issue.affectedEntityType}
          </p>
        </div>
        <div className="flex flex-wrap justify-end gap-2">
          <StatusBadge status={issue.severity} />
          <StatusBadge status={issue.status} />
        </div>
      </div>
      <p className="mt-3 text-sm text-slate-300">{issue.evidenceSummary}</p>
      <div className="mt-3 grid gap-1 text-xs text-slate-500 md:grid-cols-2">
        <p>Code: {issue.issueCode}</p>
        <p>Priority: {issue.reviewPriority}</p>
        <p>Trust penalty: {(issue.trustImpactPenalty * 100).toFixed(1)}%</p>
        <p>Resulting trust: {formatStatus(issue.resultingTrustState)}</p>
        <p>Excluded from trusted recommendations: {issue.excludedFromTrustedRecommendations ? "yes" : "no"}</p>
        <p>Review hook: {issue.reviewTaskReady ? issue.reviewTaskHint ?? "ready" : "not ready"}</p>
        <p>Import batch: {issue.importBatchId ?? "n/a"}</p>
        <p>Security event: {issue.securityEventId ?? "n/a"}</p>
        <p className="md:col-span-2">Sources: {sourceLinks || "none"}</p>
        <p className="md:col-span-2">Trust breakdown: {trustBreakdown || "none"}</p>
      </div>
    </article>
  );
}

function MonitoringPlaceholderCard(definition: MonitoringIssueTypeDefinition) {
  return (
    <article key={definition.id} className="rounded-2xl border border-slate-800 bg-slate-950 p-4">
      <div className="flex items-center justify-between gap-3">
        <div>
          <h3 className="font-semibold">{definition.displayName}</h3>
          <p className="mt-1 font-mono text-xs text-cyan-200">{definition.issueTypeKey}</p>
        </div>
        <StatusBadge status={definition.isEnabled ? "enabled" : "disabled"} />
      </div>
      <div className="mt-3 grid gap-1 text-xs text-slate-500">
        <p>{definition.safeSummary}</p>
        <p>Live source scanning: {definition.allowsLiveSourceScanning ? "enabled" : "disabled"}</p>
      </div>
    </article>
  );
}

function DataQualityPanel({
  issues,
  monitoringPlaceholders,
}: {
  issues: ApiResult<DataQualityIssue[]>;
  monitoringPlaceholders: ApiResult<MonitoringIssueTypeDefinition[]>;
}) {
  return (
    <div className="grid gap-6 xl:grid-cols-2">
      {issues.error ? (
        <ErrorState error={issues.error} />
      ) : (
        <ListSection
          title="Data Quality Issues"
          description="Durable quality issues promoted from import validation, manual review hooks, and security events."
          items={issues.data ?? []}
          emptyMessage="No data quality issues have been generated for this tenant."
          renderItem={DataQualityIssueCard}
        />
      )}
      {monitoringPlaceholders.error ? (
        <ErrorState error={monitoringPlaceholders.error} />
      ) : (
        <ListSection
          title="Monitoring Placeholders"
          description="Disabled MVP contracts for future monitoring agents that inspect existing issue types only."
          items={monitoringPlaceholders.data ?? []}
          emptyMessage="No monitoring placeholders are available."
          renderItem={MonitoringPlaceholderCard}
        />
      )}
    </div>
  );
}

function FirstBatchDetail({ result }: { result: ApiResult<ImportBatchDetail> }) {
  if (result.error) {
    return <ErrorState error={result.error} />;
  }

  if (!result.data) {
    return <EmptyState message="Create a demo import to inspect evidence, mappings, validation issues, and staging runs." />;
  }

  return (
    <div className="grid gap-6 xl:grid-cols-2">
      <ListSection
        title="Raw Evidence"
        description="Stored file evidence metadata. Raw payloads stay out of list responses."
        items={result.data.evidence}
        emptyMessage="No file evidence has been uploaded."
        renderItem={EvidenceCard}
      />
      <ListSection
        title="Mapping Versions"
        description="Draft and approved import mappings generated from deterministic preview suggestions."
        items={result.data.mappingVersions}
        emptyMessage="No import mappings have been created."
        renderItem={MappingCard}
      />
      <ListSection
        title="Validation Issues"
        description="Row and column scoped failures or warnings from the active approved mapping."
        items={result.data.validationIssues}
        emptyMessage="No validation issues have been recorded."
        renderItem={IssueCard}
      />
      <ListSection
        title="Staging Runs"
        description="Graph creation summaries for staging/unverified records."
        items={result.data.stagingRuns}
        emptyMessage="No staging graph run has been created."
        renderItem={StagingRunCard}
      />
    </div>
  );
}

function IdentityResolutionPanel({
  candidates,
  trustScores,
}: {
  candidates: ApiResult<IdentityCandidateLink[]>;
  trustScores: ApiResult<TrustScoreRecord[]>;
}) {
  if (candidates.error) {
    return <ErrorState error={candidates.error} />;
  }

  return (
    <div className="grid gap-6 xl:grid-cols-2">
      <ListSection
        title="Identity Candidates"
        description="Reviewable source-record links generated from staged import identity fields."
        items={candidates.data ?? []}
        emptyMessage="No identity candidates have been generated for the latest batch."
        renderItem={IdentityCandidateCard}
      />
      {trustScores.error ? (
        <ErrorState error={trustScores.error} />
      ) : (
        <ListSection
          title="Trust Scores"
          description="Current score breakdowns for identity candidates and graph link trust state."
          items={trustScores.data ?? []}
          emptyMessage="No trust scores have been calculated."
          renderItem={TrustScoreCard}
        />
      )}
    </div>
  );
}

export default async function ImportsPage() {
  const lists = await getImportLists();
  const batches = lists.batches.data ?? [];

  return (
    <main className="min-h-screen bg-slate-950 px-6 py-10 text-slate-100">
      <div className="mx-auto grid max-w-7xl gap-8">
        <header className="rounded-3xl border border-slate-800 bg-slate-900 p-8">
          <p className="text-sm font-semibold uppercase tracking-[0.3em] text-cyan-300">
            EnterpriseThreadOS
          </p>
          <div className="mt-4 flex flex-col gap-6 lg:flex-row lg:items-end lg:justify-between">
            <div>
              <h1 className="text-4xl font-bold tracking-tight">Import Mapping and Staging</h1>
              <p className="mt-3 max-w-3xl text-slate-300">
                Upload evidence through the imports API, approve deterministic mapping versions, validate CSV rows,
                and create untrusted staging graph records before later review slices promote trust.
              </p>
            </div>
          </div>
          <div className="mt-5 grid gap-2 text-xs text-slate-500 md:grid-cols-2">
            <p>Admin user: {adminUserId}</p>
            <p>Tenant: {selectedTenantId}</p>
          </div>
          <div className="mt-6 grid gap-4 xl:grid-cols-2">
            <ButtonGroup
              title="Recommended Demo"
              description="Creates source batches, approves mappings, validates rows, stages them, generates identity candidates, and can promote validation issues."
            >
              <ActionButton action={runIdentityDemo}>Run identity demo</ActionButton>
              <ActionButton action={approveIdentityCandidate}>Approve first reviewable candidate</ActionButton>
              <ActionButton action={markIdentityCandidateConflicted}>Mark first candidate conflicted</ActionButton>
              <ActionButton action={generateDataQualityIssues}>Generate quality issues</ActionButton>
            </ButtonGroup>
            <ButtonGroup
              title="Manual Latest-Batch Tools"
              description="These buttons intentionally operate on the newest batch only. Use them for step-by-step debugging, not the full identity demo."
            >
              <ActionButton action={createDemoImport}>Create CAD/PDM draft batch</ActionButton>
              <ActionButton action={createComparisonImport}>Create ERP draft batch</ActionButton>
              <ActionButton action={approveDraftMapping}>Approve latest draft mapping</ActionButton>
              <ActionButton action={validateBatch}>Validate latest batch only</ActionButton>
              <ActionButton action={stageBatch}>Stage latest batch only</ActionButton>
              <ActionButton action={generateIdentityCandidates}>Generate candidates for latest batch</ActionButton>
              <ActionButton action={createManualDataQualityIssue}>Create manual quality issue</ActionButton>
              <ActionButton action={createSecurityEventDataQualityIssue}>Create issue from security event</ActionButton>
            </ButtonGroup>
          </div>
        </header>

        <section className="rounded-3xl border border-cyan-400/30 bg-cyan-400/10 p-6">
          <h2 className="text-2xl font-semibold">API Upload Support</h2>
          <p className="mt-2 text-sm text-slate-300">
            Multipart upload is available at <code>/api/admin/imports/batches/{"{batchId}"}/files</code>.
            This page keeps the UI minimal and uses a small server-side CSV demo action for repeatable validation.
          </p>
        </section>

        {lists.batches.error ? (
          <ErrorState error={lists.batches.error} />
        ) : (
          <ListSection
            title="Import Batches"
            description="Tenant-scoped batches tied to the active published model package at creation time."
            items={batches}
            emptyMessage="No import batches have been created."
            renderItem={BatchCard}
          />
        )}

        <FirstBatchDetail result={lists.firstBatchDetail} />
        <IdentityResolutionPanel
          candidates={lists.firstBatchIdentityCandidates}
          trustScores={lists.firstBatchTrustScores}
        />
        <DataQualityPanel
          issues={lists.dataQualityIssues}
          monitoringPlaceholders={lists.monitoringPlaceholders}
        />
      </div>
    </main>
  );
}
