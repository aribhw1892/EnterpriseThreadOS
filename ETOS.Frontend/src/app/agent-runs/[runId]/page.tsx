import Link from "next/link";
import { ExplorerNavLink } from "@/components/explorers/ExplorerListShell";
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
      <main className="min-h-screen bg-slate-950 px-6 py-10 text-slate-100">
        <div className="mx-auto max-w-3xl rounded-3xl border border-amber-500/30 bg-amber-500/10 p-6 text-sm text-amber-100">
          {run.error ?? "Agent run was not found."}
        </div>
      </main>
    );
  }

  const detail = run.data;
  const childToolRunIds = await loadChildToolRunIds(detail.id);

  return (
    <main className="min-h-screen bg-slate-950 px-6 py-10 text-slate-100">
      <div className="mx-auto flex max-w-6xl flex-col gap-8">
        <section className="rounded-3xl border border-slate-800 bg-slate-900 p-8">
          <div className="flex flex-wrap items-start justify-between gap-4">
            <div>
              <p className="text-sm uppercase tracking-wide text-cyan-300">Issue 23 · Agent run</p>
              <h1 className="mt-2 text-4xl font-semibold">{detail.status}</h1>
              <p className="mt-3 font-mono text-sm text-slate-400">{detail.id}</p>
            </div>
            <div className="flex flex-wrap gap-3">
              <ExplorerNavLink href="/agent-runs">Agent runs</ExplorerNavLink>
              <ExplorerNavLink href="/agents">Agents</ExplorerNavLink>
            </div>
          </div>
        </section>

        <section className="rounded-3xl border border-slate-800 bg-slate-900 p-6">
          <h2 className="text-2xl font-semibold">Run status</h2>
          <ul className="mt-4 space-y-2 text-sm text-slate-300">
            <li>Mode: {detail.isPreview ? "Preview" : detail.isDryRun ? "Dry-run test" : "Execute"}</li>
            <li>Safe mode applied: {detail.safeModeApplied ? "Yes" : "No"}</li>
            <li>Agent version: {detail.agentVersionId}</li>
            <li>Started: {detail.startedAt}</li>
            {detail.completedAt ? <li>Completed: {detail.completedAt}</li> : null}
            {detail.errorSafeSummary ? (
              <li className="text-amber-100">Error: {detail.errorSafeSummary}</li>
            ) : null}
          </ul>
        </section>

        <section className="rounded-3xl border border-slate-800 bg-slate-900 p-6">
          <h2 className="text-2xl font-semibold">Safe summaries</h2>
          <div className="mt-4 grid gap-4 lg:grid-cols-2">
            <div>
              <h3 className="text-sm font-semibold text-slate-400">Input</h3>
              <pre className="mt-2 overflow-x-auto rounded-2xl border border-slate-800 bg-slate-950 p-4 text-xs text-slate-300">
                {detail.inputSafeSummaryJson}
              </pre>
            </div>
            <div>
              <h3 className="text-sm font-semibold text-slate-400">Output</h3>
              <pre className="mt-2 overflow-x-auto rounded-2xl border border-slate-800 bg-slate-950 p-4 text-xs text-slate-300">
                {detail.outputSafeSummaryJson ?? "No output summary."}
              </pre>
            </div>
          </div>
          {detail.structuredOutputJson ? (
            <div className="mt-4">
              <h3 className="text-sm font-semibold text-slate-400">Structured output</h3>
              <pre className="mt-2 overflow-x-auto rounded-2xl border border-slate-800 bg-slate-950 p-4 text-xs text-slate-300">
                {detail.structuredOutputJson}
              </pre>
            </div>
          ) : null}
          {detail.fallbackUsedJson ? (
            <div className="mt-4">
              <h3 className="text-sm font-semibold text-slate-400">Fallback used</h3>
              <pre className="mt-2 overflow-x-auto rounded-2xl border border-slate-800 bg-slate-950 p-4 text-xs text-slate-300">
                {detail.fallbackUsedJson}
              </pre>
            </div>
          ) : null}
        </section>

        <section className="rounded-3xl border border-slate-800 bg-slate-900 p-6">
          <h2 className="text-2xl font-semibold">Trace links</h2>
          <ul className="mt-4 space-y-2 text-sm text-slate-300">
            {detail.aiTraceRecordId ? (
              <li>
                AI trace:{" "}
                <Link href="/ai-traces" className="text-cyan-300 hover:text-cyan-100">
                  {detail.aiTraceRecordId}
                </Link>
              </li>
            ) : (
              <li>No AI trace linked yet.</li>
            )}
            {detail.retrievalRunId ? (
              <li>
                Retrieval run:{" "}
                <Link href="/context-packages" className="text-cyan-300 hover:text-cyan-100">
                  {detail.retrievalRunId}
                </Link>
              </li>
            ) : null}
            {detail.recommendationArtifactId ? (
              <li>
                Recommendation artifact:{" "}
                <Link
                  href={`/recommendations/${detail.recommendationArtifactId}`}
                  className="text-cyan-300 hover:text-cyan-100"
                >
                  {detail.recommendationArtifactId}
                </Link>
              </li>
            ) : null}
            {detail.auditRecordId ? <li>Audit record: {detail.auditRecordId}</li> : null}
          </ul>
        </section>

        <section className="rounded-3xl border border-slate-800 bg-slate-900 p-6">
          <h2 className="text-2xl font-semibold">Tool runs</h2>
          {childToolRunIds.length > 0 ? (
            <ul className="mt-4 space-y-2 text-sm text-slate-300">
              {childToolRunIds.map((toolRunId) => (
                <li key={toolRunId}>
                  <Link href={`/tool-runs/${toolRunId}`} className="text-cyan-300 hover:text-cyan-100">
                    {toolRunId}
                  </Link>
                </li>
              ))}
            </ul>
          ) : (
            <p className="mt-4 text-sm text-slate-500">
              No child ToolRun records found for this agent run yet.
            </p>
          )}
        </section>
      </div>
    </main>
  );
}
