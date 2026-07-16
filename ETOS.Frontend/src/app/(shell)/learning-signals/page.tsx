import Link from "next/link";
import { StatusBadge } from "@/components/ui/Badge";
import { Button } from "@/components/ui/Button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/Card";
import { DataTable } from "@/components/ui/DataTable";
import { EmptyState } from "@/components/ui/EmptyState";
import { ErrorState } from "@/components/ui/ErrorState";
import { KpiCard } from "@/components/ui/KpiCard";
import { PageHeader } from "@/components/ui/PageHeader";
import {
  listLearningSignals,
  type LearningSignalSummary,
} from "@/lib/etos-api";

export const dynamic = "force-dynamic";

type PageProps = {
  searchParams: Promise<{ status?: string; patternKey?: string }>;
};

export default async function LearningSignalsPage({ searchParams }: PageProps) {
  const filters = await searchParams;
  const signals = await listLearningSignals({
    status: filters.status,
    patternKey: filters.patternKey,
  });
  const rows = signals.data ?? [];
  const activeCount = rows.filter((row) =>
    row.status.toLowerCase() === "active",
  ).length;
  const distinctPatterns = new Set(rows.map((row) => row.patternKey)).size;
  const totalOccurrences = rows.reduce(
    (sum, row) => sum + (row.occurrenceCount || 0),
    0,
  );
  const linkedDecisions = new Set(
    rows.flatMap((row) => row.sourceDecisionIds ?? []),
  ).size;

  return (
    <main className="px-6 py-8 lg:px-8">
      <PageHeader
        title="Learning signals"
        description="Tenant-scoped rollups from repeated decision evidence patterns. Signals appear after the rollup threshold is met for a pattern key."
        actions={
          <>
            <Link href="/governance">
              <Button variant="ghost">Governance</Button>
            </Link>
            <Link href="/decisions">
              <Button variant="ghost">Decisions</Button>
            </Link>
            <Link href="/tasks">
              <Button variant="ghost">Review tasks</Button>
            </Link>
          </>
        }
      />

      {signals.error ? <ErrorState error={signals.error} /> : null}

      <div className="mb-4 grid gap-4 md:grid-cols-4">
        <KpiCard label="Signals" value={rows.length} hint="This tenant" />
        <KpiCard label="Active" value={activeCount} hint="status = active" />
        <KpiCard
          label="Patterns"
          value={distinctPatterns}
          hint="Distinct pattern keys"
        />
        <KpiCard
          label="Linked decisions"
          value={linkedDecisions}
          hint={`${totalOccurrences} evidence occurrences`}
        />
      </div>

      <Card className="mb-4">
        <CardHeader>
          <CardTitle>Filters</CardTitle>
        </CardHeader>
        <CardContent>
          <form className="grid gap-3 md:grid-cols-3">
            <label className="flex flex-col gap-1 text-sm">
              <span className="font-semibold text-etos-ink-muted">Status</span>
              <input
                name="status"
                defaultValue={filters.status ?? ""}
                placeholder="active"
                className="rounded-xl border border-etos-border bg-etos-panel px-3 py-2 text-etos-ink"
              />
            </label>
            <label className="flex flex-col gap-1 text-sm">
              <span className="font-semibold text-etos-ink-muted">
                Pattern key
              </span>
              <input
                name="patternKey"
                defaultValue={filters.patternKey ?? ""}
                placeholder="manual:accept"
                className="rounded-xl border border-etos-border bg-etos-panel px-3 py-2 text-etos-ink"
              />
            </label>
            <div className="flex items-end gap-2">
              <Button type="submit" variant="primary">
                Apply
              </Button>
              <Link href="/learning-signals">
                <Button variant="ghost">Clear</Button>
              </Link>
            </div>
          </form>
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>Signals for this tenant</CardTitle>
        </CardHeader>
        <CardContent>
          {rows.length === 0 && !signals.error ? (
            <EmptyState message="No learning signals yet. Finalize similar decisions until the rollup threshold is met (default: 3 matching evidence rows in 30 days)." />
          ) : (
            <DataTable<LearningSignalSummary>
              rows={rows}
              rowKey={(row) => row.artifactId}
              emptyMessage="No learning signals match the filter."
              columns={[
                {
                  key: "pattern",
                  header: "Pattern",
                  render: (row) => (
                    <Link
                      href={`/learning-signals/${row.artifactId}`}
                      className="font-extrabold text-etos-accent hover:underline"
                    >
                      {row.patternKey || row.name}
                    </Link>
                  ),
                },
                {
                  key: "occurrences",
                  header: "Occurrences",
                  render: (row) => (
                    <span className="text-etos-ink-muted">
                      {row.occurrenceCount}
                    </span>
                  ),
                },
                {
                  key: "status",
                  header: "Status",
                  render: (row) => <StatusBadge status={row.status} />,
                },
                {
                  key: "summary",
                  header: "Summary",
                  render: (row) => (
                    <span className="text-etos-ink-muted">{row.summary}</span>
                  ),
                },
                {
                  key: "updated",
                  header: "Updated",
                  render: (row) => (
                    <span className="text-xs text-etos-ink-subtle">
                      {new Date(row.updatedAt).toLocaleString()}
                    </span>
                  ),
                },
              ]}
            />
          )}
        </CardContent>
      </Card>
    </main>
  );
}
