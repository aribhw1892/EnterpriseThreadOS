import Link from "next/link";
import { Button } from "@/components/ui/Button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/Card";
import { ErrorState } from "@/components/ui/ErrorState";
import { KpiCard } from "@/components/ui/KpiCard";
import { Notice } from "@/components/ui/Notice";
import { PageHeader } from "@/components/ui/PageHeader";
import { PillStack, SidePanel } from "@/components/ui/SidePanel";
import { TraceTimeline, type TraceTimelineStep } from "@/components/ui/TraceTimeline";
import { getWorkflowRunDetail } from "@/lib/etos-api";

export const dynamic = "force-dynamic";

type PageProps = {
  params: Promise<{ runId: string }>;
};

export default async function WorkflowRunDetailPage({ params }: PageProps) {
  const { runId } = await params;
  const run = await getWorkflowRunDetail(runId);

  if (!run.data) {
    return (
      <main className="px-6 py-8 lg:px-8">
        <PageHeader title="Workflow run" description="Run not found." />
        <ErrorState error={run.error ?? "Workflow run was not found."} />
      </main>
    );
  }

  const detail = run.data;
  const safeEvents = detail.safeModeEvents;
  const mode = detail.isPreview ? "Preview" : "Execute";

  const timeline: TraceTimelineStep[] = [
    {
      id: "start",
      title: "WorkflowRun started",
      description: `Mode ${mode} · version ${detail.workflowVersionId}`,
      status: "Pass",
      meta: new Date(detail.startedAt).toLocaleString(),
    },
    ...safeEvents.map((event) => ({
      id: event.id,
      title: `Safe mode · ${event.stepKey}`,
      description: `${event.eventKind}: ${event.reason}`,
      status: "Filtered",
      meta: new Date(event.createdAt).toLocaleString(),
      href: event.agentRunId
        ? `/agent-runs/${event.agentRunId}`
        : event.toolRunId
          ? `/tool-runs/${event.toolRunId}`
          : null,
    })),
    {
      id: "complete",
      title: "Run completion",
      description: `Status: ${detail.status}${detail.partialCompletion ? " · partial" : ""}`,
      status: detail.status,
      meta: detail.completedAt ? new Date(detail.completedAt).toLocaleString() : undefined,
      href: detail.aiTraceRecordId ? `/ai-traces/${detail.aiTraceRecordId}` : null,
    },
  ];

  return (
    <main className="px-6 py-8 lg:px-8">
      <PageHeader
        title={`Workflow run · ${detail.status}`}
        description={detail.id}
        actions={
          <>
            <Link href="/workflow-runs">
              <Button type="button" variant="ghost">
                All runs
              </Button>
            </Link>
            <Link href="/workflows">
              <Button type="button" variant="ghost">
                Workflows
              </Button>
            </Link>
          </>
        }
      />

      {detail.safeModeApplied ? (
        <div className="mb-4">
          <Notice variant="warning">
            Safe mode applied on this run. Source writes remain blocked (SAFE = 0).
          </Notice>
        </div>
      ) : null}

      <div className="grid gap-4 md:grid-cols-4">
        <KpiCard label="Run" value={`${detail.id.slice(0, 8)}…`} hint="WorkflowRun id" />
        <KpiCard
          label="Safe-mode events"
          value={safeEvents.length}
          trend={safeEvents.length > 0 ? "bad" : "flat"}
          hint="Blocked / skipped steps"
        />
        <KpiCard
          label="Partial"
          value={detail.partialCompletion ? "Yes" : "No"}
          hint="Allow partial completion"
        />
        <KpiCard label="Source writes" value={0} hint="SAFE — enterprise writes blocked" />
      </div>

      <div className="mt-4 grid gap-4 lg:grid-cols-3">
        <Card className="lg:col-span-2">
          <CardHeader>
            <CardTitle>Execution steps</CardTitle>
          </CardHeader>
          <CardContent className="space-y-4">
            <TraceTimeline steps={timeline} />
            <div className="grid gap-4 lg:grid-cols-2">
              <div>
                <h3 className="text-sm font-semibold text-etos-ink">Input</h3>
                <pre className="mt-2 overflow-x-auto rounded-xl border border-etos-border bg-etos-panel-muted p-3 text-xs">
                  {detail.inputSafeSummaryJson}
                </pre>
              </div>
              <div>
                <h3 className="text-sm font-semibold text-etos-ink">Output</h3>
                <pre className="mt-2 overflow-x-auto rounded-xl border border-etos-border bg-etos-panel-muted p-3 text-xs">
                  {detail.outputSafeSummaryJson ?? "No output summary."}
                </pre>
              </div>
            </div>
          </CardContent>
        </Card>

        <SidePanel title="Safe mode & links">
          <PillStack
            items={[
              { label: "Mode", value: mode, variant: "info" },
              {
                label: "Safe mode",
                value: detail.safeModeApplied ? "Applied" : "Off",
                variant: detail.safeModeApplied ? "warning" : "success",
              },
              {
                label: "Child agents",
                value: String(detail.childAgentRunIds.length),
              },
              {
                label: "Child tools",
                value: String(detail.childToolRunIds.length),
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
            {detail.childAgentRunIds.map((id) => (
              <li key={id}>
                <Link href={`/agent-runs/${id}`} className="text-etos-accent hover:underline">
                  Agent run {id.slice(0, 8)}…
                </Link>
              </li>
            ))}
            {detail.childToolRunIds.map((id) => (
              <li key={id}>
                <Link href={`/tool-runs/${id}`} className="text-etos-accent hover:underline">
                  Tool run {id.slice(0, 8)}…
                </Link>
              </li>
            ))}
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
