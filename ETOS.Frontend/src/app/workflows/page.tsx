import Link from "next/link";
import { ExplorerNavLink } from "@/components/explorers/ExplorerListShell";
import { getWorkflowDefinitionArtifacts, getWorkflowRuns } from "@/lib/etos-api";

export const dynamic = "force-dynamic";

function readinessLabel(state?: string | null): string {
  if (!state) {
    return "Unknown";
  }

  const normalized = state.toLowerCase();
  if (normalized.includes("publish")) {
    return "Published";
  }

  if (normalized.includes("ready")) {
    return "Ready";
  }

  if (normalized.includes("draft")) {
    return "Draft";
  }

  return state;
}

export default async function WorkflowsPage() {
  const [workflows, runs] = await Promise.all([getWorkflowDefinitionArtifacts(), getWorkflowRuns()]);

  return (
    <main className="min-h-screen bg-slate-950 px-6 py-10 text-slate-100">
      <div className="mx-auto flex max-w-6xl flex-col gap-8">
        <section className="rounded-3xl border border-slate-800 bg-slate-900 p-8">
          <div className="flex flex-wrap items-start justify-between gap-4">
            <div>
              <p className="text-sm uppercase tracking-wide text-cyan-300">Issue 24</p>
              <h1 className="mt-2 text-4xl font-semibold">Tenant Workflows</h1>
              <p className="mt-3 max-w-3xl text-slate-400">
                Governed WorkflowVersion artifacts with step graphs, safe mode, preview defaults, and governed runtime
                execution through agents, tools, policies, and optimization models.
              </p>
            </div>
            <div className="flex flex-wrap gap-3">
              <ExplorerNavLink href="/workflows/new">Create workflow</ExplorerNavLink>
              <ExplorerNavLink href="/agents">Agents</ExplorerNavLink>
              <ExplorerNavLink href="/explorers">Explorers</ExplorerNavLink>
              <ExplorerNavLink href="/">Home</ExplorerNavLink>
            </div>
          </div>
        </section>

        {workflows.error ? (
          <div className="rounded-2xl border border-amber-500/30 bg-amber-500/10 p-4 text-sm text-amber-100">
            {workflows.error}
          </div>
        ) : null}

        <section className="rounded-3xl border border-slate-800 bg-slate-900 p-6">
          <h2 className="text-2xl font-semibold">WorkflowVersion artifacts</h2>
          {workflows.data && workflows.data.length > 0 ? (
            <ul className="mt-6 space-y-3">
              {workflows.data.map((artifact) => (
                <li key={artifact.id}>
                  <div className="rounded-2xl border border-slate-800 bg-slate-950 p-4">
                    <div className="flex flex-wrap items-center justify-between gap-3">
                      <div>
                        <p className="font-semibold">{artifact.displayName ?? artifact.name}</p>
                        <p className="text-sm text-slate-400">
                          {artifact.workflowKey ?? artifact.artifactType}
                          {artifact.workflowScope ? ` · ${artifact.workflowScope}` : ""}
                        </p>
                      </div>
                      <div className="text-right text-sm text-slate-400">
                        <p>{artifact.latestVersionLabel ?? "No version"}</p>
                        <p>{readinessLabel(artifact.readinessState)}</p>
                      </div>
                    </div>
                    {artifact.workflowKey ? (
                      <div className="mt-4 flex flex-wrap gap-3 text-sm">
                        <Link
                          href={`/workflows/${encodeURIComponent(artifact.workflowKey)}/edit`}
                          className="text-cyan-300 hover:text-cyan-100"
                        >
                          Edit
                        </Link>
                        <Link
                          href={`/workflows/${encodeURIComponent(artifact.workflowKey)}/publish`}
                          className="text-cyan-300 hover:text-cyan-100"
                        >
                          Publish
                        </Link>
                      </div>
                    ) : null}
                  </div>
                </li>
              ))}
            </ul>
          ) : (
            <p className="mt-4 text-sm text-slate-500">
              No tenant workflows yet.{" "}
              <Link href="/workflows/new" className="text-cyan-300 hover:text-cyan-100">
                Create a draft workflow
              </Link>
              .
            </p>
          )}
        </section>

        <section className="rounded-3xl border border-slate-800 bg-slate-900 p-6">
          <h2 className="text-2xl font-semibold">Recent workflow runs</h2>
          {runs.error ? (
            <p className="mt-4 text-sm text-amber-100">{runs.error}</p>
          ) : runs.data && runs.data.length > 0 ? (
            <ul className="mt-6 space-y-3">
              {runs.data.slice(0, 10).map((run) => (
                <li key={run.id}>
                  <Link
                    href={`/workflow-runs/${run.id}`}
                    className="block rounded-2xl border border-slate-800 bg-slate-950 p-4 transition hover:border-cyan-300/40"
                  >
                    <div className="flex flex-wrap items-center justify-between gap-3">
                      <div>
                        <p className="font-semibold">{run.status}</p>
                        <p className="text-sm text-slate-400">
                          {run.isPreview ? "Preview" : "Execute"} · workflow version {run.workflowVersionId}
                        </p>
                      </div>
                      <div className="text-right text-sm text-slate-400">
                        <p>{run.startedAt}</p>
                        <p>{run.safeModeApplied ? "Safe mode applied" : "No safe mode"}</p>
                      </div>
                    </div>
                    <p className="mt-3 line-clamp-2 text-sm text-slate-500">{run.inputSafeSummary}</p>
                  </Link>
                </li>
              ))}
            </ul>
          ) : (
            <p className="mt-4 text-sm text-slate-500">No workflow runs yet.</p>
          )}
        </section>
      </div>
    </main>
  );
}
