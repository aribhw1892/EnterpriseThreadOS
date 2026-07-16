import {
  AiTraceSummary,
  ApiResult,
  adminUserId,
  exportAiTrace,
  getAiTraceLists,
  runDemoGovernedQueryFlow,
  selectedTenantId,
} from "@/lib/etos-api";
import { Button } from "@/components/ui/Button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/Card";
import { DataTable } from "@/components/ui/DataTable";
import { ErrorState } from "@/components/ui/ErrorState";
import { KpiCard } from "@/components/ui/KpiCard";
import { PageHeader } from "@/components/ui/PageHeader";
import { StatusBadge } from "@/components/ui/Badge";
import Link from "next/link";
import { revalidatePath } from "next/cache";
import { redirect } from "next/navigation";

export const dynamic = "force-dynamic";

async function exportLatestTrace(formData: FormData) {
  "use server";

  const traceId = formData.get("traceId");
  if (typeof traceId !== "string" || traceId.length === 0) {
    return;
  }

  await exportAiTrace(traceId);
  revalidatePath("/ai-traces");
}

async function runDemoGovernedQuery() {
  "use server";

  const result = await runDemoGovernedQueryFlow();
  if (result.error) {
    redirect(`/ai-traces?error=${encodeURIComponent(result.error)}`);
  }

  revalidatePath("/ai-traces");
  redirect("/ai-traces");
}

function renderApiError(result: ApiResult<unknown>) {
  return result.error ? <ErrorState error={result.error} /> : null;
}

type PageProps = {
  searchParams: Promise<{ error?: string }>;
};

export default async function AiTracesPage({ searchParams }: PageProps) {
  const { error: actionError } = await searchParams;
  const { traces, latestTrace } = await getAiTraceLists();
  const rows = traces.data ?? [];
  const succeeded = rows.filter((t) =>
    String(t.status).toLowerCase().includes("succeed") ||
    String(t.status).toLowerCase().includes("complet") ||
    String(t.status).toLowerCase().includes("ok"),
  ).length;
  const denied = rows.filter((t) =>
    String(t.status).toLowerCase().includes("deni") ||
    String(t.status).toLowerCase().includes("fail"),
  ).length;

  return (
    <main className="px-6 py-8 lg:px-8">
      <PageHeader
        title="AI Trace explorer"
        description="Inspect governed retrieval traces, filtered and denied summaries, confidence impact, and export packages."
        actions={
          latestTrace.data ? (
            <Link href={`/ai-traces/${latestTrace.data.id}`}>
              <Button variant="primary">Open latest detail</Button>
            </Link>
          ) : undefined
        }
      />

      {actionError ? <ErrorState error={actionError} /> : null}
      {renderApiError(traces)}
      {renderApiError(latestTrace)}

      <div className="mb-4 grid gap-4 md:grid-cols-4">
        <KpiCard label="Traces" value={rows.length} hint="Tenant retrieval audits" />
        <KpiCard label="Succeeded" value={succeeded} hint="Completed governed queries" />
        <KpiCard
          label="Denied / failed"
          value={denied}
          trend={denied > 0 ? "warn" : "flat"}
          trendLabel={denied > 0 ? String(denied) : undefined}
          hint="Policy or retrieval blockers"
        />
        <KpiCard
          label="Latest"
          value={latestTrace.data ? latestTrace.data.intentKey : "—"}
          hint={
            latestTrace.data
              ? new Date(latestTrace.data.createdAt).toLocaleString()
              : "Run chat or demo query"
          }
        />
      </div>

      <Card>
        <CardHeader>
          <CardTitle>Trace list</CardTitle>
        </CardHeader>
        <CardContent>
          <DataTable<AiTraceSummary>
            rows={rows}
            rowKey={(trace) => trace.id}
            emptyMessage="No AI traces yet. Ask in governed chat or run a demo query from Advanced."
            columns={[
              {
                key: "intent",
                header: "Intent",
                render: (trace) => (
                  <Link
                    href={`/ai-traces/${trace.id}`}
                    className="font-extrabold text-etos-accent hover:underline"
                  >
                    {trace.intentKey}
                  </Link>
                ),
              },
              {
                key: "summary",
                header: "Summary",
                render: (trace) => (
                  <span className="text-etos-ink-muted">{trace.safeSummary}</span>
                ),
              },
              {
                key: "strategy",
                header: "Strategy",
                render: (trace) => (
                  <span className="text-etos-ink-subtle">{trace.strategyKey}</span>
                ),
              },
              {
                key: "status",
                header: "Status",
                render: (trace) => <StatusBadge status={trace.status} />,
              },
              {
                key: "created",
                header: "Created",
                render: (trace) => new Date(trace.createdAt).toLocaleString(),
              },
            ]}
          />
        </CardContent>
      </Card>

      <details className="mt-6 rounded-etos-card border border-etos-border bg-etos-panel-muted p-4 text-sm text-etos-ink-muted">
        <summary className="cursor-pointer font-extrabold text-etos-ink">
          Advanced / Debug
        </summary>
        <div className="mt-4 flex flex-wrap items-center gap-3 text-xs">
          <p>
            Tenant {selectedTenantId} · User {adminUserId}
          </p>
          <form action={runDemoGovernedQuery}>
            <Button type="submit" variant="ghost">
              Run demo governed query
            </Button>
          </form>
          {latestTrace.data ? (
            <form action={exportLatestTrace}>
              <input type="hidden" name="traceId" value={latestTrace.data.id} />
              <Button type="submit" variant="ghost">
                Export latest trace package
              </Button>
            </form>
          ) : null}
        </div>
      </details>
    </main>
  );
}
