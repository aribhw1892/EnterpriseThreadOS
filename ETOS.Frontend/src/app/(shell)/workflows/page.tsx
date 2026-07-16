import Link from "next/link";
import { Badge, StatusBadge } from "@/components/ui/Badge";
import { Button } from "@/components/ui/Button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/Card";
import { DataTable } from "@/components/ui/DataTable";
import { EmptyState } from "@/components/ui/EmptyState";
import { ErrorState } from "@/components/ui/ErrorState";
import { KpiCard } from "@/components/ui/KpiCard";
import { Notice } from "@/components/ui/Notice";
import { PageHeader } from "@/components/ui/PageHeader";
import {
  getWorkflowDefinitionArtifacts,
  getWorkflowRuns,
  type WorkflowVersionArtifactSummary,
} from "@/lib/etos-api";

export const dynamic = "force-dynamic";

type WorkflowRow = {
  id: string;
  name: string;
  workflowKey: string;
  scope: string;
  version: string;
  state: string;
  editHref: string | null;
  publishHref: string | null;
};

function readinessBucket(state?: string | null): "published" | "draft" | "blocked" | "ready" | "other" {
  const value = (state ?? "").toLowerCase();
  if (value.includes("publish")) return "published";
  if (value.includes("ready")) return "ready";
  if (value.includes("draft")) return "draft";
  if (value.includes("block") || value.includes("fail")) return "blocked";
  return "other";
}

function buildRows(workflows: WorkflowVersionArtifactSummary[]): WorkflowRow[] {
  return workflows.map((wf) => {
    const key = wf.workflowKey ?? null;
    return {
      id: wf.id,
      name: wf.displayName ?? wf.name,
      workflowKey: key ?? "—",
      scope: wf.workflowScope ?? "—",
      version: wf.latestVersionLabel ?? "No version",
      state: wf.readinessState ?? "Unknown",
      editHref: key ? `/workflows/${encodeURIComponent(key)}/edit` : null,
      publishHref: key ? `/workflows/${encodeURIComponent(key)}/publish` : null,
    };
  });
}

type PageProps = {
  searchParams: Promise<{ error?: string; notice?: string }>;
};

export default async function WorkflowsPage({ searchParams }: PageProps) {
  const { error: queryError, notice } = await searchParams;
  const [workflows, runs] = await Promise.all([
    getWorkflowDefinitionArtifacts(),
    getWorkflowRuns(),
  ]);
  const list = workflows.data ?? [];
  const rows = buildRows(list);
  const published = list.filter((w) => readinessBucket(w.readinessState) === "published").length;
  const draft = list.filter((w) => readinessBucket(w.readinessState) === "draft").length;
  const runList = runs.data ?? [];

  return (
    <main className="px-6 py-8 lg:px-8">
      <PageHeader
        title="Workflow registry"
        description="Governed WorkflowVersion artifacts with step graphs, safe mode, and runtime execution through agents, tools, and policies."
        actions={
          <>
            <Link href="/workflows/new">
              <Button type="button">Create workflow</Button>
            </Link>
            <Link href="/workflow-runs">
              <Button type="button" variant="ghost">
                Workflow runs
              </Button>
            </Link>
          </>
        }
      />

      {queryError ? (
        <div className="mb-4">
          <Notice variant="danger">{queryError}</Notice>
        </div>
      ) : null}
      {notice ? (
        <div className="mb-4">
          <Notice variant="info">{notice}</Notice>
        </div>
      ) : null}
      {workflows.error ? (
        <div className="mb-4">
          <ErrorState error={workflows.error} />
        </div>
      ) : null}

      <div className="grid gap-4 md:grid-cols-4">
        <KpiCard label="Workflows" value={list.length} hint="Tenant WorkflowVersion artifacts" />
        <KpiCard label="Published" value={published} hint="Executable definitions" />
        <KpiCard label="Draft" value={draft} hint="Needs mark-ready / publish" />
        <KpiCard label="Recent runs" value={runList.length} hint="WorkflowRun records" />
      </div>

      <Card className="mt-4">
        <CardHeader>
          <CardTitle>Workflows</CardTitle>
        </CardHeader>
        <CardContent>
          {rows.length === 0 ? (
            <EmptyState message="No tenant workflows yet. Create a draft or install a package." />
          ) : (
            <DataTable<WorkflowRow>
              rows={rows}
              rowKey={(row) => row.id}
              emptyMessage="No workflows."
              columns={[
                {
                  key: "name",
                  header: "Name",
                  render: (row) =>
                    row.editHref ? (
                      <Link href={row.editHref} className="text-etos-accent hover:underline">
                        {row.name}
                      </Link>
                    ) : (
                      <span>{row.name}</span>
                    ),
                },
                {
                  key: "key",
                  header: "Key",
                  render: (row) => (
                    <span className="font-mono text-xs text-etos-ink-muted">{row.workflowKey}</span>
                  ),
                },
                {
                  key: "scope",
                  header: "Scope",
                  render: (row) => <Badge variant="info">{row.scope}</Badge>,
                },
                {
                  key: "version",
                  header: "Version",
                  render: (row) => (
                    <span className="font-mono text-xs text-etos-ink-muted">{row.version}</span>
                  ),
                },
                {
                  key: "state",
                  header: "State",
                  render: (row) => <StatusBadge status={row.state} />,
                },
                {
                  key: "actions",
                  header: "Actions",
                  render: (row) => (
                    <div className="flex flex-wrap gap-2 text-xs">
                      {row.editHref ? (
                        <Link href={row.editHref} className="text-etos-accent hover:underline">
                          Edit
                        </Link>
                      ) : null}
                      {row.publishHref ? (
                        <Link href={row.publishHref} className="text-etos-accent hover:underline">
                          Publish
                        </Link>
                      ) : null}
                    </div>
                  ),
                },
              ]}
            />
          )}
        </CardContent>
      </Card>

      <details className="mt-6 rounded-etos-card border border-etos-border bg-etos-panel-muted p-4 text-sm text-etos-ink-muted">
        <summary className="cursor-pointer font-extrabold text-etos-ink">Advanced / Debug</summary>
        <div className="mt-4 space-y-3">
          <Link href="/workflow-runs" className="text-etos-accent hover:underline">
            Workflow runs list ({runList.length})
          </Link>
          {runs.error ? <p className="text-etos-warning-fg">{runs.error}</p> : null}
          <pre className="overflow-x-auto rounded-xl border border-etos-border bg-etos-panel p-3 text-xs">
            {JSON.stringify(list, null, 2)}
          </pre>
        </div>
      </details>
    </main>
  );
}
