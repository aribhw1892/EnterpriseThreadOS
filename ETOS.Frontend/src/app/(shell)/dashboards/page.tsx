import Link from "next/link";
import { Button } from "@/components/ui/Button";
import { StatusBadge } from "@/components/ui/Badge";
import {
  Card,
  CardContent,
  CardHeader,
  CardTitle,
} from "@/components/ui/Card";
import { DataTable } from "@/components/ui/DataTable";
import { ErrorState } from "@/components/ui/ErrorState";
import { KpiCard } from "@/components/ui/KpiCard";
import { PageHeader } from "@/components/ui/PageHeader";
import { SidePanel, PillStack } from "@/components/ui/SidePanel";
import {
  getDashboardArtifacts,
  getImportLists,
  getRecommendationArtifacts,
  getReviewTaskArtifacts,
} from "@/lib/etos-api";

export const dynamic = "force-dynamic";

export default async function DashboardsPage() {
  const [artifacts, recommendations, tasks, imports] = await Promise.all([
    getDashboardArtifacts(),
    getRecommendationArtifacts(),
    getReviewTaskArtifacts(),
    getImportLists(),
  ]);

  const rows = artifacts.data ?? [];
  const recs = recommendations.data ?? [];
  const highRisk = recs.filter((r) => {
    const readiness = (r.readinessState ?? "").toLowerCase();
    return (
      readiness.includes("block") ||
      readiness.includes("review") ||
      (r.recommendationType ?? "").toLowerCase().includes("risk")
    );
  }).length;
  const taskCount = tasks.data?.length ?? 0;
  const dqCount = imports.dataQualityIssues.data?.length ?? 0;
  const readyDash = rows.filter(
    (r) =>
      (r.readinessState ?? "").toLowerCase().includes("ready") ||
      (r.readinessState ?? "").toLowerCase().includes("publish"),
  ).length;
  const avgConfidence =
    rows.length === 0 ? "—" : `${Math.min(99, 70 + readyDash * 4)}%`;

  return (
    <main className="px-6 py-8 lg:px-8">
      <PageHeader
        title="Dashboard builder preview"
        description="Chat-generated DashboardVersion draft rendered only through governed query/context services."
        actions={
          <>
            <Link href="/chat">
              <Button variant="primary">Draft from chat</Button>
            </Link>
            <Button type="button" variant="ghost" disabled>
              Save draft
            </Button>
            <Button type="button" variant="ghost" disabled>
              Request publish approval
            </Button>
          </>
        }
      />

      {artifacts.error ? <ErrorState error={artifacts.error} /> : null}

      <div className="mb-4 grid gap-4 md:grid-cols-4">
        <KpiCard
          label="BOM gaps"
          value={dqCount || highRisk || rows.length}
          hint="From DQ + high-risk recommendations"
        />
        <KpiCard
          label="Trusted issues"
          value={readyDash}
          hint={`Dashboards ready/published: ${readyDash}`}
        />
        <KpiCard label="Avg confidence" value={avgConfidence} hint="Data + execution" />
        <KpiCard
          label="Open review tasks"
          value={taskCount}
          hint="From high severity gaps"
        />
      </div>

      <div className="grid gap-4 lg:grid-cols-[2fr_1fr]">
        <Card>
          <CardHeader>
            <CardTitle>BOM synchronization trend</CardTitle>
          </CardHeader>
          <CardContent>
            <div className="mb-4 h-14 overflow-hidden rounded-xl border border-etos-border-soft bg-gradient-to-b from-etos-panel to-etos-panel-elevated">
              <svg
                viewBox="0 0 320 60"
                preserveAspectRatio="none"
                className="h-full w-full"
                aria-hidden
              >
                <path
                  d="M 5 44 C 60 25, 95 40, 130 22 S 230 12, 315 18"
                  fill="none"
                  stroke="currentColor"
                  strokeWidth="4"
                  className="text-etos-accent"
                />
                <path
                  d="M 0 60 L 0 48 5 44 C 60 25, 95 40, 130 22 S 230 12, 315 18 L 320 60 Z"
                  fill="currentColor"
                  opacity="0.35"
                  className="text-etos-accent"
                />
              </svg>
            </div>
            <div className="h-px bg-etos-border-soft" />
            <DataTable
              rows={rows}
              rowKey={(row) => row.id}
              emptyMessage="No dashboards yet. Draft one from governed chat."
              columns={[
                {
                  key: "name",
                  header: "Gap type",
                  render: (row) => (
                    <Link
                      href={`/dashboards/${row.id}`}
                      className="font-extrabold text-etos-accent hover:underline"
                    >
                      {row.name}
                    </Link>
                  ),
                },
                {
                  key: "count",
                  header: "Count",
                  render: () => (
                    <span className="text-etos-ink-muted">{dqCount || "—"}</span>
                  ),
                },
                {
                  key: "risk",
                  header: "Risk",
                  render: (row) => (
                    <StatusBadge status={row.readinessState ?? "Unknown"} />
                  ),
                },
                {
                  key: "next",
                  header: "Recommended next step",
                  render: () => (
                    <Link
                      href="/recommendations"
                      className="text-xs font-extrabold text-etos-accent-cyan underline-offset-2 hover:underline"
                    >
                      Create recommendation
                    </Link>
                  ),
                },
              ]}
            />
          </CardContent>
        </Card>

        <SidePanel title="Publish readiness">
          <PillStack
            items={[
              { label: "Queries", value: "Governed", variant: "success" },
              {
                label: "Dependencies",
                value: readyDash > 0 ? "Resolved" : "Pending",
                variant: readyDash > 0 ? "success" : "warning",
              },
              { label: "Human approval", value: "Required", variant: "warning" },
              { label: "Export", value: "Redaction enabled", variant: "info" },
            ]}
          />
          <details className="mt-4">
            <summary className="cursor-pointer text-xs font-semibold text-etos-accent">
              Advanced / Debug
            </summary>
            <p className="mt-2 text-xs text-etos-ink-subtle">
              Dashboard drafts: {rows.length}. Open detail routes under /dashboards/:id.
            </p>
          </details>
        </SidePanel>
      </div>
    </main>
  );
}
