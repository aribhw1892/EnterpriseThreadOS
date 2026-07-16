import Link from "next/link";
import { Button } from "@/components/ui/Button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/Card";
import { ErrorState } from "@/components/ui/ErrorState";
import { KpiCard } from "@/components/ui/KpiCard";
import { Notice } from "@/components/ui/Notice";
import { PageHeader } from "@/components/ui/PageHeader";
import { PillStack, SidePanel } from "@/components/ui/SidePanel";
import { TraceTimeline, type TraceTimelineStep } from "@/components/ui/TraceTimeline";
import { getAgentRunDetail, getToolRunDetail, getToolRuns } from "@/lib/etos-api";

export const dynamic = "force-dynamic";

type PageProps = {
  params: Promise<{ runId: string }>;
};

async function loadChildToolRunIds(agentRunId: string): Promise<string[]> {
  const toolRuns = await getToolRuns();
  if (!toolRuns.data) {
    return [];
  }

  const fromList = toolRuns.data
    .filter((item) => item.parentAgentRunId === agentRunId)
    .map((item) => item.id);

  if (fromList.length > 0) {
    return fromList;
  }

  const detailChecks = await Promise.all(
    toolRuns.data.slice(0, 25).map(async (item) => {
      const detail = await getToolRunDetail(item.id);
      return detail.data?.parentAgentRunId === agentRunId ? item.id : null;
    }),
  );

  return detailChecks.filter((id): id is string => id !== null);
}

export default async function AgentRunDetailPage({ params }: PageProps) {
  const { runId } = await params;
  const run = await getAgentRunDetail(runId);

  if (!run.data) {
    return (
      <main className="px-6 py-8 lg:px-8">
        <PageHeader title="Agent run" description="Run not found." />
        <ErrorState error={run.error ?? "Agent run was not found."} />
      </main>
    );
  }

  const detail = run.data;
  const childToolRunIds = await loadChildToolRunIds(detail.id);
  const mode = detail.isPreview ? "Preview" : detail.isDryRun ? "Dry-run" : "Execute";

  const timeline: TraceTimelineStep[] = [
    {
      id: "start",
      title: "AgentRun started",
      description: `Mode ${mode} · version ${detail.agentVersionId}`,
      status: "Pass",
      meta: new Date(detail.startedAt).toLocaleString(),
    },
    {
      id: "safe",
      title: "Safe mode gate",
      description: detail.safeModeApplied ? "Safe mode applied" : "Safe mode not applied",
      status: detail.safeModeApplied ? "Reviewed" : "Pass",
    },
    ...childToolRunIds.map((id) => ({
      id: `tool-${id}`,
      title: "Child ToolRun",
      description: id,
      status: "Linked",
      href: `/tool-runs/${id}`,
    })),
    {
      id: "complete",
      title: "Run completion",
      description: detail.errorSafeSummary ?? `Status: ${detail.status}`,
      status: detail.status,
      meta: detail.completedAt ? new Date(detail.completedAt).toLocaleString() : undefined,
      href: detail.aiTraceRecordId ? `/ai-traces/${detail.aiTraceRecordId}` : null,
    },
  ];

  return (
    <main className="px-6 py-8 lg:px-8">
      <PageHeader
        title={`Agent run · ${detail.status}`}
        description={detail.id}
        actions={
          <Link href="/agent-runs">
            <Button type="button" variant="ghost">
              All runs
            </Button>
          </Link>
        }
      />

      {detail.errorSafeSummary ? (
        <div className="mb-4">
          <Notice variant="danger">{detail.errorSafeSummary}</Notice>
        </div>
      ) : null}

      <div className="grid gap-4 md:grid-cols-4">
        <KpiCard label="Mode" value={mode} hint="Execution boundary" />
        <KpiCard
          label="Safe mode"
          value={detail.safeModeApplied ? "Applied" : "Off"}
          hint="Runtime gate"
        />
        <KpiCard label="Tool runs" value={childToolRunIds.length} hint="Child ToolRun links" />
        <KpiCard
          label="AI Trace"
          value={detail.aiTraceRecordId ? "Linked" : "None"}
          hint="Audit posture"
        />
      </div>

      <div className="mt-4 grid gap-4 lg:grid-cols-3">
        <Card className="lg:col-span-2">
          <CardHeader>
            <CardTitle>Execution timeline</CardTitle>
          </CardHeader>
          <CardContent className="space-y-4">
            <TraceTimeline steps={timeline} />
            <div className="grid gap-4 lg:grid-cols-2">
              <div>
                <h3 className="text-sm font-semibold text-etos-ink">Input summary</h3>
                <pre className="mt-2 overflow-x-auto rounded-xl border border-etos-border bg-etos-panel-muted p-3 text-xs">
                  {detail.inputSafeSummaryJson}
                </pre>
              </div>
              <div>
                <h3 className="text-sm font-semibold text-etos-ink">Output summary</h3>
                <pre className="mt-2 overflow-x-auto rounded-xl border border-etos-border bg-etos-panel-muted p-3 text-xs">
                  {detail.outputSafeSummaryJson ?? "No output summary."}
                </pre>
              </div>
            </div>
          </CardContent>
        </Card>

        <SidePanel title="Links">
          <PillStack
            items={[
              { label: "Status", value: detail.status, variant: "info" },
              { label: "Mode", value: mode, variant: "purple" },
              {
                label: "Safe mode",
                value: detail.safeModeApplied ? "Yes" : "No",
                variant: detail.safeModeApplied ? "warning" : "success",
              },
            ]}
          />
          <ul className="mt-4 space-y-2 text-sm">
            {detail.aiTraceRecordId ? (
              <li>
                <Link
                  href={`/ai-traces/${detail.aiTraceRecordId}`}
                  className="text-etos-accent hover:underline"
                >
                  AI Trace
                </Link>
              </li>
            ) : (
              <li className="text-etos-ink-muted">No AI Trace linked.</li>
            )}
            {detail.recommendationArtifactId ? (
              <li>
                <Link
                  href={`/recommendations/${detail.recommendationArtifactId}`}
                  className="text-etos-accent hover:underline"
                >
                  Recommendation
                </Link>
              </li>
            ) : null}
            {childToolRunIds.map((id) => (
              <li key={id}>
                <Link href={`/tool-runs/${id}`} className="text-etos-accent hover:underline">
                  Tool run {id.slice(0, 8)}…
                </Link>
              </li>
            ))}
            <li>
              <Link href="/agents" className="text-etos-accent hover:underline">
                Agent registry
              </Link>
            </li>
          </ul>
        </SidePanel>
      </div>

      <details className="mt-6 rounded-etos-card border border-etos-border bg-etos-panel-muted p-4 text-sm text-etos-ink-muted">
        <summary className="cursor-pointer font-extrabold text-etos-ink">Advanced / Debug</summary>
        <pre className="mt-4 overflow-x-auto rounded-xl border border-etos-border bg-etos-panel p-3 text-xs">
          {JSON.stringify(detail, null, 2)}
        </pre>
      </details>
    </main>
  );
}
