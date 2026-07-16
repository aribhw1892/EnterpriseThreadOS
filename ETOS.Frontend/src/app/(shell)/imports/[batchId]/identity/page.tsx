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
import {
  approveIdentityCandidate,
  generateIdentityCandidates,
  markIdentityCandidateConflicted,
} from "@/app/(shell)/imports/actions";
import {
  getIdentityCandidatesForBatch,
  getImportBatchDetail,
  getImportLists,
  getTrustScoresForBatch,
} from "@/lib/etos-api";

export const dynamic = "force-dynamic";

export default async function ImportIdentityPage({
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

  const candidatesResult = await getIdentityCandidatesForBatch(batchId);
  const trustResult = await getTrustScoresForBatch(batchId);
  const candidates = candidatesResult.data ?? [];
  const trustScores = trustResult.data ?? [];
  const selected = trustScores[0];
  const breakdown = selected?.breakdown ?? {};

  return (
    <main className="px-6 py-8 lg:px-8">
      <div className="mb-[18px] flex flex-wrap items-start justify-between gap-4">
        <div>
          <h1 className="text-[30px] font-bold tracking-tight text-etos-ink">
            Identity resolution review
          </h1>
          <p className="mt-2 max-w-[900px] text-sm text-etos-ink-muted">
            Review cross-system identity candidates with confidence, trust
            state, conflict state, and trusted-recommendation eligibility.
          </p>
        </div>
        <div className="flex flex-wrap gap-2.5">
          <Link href={`/imports/${batchId}/staging`}>
            <Button variant="ghost">Back</Button>
          </Link>
          <ActionButton action={generateIdentityCandidates}>
            Generate candidates
          </ActionButton>
        </div>
      </div>

      <ImportStepper batch={batch} currentStepId="identity" />
      {detail.error ? <ErrorState error={detail.error} /> : null}
      {candidatesResult.error ? (
        <ErrorState error={candidatesResult.error} />
      ) : null}

      <div className="mt-2 grid gap-4 lg:grid-cols-3">
        <Card className="lg:col-span-2">
          <CardHeader>
            <CardTitle>Identity candidates</CardTitle>
          </CardHeader>
          <CardContent>
            <DataTable
              rows={candidates}
              rowKey={(row) => row.id}
              emptyMessage="No identity candidates yet."
              columns={[
                {
                  key: "source",
                  header: "CAD/PDM record",
                  render: (row) => (
                    <div>
                      <p>{row.sourceRecordId}</p>
                      <p className="text-xs text-etos-ink-muted">
                        {row.sourceSystem}
                      </p>
                    </div>
                  ),
                },
                {
                  key: "target",
                  header: "ERP record",
                  render: (row) => (
                    <div>
                      <p>{row.targetRecordId}</p>
                      <p className="text-xs text-etos-ink-muted">
                        {row.targetSystem}
                      </p>
                    </div>
                  ),
                },
                {
                  key: "confidence",
                  header: "Confidence",
                  render: (row) => (
                    <Badge
                      variant={
                        row.confidenceScore >= 0.9
                          ? "success"
                          : row.confidenceScore >= 0.7
                            ? "warning"
                            : "danger"
                      }
                    >
                      {Math.round(row.confidenceScore * 100)}%
                    </Badge>
                  ),
                },
                {
                  key: "trust",
                  header: "Trust state",
                  render: (row) => (
                    <StatusBadge status={String(row.trustState)} />
                  ),
                },
                {
                  key: "rec",
                  header: "Recommendation use",
                  render: (row) =>
                    row.excludedFromTrustedRecommendations
                      ? "Excluded"
                      : "Allowed after approval",
                },
              ]}
            />
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>Trust score breakdown</CardTitle>
          </CardHeader>
          <CardContent>
            {Object.keys(breakdown).length === 0 ? (
              <p className="text-sm text-etos-ink-muted">
                Generate candidates to populate trust breakdown.
              </p>
            ) : (
              <div className="flex flex-col gap-2.5">
                {Object.entries(breakdown).map(([label, value]) => {
                  const numeric = Number(value);
                  const width = Math.min(100, Math.abs(numeric));
                  return (
                    <div
                      key={label}
                      className="grid grid-cols-[110px_1fr_36px] items-center gap-2.5 text-xs text-etos-ink-muted"
                    >
                      <span>{label}</span>
                      <div className="h-2 overflow-hidden rounded-full bg-etos-border-soft">
                        <span
                          className="block h-full rounded-full bg-gradient-to-r from-etos-accent to-etos-purple-fg"
                          style={{ width: `${width}%` }}
                        />
                      </div>
                      <b className="text-etos-ink">{numeric}</b>
                    </div>
                  );
                })}
              </div>
            )}
            <div className="my-3.5 h-px bg-etos-border" />
            <div className="flex flex-wrap gap-2.5">
              <ActionButton action={approveIdentityCandidate}>
                Approve link
              </ActionButton>
              <form action={markIdentityCandidateConflicted}>
                <Button type="submit" variant="danger">
                  Mark conflicted
                </Button>
              </form>
            </div>
          </CardContent>
        </Card>
      </div>
    </main>
  );
}
