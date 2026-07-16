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
  getAgentDefinitionArtifacts,
  getAgentRuns,
  type AgentVersionArtifactSummary,
} from "@/lib/etos-api";

export const dynamic = "force-dynamic";

type AgentRow = {
  id: string;
  name: string;
  agentKey: string;
  version: string;
  state: string;
  runtime: string;
  configureHref: string | null;
  testHref: string | null;
};

function readinessBucket(state?: string | null): "published" | "draft" | "blocked" | "ready" | "other" {
  const value = (state ?? "").toLowerCase();
  if (value.includes("publish")) return "published";
  if (value.includes("ready")) return "ready";
  if (value.includes("draft")) return "draft";
  if (value.includes("block") || value.includes("fail")) return "blocked";
  return "other";
}

function buildRows(agents: AgentVersionArtifactSummary[]): AgentRow[] {
  return agents.map((agent) => {
    const key = agent.agentKey ?? null;
    return {
      id: agent.id,
      name: agent.displayName ?? agent.name,
      agentKey: key ?? "—",
      version: agent.latestVersionLabel ?? "No version",
      state: agent.readinessState ?? "Unknown",
      runtime: agent.preferredRuntimeAdapterKey ?? "—",
      configureHref: key ? `/agents/${encodeURIComponent(key)}/configure` : null,
      testHref: key ? `/agents/${encodeURIComponent(key)}/test-run` : null,
    };
  });
}

type PageProps = {
  searchParams: Promise<{ error?: string; notice?: string }>;
};

export default async function AgentsPage({ searchParams }: PageProps) {
  const { error: queryError, notice } = await searchParams;
  const [agents, runs] = await Promise.all([getAgentDefinitionArtifacts(), getAgentRuns()]);
  const list = agents.data ?? [];
  const rows = buildRows(list);
  const published = list.filter((a) => readinessBucket(a.readinessState) === "published").length;
  const draft = list.filter((a) => readinessBucket(a.readinessState) === "draft").length;
  const blocked = list.filter((a) => readinessBucket(a.readinessState) === "blocked").length;
  const runList = runs.data ?? [];

  return (
    <main className="px-6 py-8 lg:px-8">
      <PageHeader
        title="Agent registry"
        description="Governed AgentVersion artifacts with draft / ready / published lifecycle, safe mode, and runtime adapter selection."
        actions={
          <>
            <Link href="/agents/new">
              <Button type="button">Create agent</Button>
            </Link>
            <Link href="/agent-runs">
              <Button type="button" variant="ghost">
                Agent runs
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
      {agents.error ? (
        <div className="mb-4">
          <ErrorState error={agents.error} />
        </div>
      ) : null}

      <div className="grid gap-4 md:grid-cols-4">
        <KpiCard label="Total agents" value={list.length} hint="Tenant AgentVersion artifacts" />
        <KpiCard label="Published" value={published} hint="Ready for governed execute" />
        <KpiCard label="Draft" value={draft} hint="Needs mark-ready / publish" />
        <KpiCard
          label="Blocked"
          value={blocked}
          trend={blocked > 0 ? "bad" : "flat"}
          trendLabel={blocked > 0 ? String(blocked) : undefined}
          hint="Readiness failures"
        />
      </div>

      <Card className="mt-4">
        <CardHeader>
          <CardTitle>Agents</CardTitle>
        </CardHeader>
        <CardContent>
          {rows.length === 0 ? (
            <EmptyState message="No tenant agents yet. Create one from a template or prompt." />
          ) : (
            <DataTable<AgentRow>
              rows={rows}
              rowKey={(row) => row.id}
              emptyMessage="No agents."
              columns={[
                {
                  key: "name",
                  header: "Name",
                  render: (row) =>
                    row.configureHref ? (
                      <Link href={row.configureHref} className="text-etos-accent hover:underline">
                        {row.name}
                      </Link>
                    ) : (
                      <span>{row.name}</span>
                    ),
                },
                {
                  key: "key",
                  header: "Agent key",
                  render: (row) => (
                    <span className="font-mono text-xs text-etos-ink-muted">{row.agentKey}</span>
                  ),
                },
                {
                  key: "version",
                  header: "Version",
                  render: (row) => (
                    <span className="font-mono text-xs text-etos-ink-muted">{row.version}</span>
                  ),
                },
                {
                  key: "runtime",
                  header: "Runtime",
                  render: (row) => <Badge variant="info">{row.runtime}</Badge>,
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
                      {row.configureHref ? (
                        <Link href={row.configureHref} className="text-etos-accent hover:underline">
                          Configure
                        </Link>
                      ) : null}
                      {row.testHref ? (
                        <Link href={row.testHref} className="text-etos-accent hover:underline">
                          Test
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
        <div className="mt-4 space-y-4">
          <div className="flex flex-wrap gap-3">
            <Link href="/agent-templates" className="text-etos-accent hover:underline">
              Agent templates
            </Link>
            <Link href="/agent-runs" className="text-etos-accent hover:underline">
              Agent runs ({runList.length})
            </Link>
          </div>
          <pre className="overflow-x-auto rounded-xl border border-etos-border bg-etos-panel p-3 text-xs">
            {JSON.stringify(list, null, 2)}
          </pre>
        </div>
      </details>
    </main>
  );
}
