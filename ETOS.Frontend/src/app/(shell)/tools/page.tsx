import Link from "next/link";
import { Suspense } from "react";
import { compatibilityScanFirstToolAction } from "@/app/(shell)/tools/actions";
import { Badge, StatusBadge } from "@/components/ui/Badge";
import { Button } from "@/components/ui/Button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/Card";
import { DataTable } from "@/components/ui/DataTable";
import { EmptyState } from "@/components/ui/EmptyState";
import { ErrorState } from "@/components/ui/ErrorState";
import { KpiCard } from "@/components/ui/KpiCard";
import { Notice } from "@/components/ui/Notice";
import { PageHeader } from "@/components/ui/PageHeader";
import { Tabs } from "@/components/ui/Tabs";
import {
  getConnectorDefinitionArtifacts,
  getSkillDefinitionArtifacts,
  getToolDefinitionArtifacts,
  getToolRuns,
  type ConnectorDefinitionArtifactSummary,
  type SkillDefinitionArtifactSummary,
  type ToolDefinitionArtifactSummary,
} from "@/lib/etos-api";

export const dynamic = "force-dynamic";

type RegistryKind = "all" | "tools" | "skills" | "connectors";

type RegistryRow = {
  id: string;
  name: string;
  kind: "Tool" | "Skill" | "Connector";
  kindLabel: string;
  risk: string;
  schemas: string;
  state: string;
  href: string | null;
};

function isSchemaFailure(readiness?: string | null) {
  const value = (readiness ?? "").toLowerCase();
  return (
    value.includes("fail") ||
    value.includes("block") ||
    value.includes("incompat") ||
    value.includes("conflict")
  );
}

function riskVariant(risk: string) {
  const value = risk.toLowerCase();
  if (value.includes("high") || value.includes("disabled") || value.includes("write")) {
    return "danger" as const;
  }
  if (value.includes("medium") || value.includes("warn")) {
    return "warning" as const;
  }
  if (value.includes("low") || value.includes("read")) {
    return "info" as const;
  }
  return "neutral" as const;
}

function buildRows(
  tools: ToolDefinitionArtifactSummary[],
  skills: SkillDefinitionArtifactSummary[],
  connectors: ConnectorDefinitionArtifactSummary[],
): RegistryRow[] {
  const toolRows: RegistryRow[] = tools.map((tool) => ({
    id: `tool-${tool.id}`,
    name: tool.name,
    kind: "Tool",
    kindLabel: "ToolDefinitionVersion",
    risk: tool.riskLevel ?? "—",
    schemas: tool.latestVersionLabel ?? "No version",
    state: tool.readinessState ?? "Unknown",
    href: `/tools/${tool.id}/edit`,
  }));

  const skillRows: RegistryRow[] = skills.map((skill) => ({
    id: `skill-${skill.id}`,
    name: skill.name,
    kind: "Skill",
    kindLabel: "SkillDefinitionVersion",
    risk: "Low",
    schemas: skill.latestVersionLabel ?? "No version",
    state: skill.readinessState ?? "Unknown",
    href: null,
  }));

  const connectorRows: RegistryRow[] = connectors.map((connector) => ({
    id: `connector-${connector.id}`,
    name: connector.name,
    kind: "Connector",
    kindLabel: "ConnectorDefinitionVersion",
    risk:
      connector.executionEnabled === false
        ? "Disabled"
        : connector.connectorKind ?? "Read-only",
    schemas: connector.latestVersionLabel ?? "No version",
    state:
      connector.executionEnabled === false
        ? connector.readinessState ?? "Disabled"
        : connector.readinessState ?? "Unknown",
    href: `/connectors/${connector.id}`,
  }));

  return [...toolRows, ...skillRows, ...connectorRows];
}

type PageProps = {
  searchParams: Promise<{ kind?: string; error?: string; notice?: string }>;
};

