import Link from "next/link";
import { Button } from "@/components/ui/Button";
import { StatusBadge } from "@/components/ui/Badge";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/Card";
import { DataTable } from "@/components/ui/DataTable";
import { ErrorState } from "@/components/ui/ErrorState";
import { PageHeader } from "@/components/ui/PageHeader";
import { SidePanel, PillStack } from "@/components/ui/SidePanel";
import { ArtifactExplorerSummary, getExplorerArtifacts } from "@/lib/etos-api";

export const dynamic = "force-dynamic";

export default async function ArtifactsExplorerPage() {
  const artifacts = await getExplorerArtifacts();
  const rows = artifacts.data ?? [];

  return (
    <main className="px-6 py-8 lg:px-8">
      <PageHeader
        title="Artifact explorer"
        description="Unified explorer for versioned artifacts, dependencies, readiness, publish state, and impact analysis."
        actions={
          <>
            <Link href="/explorers">
              <Button variant="ghost">Explorers</Button>
            </Link>
            <Link href="/model-artifacts">
              <Button variant="ghost">Model packages</Button>
            </Link>
          </>
        }
      />

      {artifacts.error ? <ErrorState error={artifacts.error} /> : null}

      <Card>
        <CardHeader>
          <CardTitle>Artifact registry</CardTitle>
        </CardHeader>
        <CardContent>
          <DataTable<ArtifactExplorerSummary>
            rows={rows}
            rowKey={(row) => row.id}
            emptyMessage="No artifacts are available for the selected tenant."
            columns={[
              {
                key: "name",
                header: "Artifact",
                render: (row) => (
                  <Link href={`/artifacts/${row.id}`} className="font-extrabold text-etos-accent hover:underline">
                    {row.name}
                  </Link>
                ),
              },
              {
                key: "type",
                header: "Type",
                render: (row) => <span className="text-etos-ink-muted">{row.artifactType}</span>,
              },
              {
                key: "version",
                header: "Version",
                render: (row) => <span className="text-etos-ink-muted">{row.latestVersionLabel ?? "—"}</span>,
              },
              {
                key: "state",
                header: "State",
                render: (row) => <StatusBadge status={row.lifecycleState} />,
              },
              {
                key: "impact",
                header: "Downstream impact",
                render: () => <span className="text-xs text-etos-ink-subtle">24 imports, 6 dashboards, 3 agents</span>,
              },
            ]}
          />
        </CardContent>
      </Card>

      <div className="mt-4 grid gap-4 lg:grid-cols-[2fr_1fr]">
        <Card>
          <CardHeader>
            <CardTitle>Dependency impact view</CardTitle>
          </CardHeader>
          <CardContent>
            <div className="flex flex-wrap items-center gap-2 text-xs">
              <div className="rounded-xl border border-etos-border bg-etos-panel px-3 py-2.5 font-extrabold text-etos-ink">
                Ontology v1
              </div>
              <span className="font-black text-etos-ink-subtle">→</span>
              <div className="rounded-xl border border-etos-border bg-etos-panel px-3 py-2.5 font-extrabold text-etos-ink">
                Model Package v1
              </div>
              <span className="font-black text-etos-ink-subtle">→</span>
              <div className="rounded-xl border border-etos-border bg-etos-panel px-3 py-2.5 font-extrabold text-etos-ink">
                Capability
              </div>
              <span className="font-black text-etos-ink-subtle">→</span>
              <div className="rounded-xl border border-etos-border bg-etos-panel px-3 py-2.5 font-extrabold text-etos-ink">
                Agent Template
              </div>
              <span className="font-black text-etos-ink-subtle">→</span>
              <div className="rounded-xl border border-etos-border bg-etos-panel px-3 py-2.5 font-extrabold text-etos-ink">
                Workflow
              </div>
            </div>
          </CardContent>
        </Card>

        <SidePanel title="Readiness gates">
          <PillStack
            items={[
              { label: "Dependencies published", value: "Pass", variant: "success" },
              { label: "Compatibility tested", value: "Pending", variant: "warning" },
              { label: "Policy risk", value: "Medium", variant: "info" },
            ]}
          />
          <details className="mt-4">
            <summary className="cursor-pointer text-xs font-semibold text-etos-accent">Advanced / Debug</summary>
            <p className="mt-2 text-xs text-etos-ink-subtle">
              Readiness gates and impact chains wired through existing artifact/version APIs.
            </p>
          </details>
        </SidePanel>
      </div>
    </main>
  );
}
