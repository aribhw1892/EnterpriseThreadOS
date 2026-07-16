import Link from "next/link";
import {
  agentExecuteAction,
  agentPreviewAction,
  agentTestRunAction,
} from "@/app/(shell)/agents/actions";
import { Button } from "@/components/ui/Button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/Card";
import { ErrorState } from "@/components/ui/ErrorState";
import { Notice } from "@/components/ui/Notice";
import { PageHeader } from "@/components/ui/PageHeader";
import { PillStack, SidePanel } from "@/components/ui/SidePanel";
import { TraceTimeline, type TraceTimelineStep } from "@/components/ui/TraceTimeline";
import { getAgentRunDetail, loadAgentVersionByKey } from "@/lib/etos-api";

export const dynamic = "force-dynamic";

type PageProps = {
  params: Promise<{ agentKey: string }>;
  searchParams: Promise<{
    error?: string;
    runId?: string;
    toolRunIds?: string;
    versionId?: string;
    mode?: string;
    output?: string;
    traceId?: string;
  }>;
};

const fieldClass =
  "mt-2 w-full rounded-xl border border-etos-border bg-etos-panel px-3.5 py-2.5 text-sm text-etos-ink";

export default async function AgentTestRunPage({ params, searchParams }: PageProps) {
  const { agentKey } = await params;
  const { error, runId, toolRunIds, versionId, mode, output, traceId } = await searchParams;
  const decodedKey = decodeURIComponent(agentKey);
  const loaded = await loadAgentVersionByKey(decodedKey, versionId);
  const linkedToolRunIds = toolRunIds
    ? toolRunIds.split(",").map((id) => id.trim()).filter((id) => id.length > 0)
    : [];

  if (!loaded.data) {
    return (
      <main className="px-6 py-8 lg:px-8">
        <PageHeader title="Agent test run" description="Agent not found." />
        <ErrorState error={loaded.error ?? "Agent was not found."} />
      </main>
    );
  }

  const { artifactId, versionId: selectedVersionId, detail } = loaded.data;
  const isPublished = detail.artifactReadinessState.toLowerCase().includes("publish");
  const executeAllowed = isPublished && !detail.safeModeEnabled;
  const executeReason = !isPublished
    ? "Execute requires a published AgentVersion."
    : detail.safeModeEnabled
      ? "Execute blocked while safe mode is enabled (recommendation-only / safe mode)."
      : undefined;

  let runDetail = null;
  if (runId) {
    const run = await getAgentRunDetail(runId);
    runDetail = run.data;
  }

  const timeline: TraceTimelineStep[] = [];
  if (runId) {
    timeline.push({
      id: "agent-run",
      title: "AgentRun",
      description: `${mode ?? "run"} · ${runDetail?.status ?? "created"}`,
      status: runDetail?.status ?? "Started",
      href: `/agent-runs/${runId}`,
    });
    for (const toolRunId of linkedToolRunIds) {
      timeline.push({
        id: `tool-${toolRunId}`,
        title: "ToolRun",
        description: toolRunId,
        status: "Linked",
        href: `/tool-runs/${toolRunId}`,
      });
    }
    const resolvedTrace = traceId ?? runDetail?.aiTraceRecordId ?? null;
    if (resolvedTrace) {
      timeline.push({
        id: "ai-trace",
        title: "AI Trace",
        description: resolvedTrace,
        status: "Linked",
        href: `/ai-traces/${resolvedTrace}`,
      });
    }
  }

  return (
    <main className="px-6 py-8 lg:px-8">
      <PageHeader
        title={`Test run · ${detail.displayName}`}
        description={`${detail.agentKey} · ${detail.versionLabel} · preview / dry-run with governed boundaries`}
        actions={
          <Link
            href={`/agents/${encodeURIComponent(detail.agentKey)}/configure?versionId=${encodeURIComponent(selectedVersionId)}`}
          >
            <Button type="button" variant="ghost">
              Configure
            </Button>
          </Link>
        }
      />

      {error ? (
        <div className="mb-4">
          <Notice variant="danger">{error}</Notice>
        </div>
      ) : null}
      {detail.safeModeEnabled ? (
        <div className="mb-4">
          <Notice variant="warning">
            Safe mode is enabled. Non-preview execute stays gated until safe mode is disabled on a published
            version.
          </Notice>
        </div>
      ) : null}

      <div className="grid gap-4 lg:grid-cols-3">
        <div className="space-y-4 lg:col-span-2">
          <Card>
            <CardHeader>
              <CardTitle>Query fixture</CardTitle>
            </CardHeader>
            <CardContent>
              <form className="space-y-4">
                <input type="hidden" name="artifactId" value={artifactId} />
                <input type="hidden" name="versionId" value={selectedVersionId} />
                <input type="hidden" name="agentKey" value={detail.agentKey} />
                <label className="block text-sm">
                  <span className="font-semibold text-etos-ink">Query text</span>
                  <textarea
                    name="queryText"
                    required
                    rows={4}
                    placeholder="Investigate BOM discrepancies for assembly A-100."
                    className={fieldClass}
                  />
                </label>
                <div className="flex flex-wrap gap-3">
                  <Button formAction={agentPreviewAction} type="submit" variant="ghost">
                    Preview
                  </Button>
                  <Button formAction={agentTestRunAction} type="submit">
                    Test run (dry-run)
                  </Button>
                  <Button
                    formAction={agentExecuteAction}
                    type="submit"
                    disabled={!executeAllowed}
                    title={executeReason}
                  >
                    Execute
                  </Button>
                </div>
                {!executeAllowed && executeReason ? (
                  <p className="text-xs text-etos-ink-muted">{executeReason}</p>
                ) : null}
              </form>
            </CardContent>
          </Card>

          {runId ? (
            <Card>
              <CardHeader>
                <CardTitle>Run output</CardTitle>
              </CardHeader>
              <CardContent className="space-y-4">
                <Notice variant="info">
                  Latest {mode ?? "run"} →{" "}
                  <Link href={`/agent-runs/${runId}`} className="text-etos-accent hover:underline">
                    {runId}
                  </Link>
                </Notice>
                {output || runDetail?.outputSafeSummaryJson ? (
                  <pre className="overflow-x-auto rounded-xl border border-etos-border bg-etos-panel-muted p-3 text-xs text-etos-ink">
                    {runDetail?.outputSafeSummaryJson ?? output}
                  </pre>
                ) : (
                  <p className="text-sm text-etos-ink-muted">
                    Recommendation-only output: open the agent run detail for structured summaries.
                  </p>
                )}
                {runDetail?.structuredOutputJson ? (
                  <pre className="overflow-x-auto rounded-xl border border-etos-border bg-etos-panel p-3 text-xs">
                    {runDetail.structuredOutputJson}
                  </pre>
                ) : null}
                {timeline.length > 0 ? <TraceTimeline steps={timeline} /> : null}
              </CardContent>
            </Card>
          ) : null}
        </div>

        <SidePanel title="Run trace">
          <PillStack
            items={[
              {
                label: "Mode",
                value: mode ?? "idle",
                variant: "info",
              },
              {
                label: "Safe mode",
                value: detail.safeModeEnabled ? "On" : "Off",
                variant: detail.safeModeEnabled ? "warning" : "success",
              },
              {
                label: "Publish",
                value: isPublished ? "Published" : detail.artifactReadinessState,
                variant: isPublished ? "success" : "neutral",
              },
            ]}
          />
          <ul className="mt-4 space-y-2 text-sm text-etos-ink-muted">
            {runId ? (
              <li>
                <Link href={`/agent-runs/${runId}`} className="text-etos-accent hover:underline">
                  Agent run
                </Link>
              </li>
            ) : (
              <li>No run yet — submit Preview or Test run.</li>
            )}
            {linkedToolRunIds.map((id) => (
              <li key={id}>
                <Link href={`/tool-runs/${id}`} className="text-etos-accent hover:underline">
                  Tool run {id.slice(0, 8)}…
                </Link>
              </li>
            ))}
            {(traceId ?? runDetail?.aiTraceRecordId) ? (
              <li>
                <Link
                  href={`/ai-traces/${traceId ?? runDetail?.aiTraceRecordId}`}
                  className="text-etos-accent hover:underline"
                >
                  AI Trace
                </Link>
              </li>
            ) : null}
            {runDetail?.recommendationArtifactId ? (
              <li>
                <Link
                  href={`/recommendations/${runDetail.recommendationArtifactId}`}
                  className="text-etos-accent hover:underline"
                >
                  Recommendation
                </Link>
              </li>
            ) : null}
          </ul>
        </SidePanel>
      </div>
    </main>
  );
}
