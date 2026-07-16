import Link from "next/link";
import {
  ActionButton,
  ImportStepper,
} from "@/components/imports/ImportHubShared";
import { Badge, StatusBadge } from "@/components/ui/Badge";
import { Button } from "@/components/ui/Button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/Card";
import { DataTable } from "@/components/ui/DataTable";
import { ErrorState } from "@/components/ui/ErrorState";
import { Callout } from "@/components/ui/Notice";
import {
  promoteStagedBatch,
  rejectStagedBatch,
  stageBatch,
  validateBatch,
} from "@/app/(shell)/imports/actions";
import { getImportBatchDetail, getImportLists } from "@/lib/etos-api";

export const dynamic = "force-dynamic";

export default async function ImportStagingPage({
  params,
}: {
  params: Promise<{ batchId: string }>;
}) {
  const { batchId } = await params;
  const detail = await getImportBatchDetail(batchId);
  const lists = await getImportLists();
  const batch =
    detail.data?.batch ??
    lists.batches.data?.find((item) => item.id === batchId) ??
    null;
  const issues = detail.data?.validationIssues ?? [];
  const stagingRuns = detail.data?.stagingRuns ?? [];
  const latestRun = stagingRuns[0];
  const blockers = issues.filter((issue) => {
    const s = issue.severity.toLowerCase();
    return s === "error" || s === "critical" || s === "high";
  });

  return (
    <main className="px-6 py-8 lg:px-8">
      <div className="mb-[18px] flex flex-wrap items-start justify-between gap-4">
        <div>
          <h1 className="text-[30px] font-bold tracking-tight text-etos-ink">
            Staging graph validation
          </h1>
          <p className="mt-2 max-w-[900px] text-sm text-etos-ink-muted">
            Preview staged nodes, relationships, validation findings, and
            blocking status before committing to the trusted graph.
          </p>
        </div>
        <div className="flex flex-wrap gap-2.5">
          <Link href={`/imports/${batchId}/mapping`}>
            <Button variant="ghost">Back</Button>
          </Link>
          <Link href="/imports/data-quality">
            <Button variant="primary">Create review tasks</Button>
          </Link>
        </div>
      </div>

      <ImportStepper batch={batch} currentStepId="validate" />
      {detail.error ? <ErrorState error={detail.error} /> : null}

      <div className="mt-2 grid gap-4 lg:grid-cols-[1.2fr_0.8fr]">
        <Card>
          <CardHeader>
            <CardTitle>Staging graph preview</CardTitle>
          </CardHeader>
          <CardContent>
            <div className="relative min-h-[280px] overflow-hidden rounded-etos-card border border-etos-border bg-[radial-gradient(circle_at_24px_24px,var(--etos-info-border)_1px,transparent_1px)] bg-[length:24px_24px] bg-etos-panel-muted">
              <PreviewNode
                className="left-8 top-10 border-etos-info-border bg-etos-info-bg"
                title="PartVersion"
                subtitle={batch?.sourceSystem ?? "Source part"}
              />
              <PreviewNode
                className="left-[40%] top-10 border-etos-info-border bg-etos-info-bg"
                title="AssemblyVersion"
                subtitle={latestRun ? `Run ${latestRun.id.slice(0, 8)}` : "Pending"}
              />
              <PreviewNode
                className="right-8 top-10 border-etos-warning-border bg-etos-warning-bg"
                title="SourceRecord"
                subtitle={`${issues.length} findings`}
              />
              <PreviewNode
                className="left-[28%] top-[55%] border-etos-success-border bg-etos-success-bg"
                title="Staging run"
                subtitle={
                  latestRun
                    ? String(latestRun.status ?? "Created")
                    : "Not staged"
                }
              />
              <div className="absolute bottom-4 left-4 rounded-xl border border-etos-border bg-etos-panel/90 px-3 py-2 text-[11px] text-etos-ink-muted">
                Logical GraphSpace = Staging · Tenant scoped · ImportBatchId
                pinned
              </div>
            </div>
            <div className="mt-4 flex flex-wrap gap-3">
              <ActionButton action={validateBatch}>Validate latest batch</ActionButton>
              <ActionButton action={stageBatch}>Stage latest batch</ActionButton>
              <ActionButton action={promoteStagedBatch}>
                Promote ready staged batch
              </ActionButton>
              <ActionButton action={rejectStagedBatch}>
                Reject latest staged batch
              </ActionButton>
            </div>
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>Validation findings</CardTitle>
          </CardHeader>
          <CardContent>
            <DataTable
              rows={issues}
              rowKey={(row) => row.id}
              emptyMessage="No validation findings."
              columns={[
                {
                  key: "severity",
                  header: "Severity",
                  render: (row) => <StatusBadge status={row.severity} />,
                },
                {
                  key: "finding",
                  header: "Finding",
                  render: (row) => (
                    <div>
                      <p className="font-extrabold">{row.issueCode}</p>
                      <p className="text-xs text-etos-ink-muted">{row.message}</p>
                    </div>
                  ),
                },
                {
                  key: "effect",
                  header: "Effect",
                  render: (row) => {
                    const s = row.severity.toLowerCase();
                    if (s === "error" || s === "critical") {
                      return <Badge variant="danger">Blocks commit</Badge>;
                    }
                    if (s === "high" || s === "warning") {
                      return <Badge variant="warning">Approval required</Badge>;
                    }
                    return <Badge variant="neutral">Warning</Badge>;
                  },
                },
              ]}
            />
            {blockers.length > 0 ? (
              <Callout title="Promotion blockers" variant="warning" className="mt-4">
                {blockers.length} blocking finding(s). Only approved staging data
                can be promoted; rejected data keeps summaries and audit records.
              </Callout>
            ) : (
              <Callout title="Ready" variant="info" className="mt-4">
                Only approved staging data can be promoted; rejected data keeps
                summaries and audit records.
              </Callout>
            )}
          </CardContent>
        </Card>
      </div>
    </main>
  );
}

function PreviewNode({
  title,
  subtitle,
  className,
}: {
  title: string;
  subtitle: string;
  className: string;
}) {
  return (
    <div
      className={`absolute min-w-[150px] rounded-2xl border bg-etos-panel px-3.5 py-3 shadow-etos ${className}`}
    >
      <p className="text-[13px] font-extrabold text-etos-ink">{title}</p>
      <p className="mt-1 text-[11px] text-etos-ink-muted">{subtitle}</p>
    </div>
  );
}
