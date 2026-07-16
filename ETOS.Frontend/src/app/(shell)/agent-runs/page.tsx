import Link from "next/link";
import { Badge, StatusBadge } from "@/components/ui/Badge";
import { Button } from "@/components/ui/Button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/Card";
import { DataTable } from "@/components/ui/DataTable";
import { EmptyState } from "@/components/ui/EmptyState";
import { ErrorState } from "@/components/ui/ErrorState";
import { KpiCard } from "@/components/ui/KpiCard";
import { PageHeader } from "@/components/ui/PageHeader";
import { getAgentRuns, type AgentRunSummary } from "@/lib/etos-api";

export const dynamic = "force-dynamic";

function modeLabel(run: AgentRunSummary) {
  if (run.isPreview) return "Preview";
  if (run.isDryRun) return "Dry-run";
  return "Execute";
}

function confidenceHint(run: AgentRunSummary) {
  if (run.aiTraceRecordId) return "Trace linked";
  if (run.isPreview) return "Preview only";
  return "—";
}

export default async function AgentRunsPage() {
  const runs = await getAgentRuns();
  const list = runs.data ?? [];
  const previews = list.filter((r) => r.isPreview).length;
  const dryRuns = list.filter((r) => r.isDryRun && !r.isPreview).length;
  const withTrace = list.filter((r) => Boolean(r.aiTraceRecordId)).length;

  return (
    <main className="px-6 py-8 lg:px-8">
      <PageHeader
        title="Agent runs explorer"
        description="Tenant AgentRun records from preview, dry-run test, and execute flows with safe summaries and AI Trace links."
        actions={
          <Link href="/agents">
            <Button type="button" variant="ghost">
              Agents
            </Button>
          </Link>
        }
      />

      {runs.error ? (
        <div className="mb-4">
          <ErrorState error={runs.error} />
        </div>
      ) : null}

      <div className="grid gap-4 md:grid-cols-4">
        <KpiCard label="Total runs" value={list.length} hint="All AgentRun records" />
        <KpiCard label="Previews" value={previews} hint="Preview-mode runs" />
        <KpiCard label="Dry-runs" value={dryRuns} hint="Test-run dry executions" />
        <KpiCard label="With AI Trace" value={withTrace} hint="Audit posture" />
      </div>

      <Card className="mt-4">
        <CardHeader>
          <CardTitle>Runs</CardTitle>
        </CardHeader>
        <CardContent>
          {list.length === 0 ? (
            <EmptyState message="No agent runs yet. Trigger preview or test-run from an agent." />
          ) : (
            <DataTable<AgentRunSummary>
              rows={list}
              rowKey={(row) => row.id}
              emptyMessage="No runs."
              columns={[
                {
                  key: "run",
                  header: "Run",
                  render: (row) => (
                    <Link
                      href={`/agent-runs/${row.id}`}
                      className="font-mono text-xs text-etos-accent hover:underline"
                    >
                      {row.id.slice(0, 8)}…
                    </Link>
                  ),
                },
                {
                  key: "agent",
                  header: "Agent version",
                  render: (row) => (
                    <span className="font-mono text-xs text-etos-ink-muted">
                      {row.agentVersionId.slice(0, 8)}…
                    </span>
                  ),
                },
                {
                  key: "invoked",
                  header: "Invoked by",
                  render: (row) => (
                    <span className="text-xs text-etos-ink-muted">{row.requestedByUserId || "—"}</span>
                  ),
                },
                {
                  key: "status",
                  header: "Status",
                  render: (row) => <StatusBadge status={row.status} />,
                },
                {
                  key: "mode",
                  header: "Mode",
                  render: (row) => (
                    <Badge variant={row.isPreview ? "info" : row.isDryRun ? "purple" : "success"}>
                      {modeLabel(row)}
                    </Badge>
                  ),
                },
                {
                  key: "confidence",
                  header: "Confidence",
                  render: (row) => (
                    <span className="text-xs text-etos-ink-muted">{confidenceHint(row)}</span>
                  ),
                },
                {
                  key: "trace",
                  header: "Trace",
                  render: (row) =>
                    row.aiTraceRecordId ? (
                      <Link
                        href={`/ai-traces/${row.aiTraceRecordId}`}
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
          )}
        </CardContent>
      </Card>
    </main>
  );
}
