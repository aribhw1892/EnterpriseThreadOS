import Link from "next/link";
import { StatusBadge } from "@/components/ui/Badge";
import { Button } from "@/components/ui/Button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/Card";
import { DataTable } from "@/components/ui/DataTable";
import { EmptyState } from "@/components/ui/EmptyState";
import { ErrorState } from "@/components/ui/ErrorState";
import { PageHeader } from "@/components/ui/PageHeader";
import {
  getLearningSignalDetail,
  type LearningEvidenceSummary,
} from "@/lib/etos-api";

export const dynamic = "force-dynamic";

type PageProps = {
  params: Promise<{ artifactId: string }>;
};

export default async function LearningSignalDetailPage({ params }: PageProps) {
  const { artifactId } = await params;
  const detail = await getLearningSignalDetail(artifactId);
  const signal = detail.data;

  return (
    <main className="px-6 py-8 lg:px-8">
      <PageHeader
        title={signal?.patternKey ?? "Learning signal"}
        description={
          signal?.summary ??
          "Tenant learning signal rolled up from repeated decision evidence."
        }
        actions={
          <>
            <Link href="/learning-signals">
              <Button variant="ghost">All signals</Button>
            </Link>
            <Link href="/decisions">
              <Button variant="ghost">Decisions</Button>
            </Link>
            <Link href={`/artifacts/${artifactId}`}>
              <Button variant="ghost">Artifact registry</Button>
            </Link>
          </>
        }
      />

      {detail.error ? <ErrorState error={detail.error} /> : null}

      {!signal && !detail.error ? (
        <EmptyState message="Learning signal was not found for this tenant." />
      ) : null}

      {signal ? (
        <div className="grid gap-4 lg:grid-cols-[2fr_1fr]">
          <div className="flex flex-col gap-4">
            <Card>
              <CardHeader>
                <CardTitle>Signal</CardTitle>
              </CardHeader>
              <CardContent className="space-y-3 text-sm">
                <div className="flex flex-wrap items-center gap-2">
                  <StatusBadge status={signal.status} />
                  <span className="text-etos-ink-muted">
                    {signal.occurrenceCount} occurrences
                  </span>
                </div>
                <dl className="grid gap-2 sm:grid-cols-2">
                  <div>
                    <dt className="text-xs font-extrabold uppercase tracking-wide text-etos-ink-muted">
                      Pattern key
                    </dt>
                    <dd className="mt-1 font-semibold text-etos-ink">
                      {signal.patternKey}
                    </dd>
                  </div>
                  <div>
                    <dt className="text-xs font-extrabold uppercase tracking-wide text-etos-ink-muted">
                      Name
                    </dt>
                    <dd className="mt-1 text-etos-ink">{signal.name}</dd>
                  </div>
                  <div>
                    <dt className="text-xs font-extrabold uppercase tracking-wide text-etos-ink-muted">
                      Created
                    </dt>
                    <dd className="mt-1 text-etos-ink-muted">
                      {new Date(signal.createdAt).toLocaleString()}
                    </dd>
                  </div>
                  <div>
                    <dt className="text-xs font-extrabold uppercase tracking-wide text-etos-ink-muted">
                      Updated
                    </dt>
                    <dd className="mt-1 text-etos-ink-muted">
                      {new Date(signal.updatedAt).toLocaleString()}
                    </dd>
                  </div>
                </dl>
              </CardContent>
            </Card>

            <Card>
              <CardHeader>
                <CardTitle>Related evidence</CardTitle>
              </CardHeader>
              <CardContent>
                <DataTable<LearningEvidenceSummary>
                  rows={signal.relatedEvidence}
                  rowKey={(row) => row.id}
                  emptyMessage="No decision learning evidence rows for this pattern."
                  columns={[
                    {
                      key: "outcome",
                      header: "Outcome",
                      render: (row) => (
                        <span className="font-extrabold text-etos-ink">
                          {row.outcomeKey}
                        </span>
                      ),
                    },
                    {
                      key: "source",
                      header: "Source",
                      render: (row) => (
                        <span className="text-etos-ink-muted">
                          {row.sourceType}
                        </span>
                      ),
                    },
                    {
                      key: "summary",
                      header: "Summary",
                      render: (row) => (
                        <span className="text-etos-ink-muted">
                          {row.evidenceSummary}
                        </span>
                      ),
                    },
                    {
                      key: "decision",
                      header: "Decision",
                      render: (row) =>
                        row.decisionArtifactId ? (
                          <Link
                            href={`/decisions/${row.decisionArtifactId}`}
                            className="text-etos-accent hover:underline"
                          >
                            Open
                          </Link>
                        ) : (
                          <span className="text-etos-ink-subtle">—</span>
                        ),
                    },
                  ]}
                />
              </CardContent>
            </Card>
          </div>

          <Card>
            <CardHeader>
              <CardTitle>Source decisions</CardTitle>
            </CardHeader>
            <CardContent>
              {signal.sourceDecisionIds.length === 0 ? (
                <EmptyState message="No source decision ids were stored on this signal." />
              ) : (
                <ul className="space-y-2 text-sm">
                  {signal.sourceDecisionIds.map((decisionId) => (
                    <li key={decisionId}>
                      <Link
                        href={`/decisions/${decisionId}`}
                        className="font-semibold text-etos-accent hover:underline"
                      >
                        {decisionId}
                      </Link>
                    </li>
                  ))}
                </ul>
              )}
            </CardContent>
          </Card>
        </div>
      ) : null}
    </main>
  );
}
