import Link from "next/link";
import { revalidatePath } from "next/cache";
import { redirect } from "next/navigation";
import { ExplorerNavLink } from "@/components/explorers/ExplorerListShell";
import {
  getArtifactVersions,
  loadWorkflowVersionByKey,
  postWorkflowMarkReady,
  postWorkflowPublish,
} from "@/lib/etos-api";

export const dynamic = "force-dynamic";

type PageProps = {
  params: Promise<{ workflowKey: string }>;
  searchParams: Promise<{ versionId?: string; error?: string; success?: string }>;
};

async function markReadyAction(formData: FormData) {
  "use server";

  const artifactId = formData.get("artifactId");
  const versionId = formData.get("versionId");
  const workflowKey = formData.get("workflowKey");

  if (
    typeof artifactId !== "string" ||
    typeof versionId !== "string" ||
    typeof workflowKey !== "string" ||
    artifactId.length === 0 ||
    versionId.length === 0
  ) {
    redirect("/workflows?error=Workflow%20context%20was%20missing.");
  }

  const result = await postWorkflowMarkReady(artifactId, versionId);
  if (result.error || !result.data) {
    redirect(
      `/workflows/${encodeURIComponent(workflowKey)}/publish?versionId=${encodeURIComponent(versionId)}&error=${encodeURIComponent(result.error ?? "Mark ready failed.")}`,
    );
  }

  revalidatePath("/workflows");
  redirect(
    `/workflows/${encodeURIComponent(workflowKey)}/publish?versionId=${encodeURIComponent(versionId)}&success=Workflow%20marked%20ready.`,
  );
}

async function publishAction(formData: FormData) {
  "use server";

  const artifactId = formData.get("artifactId");
  const versionId = formData.get("versionId");
  const workflowKey = formData.get("workflowKey");
  const summary = formData.get("summary");

  if (
    typeof artifactId !== "string" ||
    typeof versionId !== "string" ||
    typeof workflowKey !== "string" ||
    artifactId.length === 0 ||
    versionId.length === 0
  ) {
    redirect("/workflows?error=Workflow%20context%20was%20missing.");
  }

  const result = await postWorkflowPublish(
    artifactId,
    versionId,
    typeof summary === "string" && summary.trim().length > 0 ? summary.trim() : undefined,
  );
  if (result.error || !result.data) {
    redirect(
      `/workflows/${encodeURIComponent(workflowKey)}/publish?versionId=${encodeURIComponent(versionId)}&error=${encodeURIComponent(result.error ?? "Publish failed.")}`,
    );
  }

  if (!result.data.succeeded) {
    const blocking = result.data.blockingReasons.join("; ");
    redirect(
      `/workflows/${encodeURIComponent(workflowKey)}/publish?versionId=${encodeURIComponent(versionId)}&error=${encodeURIComponent(blocking || "Publish blocked.")}`,
    );
  }

  revalidatePath("/workflows");
  redirect(`/workflows/${encodeURIComponent(workflowKey)}/edit?versionId=${encodeURIComponent(versionId)}`);
}

