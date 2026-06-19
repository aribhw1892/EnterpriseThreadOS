import Link from "next/link";
import { ExplorerNavLink } from "@/components/explorers/ExplorerListShell";
import {
  getConnectorDefinitionArtifacts,
  getSkillDefinitionArtifacts,
  getToolDefinitionArtifacts,
} from "@/lib/etos-api";

export const dynamic = "force-dynamic";

export default async function ToolsPage() {
  const [tools, skills, connectors] = await Promise.all([
    getToolDefinitionArtifacts(),
    getSkillDefinitionArtifacts(),
    getConnectorDefinitionArtifacts(),
  ]);

  const error = tools.error ?? skills.error ?? connectors.error;

  return (
    <main className="min-h-screen bg-slate-950 px-6 py-10 text-slate-100">
      <div className="mx-auto flex max-w-6xl flex-col gap-8">
        <section className="rounded-3xl border border-slate-800 bg-slate-900 p-8">
          <div className="flex flex-wrap items-start justify-between gap-4">
            <div>
              <p className="text-sm uppercase tracking-wide text-cyan-300">Issue 22</p>
              <h1 className="mt-2 text-4xl font-semibold">Tool Registry</h1>
              <p className="mt-3 max-w-3xl text-slate-400">
                Governed ToolDefinitionVersion, SkillDefinitionVersion, and ConnectorDefinitionVersion artifacts with
                schema compatibility, dry-run, and sync execution boundaries.
              </p>
            </div>
            <div className="flex flex-wrap gap-3">
              <ExplorerNavLink href="/explorers">Explorers</ExplorerNavLink>
              <ExplorerNavLink href="/">Home</ExplorerNavLink>
            </div>
          </div>
        </section>

        {error ? (
          <div className="rounded-2xl border border-amber-500/30 bg-amber-500/10 p-4 text-sm text-amber-100">
            {error}
          </div>
        ) : null}

        <section className="rounded-3xl border border-slate-800 bg-slate-900 p-6">
          <h2 className="text-2xl font-semibold">ToolDefinitionVersion artifacts</h2>
          {tools.data && tools.data.length > 0 ? (
            <ul className="mt-6 space-y-3">
              {tools.data.map((artifact) => (
                <li key={artifact.id}>
                  <Link
                    href={`/tools/${artifact.id}`}
                    className="block rounded-2xl border border-slate-800 bg-slate-950 p-4 transition hover:border-cyan-300/40"
                  >
                    <div className="flex flex-wrap items-center justify-between gap-3">
                      <div>
                        <p className="font-semibold">{artifact.name}</p>
                        <p className="text-sm text-slate-400">
                          {artifact.toolKey ?? artifact.artifactType}
                          {artifact.toolCategory ? ` · ${artifact.toolCategory}` : ""}
                          {artifact.riskLevel ? ` · ${artifact.riskLevel} risk` : ""}
                        </p>
                      </div>
                      <div className="text-right text-sm text-slate-400">
                        <p>{artifact.latestVersionLabel ?? "No version"}</p>
                        <p>{artifact.readinessState ?? "Unknown"}</p>
                      </div>
                    </div>
                  </Link>
                </li>
              ))}
            </ul>
          ) : (
            <p className="mt-4 text-sm text-slate-500">
              No tool definitions yet. Create one via the admin API or install the manufacturing reference package.
            </p>
          )}
        </section>

        <section className="rounded-3xl border border-slate-800 bg-slate-900 p-6">
          <h2 className="text-2xl font-semibold">SkillDefinitionVersion artifacts</h2>
          {skills.data && skills.data.length > 0 ? (
            <ul className="mt-6 space-y-3">
              {skills.data.map((artifact) => (
                <li
                  key={artifact.id}
                  className="rounded-2xl border border-slate-800 bg-slate-950 p-4 text-sm text-slate-300"
                >
                  <p className="font-semibold">{artifact.name}</p>
                  <p className="text-slate-400">
                    {artifact.skillKey ?? artifact.artifactType} · {artifact.latestVersionLabel ?? "No version"} ·{" "}
                    {artifact.readinessState ?? "Unknown"}
                  </p>
                </li>
              ))}
            </ul>
          ) : (
            <p className="mt-4 text-sm text-slate-500">No skill definitions installed yet.</p>
          )}
        </section>

        <section className="rounded-3xl border border-slate-800 bg-slate-900 p-6">
          <h2 className="text-2xl font-semibold">ConnectorDefinitionVersion artifacts</h2>
          {connectors.data && connectors.data.length > 0 ? (
            <ul className="mt-6 space-y-3">
              {connectors.data.map((artifact) => (
                <li key={artifact.id}>
                  <Link
                    href={`/connectors/${artifact.id}`}
                    className="block rounded-2xl border border-slate-800 bg-slate-950 p-4 transition hover:border-cyan-300/40"
                  >
                    <div className="flex flex-wrap items-center justify-between gap-3">
                      <div>
                        <p className="font-semibold">{artifact.name}</p>
                        <p className="text-sm text-slate-400">
                          {artifact.connectorKey ?? artifact.artifactType}
                          {artifact.connectorKind ? ` · ${artifact.connectorKind}` : ""}
                        </p>
                      </div>
                      <div className="text-right text-sm text-slate-400">
                        <p>{artifact.latestVersionLabel ?? "No version"}</p>
                        <p>
                          {artifact.executionEnabled === false ? "Disabled" : "Enabled"} ·{" "}
                          {artifact.readinessState ?? "Unknown"}
                        </p>
                      </div>
                    </div>
                  </Link>
                </li>
              ))}
            </ul>
          ) : (
            <p className="mt-4 text-sm text-slate-500">No connector definitions installed yet.</p>
          )}
        </section>
      </div>
    </main>
  );
}
