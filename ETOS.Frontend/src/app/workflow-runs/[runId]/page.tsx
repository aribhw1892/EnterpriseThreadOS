import Link from "next/link";
import { ExplorerNavLink } from "@/components/explorers/ExplorerListShell";
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
      <main className="min-h-screen bg-slate-950 px-6 py-10 text-slate-100">
        <div className="mx-auto max-w-3xl rounded-3xl border border-amber-500/30 bg-amber-500/10 p-6 text-sm text-amber-100">
          {run.error ?? "Workflow run was not found."}
        </div>
      </main>
    );
  }

  const detail = run.data;

  return (
    <main className="min-h-screen bg-slate-950 px-6 py-10 text-slate-100">
      <div className="mx-auto flex max-w-6xl flex-col gap-8">
        <section className="rounded-3xl border border-slate-800 bg-slate-900 p-8">
          <div className="flex flex-wrap items-start justify-between gap-4">
            <div>
              <p className="text-sm uppercase tracking-wide text-cyan-300">Issue 24 · Workflow run</p>
              <h1 className="mt-2 text-4xl font-semibold">{detail.status}</h1>
              <p className="mt-3 font-mono text-sm text-slate-400">{detail.id}</p>
            </div>
            <div className="flex flex-wrap gap-3">
              <ExplorerNavLink href="/workflows">Workflows</ExplorerNavLink>
              <ExplorerNavLink href="/agents">Agents</ExplorerNavLink>
            </div>
          </div>
        </section>

        <section className="rounded-3xl border border-slate-800 bg-slate-900 p-6">
          <h2 className="text-2xl font-semibold">Run status</h2>
          <ul className="mt-4 space-y-2 text-sm text-slate-300">
            <li>Mode: {detail.isPreview ? "Preview" : "Execute"}</li>
            <li>Safe mode applied: {detail.safeModeApplied ? "Yes" : "No"}</li>
            <li>Partial completion: {detail.partialCompletion ? "Yes" : "No"}</li>
            <li>Workflow version: {detail.workflowVersionId}</li>
            <li>Started: {detail.startedAt}</li>
            {detail.completedAt ? <li>Completed: {detail.completedAt}</li> : null}
          </ul>
        </section>

        <section className="rounded-3xl border border-slate-800 bg-slate-900 p-6">
          <h2 className="text-2xl font-semibold">Safe mode events</h2>
          {detail.safeModeEvents.length > 0 ? (
            <ul className="mt-4 space-y-3">
              {detail.safeModeEvents.map((event) => (
                <li
                  key={event.id}
                  className="rounded-2xl border border-slate-800 bg-slate-950 p-4 text-sm text-slate-300"
                >
                  <div className="flex flex-wrap items-center justify-between gap-2">
                    <p className="font-semibold text-slate-100">
                      {event.stepKey} · {event.eventKind}
                    </p>
                    <p className="text-xs text-slate-500">{event.createdAt}</p>
                  </div>
                  <p className="mt-2">{event.reason}</p>
                  {event.policyRuleKey ? <p className="mt-1 text-slate-400">Policy rule: {event.policyRuleKey}</p> : null}
                  {event.blockedAction ? (
                    <p className="mt-1 text-slate-400">Blocked action: {event.blockedAction}</p>
                  ) : null}
                  <div className="mt-3 flex flex-wrap gap-3 text-xs">
                    {event.agentRunId ? (
                      <Link href={`/agent-runs/${event.agentRunId}`} className="text-cyan-300 hover:text-cyan-100">
                        Agent run {event.agentRunId}
                      </Link>
                    ) : null}
                    {event.toolRunId ? (
                      <Link href={`/tool-runs/${event.toolRunId}`} className="text-cyan-300 hover:text-cyan-100">
                        Tool run {event.toolRunId}
                      </Link>
                    ) : null}
                    {event.reviewTaskArtifactId ? (
                      <Link href="/tasks" className="text-cyan-300 hover:text-cyan-100">
                        Review task {event.reviewTaskArtifactId}
                      </Link>
                    ) : null}
                  </div>
                </li>
              ))}
            </ul>
          ) : (
            <p className="mt-4 text-sm text-slate-500">No safe mode events recorded for this run.</p>
          )}
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
          {detail.stepResultsJson ? (
            <div className="mt-4">
              <h3 className="text-sm font-semibold text-slate-400">Step results</h3>
              <pre className="mt-2 overflow-x-auto rounded-2xl border border-slate-800 bg-slate-950 p-4 text-xs text-slate-300">
                {detail.stepResultsJson}
              </pre>
            </div>
          ) : null}
          {detail.inheritedRiskSnapshotJson ? (
            <div className="mt-4">
              <h3 className="text-sm font-semibold text-slate-400">Inherited risk snapshot</h3>
              <pre className="mt-2 overflow-x-auto rounded-2xl border border-slate-800 bg-slate-950 p-4 text-xs text-slate-300">
                {detail.inheritedRiskSnapshotJson}
              </pre>
            </div>
          ) : null}
          {detail.runtimeTrustRecalculationJson ? (
            <div className="mt-4">
              <h3 className="text-sm font-semibold text-slate-400">Runtime trust recalculation</h3>
              <pre className="mt-2 overflow-x-auto rounded-2xl border border-slate-800 bg-slate-950 p-4 text-xs text-slate-300">
                {detail.runtimeTrustRecalculationJson}
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
            {detail.auditRecordId ? <li>Audit record: {detail.auditRecordId}</li> : null}
          </ul>
        </section>

        <section className="rounded-3xl border border-slate-800 bg-slate-900 p-6">
          <h2 className="text-2xl font-semibold">Child runs</h2>
          <div className="mt-4 grid gap-6 lg:grid-cols-2">
            <div>
              <h3 className="text-sm font-semibold text-slate-400">Agent runs</h3>
              {detail.childAgentRunIds.length > 0 ? (
                <ul className="mt-2 space-y-2 text-sm text-slate-300">
                  {detail.childAgentRunIds.map((agentRunId) => (
                    <li key={agentRunId}>
                      <Link href={`/agent-runs/${agentRunId}`} className="text-cyan-300 hover:text-cyan-100">
                        {agentRunId}
                      </Link>
                    </li>
                  ))}
                </ul>
              ) : (
                <p className="mt-2 text-sm text-slate-500">No child agent runs.</p>
              )}
            </div>
            <div>
              <h3 className="text-sm font-semibold text-slate-400">Tool runs</h3>
              {detail.childToolRunIds.length > 0 ? (
                <ul className="mt-2 space-y-2 text-sm text-slate-300">
                  {detail.childToolRunIds.map((toolRunId) => (
                    <li key={toolRunId}>
                      <Link href={`/tool-runs/${toolRunId}`} className="text-cyan-300 hover:text-cyan-100">
                        {toolRunId}
                      </Link>
                    </li>
                  ))}
                </ul>
              ) : (
                <p className="mt-2 text-sm text-slate-500">No child tool runs.</p>
              )}
            </div>
          </div>
        </section>
      </div>
    </main>
  );
}
