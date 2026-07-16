import Link from "next/link";
import type { ReactNode } from "react";
import { StatusBadge } from "@/components/ui/Badge";
import { Button } from "@/components/ui/Button";
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@/components/ui/Card";
import { EmptyState } from "@/components/ui/EmptyState";
import { ErrorState } from "@/components/ui/ErrorState";
import { Stepper, type StepperStatus } from "@/components/ui/Stepper";
import type {
  ApiResult,
  DataQualityIssue,
  IdentityCandidateLink,
  ImportBatch,
  ImportBatchDetail,
  ImportColumnMapping,
  ImportFileEvidence,
  ImportMappingVersion,
  ImportPromotionRun,
  ImportStagingGraphRun,
  ImportValidationIssue,
  MonitoringIssueTypeDefinition,
  TrustScoreRecord,
} from "@/lib/etos-api";

/** Mockup wizard steps: Source → Mapping → Validate → Identity → Commit */
export const IMPORT_STEPS = [
  { id: "source", label: "Source" },
  { id: "mapping", label: "Mapping" },
  { id: "validate", label: "Validate" },
  { id: "identity", label: "Identity" },
  { id: "commit", label: "Commit" },
] as const;

export function batchStepId(status: string): string {
  const normalized = status.toLowerCase();
  if (normalized === "draft" || normalized === "uploaded") return "source";
  if (normalized === "mapped" || normalized === "mappingapproved") return "mapping";
  if (normalized === "validated") return "validate";
  if (normalized === "staged") return "identity";
  if (normalized === "promoted") return "commit";
  return "source";
}

export function ImportStepper({
  batch,
  currentStepId,
}: {
  batch?: ImportBatch | null;
  currentStepId: string;
}) {
  const statusForStep = (stepId: string): StepperStatus => {
    if (!batch) {
      return stepId === currentStepId ? "current" : "upcoming";
    }
    const order = IMPORT_STEPS.map((step) => step.id) as string[];
    const batchStep = batchStepId(batch.status);
    const batchIndex = order.indexOf(batchStep);
    const stepIndex = order.indexOf(stepId);
    const currentIndex = order.indexOf(currentStepId);
    if (stepIndex === currentIndex) return "current";
    if (stepIndex < batchIndex) return "complete";
    return "upcoming";
  };

  return (
    <div className="mb-6">
      <Stepper
        steps={[...IMPORT_STEPS]}
        currentStepId={currentStepId}
        statusForStep={statusForStep}
      />
      {batch ? (
        <div className="mt-3 flex flex-wrap gap-3 text-sm">
          <Link
            className="text-etos-accent-cyan underline-offset-2 hover:underline"
            href={`/imports/${batch.id}/mapping`}
          >
            Mapping
          </Link>
          <Link
            className="text-etos-accent-cyan underline-offset-2 hover:underline"
            href={`/imports/${batch.id}/staging`}
          >
            Validate
          </Link>
          <Link
            className="text-etos-accent-cyan underline-offset-2 hover:underline"
            href={`/imports/${batch.id}/identity`}
          >
            Identity
          </Link>
          <Link
            className="text-etos-accent-cyan underline-offset-2 hover:underline"
            href="/imports/data-quality"
          >
            Data quality
          </Link>
          <Link
            className="text-etos-accent-cyan underline-offset-2 hover:underline"
            href="/imports/new"
          >
            New upload
          </Link>
        </div>
      ) : null}
    </div>
  );
}

export function ActionButton({
  action,
  children,
}: {
  action: () => Promise<void>;
  children: ReactNode;
}) {
  return (
    <form action={action}>
      <Button type="submit">{children}</Button>
    </form>
  );
}

export function ButtonGroup({
  title,
  description,
  children,
}: {
  title: string;
  description: string;
  children: ReactNode;
}) {
  return (
    <Card>
      <CardHeader>
        <CardTitle>{title}</CardTitle>
        <CardDescription>{description}</CardDescription>
      </CardHeader>
      <CardContent className="flex flex-wrap gap-3">{children}</CardContent>
    </Card>
  );
}

