import Link from "next/link";
import { Badge, StatusBadge } from "@/components/ui/Badge";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/Card";
import { DataTable } from "@/components/ui/DataTable";
import { EmptyState } from "@/components/ui/EmptyState";
import { ErrorState } from "@/components/ui/ErrorState";
import { PageHeader } from "@/components/ui/PageHeader";
import { getToolRuns, type ToolRunSummary } from "@/lib/etos-api";

export const dynamic = "force-dynamic";

export default async function ToolRunsPage() {
  const runs = await getToolRuns();
  const list = runs.data ?? [];

  return (
    <main className="px-6 py-8 lg:px-8">
      <PageHeader
        title="Tool runs"
        description="Dry-run and execution history for governed ToolDefinitionVersion artifacts."
        actions={
          <Link href="/tools">
            <span className="inline-flex items-center rounded-etos-button border border-etos-border bg-etos-panel-muted px-3.5 py-2.5 text-[13px] font-extrabold text-etos-ink">
              Back to registry
            </span>
          </Link>
        }
      />

      {runs.error ? (
        <div className="mb-4">
          <ErrorState error={runs.error} />
        </div>
      ) : null}

      <Card>
        <CardHeader>
          <CardTitle>Recent runs</CardTitle>
        </CardHeader>
        <CardContent>
          {list.length === 0 ? (
            <EmptyState message="No tool runs yet. Use Dry-run from a tool editor." />
          ) : (
            <DataTable<ToolRunSummary>
              rows={list}
              rowKey={(row) => row.id}
              emptyMessage="No tool runs."
              columns={[
                {
                  key: "id",
                  header: "Run",
                  render: (row) => (
                    <Link
                      href={`/tool-runs/${row.id}`}
                      className="font-mono text-xs text-etos-accent hover:underline"
                    >
                      {row.id.slice(0, 8)}…
                    </Link>
                  ),
                },
                {
                  key: "mode",
                  header: "Mode",
                  render: (row) => (
                    <Badge variant={row.isDryRun ? "info" : "purple"}>
                      {row.isDryRun ? "Dry-run" : "Execute"}
                    </Badge>
                  ),
                },
                {
                  key: "status",
                  header: "Status",
                  render: (row) => <StatusBadge status={row.status} />,
                },
                {
                  key: "summary",
                  header: "Input summary",
                  render: (row) => (
                    <span className="line-clamp-2 text-etos-ink-muted">
                      {row.inputSafeSummary || "—"}
                    </span>
                  ),
                },
                {
                  key: "created",
                  header: "Created",
                  render: (row) => (
                    <span className="text-xs text-etos-ink-subtle">
                      {new Date(row.createdAt).toLocaleString()}
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
