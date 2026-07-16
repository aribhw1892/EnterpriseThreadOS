import Link from "next/link";
import { Badge, StatusBadge } from "@/components/ui/Badge";
import { Button } from "@/components/ui/Button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/Card";
import { DataTable } from "@/components/ui/DataTable";
import { EmptyState } from "@/components/ui/EmptyState";
import { ErrorState } from "@/components/ui/ErrorState";
import { KpiCard } from "@/components/ui/KpiCard";
import { PageHeader } from "@/components/ui/PageHeader";
import {
  getRecommendationArtifacts,
  type RecommendationArtifactSummary,
} from "@/lib/etos-api";

export const dynamic = "force-dynamic";

function isBlocked(artifact: RecommendationArtifactSummary) {
  const readiness = (artifact.readinessState ?? "").toLowerCase();
  const lifecycle = (artifact.lifecycleStatus ?? "").toLowerCase();
  return (
    readiness.includes("block") ||
    readiness.includes("denied") ||
    readiness.includes("conflict") ||
    lifecycle.includes("block") ||
    lifecycle.includes("conflict")
  );
}

function isTrusted(artifact: RecommendationArtifactSummary) {
  const readiness = (artifact.readinessState ?? "").toLowerCase();
  const lifecycle = (artifact.lifecycleStatus ?? "").toLowerCase();
  return (
    readiness.includes("publish") ||
    readiness.includes("ready") ||
    readiness.includes("trusted") ||
    lifecycle.includes("publish") ||
    lifecycle.includes("approved")
  );
}

function isHighRisk(artifact: RecommendationArtifactSummary) {
  const type = (artifact.recommendationType ?? "").toLowerCase();
  const readiness = (artifact.readinessState ?? "").toLowerCase();
  return (
    isBlocked(artifact) ||
    type.includes("high") ||
    type.includes("risk") ||
    readiness.includes("review") ||
    readiness.includes("warn")
  );
}

function trustLabel(artifact: RecommendationArtifactSummary) {
  if (isBlocked(artifact)) {
    return { label: "Blocked", variant: "danger" as const };
  }
  if (isTrusted(artifact)) {
    return { label: "Ready", variant: "success" as const };
  }
  return { label: "Review", variant: "warning" as const };
}

type PageProps = {
  searchParams: Promise<{ filter?: string }>;
};

export default async function RecommendationsPage({ searchParams }: PageProps) {
  const { filter } = await searchParams;
  const artifacts = await getRecommendationArtifacts();
  const all = artifacts.data ?? [];
  const draft = all.filter((a) =>
    (a.readinessState ?? "").toLowerCase().includes("draft"),
  );
  const ready = all.filter(isTrusted);
  const blocked = all.filter(isBlocked);
  const highRisk = all.filter(isHighRisk);

  const displayRows = filter === "high-risk" ? highRisk : all;

  return (
    <main className="px-6 py-8 lg:px-8">
      <PageHeader
        title="Recommendation inbox"
        description="Evidence-backed recommendations created from BOM comparison, data quality, chat, dashboards, and future agents."
        actions={
          <>
            <Link href="/chat">
              <Button variant="primary">Create recommendation</Button>
            </Link>
            <Link
              href={
                filter === "high-risk"
                  ? "/recommendations"
                  : "/recommendations?filter=high-risk"
              }
            >
              <Button variant="ghost">
                {filter === "high-risk" ? "Clear filter" : "Filter high risk"}
              </Button>
            </Link>
          </>
        }
      />

      {artifacts.error ? <ErrorState error={artifacts.error} /> : null}

      <div className="mb-4 grid gap-4 md:grid-cols-4">
        <KpiCard label="Draft" value={draft.length} hint="Awaiting evidence review" />
        <KpiCard label="Ready" value={ready.length} hint="Can create review tasks" />
        <KpiCard
          label="Blocked"
          value={blocked.length}
          trend="bad"
          trendLabel={blocked.length.toString()}
          hint="Conflicted/unverified evidence"
        />
        <KpiCard
          label="High risk"
          value={highRisk.length}
          trend="warn"
          trendLabel={highRisk.length.toString()}
          hint="Needs owner assignment"
        />
      </div>

      <Card>
        <CardHeader>
          <CardTitle>
            Recommendations
            {filter === "high-risk" ? (
              <span className="ml-2 text-sm font-normal text-etos-warning-fg">
                · high risk filter
              </span>
            ) : null}
          </CardTitle>
        </CardHeader>
        <CardContent>
          {displayRows.length > 0 ? (
            <DataTable<RecommendationArtifactSummary>
              rows={displayRows}
              rowKey={(row) => row.id}
              emptyMessage="No recommendations match the filter."
              columns={[
                {
                  key: "name",
                  header: "Recommendation",
                  render: (row) => (
                    <Link
                      href={`/recommendations/${row.id}`}
                      className="font-extrabold text-etos-accent hover:underline"
                    >
                      {row.name}
                    </Link>
                  ),
                },
                {
                  key: "type",
                  header: "Type",
                  render: (row) => (
                    <span className="text-etos-ink-muted">
                      {row.recommendationType ?? row.artifactType}
                    </span>
                  ),
                },
                {
                  key: "evidence",
                  header: "Evidence",
                  render: () => (
                    <span className="text-xs text-etos-ink-subtle">
                      Graph, import, trace
                    </span>
                  ),
                },
                {
                  key: "trust",
                  header: "Trust",
                  render: (row) => {
                    const trust = trustLabel(row);
                    return <Badge variant={trust.variant}>{trust.label}</Badge>;
                  },
                },
                {
                  key: "state",
                  header: "State",
                  render: (row) =>
                    row.readinessState ? (
                      <StatusBadge status={row.readinessState} />
                    ) : (
                      <StatusBadge status="unknown" />
                    ),
                },
              ]}
            />
          ) : (
            <EmptyState message="No recommendations yet. Create one from governed chat, data quality, BOM comparison, or API." />
          )}
        </CardContent>
      </Card>

      <details className="mt-6 rounded-etos-card border border-etos-border bg-etos-panel-muted p-4 text-sm text-etos-ink-muted">
        <summary className="cursor-pointer font-extrabold text-etos-ink">
          Advanced / Debug
        </summary>
        <div className="mt-4 space-y-2 text-xs">
          <p>Total: {all.length}</p>
          <p>
            Draft: {draft.length} · Ready: {ready.length} · Blocked:{" "}
            {blocked.length} · High risk: {highRisk.length}
          </p>
        </div>
      </details>
    </main>
  );
}
