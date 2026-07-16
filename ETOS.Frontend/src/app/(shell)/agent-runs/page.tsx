import Link from "next/link";
import { ExplorerNavLink } from "@/components/explorers/ExplorerListShell";
import { getAgentRuns } from "@/lib/etos-api";

export const dynamic = "force-dynamic";

export default async function AgentRunsPage() {
  const runs = await getAgentRuns();

  return (
    <main className="min-h-screen bg-slate-950 px-6 py-10 text-slate-100">
      <div className="mx-auto flex max-w-6xl flex-col gap-8">
        <section className="rounded-3xl border border-slate-800 bg-slate-900 p-8">
          <div className="flex flex-wrap items-start justify-between gap-4">
            <div>
              <p className="text-sm uppercase tracking-wide text-cyan-300">Issue 23</p>
              <h1 className="mt-2 text-4xl font-semibold">Agent Runs</h1>
              <p className="mt-3 max-w-3xl text-slate-400">
                Tenant AgentRun records from preview, dry-run test, and execute flows with safe summaries and trace
                links.
              </p>
            </div>
            <div className="flex flex-wrap gap-3">
              <ExplorerNavLink href="/agents">Agents</ExplorerNavLink>
              <ExplorerNavLink href="/agent-templates">Templates</ExplorerNavLink>
              <ExplorerNavLink href="/explorers">Explorers</ExplorerNavLink>
              <ExplorerNavLink href="/">Home</ExplorerNavLink>
            </div>
          </div>
        </section>

        {runs.error ? (
          <div className="rounded-2xl border border-amber-500/30 bg-amber-500/10 p-4 text-sm text-amber-100">
            {runs.error}
          </div>
        ) : null}

        <section className="rounded-3xl border border-slate-800 bg-slate-900 p-6">
          <h2 className="text-2xl font-semibold">Recent runs</h2>
          {runs.data && runs.data.length > 0 ? (
            <ul className="mt-6 space-y-3">
              {runs.data.map((run) => (
                <li key={run.id}>
                  <Link
                    href={`/agent-runs/${run.id}`}
                    className="block rounded-2xl border border-slate-800 bg-slate-950 p-4 transition hover:border-cyan-300/40"
                  >
                    <div className="flex flex-wrap items-center justify-between gap-3">
                      <div>
                        <p className="font-semibold">{run.status}</p>
                        <p className="text-sm text-slate-400">
                          {run.isPreview ? "Preview" : run.isDryRun ? "Dry-run test" : "Execute"} · agent version{" "}
                          {run.agentVersionId}
                        </p>
                      </div>
                      <div className="text-right text-sm text-slate-400">
                        <p>{run.startedAt}</p>
                        {run.aiTraceRecordId ? <p>Trace linked</p> : <p>No trace yet</p>}
                      </div>
                    </div>
                    <p className="mt-3 line-clamp-2 text-sm text-slate-500">{run.inputSafeSummary}</p>
                  </Link>
                </li>
              ))}
            </ul>
          ) : (
            <p className="mt-4 text-sm text-slate-500">
              No agent runs yet. Trigger a preview or test run from an agent&apos;s test-run page.
            </p>
          )}
        </section>
      </div>
    </main>
  );
}
