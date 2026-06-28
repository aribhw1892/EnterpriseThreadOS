import Link from "next/link";
import { ExplorerNavLink } from "@/components/explorers/ExplorerListShell";
import { AgentModelConfigPanel } from "@/components/agents/AgentModelConfigPanel";
import { ensureMappingAgentSeedAction } from "@/components/agents/agent-configure-actions";
import { getArtifactVersions, loadAgentVersionByKey } from "@/lib/etos-api";

export const dynamic = "force-dynamic";

type PageProps = {
  params: Promise<{ agentKey: string }>;
  searchParams: Promise<{ versionId?: string; error?: string }>;
};

export default async function AgentConfigurePage({ params, searchParams }: PageProps) {
  const { agentKey } = await params;
  const { versionId, error } = await searchParams;
  const decodedKey = decodeURIComponent(agentKey);
  const loaded = await loadAgentVersionByKey(decodedKey, versionId);

  if (!loaded.data) {
    return (
      <main className="min-h-screen bg-slate-950 px-6 py-10 text-slate-100">
        <div className="mx-auto flex max-w-3xl flex-col gap-6">
          <div className="rounded-3xl border border-amber-500/30 bg-amber-500/10 p-6 text-sm text-amber-100">
            {loaded.error ?? "Agent was not found."}
          </div>
          <section className="rounded-3xl border border-slate-800 bg-slate-900 p-6">
            <h2 className="text-xl font-semibold">Recover local mapping assistant</h2>
            <p className="mt-3 text-sm text-slate-400">
              Tenant agent <code className="text-cyan-200">{decodedKey}</code> is seeded when the manufacturing
              reference package installs. That step is skipped if the package was already published, or after{" "}
              <strong className="font-semibold text-slate-300">Clean demo dataset</strong> removed artifacts.
            </p>
            <div className="mt-6 flex flex-wrap gap-3">
              <form action={ensureMappingAgentSeedAction}>
                <input type="hidden" name="agentKey" value={decodedKey} />
                <button
                  type="submit"
                  className="rounded-2xl border border-cyan-500/40 bg-cyan-500/10 px-4 py-2 text-sm font-semibold text-cyan-100 transition hover:border-cyan-300"
                >
                  Install / ensure reference package
                </button>
              </form>
              <ExplorerNavLink href="/agents/new">Create agent from template</ExplorerNavLink>
              <ExplorerNavLink href="/model-artifacts">Model artifacts</ExplorerNavLink>
              <ExplorerNavLink href="/agents">Agents</ExplorerNavLink>
            </div>
            {error ? (
              <p className="mt-4 rounded-xl border border-amber-500/30 bg-amber-500/10 px-3 py-2 text-sm text-amber-100">
                {error}
              </p>
            ) : null}
          </section>
        </div>
      </main>
    );
  }

  const { detail, readiness, artifactId, versionId: selectedVersionId } = loaded.data;
  const versions = await getArtifactVersions(artifactId);

  return (
    <main className="min-h-screen bg-slate-950 px-6 py-10 text-slate-100">
      <div className="mx-auto flex max-w-6xl flex-col gap-8">
        <section className="rounded-3xl border border-slate-800 bg-slate-900 p-8">
          <div className="flex flex-wrap items-start justify-between gap-4">
            <div>
              <p className="text-sm uppercase tracking-wide text-cyan-300">Issue 23 · Configure</p>
              <h1 className="mt-2 text-4xl font-semibold">{detail.displayName}</h1>
              <p className="mt-3 text-slate-400">
                {detail.agentKey} · {detail.versionLabel} · {detail.artifactReadinessState}
              </p>
            </div>
            <div className="flex flex-wrap gap-3">
              <ExplorerNavLink href={`/agents/${encodeURIComponent(detail.agentKey)}/test-run?versionId=${encodeURIComponent(selectedVersionId)}`}>
                Test run
              </ExplorerNavLink>
              <ExplorerNavLink href="/agent-runs">Agent runs</ExplorerNavLink>
              <ExplorerNavLink href="/agent-templates">Templates</ExplorerNavLink>
              <ExplorerNavLink href="/agents">Agents</ExplorerNavLink>
              <ExplorerNavLink href="/explorers">Explorers</ExplorerNavLink>
              <ExplorerNavLink href="/">Home</ExplorerNavLink>
            </div>
          </div>

          {versions.data && versions.data.length > 1 ? (
            <div className="mt-6">
              <p className="text-sm font-semibold text-slate-400">Versions</p>
              <ul className="mt-2 flex flex-wrap gap-2">
                {versions.data.map((version) => {
                  const isSelected = version.id === selectedVersionId;
                  return (
                    <li key={version.id}>
                      <Link
                        href={`/agents/${encodeURIComponent(detail.agentKey)}/configure?versionId=${encodeURIComponent(version.id)}`}
                        className={`rounded-full px-3 py-1 text-xs font-semibold transition ${
                          isSelected
                            ? "bg-cyan-300 text-slate-950"
                            : "border border-slate-700 text-slate-300 hover:border-cyan-300 hover:text-cyan-100"
                        }`}
                      >
                        {version.versionLabel} · {version.readinessState}
                      </Link>
                    </li>
                  );
                })}
              </ul>
            </div>
          ) : null}
        </section>

        <AgentModelConfigPanel
          artifactId={artifactId}
          versionId={selectedVersionId}
          agentKey={detail.agentKey}
          detail={detail}
          errorMessage={error ?? null}
        />

        <section className="rounded-3xl border border-slate-800 bg-slate-900 p-6">
          <h2 className="text-2xl font-semibold">Safe mode and preview defaults</h2>
          <ul className="mt-4 grid gap-2 text-sm text-slate-300 md:grid-cols-2">
            <li>Safe mode enabled: {detail.safeModeEnabled ? "Yes" : "No"}</li>
            <li>Preview mode default: {detail.previewModeDefault ? "Yes" : "No"}</li>
          </ul>
          {detail.blockedModeMessage ? (
            <p className="mt-4 rounded-xl border border-amber-500/30 bg-amber-500/10 px-3 py-2 text-sm text-amber-100">
              {detail.blockedModeMessage}
            </p>
          ) : null}
        </section>

        <section className="rounded-3xl border border-slate-800 bg-slate-900 p-6">
          <h2 className="text-2xl font-semibold">Read-only references</h2>
          {readiness.blockingReasons.length > 0 ? (
            <ul className="mt-4 space-y-2 text-sm text-amber-100">
              {readiness.blockingReasons.map((reason) => (
                <li key={reason} className="rounded-xl border border-amber-500/30 bg-amber-500/10 px-3 py-2">
                  {reason}
                </li>
              ))}
            </ul>
          ) : (
            <p className="mt-4 text-sm text-slate-400">No blocking readiness notes for this version.</p>
          )}

          {detail.agentType ? (
            <div className="mt-6">
              <h3 className="text-sm font-semibold text-slate-400">Agent type</h3>
              <p className="mt-2 text-sm text-slate-300">
                {detail.agentType.typeKey} · {detail.agentType.versionLabel} · {detail.agentType.readinessState} ·{" "}
                {detail.agentType.riskBaseline} baseline
              </p>
            </div>
          ) : null}

          {detail.promptTemplate ? (
            <div className="mt-6">
              <h3 className="text-sm font-semibold text-slate-400">Prompt template</h3>
              <p className="mt-2 text-sm text-slate-300">
                {detail.promptTemplate.artifactName} · {detail.promptTemplate.versionLabel} ·{" "}
                {detail.promptTemplate.readinessState}
              </p>
            </div>
          ) : null}

          {detail.outputSchema ? (
            <div className="mt-6">
              <h3 className="text-sm font-semibold text-slate-400">Output schema</h3>
              <p className="mt-2 text-sm text-slate-300">
                {detail.outputSchema.artifactName} · {detail.outputSchema.versionLabel} ·{" "}
                {detail.outputSchema.readinessState}
              </p>
            </div>
          ) : null}

          {detail.queryIntent ? (
            <div className="mt-6">
              <h3 className="text-sm font-semibold text-slate-400">Query intent</h3>
              <p className="mt-2 text-sm text-slate-300">
                {detail.queryIntent.intentKey} · {detail.queryIntent.versionLabel}
                {detail.queryIntent.isEnabled ? "" : " · disabled"}
              </p>
            </div>
          ) : null}

          {detail.retrievalStrategy ? (
            <div className="mt-6">
              <h3 className="text-sm font-semibold text-slate-400">Retrieval strategy</h3>
              <p className="mt-2 text-sm text-slate-300">
                {detail.retrievalStrategy.strategyKey} · {detail.retrievalStrategy.versionLabel}
              </p>
            </div>
          ) : null}

          {detail.referencedTools.length > 0 ? (
            <div className="mt-6">
              <h3 className="text-sm font-semibold text-slate-400">Referenced tools</h3>
              <ul className="mt-2 space-y-1 text-sm text-slate-300">
                {detail.referencedTools.map((tool) => (
                  <li key={tool.toolDefinitionVersionId}>
                    <Link href={`/tools/${tool.toolArtifactId}`} className="text-cyan-300 hover:text-cyan-100">
                      {tool.toolArtifactName}
                    </Link>{" "}
                    · {tool.versionLabel} · {tool.riskLevel} risk · {tool.readinessState}
                  </li>
                ))}
              </ul>
            </div>
          ) : null}

          {detail.referencedSkills.length > 0 ? (
            <div className="mt-6">
              <h3 className="text-sm font-semibold text-slate-400">Referenced skills</h3>
              <ul className="mt-2 space-y-1 text-sm text-slate-300">
                {detail.referencedSkills.map((skill) => (
                  <li key={skill.skillDefinitionVersionId}>
                    {skill.skillKey} · {skill.versionLabel} · {skill.readinessState}
                  </li>
                ))}
              </ul>
            </div>
          ) : null}
        </section>

        {detail.derivedCapabilityRisk ? (
          <section className="rounded-3xl border border-slate-800 bg-slate-900 p-6">
            <h2 className="text-2xl font-semibold">Derived capability risk</h2>
            <ul className="mt-4 space-y-2 text-sm text-slate-300">
              <li>Effective risk: {detail.derivedCapabilityRisk.effectiveRiskLevel}</li>
              <li>Permission ceiling: {detail.derivedCapabilityRisk.permissionCeiling}</li>
              <li>
                Retrieval fallback: semantic{" "}
                {detail.derivedCapabilityRisk.retrievalRisk.allowsSemanticFallback ? "allowed" : "blocked"}, vector{" "}
                {detail.derivedCapabilityRisk.retrievalRisk.allowsVectorFallback ? "allowed" : "blocked"}
              </li>
            </ul>
          </section>
        ) : null}
      </div>
    </main>
  );
}