export function ListSection<T>({
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
    <Card>
      <CardHeader>
        <CardTitle>{title}</CardTitle>
        <CardDescription>{description}</CardDescription>
      </CardHeader>
      <CardContent>
        {items.length > 0 ? (
          <div className="grid gap-3">{items.map(renderItem)}</div>
        ) : (
          <EmptyState message={emptyMessage} />
        )}
      </CardContent>
    </Card>
  );
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

export function BatchCard(batch: ImportBatch) {
  return (
    <article
      key={batch.id}
      className="rounded-etos-card border border-etos-border bg-etos-panel-muted p-4"
    >
      <div className="flex items-center justify-between gap-3">
        <div>
          <h3 className="font-semibold text-etos-ink">{batch.sourceSystem}</h3>
          <p className="mt-1 text-sm text-etos-ink-muted">
            {batch.description ?? "No description."}
          </p>
        </div>
        <StatusBadge status={batch.status} />
      </div>
      <div className="mt-3 grid gap-1 text-xs text-etos-ink-muted md:grid-cols-2">
        <p>Model: {batch.activeModelPackageKey ?? batch.activeModelPackageVersionId}</p>
        <p>Evidence: {batch.evidenceCount}</p>
        <p>Mappings: {batch.mappingVersionCount}</p>
        <p>Validation issues: {batch.validationIssueCount}</p>
      </div>
      <div className="mt-3 flex flex-wrap gap-2 text-sm">
        <Link
          href={`/imports/${batch.id}/mapping`}
          className="text-etos-accent-cyan underline-offset-2 hover:underline"
        >
          Mapping
        </Link>
        <Link
          href={`/imports/${batch.id}/staging`}
          className="text-etos-accent-cyan underline-offset-2 hover:underline"
        >
          Staging
        </Link>
        <Link
          href={`/imports/${batch.id}/identity`}
          className="text-etos-accent-cyan underline-offset-2 hover:underline"
        >
          Identity
        </Link>
      </div>
    </article>
  );
}

export function EvidenceCard(evidence: ImportFileEvidence) {
  return (
    <article
      key={evidence.id}
      className="rounded-etos-card border border-etos-border bg-etos-panel-muted p-4"
    >
      <h3 className="font-semibold text-etos-ink">{evidence.originalFileName}</h3>
      <div className="mt-3 grid gap-1 text-xs text-etos-ink-muted">
        <p>Checksum: {evidence.sha256Checksum}</p>
        <p>Size: {evidence.sizeBytes} bytes</p>
        <p>Content type: {evidence.contentType}</p>
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

export function MappingCard(mapping: ImportMappingVersion) {
  return (
    <article
      key={mapping.id}
      className="rounded-etos-card border border-etos-border bg-etos-panel-muted p-4"
    >
      <div className="flex items-center justify-between gap-3">
        <h3 className="font-semibold text-etos-ink">
          Mapping {mapping.versionLabel ?? mapping.id.slice(0, 8)}
        </h3>
        <StatusBadge status={mapping.state} />
      </div>
      <ul className="mt-3 space-y-1 text-xs text-etos-ink-muted">
        {(mapping.columnMappings ?? []).slice(0, 8).map((column) => (
          <li key={`${mapping.id}-${column.sourceColumn}`}>
            {formatColumnMapping(column)}
          </li>
        ))}
      </ul>
    </article>
  );
}

export function IssueCard(issue: ImportValidationIssue) {
  return (
    <article
      key={issue.id}
      className="rounded-etos-card border border-etos-border bg-etos-panel-muted p-4"
    >
      <div className="flex items-center justify-between gap-3">
        <h3 className="font-semibold text-etos-ink">{issue.issueCode}</h3>
        <StatusBadge status={issue.severity} />
      </div>
      <p className="mt-2 text-sm text-etos-ink-muted">{issue.message}</p>
    </article>
  );
}

export function StagingRunCard(run: ImportStagingGraphRun) {
  return (
    <article
      key={run.id}
      className="rounded-etos-card border border-etos-border bg-etos-panel-muted p-4"
    >
      <div className="flex items-center justify-between gap-3">
        <h3 className="font-semibold text-etos-ink">Staging run</h3>
        <StatusBadge status={run.status} />
      </div>
      <p className="mt-2 text-xs text-etos-ink-muted">
        Nodes: {run.nodeCount} · Relationships: {run.relationshipCount}
      </p>
    </article>
  );
}

export function PromotionRunCard(run: ImportPromotionRun) {
  return (
    <article
      key={run.id}
      className="rounded-etos-card border border-etos-border bg-etos-panel-muted p-4"
    >
      <div className="flex items-center justify-between gap-3">
        <h3 className="font-semibold text-etos-ink">Promotion run</h3>
        <StatusBadge status={run.status} />
      </div>
      <p className="mt-2 text-xs text-etos-ink-muted">
        Promoted nodes: {run.promotedNodeCount} · Relationships:{" "}
        {run.promotedRelationshipCount}
        {run.failureSummary ? ` · ${run.failureSummary}` : ""}
      </p>
    </article>
  );
}

export function IdentityCandidateCard(candidate: IdentityCandidateLink) {
  return (
    <article
      key={candidate.id}
      className="rounded-etos-card border border-etos-border bg-etos-panel-muted p-4"
    >
      <div className="flex items-center justify-between gap-3">
        <h3 className="font-semibold text-etos-ink">{candidate.identityKey}</h3>
        <StatusBadge status={formatStatus(candidate.state)} />
      </div>
      <p className="mt-2 text-xs text-etos-ink-muted">
        {candidate.sourceSystem} → {candidate.targetSystem} · Confidence:{" "}
        {candidate.confidenceScore} · Trust: {formatStatus(candidate.trustState)}
      </p>
      <p className="mt-1 text-xs text-etos-ink-muted">{candidate.evidenceSummary}</p>
    </article>
  );
}

export function TrustScoreCard(score: TrustScoreRecord) {
  return (
    <article
      key={score.id}
      className="rounded-etos-card border border-etos-border bg-etos-panel-muted p-4"
    >
      <div className="flex items-center justify-between gap-3">
        <h3 className="font-semibold text-etos-ink">Trust score</h3>
        <StatusBadge status={formatStatus(score.trustState)} />
      </div>
      <p className="mt-2 text-xs text-etos-ink-muted">
        Score: {score.score} · Entity: {score.entityType}
        {score.graphNodeId ? ` · Node ${score.graphNodeId}` : ""}
      </p>
    </article>
  );
}

export function DataQualityIssueCard(issue: DataQualityIssue) {
  return (
    <article
      key={issue.id}
      className="rounded-etos-card border border-etos-border bg-etos-panel-muted p-4"
    >
      <div className="flex items-center justify-between gap-3">
        <h3 className="font-semibold text-etos-ink">{issue.title}</h3>
        <StatusBadge status={issue.severity} />
      </div>
      <p className="mt-2 text-sm text-etos-ink-muted">{issue.evidenceSummary}</p>
      <p className="mt-2 text-xs text-etos-ink-muted">
        Code: {issue.issueCode} · Trust penalty: {issue.trustImpactPenalty} · Status:{" "}
        {issue.status}
      </p>
    </article>
  );
}

export function MonitoringPlaceholderCard(
  definition: MonitoringIssueTypeDefinition,
) {
  return (
    <article
      key={definition.id}
      className="rounded-etos-card border border-etos-border bg-etos-panel-muted p-4"
    >
      <h3 className="font-semibold text-etos-ink">{definition.displayName}</h3>
      <p className="mt-2 text-sm text-etos-ink-muted">{definition.safeSummary}</p>
      <p className="mt-2 text-xs text-etos-ink-muted">
        Key: {definition.issueTypeKey} · Enabled:{" "}
        {definition.isEnabled ? "yes" : "no (MVP placeholder)"}
      </p>
    </article>
  );
}

export function DataQualityPanel({
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
          description="Durable quality issues from import validation, manual review, and security events."
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
          description="Disabled MVP contracts for future monitoring agents."
          items={monitoringPlaceholders.data ?? []}
          emptyMessage="No monitoring placeholders are available."
          renderItem={MonitoringPlaceholderCard}
        />
      )}
    </div>
  );
}

export function BatchDetailPanels({
  result,
  promotionRuns,
}: {
  result: ApiResult<ImportBatchDetail>;
  promotionRuns?: ApiResult<ImportPromotionRun[]>;
}) {
  if (result.error) {
    return <ErrorState error={result.error} />;
  }
  if (!result.data) {
    return (
      <EmptyState message="Create a demo import to inspect evidence, mappings, validation, and staging." />
    );
  }

  return (
    <div className="grid gap-6 xl:grid-cols-2">
      <ListSection
        title="Raw Evidence"
        description="Stored file evidence metadata."
        items={result.data.evidence}
        emptyMessage="No file evidence has been uploaded."
        renderItem={EvidenceCard}
      />
      <ListSection
        title="Mapping Versions"
        description="Draft and approved import mappings."
        items={result.data.mappingVersions}
        emptyMessage="No import mappings have been created."
        renderItem={MappingCard}
      />
      <ListSection
        title="Validation Issues"
        description="Failures or warnings from the active approved mapping."
        items={result.data.validationIssues}
        emptyMessage="No validation issues have been recorded."
        renderItem={IssueCard}
      />
      <ListSection
        title="Staging Runs"
        description="Graph creation summaries for staging records."
        items={result.data.stagingRuns}
        emptyMessage="No staging graph run has been created."
        renderItem={StagingRunCard}
      />
      {promotionRuns ? (
        promotionRuns.error ? (
          <ErrorState error={promotionRuns.error} />
        ) : (
          <ListSection
            title="Promotion Runs"
            description="Trusted graph copies after review gates."
            items={promotionRuns.data ?? []}
            emptyMessage="No promotion run has been created."
            renderItem={PromotionRunCard}
          />
        )
      ) : null}
    </div>
  );
}

export function IdentityResolutionPanel({
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
        description="Reviewable source-record links from staged identity fields."
        items={candidates.data ?? []}
        emptyMessage="No identity candidates have been generated."
        renderItem={IdentityCandidateCard}
      />
      {trustScores.error ? (
        <ErrorState error={trustScores.error} />
      ) : (
        <ListSection
          title="Trust Scores"
          description="Score breakdowns for identity candidates and graph link trust."
          items={trustScores.data ?? []}
          emptyMessage="No trust scores have been calculated."
          renderItem={TrustScoreCard}
        />
      )}
    </div>
  );
}
