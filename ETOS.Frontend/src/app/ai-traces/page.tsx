import {
  AiTraceDetail,
  AiTraceSummary,
  ApiResult,
  adminUserId,
  exportAiTrace,
  getAiTraceLists,
  runGovernedQueryForGraphNode,
  selectedTenantId,
} from "@/lib/etos-api";
import Link from "next/link";
import { revalidatePath } from "next/cache";
import type { ReactNode } from "react";

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

  const graphNodeId = "33333333-3333-3333-3333-333333333333";
  await runGovernedQueryForGraphNode(graphNodeId);
  revalidatePath("/ai-traces");
}

function StatusBadge({ status }: { status: string }) {
  const normalized = status.toLowerCase();
  const className =
    normalized === "completed"
      ? "bg-emerald-100 text-emerald-800 dark:bg-emerald-950 dark:text-emerald-200"
      : normalized === "failed"
        ? "bg-rose-100 text-rose-800 dark:bg-rose-950 dark:text-rose-200"
        : "bg-cyan-100 text-cyan-800 dark:bg-cyan-950 dark:text-cyan-200";

  return (
    <span className={`rounded-full px-3 py-1 text-xs font-semibold uppercase tracking-wide ${className}`}>
      {status}
    </span>
  );
}

function ErrorState({ error }: { error: string }) {
  return (
    <div className="rounded-2xl border border-amber-500/30 bg-amber-500/10 p-4 text-sm text-amber-100">
      {error}
    </div>
  );
}

function EmptyState({ message }: { message: string }) {
  return (
    <div className="rounded-2xl border border-slate-800 bg-slate-950 p-4 text-sm text-slate-400">
      {message}
    </div>
  );
}

function ActionButton({ action, children }: { action: () => Promise<void>; children: ReactNode }) {
  return (
    <form action={action}>
      <button
        type="submit"
        className="rounded-2xl bg-cyan-300 px-5 py-3 text-sm font-semibold text-slate-950 transition hover:bg-cyan-200"
      >
        {children}
      </button>
    </form>
  );
}

function TraceCard(trace: AiTraceSummary) {
  return (
    <article key={trace.id} className="rounded-2xl border border-slate-800 bg-slate-950 p-4">
      <div className="flex items-start justify-between gap-3">
        <div>
          <h3 className="font-semibold">{trace.intentKey}</h3>
          <p className="mt-1 text-sm text-slate-400">{trace.safeSummary}</p>
          <p className="mt-2 text-xs text-slate-500">
            Strategy: {trace.strategyKey} · {new Date(trace.createdAt).toLocaleString()}
          </p>
        </div>
        <StatusBadge status={trace.status} />
      </div>
    </article>
  );
}

