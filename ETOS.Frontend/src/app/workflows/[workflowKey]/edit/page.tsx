import Link from "next/link";
import { ExplorerNavLink } from "@/components/explorers/ExplorerListShell";
import { getArtifactVersions, loadWorkflowVersionByKey } from "@/lib/etos-api";

export const dynamic = "force-dynamic";

type PageProps = {
  params: Promise<{ workflowKey: string }>;
  searchParams: Promise<{ versionId?: string }>;
};

export default async function WorkflowEditPage({ params, searchParams }: PageProps) {
  const { workflowKey } = await params;
  const { versionId } = await searchParams;
  const decodedKey = decodeURIComponent(workflowKey);
  const loaded = await loadWorkflowVersionByKey(decodedKey, versionId);

  if (!loaded.data) {
    return (
      <main className="min-h-screen bg-slate-950 px-6 py-10 text-slate-100">
        <div className="mx-auto max-w-3xl rounded-3xl border border-amber-500/30 bg-amber-500/10 p-6 text-sm text-amber-100">
          {loaded.error ?? "Workflow was not found."}
        </div>
      </main>
    );
  }

  const { detail, readiness, artifactId, versionId: selectedVersionId } = loaded.data;
  const versions = await getArtifactVersions(artifactId);
  const stepsJson = JSON.stringify(detail.steps, null, 2);

  return (
    <main className="min-h-screen bg-slate-950 px-6 py-10 text-slate-100">
      <div className="mx-auto flex max-w-6xl flex-col gap-8">
        <section className="rounded-3xl border border-slate-800 bg-slate-900 p-8">
          <div className="flex flex-wrap items-start justify-between gap-4">
            <div>
              <p className="text-sm uppercase tracking-wide text-cyan-300">Issue 24 · Edit</p>
              <h1 className="mt-2 text-4xl font-semibold">{detail.displayName}</h1>
              <p className="mt-3 text-slate-400">
                {detail.workflowKey} · {detail.versionLabel} · {detail.artifactReadinessState} · {detail.workflowScope}
              </p>
            </div>
            <div className="flex flex-wrap gap-3">
              <ExplorerNavLink
                href={`/workflows/${encodeURIComponent(detail.workflowKey)}/publish?versionId=${encodeURIComponent(selectedVersionId)}`}
              >
                Publish
              </ExplorerNavLink>
              <ExplorerNavLink href="/workflows">Workflows</ExplorerNavLink>
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
                        href={`/workflows/${encodeURIComponent(detail.workflowKey)}/edit?versionId=${encodeURIComponent(version.id)}`}
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

        <section className="rounded-3xl border border-slate-800 bg-slate-900 p-6">
          <h2 className="text-2xl font-semibold">Workflow definition</h2>
          <ul className="mt-4 grid gap-2 text-sm text-slate-300 md:grid-cols-2">
            <li>Safe mode enabled: {detail.safeModeEnabled ? "Yes" : "No"}</li>
            <li>Preview mode default: {detail.previewModeDefault ? "Yes" : "No"}</li>
            <li>Allow partial completion: {detail.allowPartialCompletion ? "Yes" : "No"}</li>
            <li>Default step safe mode: {detail.defaultStepSafeModeBehavior}</li>
            <li>Manual trigger: {detail.triggerConfig.manualEnabled ? "Enabled" : "Disabled"}</li>
            <li>Scheduled trigger: {detail.triggerConfig.scheduledEnabled ? "Enabled" : "Disabled"}</li>
          </ul>
          {detail.workflowDescription ? (
            <p className="mt-4 text-sm text-slate-400">{detail.workflowDescription}</p>
          ) : null}
          {detail.blockedModeMessage ? (
            <p className="mt-4 rounded-xl border border-amber-500/30 bg-amber-500/10 px-3 py-2 text-sm text-amber-100">
              {detail.blockedModeMessage}
            </p>
          ) : null}
        </section>

        <section className="rounded-3xl border border-slate-800 bg-slate-900 p-6">
          <h2 className="text-2xl font-semibold">Steps JSON</h2>
          <p className="mt-2 text-sm text-slate-400">
            Read-only view of governed step definitions for this version. Step editing via API is not wired in this
            shell yet.
          </p>
          <pre className="mt-4 overflow-x-auto rounded-2xl border border-slate-800 bg-slate-950 p-4 text-xs text-slate-300">
            {stepsJson}
          </pre>
        </section>

        <section className="rounded-3xl border border-slate-800 bg-slate-900 p-6">
          <h2 className="text-2xl font-semibold">Readiness</h2>
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

          {detail.referencedAgents.length > 0 ? (
            <div className="mt-6">
              <h3 className="text-sm font-semibold text-slate-400">Referenced agents</h3>
              <ul className="mt-2 space-y-1 text-sm text-slate-300">
                {detail.referencedAgents.map((agent) => (
                  <li key={agent.agentVersionId}>
                    {agent.agentKey} · {agent.versionLabel} · {agent.readinessState}
                  </li>
                ))}
              </ul>
            </div>
          ) : null}

          {detail.referencedTools.length > 0 ? (
            <div className="mt-6">
              <h3 className="text-sm font-semibold text-slate-400">Referenced tools</h3>
              <ul className="mt-2 space-y-1 text-sm text-slate-300">
                {detail.referencedTools.map((tool) => (
                  <li key={tool.toolDefinitionVersionId}>
                    {tool.toolArtifactName} · {tool.versionLabel} · {tool.riskLevel}
                  </li>
                ))}
              </ul>
            </div>
          ) : null}

          {detail.derivedCapabilityRisk ? (
            <div className="mt-6">
              <h3 className="text-sm font-semibold text-slate-400">Derived capability risk</h3>
              <ul className="mt-2 space-y-1 text-sm text-slate-300">
                <li>Effective risk: {detail.derivedCapabilityRisk.effectiveRiskLevel}</li>
                <li>Permission ceiling: {detail.derivedCapabilityRisk.permissionCeiling}</li>
              </ul>
            </div>
          ) : null}
        </section>
      </div>
    </main>
  );
}
