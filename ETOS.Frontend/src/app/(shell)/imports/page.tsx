import Link from "next/link";
import { MappingAgentDebugPanel } from "@/components/imports/MappingAgentDebugPanel";
import {
  ActionButton,
  BatchCard,
  BatchDetailPanels,
  ButtonGroup,
  DataQualityPanel,
  IdentityResolutionPanel,
} from "@/components/imports/ImportHubShared";
import { Button } from "@/components/ui/Button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/Card";
import { ErrorState } from "@/components/ui/ErrorState";
import { KpiCard } from "@/components/ui/KpiCard";
import { ListItem, ListStack } from "@/components/ui/ListItem";
import { Timeline, TimelineCard } from "@/components/ui/Timeline";
import {
  approveIdentityCandidate,
  createBomRecommendation,
  createComparisonImport,
  createDemoImport,
  createManualDataQualityIssue,
  createSecurityEventDataQualityIssue,
  generateDataQualityIssues,
  generateIdentityCandidates,
  markIdentityCandidateConflicted,
  promoteStagedBatch,
  rejectStagedBatch,
  runBomComparison,
  runIdentityDemo,
  runMappingPreviewDebug,
  captureTrustedSnapshot,
  approveDraftMapping,
  stageBatch,
  validateBatch,
} from "@/app/(shell)/imports/actions";
import { getImportLists } from "@/lib/etos-api";

export const dynamic = "force-dynamic";

type PageProps = {
  searchParams: Promise<{ error?: string }>;
};