function DetailPanel({ trace }: { trace: AiTraceDetail }) {
  return (
    <section className="rounded-3xl border border-slate-800 bg-slate-900 p-6">
      <div className="mb-5 flex flex-wrap items-start justify-between gap-3">
        <div>
          <h2 className="text-2xl font-semibold">Trace detail</h2>
          <p className="mt-1 text-sm text-slate-400">{trace.safeSummary}</p>
        </div>
        <StatusBadge status={trace.status} />
      </div>

      <dl className="grid gap-4 md:grid-cols-2">
        <div className="rounded-2xl border border-slate-800 bg-slate-950 p-4">
          <dt className="text-xs uppercase tracking-wide text-slate-500">Retrieval strategy</dt>
          <dd className="mt-1 font-medium">{trace.strategyKey}</dd>
        </div>
        <div className="rounded-2xl border border-slate-800 bg-slate-950 p-4">
          <dt className="text-xs uppercase tracking-wide text-slate-500">Query intent</dt>
          <dd className="mt-1 font-medium">{trace.intentKey}</dd>
        </div>
        <div className="rounded-2xl border border-slate-800 bg-slate-950 p-4 md:col-span-2">
          <dt className="text-xs uppercase tracking-wide text-slate-500">Query text</dt>
          <dd className="mt-1 text-sm text-slate-300">{trace.queryText}</dd>
        </div>
      </dl>

      <div className="mt-6 grid gap-4 lg:grid-cols-2">
        <div className="rounded-2xl border border-slate-800 bg-slate-950 p-4">
          <h3 className="font-semibold">Confidence impact</h3>
          <ul className="mt-3 space-y-2 text-sm text-slate-300">
            <li>Retrieved: {trace.confidenceImpact.retrievedCount}</li>
            <li>Filtered: {trace.confidenceImpact.filteredCount}</li>
            <li>Denied: {trace.confidenceImpact.deniedCount}</li>
            <li>Trust filtered: {trace.confidenceImpact.trustFilteredCount}</li>
            <li>Policy: {trace.confidenceImpact.policyKey ?? "none"}</li>
          </ul>
          <p className="mt-3 text-xs text-slate-500">{trace.confidenceImpact.notes}</p>
        </div>
        <div className="rounded-2xl border border-slate-800 bg-slate-950 p-4">
          <h3 className="font-semibold">Sources summary</h3>
          {trace.sourcesSummary.length > 0 ? (
            <ul className="mt-3 space-y-2 text-sm text-slate-300">
              {trace.sourcesSummary.map((source) => (
                <li key={source.sourceKind}>
                  {source.sourceKind}: {source.count}
                </li>
              ))}
            </ul>
          ) : (
            <p className="mt-3 text-sm text-slate-500">No source summary recorded.</p>
          )}
        </div>
      </div>

      <div className="mt-6 grid gap-4 lg:grid-cols-2">
        <div className="rounded-2xl border border-slate-800 bg-slate-950 p-4">
          <h3 className="font-semibold">Filtered summaries</h3>
          <ul className="mt-3 space-y-2 text-sm text-slate-300">
            {trace.filteredSummaries.map((item) => (
              <li key={item.contextId}>{item.safeSummary}</li>
            ))}
          </ul>
        </div>
        <div className="rounded-2xl border border-slate-800 bg-slate-950 p-4">
          <h3 className="font-semibold">Denied safe summaries</h3>
          {trace.deniedSafeSummaries.length > 0 ? (
            <ul className="mt-3 space-y-2 text-sm text-slate-300">
              {trace.deniedSafeSummaries.map((item) => (
                <li key={item.contextId}>
                  {item.safeSummary}
                  <span className="block text-xs text-slate-500">{item.reason}</span>
                </li>
              ))}
            </ul>
          ) : (
            <p className="mt-3 text-sm text-slate-500">No denied summaries.</p>
          )}
        </div>
      </div>

      <div className="mt-6 rounded-2xl border border-slate-800 bg-slate-950 p-4">
        <h3 className="font-semibold">Artifact links</h3>
        <ul className="mt-3 space-y-2 text-sm text-slate-300">
          {trace.artifactLinks.map((link) => (
            <li key={link.id}>
              {link.linkKind}: {link.objectType} ({link.objectId})
            </li>
          ))}
        </ul>
      </div>

      <form action={exportLatestTrace} className="mt-6">
        <input type="hidden" name="traceId" value={trace.id} />
        <button
          type="submit"
          className="rounded-2xl border border-cyan-300 px-5 py-3 text-sm font-semibold text-cyan-100 transition hover:bg-cyan-300 hover:text-slate-950"
        >
          Export trace package
        </button>
      </form>
    </section>
  );
}

function renderApiError(result: ApiResult<unknown>) {
  return result.error ? <ErrorState error={result.error} /> : null;
}

export default async function AiTracesPage() {
  const { traces, latestTrace } = await getAiTraceLists();

  return (
    <main className="min-h-screen bg-slate-950 text-slate-100">
      <div className="mx-auto flex max-w-6xl flex-col gap-6 px-6 py-10">
        <section className="rounded-3xl border border-slate-800 bg-slate-900 p-6">
          <div className="flex flex-wrap items-start justify-between gap-4">
            <div>
              <p className="text-sm uppercase tracking-[0.2em] text-cyan-300">Issue 14</p>
              <h1 className="mt-2 text-3xl font-semibold">AI Trace Explorer</h1>
              <p className="mt-2 max-w-3xl text-sm text-slate-400">
                Inspect governed retrieval traces, filtered context summaries, denied safe summaries, confidence impact,
                and on-demand export packages with separate view and export permissions.
              </p>
              <p className="mt-2 text-xs text-slate-500">
                Tenant {selectedTenantId} · User {adminUserId}
              </p>
            </div>
            <div className="flex flex-wrap gap-3">
              <Link
                href="/"
                className="rounded-full border border-slate-700 px-4 py-2 text-sm font-semibold text-slate-200 transition hover:border-cyan-300 hover:text-cyan-100"
              >
                Home
              </Link>
              <ActionButton action={runDemoGovernedQuery}>Run demo governed query</ActionButton>
            </div>
          </div>
        </section>

        {renderApiError(traces)}
        {renderApiError(latestTrace)}

        <section className="rounded-3xl border border-slate-800 bg-slate-900 p-6">
          <div className="mb-5">
            <h2 className="text-2xl font-semibold">Trace list</h2>
            <p className="mt-1 text-sm text-slate-400">Latest governed-query AI traces for the active tenant.</p>
          </div>
          {traces.data && traces.data.length > 0 ? (
            <div className="grid gap-3">{traces.data.map((trace) => TraceCard(trace))}</div>
          ) : (
            <EmptyState message="No AI traces yet. Run a governed query to create one." />
          )}
        </section>

        {latestTrace.data ? <DetailPanel trace={latestTrace.data} /> : null}
      </div>
    </main>
  );
}