export default async function ToolsPage({ searchParams }: PageProps) {
  const { kind: kindParam, error: queryError, notice } = await searchParams;
  const kind: RegistryKind =
    kindParam === "tools" || kindParam === "skills" || kindParam === "connectors"
      ? kindParam
      : "all";

  const [tools, skills, connectors, runs] = await Promise.all([
    getToolDefinitionArtifacts(),
    getSkillDefinitionArtifacts(),
    getConnectorDefinitionArtifacts(),
    getToolRuns(),
  ]);

  const fetchError = tools.error ?? skills.error ?? connectors.error;
  const toolList = tools.data ?? [];
  const skillList = skills.data ?? [];
  const connectorList = connectors.data ?? [];
  const runList = runs.data ?? [];
  const allRows = buildRows(toolList, skillList, connectorList);
  const filteredRows =
    kind === "all"
      ? allRows
      : allRows.filter((row) => {
          if (kind === "tools") return row.kind === "Tool";
          if (kind === "skills") return row.kind === "Skill";
          return row.kind === "Connector";
        });

  const schemaFailures = [
    ...toolList.filter((t) => isSchemaFailure(t.readinessState)),
    ...skillList.filter((s) => isSchemaFailure(s.readinessState)),
    ...connectorList.filter((c) => isSchemaFailure(c.readinessState)),
  ].length;

  return (
    <main className="px-6 py-8 lg:px-8">
      <PageHeader
        title="Tool, skill & connector registry"
        description="Issue 22 registry surface for versioned tools, skills, connectors, schemas, capability/risk metadata, dry-run support, and disabled write contracts."
        actions={
          <>
            <Button type="button" disabled title="Create tool UI not wired — use admin API or package install.">
              Register tool
            </Button>
            <form action={compatibilityScanFirstToolAction}>
              <Button type="submit" variant="ghost" disabled={toolList.length === 0}>
                Run compatibility scan
              </Button>
            </form>
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
      {fetchError ? (
        <div className="mb-4">
          <ErrorState error={fetchError} />
        </div>
      ) : null}

      <div className="grid gap-4 md:grid-cols-4">
        <KpiCard label="Tool definitions" value={toolList.length} hint="Internal read-only tools" />
        <KpiCard label="Skills" value={skillList.length} hint="Reusable governed capabilities" />
        <KpiCard
          label="Connectors"
          value={connectorList.length}
          hint="Read-only + disabled writes"
        />
        <KpiCard
          label="Schema failures"
          value={schemaFailures}
          trend={schemaFailures > 0 ? "bad" : "flat"}
          trendLabel={schemaFailures > 0 ? String(schemaFailures) : undefined}
          hint="Blocked downstream execution"
        />
      </div>

      <Card className="mt-4">
        <CardHeader>
          <CardTitle>Registry</CardTitle>
        </CardHeader>
        <CardContent>
          <Suspense
            fallback={
              <div className="mb-4 h-10 animate-pulse rounded-xl bg-etos-panel-muted" />
            }
          >
            <Tabs
              paramName="kind"
              activeId={kind}
              items={[
                { id: "all", label: "All" },
                { id: "tools", label: "Tools" },
                { id: "skills", label: "Skills" },
                { id: "connectors", label: "Connectors" },
              ]}
            />
          </Suspense>

          <div className="mt-4">
            {filteredRows.length === 0 ? (
              <EmptyState message="No registry entries for this filter. Install the manufacturing reference package or create definitions via the admin API." />
            ) : (
              <DataTable<RegistryRow>
                rows={filteredRows}
                rowKey={(row) => row.id}
                emptyMessage="No registry entries."
                columns={[
                  {
                    key: "name",
                    header: "Name",
                    render: (row) =>
                      row.href ? (
                        <Link
                          href={row.href}
                          className="text-etos-accent hover:underline"
                        >
                          {row.name}
                        </Link>
                      ) : (
                        <span title="Skill detail route not implemented yet">{row.name}</span>
                      ),
                  },
                  {
                    key: "kind",
                    header: "Kind",
                    render: (row) => (
                      <span className="text-etos-ink-muted">{row.kindLabel}</span>
                    ),
                  },
                  {
                    key: "risk",
                    header: "Risk",
                    render: (row) => (
                      <Badge variant={riskVariant(row.risk)}>{row.risk}</Badge>
                    ),
                  },
                  {
                    key: "schemas",
                    header: "Schemas",
                    render: (row) => (
                      <span className="font-mono text-xs text-etos-ink-muted">
                        {row.schemas}
                      </span>
                    ),
                  },
                  {
                    key: "state",
                    header: "State",
                    render: (row) => <StatusBadge status={row.state} />,
                  },
                ]}
              />
            )}
          </div>
        </CardContent>
      </Card>

      <details className="mt-6 rounded-etos-card border border-etos-border bg-etos-panel-muted p-4 text-sm text-etos-ink-muted">
        <summary className="cursor-pointer font-extrabold text-etos-ink">
          Advanced / Debug
        </summary>
        <div className="mt-4 space-y-4">
          <p>
            Register tool create wizard is deferred — definitions come from package install or{" "}
            <code className="font-mono text-xs">POST /api/admin/tools</code>. Skills have no
            detail route yet.
          </p>
          <div className="flex flex-wrap gap-3">
            <Link href="/tool-runs" className="text-etos-accent hover:underline">
              Tool runs ({runList.length})
            </Link>
            {runs.error ? (
              <span className="text-etos-warning-fg">Runs: {runs.error}</span>
            ) : null}
          </div>
          {runList.length > 0 ? (
            <ul className="space-y-1 font-mono text-xs">
              {runList.slice(0, 8).map((run) => (
                <li key={run.id}>
                  <Link href={`/tool-runs/${run.id}`} className="text-etos-accent hover:underline">
                    {run.id}
                  </Link>{" "}
                  · {run.isDryRun ? "dry-run" : "execute"} · {run.status}
                </li>
              ))}
            </ul>
          ) : null}
          <pre className="overflow-x-auto rounded-xl border border-etos-border bg-etos-panel p-3 text-xs">
            {JSON.stringify(
              {
                tools: toolList,
                skills: skillList,
                connectors: connectorList,
              },
              null,
              2,
            )}
          </pre>
        </div>
      </details>
    </main>
  );
}