export default async function WorkflowPublishPage({ params, searchParams }: PageProps) {
  const { workflowKey } = await params;
  const { versionId, error, success } = await searchParams;
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
  const derivedRisk = detail.derivedCapabilityRisk;

  return (
    <main className="min-h-screen bg-slate-950 px-6 py-10 text-slate-100">
      <div className="mx-auto flex max-w-6xl flex-col gap-8">
        <section className="rounded-3xl border border-slate-800 bg-slate-900 p-8">
          <div className="flex flex-wrap items-start justify-between gap-4">
            <div>
              <p className="text-sm uppercase tracking-wide text-cyan-300">Issue 24 · Publish</p>
              <h1 className="mt-2 text-4xl font-semibold">{detail.displayName}</h1>
              <p className="mt-3 text-slate-400">
                {detail.workflowKey} · {detail.versionLabel} · {detail.artifactReadinessState}
              </p>
            </div>
            <div className="flex flex-wrap gap-3">
              <ExplorerNavLink
                href={`/workflows/${encodeURIComponent(detail.workflowKey)}/edit?versionId=${encodeURIComponent(selectedVersionId)}`}
              >
                Edit
              </ExplorerNavLink>
              <ExplorerNavLink href="/workflows">Workflows</ExplorerNavLink>
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
                        href={`/workflows/${encodeURIComponent(detail.workflowKey)}/publish?versionId=${encodeURIComponent(version.id)}`}
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

        {error ? (
          <div className="rounded-2xl border border-amber-500/30 bg-amber-500/10 p-4 text-sm text-amber-100">
            {error}
          </div>
        ) : null}

        {success ? (
          <div className="rounded-2xl border border-cyan-500/30 bg-cyan-500/10 p-4 text-sm text-cyan-100">
            {success}
          </div>
        ) : null}

        <section className="rounded-3xl border border-slate-800 bg-slate-900 p-6">
          <h2 className="text-2xl font-semibold">Derived capability risk review</h2>
          {derivedRisk ? (
            <ul className="mt-4 space-y-2 text-sm text-slate-300">
              <li>Effective risk level: {derivedRisk.effectiveRiskLevel}</li>
              <li>Permission ceiling: {derivedRisk.permissionCeiling}</li>
              {derivedRisk.toolRiskContributions.length > 0 ? (
                <li>
                  Tool risk contributions:{" "}
                  {derivedRisk.toolRiskContributions
                    .map((item) => `${item.toolDefinitionVersionId} (${item.riskLevel})`)
                    .join(", ")}
                </li>
              ) : (
                <li>No tool risk contributions recorded.</li>
              )}
            </ul>
          ) : (
            <p className="mt-4 text-sm text-slate-400">
              Derived risk is calculated when the workflow is marked ready. Mark ready below to refresh the risk
              snapshot.
            </p>
          )}

          {detail.referencedTools.length > 0 ? (
            <div className="mt-6">
              <h3 className="text-sm font-semibold text-slate-400">Referenced tools</h3>
              <ul className="mt-2 space-y-1 text-sm text-slate-300">
                {detail.referencedTools.map((tool) => (
                  <li key={tool.toolDefinitionVersionId}>
                    {tool.toolArtifactName} · {tool.riskLevel} · {tool.readinessState}
                  </li>
                ))}
              </ul>
            </div>
          ) : null}
        </section>

        <section className="rounded-3xl border border-slate-800 bg-slate-900 p-6">
          <h2 className="text-2xl font-semibold">Readiness blockers</h2>
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

          {detail.compatibilityTestNotes.length > 0 ? (
            <div className="mt-6">
              <h3 className="text-sm font-semibold text-slate-400">Compatibility test notes</h3>
              <ul className="mt-2 space-y-1 text-sm text-slate-300">
                {detail.compatibilityTestNotes.map((note) => (
                  <li key={note}>{note}</li>
                ))}
              </ul>
            </div>
          ) : null}
        </section>

        <section className="rounded-3xl border border-slate-800 bg-slate-900 p-6">
          <h2 className="text-2xl font-semibold">Lifecycle actions</h2>
          <div className="mt-6 flex flex-col gap-6 lg:flex-row">
            <form action={markReadyAction} className="flex-1 space-y-4 rounded-2xl border border-slate-800 bg-slate-950 p-4">
              <h3 className="font-semibold">Mark ready</h3>
              <p className="text-sm text-slate-400">
                Validates dependencies and computes derived capability risk before publish.
              </p>
              <input type="hidden" name="artifactId" value={artifactId} />
              <input type="hidden" name="versionId" value={selectedVersionId} />
              <input type="hidden" name="workflowKey" value={detail.workflowKey} />
              <button
                type="submit"
                className="rounded-2xl border border-cyan-500/40 bg-cyan-500/10 px-4 py-2 text-sm font-semibold text-cyan-100 transition hover:border-cyan-300"
              >
                Mark ready
              </button>
            </form>

            <form action={publishAction} className="flex-1 space-y-4 rounded-2xl border border-slate-800 bg-slate-950 p-4">
              <h3 className="font-semibold">Publish</h3>
              <p className="text-sm text-slate-400">
                Publishes the workflow version when readiness checks pass.
              </p>
              <input type="hidden" name="artifactId" value={artifactId} />
              <input type="hidden" name="versionId" value={selectedVersionId} />
              <input type="hidden" name="workflowKey" value={detail.workflowKey} />
              <label className="block text-sm">
                <span className="font-semibold text-slate-300">Publish summary (optional)</span>
                <input
                  name="summary"
                  type="text"
                  placeholder="Initial publish for manufacturing investigation"
                  className="mt-2 w-full rounded-2xl border border-slate-700 bg-slate-900 px-4 py-3 text-slate-100"
                />
              </label>
              <button
                type="submit"
                className="rounded-2xl border border-cyan-500/40 bg-cyan-500/10 px-4 py-2 text-sm font-semibold text-cyan-100 transition hover:border-cyan-300"
              >
                Publish workflow
              </button>
            </form>
          </div>
        </section>
      </div>
    </main>
  );
}