export default async function ImportsPage({ searchParams }: PageProps) {
  const { error: actionError } = await searchParams;
  const lists = await getImportLists();
  const batches = lists.batches.data ?? [];
  const firstBatch = batches[0];
  const firstEvidence = lists.firstBatchDetail.data?.evidence[0];
  const stagedRows = batches.reduce(
    (sum, batch) => sum + (batch.validationIssueCount ?? 0),
    0,
  );
  const candidates = lists.firstBatchIdentityCandidates.data ?? [];
  const reviewable = candidates.filter(
    (c) =>
      String(c.state).toLowerCase().includes("review") ||
      String(c.trustState).toLowerCase().includes("review"),
  ).length;
  const criticalBlockers =
    lists.dataQualityIssues.data?.filter(
      (issue) =>
        String(issue.severity).toLowerCase() === "critical" ||
        String(issue.severity).toLowerCase() === "high",
    ).length ?? 0;
  const hasStaged = batches.some((b) => b.status === "Staged");
  const hasMapped = batches.some(
    (b) =>
      b.status === "Mapped" ||
      b.status === "MappingApproved" ||
      b.mappingVersionCount > 0,
  );
  const hasPromoted = batches.some((b) => b.status === "Promoted");

  return (
    <main className="px-6 py-8 lg:px-8">
      <div className="mb-[18px] flex flex-wrap items-start justify-between gap-4">
        <div>
          <h1 className="text-[30px] font-bold tracking-tight text-etos-ink">
            Import hub
          </h1>
          <p className="mt-2 max-w-[900px] text-sm text-etos-ink-muted">
            UI-first workflow to import source-owned CAD/PDM and ERP files into
            staging before trusted graph promotion.
          </p>
        </div>
        <div className="flex flex-wrap gap-2.5">
          <Link href="/imports/new">
            <Button variant="primary">New import</Button>
          </Link>
          <Link href="/imports/new">
            <Button variant="ghost">Upload CSV/Excel</Button>
          </Link>
        </div>
      </div>

      {actionError ? (
        <div className="mb-4">
          <ErrorState error={actionError} />
        </div>
      ) : null}

      <div className="grid gap-4 md:grid-cols-4">
        <KpiCard
          label="Import batches"
          value={batches.length}
          hint="CAD/PDM + ERP demo"
        />
        <KpiCard
          label="Staged rows"
          value={stagedRows || batches.length * 0}
          hint={hasStaged ? "Awaiting review" : "Run demo to stage"}
        />
        <KpiCard
          label="Identity candidates"
          value={candidates.length}
          trend={reviewable > 0 ? "warn" : "flat"}
          trendLabel={reviewable > 0 ? String(reviewable) : undefined}
          hint={
            reviewable > 0
              ? `${reviewable} require review`
              : "No review queue"
          }
        />
        <KpiCard
          label="Critical blockers"
          value={criticalBlockers}
          trend={criticalBlockers > 0 ? "bad" : "flat"}
          trendLabel={criticalBlockers > 0 ? String(criticalBlockers) : undefined}
          hint={
            criticalBlockers > 0
              ? "Cannot promote until resolved"
              : "Clear to promote"
          }
        />
      </div>

      <div className="mt-4 grid gap-4 lg:grid-cols-3">
        <Card className="lg:col-span-2">
          <CardHeader>
            <CardTitle>Recommended demo actions</CardTitle>
          </CardHeader>
          <CardContent>
            <ListStack>
              <ListItem
                index={1}
                title="Run identity demo"
                description="Creates CAD/PDM and ERP batches, previews mapping, validates, stages, and generates identity candidates."
                action={runIdentityDemo}
                actionLabel="Run"
                actionVariant="primary"
              />
              <ListItem
                index={2}
                title="Approve first reviewable candidate"
                description="Exercises trusted identity link path and updates trust score."
                action={approveIdentityCandidate}
                actionLabel="Approve"
              />
              <ListItem
                index={3}
                title="Generate quality issues"
                description="Promotes validation findings into DataQualityIssueArtifact records."
                action={generateDataQualityIssues}
                actionLabel="Generate"
              />
            </ListStack>
          </CardContent>
        </Card>

        <TimelineCard title="Import state">
          <Timeline
            items={[
              {
                title: "Raw import received",
                description:
                  batches.length > 0
                    ? "Files stored as evidence artifacts"
                    : "Waiting for first batch",
              },
              {
                title: "Mapping preview ready",
                description: hasMapped
                  ? "Suggestions from active model package"
                  : "Awaiting mapping preview",
              },
              {
                title: "Staging graph created",
                description: hasStaged
                  ? "GraphSpace = Staging"
                  : "Not staged yet",
              },
              {
                title: "Trusted graph promotion",
                description: hasPromoted
                  ? "Promoted into trusted graph"
                  : criticalBlockers > 0
                    ? "Blocked by critical quality issue"
                    : "Ready when staging clears blockers",
              },
            ]}
          />
        </TimelineCard>
      </div>

      {lists.batches.error ? (
        <div className="mt-4">
          <ErrorState error={lists.batches.error} />
        </div>
      ) : batches.length > 0 ? (
        <Card className="mt-4">
          <CardHeader>
            <CardTitle>Import batches</CardTitle>
          </CardHeader>
          <CardContent>
            <div className="grid gap-3 md:grid-cols-2">{batches.map(BatchCard)}</div>
          </CardContent>
        </Card>
      ) : null}

      <details className="mt-6 rounded-etos-card border border-etos-border bg-etos-panel-muted p-4">
        <summary className="cursor-pointer text-sm font-extrabold text-etos-ink">
          Advanced / Debug
        </summary>
        <p className="mt-2 text-xs text-etos-ink-muted">
          Manual latest-batch tools, Mapping Agent Debug, promotion helpers, and
          raw panels. Not part of the primary import hub surface.
        </p>
        <div className="mt-4 grid gap-4 xl:grid-cols-2">
          <ButtonGroup
            title="Manual latest-batch tools"
            description="Operate on the newest batch only for step-by-step debugging."
          >
            <ActionButton action={createDemoImport}>
              Create CAD/PDM draft batch
            </ActionButton>
            <ActionButton action={createComparisonImport}>
              Create ERP draft batch
            </ActionButton>
            <ActionButton action={approveDraftMapping}>
              Approve latest draft mapping
            </ActionButton>
            <ActionButton action={validateBatch}>Validate latest batch</ActionButton>
            <ActionButton action={stageBatch}>Stage latest batch</ActionButton>
            <ActionButton action={generateIdentityCandidates}>
              Generate candidates
            </ActionButton>
            <ActionButton action={markIdentityCandidateConflicted}>
              Mark first candidate conflicted
            </ActionButton>
            <ActionButton action={promoteStagedBatch}>
              Promote ready staged batch
            </ActionButton>
            <ActionButton action={captureTrustedSnapshot}>
              Capture trusted graph snapshot
            </ActionButton>
            <ActionButton action={runBomComparison}>Run BOM comparison</ActionButton>
            <ActionButton action={createBomRecommendation}>
              Create recommendation from BOM
            </ActionButton>
            <ActionButton action={rejectStagedBatch}>
              Reject latest staged batch
            </ActionButton>
            <ActionButton action={createManualDataQualityIssue}>
              Create manual quality issue
            </ActionButton>
            <ActionButton action={createSecurityEventDataQualityIssue}>
              Create issue from security event
            </ActionButton>
          </ButtonGroup>
          <div className="space-y-4">
            <Link
              href="/imports/data-quality"
              className="inline-block text-sm font-extrabold text-etos-accent-cyan underline-offset-2 hover:underline"
            >
              Data quality triage →
            </Link>
            <Link
              href="/graph/promote"
              className="ml-4 inline-block text-sm font-extrabold text-etos-accent-cyan underline-offset-2 hover:underline"
            >
              Promotion workspace →
            </Link>
            <MappingAgentDebugPanel
              batchId={firstBatch?.id}
              evidenceId={firstEvidence?.id}
              runPreview={runMappingPreviewDebug}
            />
          </div>
        </div>
        <div className="mt-4 space-y-4">
          <BatchDetailPanels
            result={lists.firstBatchDetail}
            promotionRuns={lists.firstBatchPromotionRuns}
          />
          <IdentityResolutionPanel
            candidates={lists.firstBatchIdentityCandidates}
            trustScores={lists.firstBatchTrustScores}
          />
          <DataQualityPanel
            issues={lists.dataQualityIssues}
            monitoringPlaceholders={lists.monitoringPlaceholders}
          />
        </div>
      </details>
    </main>
  );
}
