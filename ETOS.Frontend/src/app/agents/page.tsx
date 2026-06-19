import Link from "next/link";
import { ExplorerNavLink } from "@/components/explorers/ExplorerListShell";
import { getAgentDefinitionArtifacts } from "@/lib/etos-api";

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

export default async function AgentsPage() {
  const agents = await getAgentDefinitionArtifacts();

  return (
    <main className="min-h-screen bg-slate-950 px-6 py-10 text-slate-100">
      <div className="mx-auto flex max-w-6xl flex-col gap-8">
        <section className="rounded-3xl border border-slate-800 bg-slate-900 p-8">
          <div className="flex flex-wrap items-start justify-between gap-4">
            <div>
              <p className="text-sm uppercase tracking-wide text-cyan-300">Issue 23</p>
              <h1 className="mt-2 text-4xl font-semibold">Tenant Agents</h1>
              <p className="mt-3 max-w-3xl text-slate-400">
                Governed AgentVersion artifacts with draft, ready, and published lifecycle states, safe mode, preview
                defaults, and runtime adapter selection.
              </p>
            </div>
            <div className="flex flex-wrap gap-3">
              <ExplorerNavLink href="/agents/new">Create agent</ExplorerNavLink>
              <ExplorerNavLink href="/agent-runs">Agent runs</ExplorerNavLink>
              <ExplorerNavLink href="/agent-templates">Templates</ExplorerNavLink>
              <ExplorerNavLink href="/explorers">Explorers</ExplorerNavLink>
            </div>
          </div>
        </section>

        {agents.error ? (
          <div className="rounded-2xl border border-amber-500/30 bg-amber-500/10 p-4 text-sm text-amber-100">
            {agents.error}
          </div>
        ) : null}

        <section className="rounded-3xl border border-slate-800 bg-slate-900 p-6">
          <h2 className="text-2xl font-semibold">AgentVersion artifacts</h2>
          {agents.data && agents.data.length > 0 ? (
            <ul className="mt-6 space-y-3">
              {agents.data.map((artifact) => (
                <li key={artifact.id}>
                  <div className="rounded-2xl border border-slate-800 bg-slate-950 p-4">
                    <div className="flex flex-wrap items-center justify-between gap-3">
                      <div>
                        <p className="font-semibold">{artifact.displayName ?? artifact.name}</p>
                        <p className="text-sm text-slate-400">
                          {artifact.agentKey ?? artifact.artifactType}
                          {artifact.preferredRuntimeAdapterKey
                            ? ` · ${artifact.preferredRuntimeAdapterKey}`
                            : ""}
                        </p>
                      </div>
                      <div className="text-right text-sm text-slate-400">
                        <p>{artifact.latestVersionLabel ?? "No version"}</p>
                        <p>{readinessLabel(artifact.readinessState)}</p>
                      </div>
                    </div>
                    {artifact.agentKey ? (
                      <div className="mt-4 flex flex-wrap gap-3 text-sm">
                        <Link
                          href={`/agents/${encodeURIComponent(artifact.agentKey)}/configure`}
                          className="text-cyan-300 hover:text-cyan-100"
                        >
                          Configure
                        </Link>
                        <Link
                          href={`/agents/${encodeURIComponent(artifact.agentKey)}/test-run`}
                          className="text-cyan-300 hover:text-cyan-100"
                        >
                          Test run
                        </Link>
                      </div>
                    ) : null}
                  </div>
                </li>
              ))}
            </ul>
          ) : (
            <p className="mt-4 text-sm text-slate-500">
              No tenant agents yet.{" "}
              <Link href="/agents/new" className="text-cyan-300 hover:text-cyan-100">
                Create one from a template or prompt
              </Link>
              .
            </p>
          )}
        </section>
      </div>
    </main>
  );
}
