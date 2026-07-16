import Link from "next/link";
import { ExplorerNavLink } from "@/components/explorers/ExplorerListShell";
import { getToolRunDetail } from "@/lib/etos-api";

export const dynamic = "force-dynamic";

type PageProps = {
  params: Promise<{ runId: string }>;
};

export default async function ToolRunDetailPage({ params }: PageProps) {
  const { runId } = await params;
  const run = await getToolRunDetail(runId);

  if (!run.data) {
    return (
      <main className="min-h-screen bg-slate-950 px-6 py-10 text-slate-100">
        <div className="mx-auto max-w-3xl rounded-3xl border border-amber-500/30 bg-amber-500/10 p-6 text-sm text-amber-100">
          {run.error ?? "Tool run was not found."}
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
              <p className="text-sm uppercase tracking-wide text-cyan-300">Issue 22 · Tool run</p>
              <h1 className="mt-2 text-4xl font-semibold">{detail.isDryRun ? "Dry-run" : "Execute"} result</h1>
              <p className="mt-3 font-mono text-sm text-slate-400">{detail.id}</p>
            </div>
            <div className="flex flex-wrap gap-3">
              <ExplorerNavLink href="/tools">Tools</ExplorerNavLink>
              <ExplorerNavLink href="/explorers">Explorers</ExplorerNavLink>
            </div>
          </div>
        </section>

        <section className="rounded-3xl border border-slate-800 bg-slate-900 p-6">
          <h2 className="text-2xl font-semibold">Run status</h2>
          <ul className="mt-4 space-y-2 text-sm text-slate-300">
            <li>Status: {detail.status}</li>
            <li>Mode: {detail.isDryRun ? "Dry-run" : "Execute"}</li>
            <li>Created: {detail.createdAt}</li>
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
          {detail.connectorCredentialSafeSummaryJson ? (
            <div className="mt-4">
              <h3 className="text-sm font-semibold text-slate-400">Scoped credential summary</h3>
              <pre className="mt-2 overflow-x-auto rounded-2xl border border-slate-800 bg-slate-950 p-4 text-xs text-slate-300">
                {detail.connectorCredentialSafeSummaryJson}
              </pre>
            </div>
          ) : null}
        </section>

        <section className="rounded-3xl border border-slate-800 bg-slate-900 p-6">
          <h2 className="text-2xl font-semibold">Trace links</h2>
          <ul className="mt-4 space-y-2 text-sm text-slate-300">
            <li>Tool definition version: {detail.toolDefinitionVersionId}</li>
            {detail.connectorDefinitionVersionId ? (
              <li>Connector definition version: {detail.connectorDefinitionVersionId}</li>
            ) : null}
            {detail.retrievalRunId ? (
              <li>
                Retrieval run:{" "}
                <Link href={`/context-packages`} className="text-cyan-300 hover:text-cyan-100">
                  {detail.retrievalRunId}
                </Link>
              </li>
            ) : null}
            {detail.auditRecordId ? <li>Audit record: {detail.auditRecordId}</li> : null}
            {detail.aiTraceRecordId ? (
              <li>
                AI trace:{" "}
                <Link href={`/ai-traces`} className="text-cyan-300 hover:text-cyan-100">
                  {detail.aiTraceRecordId}
                </Link>
              </li>
            ) : null}
          </ul>
        </section>
      </div>
    </main>
  );
}
