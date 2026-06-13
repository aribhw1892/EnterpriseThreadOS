import Link from "next/link";
import { revalidatePath } from "next/cache";
import {
  ApiResult,
  ArtifactReadiness,
  RecommendationPayload,
  getArtifactImpact,
  getArtifactReadiness,
  getArtifactVersions,
  getRecommendationArtifacts,
  getRecommendationPayload,
  markRecommendationReady,
  markRecommendationReviewed,
  publishArtifactVersion,
  updateRecommendationSuggestedActionStatus,
} from "@/lib/etos-api";
import { ExplorerNavLink } from "@/components/explorers/ExplorerListShell";

type RecommendationDetailProps = {
  artifactId: string;
  versionId: string;
  artifactName: string;
  payload: RecommendationPayload;
  readiness: ArtifactReadiness;
  dependencyCount: number;
};

async function markReviewedAction(formData: FormData) {
  "use server";

  const artifactId = formData.get("artifactId");
  const versionId = formData.get("versionId");
  if (typeof artifactId !== "string" || typeof versionId !== "string") {
    return;
  }

  await markRecommendationReviewed(artifactId, versionId);
  revalidatePath(`/recommendations/${artifactId}`);
}

async function markReadyAction(formData: FormData) {
  "use server";

  const artifactId = formData.get("artifactId");
  const versionId = formData.get("versionId");
  if (typeof artifactId !== "string" || typeof versionId !== "string") {
    return;
  }

  await markRecommendationReady(artifactId, versionId);
  revalidatePath(`/recommendations/${artifactId}`);
}

async function publishAction(formData: FormData) {
  "use server";

  const artifactId = formData.get("artifactId");
  const versionId = formData.get("versionId");
  if (typeof artifactId !== "string" || typeof versionId !== "string") {
    return;
  }

  await publishArtifactVersion(artifactId, versionId, "Published from recommendation UI.");
  revalidatePath(`/recommendations/${artifactId}`);
}

async function selectActionForReviewAction(formData: FormData) {
  "use server";

  const artifactId = formData.get("artifactId");
  const versionId = formData.get("versionId");
  const actionId = formData.get("actionId");
  if (typeof artifactId !== "string" || typeof versionId !== "string" || typeof actionId !== "string") {
    return;
  }

  await updateRecommendationSuggestedActionStatus(artifactId, versionId, actionId, "selectedForReview");
  revalidatePath(`/recommendations/${artifactId}`);
}

function ActionForm({
  action,
  artifactId,
  versionId,
  label,
  children,
}: {
  action: (formData: FormData) => Promise<void>;
  artifactId: string;
  versionId: string;
  label: string;
  children?: React.ReactNode;
}) {
  return (
    <form action={action}>
      <input type="hidden" name="artifactId" value={artifactId} />
      <input type="hidden" name="versionId" value={versionId} />
      {children}
      <button
        type="submit"
        className="rounded-2xl border border-slate-700 px-4 py-2 text-sm font-semibold text-slate-200 transition hover:border-cyan-300 hover:text-cyan-100"
      >
        {label}
      </button>
    </form>
  );
}

export function RecommendationDetailView({
  artifactId,
  versionId,
  artifactName,
  payload,
  readiness,
  dependencyCount,
}: RecommendationDetailProps) {
  return (
    <main className="min-h-screen bg-slate-950 px-6 py-10 text-slate-100">
      <div className="mx-auto flex max-w-6xl flex-col gap-8">
        <section className="rounded-3xl border border-slate-800 bg-slate-900 p-8">
          <div className="flex flex-wrap items-start justify-between gap-4">
            <div>
              <p className="text-sm uppercase tracking-wide text-cyan-300">Issue 18</p>
              <h1 className="mt-2 text-4xl font-semibold">{artifactName}</h1>
              <p className="mt-3 max-w-3xl text-slate-400">
                {payload.recommendationType} · lifecycle {payload.lifecycleStatus} · readiness{" "}
                {readiness.storedReadinessState}
              </p>
              <p className="mt-2 text-sm text-slate-500">{payload.summary}</p>
            </div>
            <div className="flex flex-wrap gap-3">
              <ExplorerNavLink href="/recommendations">Recommendations</ExplorerNavLink>
              <ExplorerNavLink href="/explorers">Explorers</ExplorerNavLink>
              <Link
                href={`/artifacts/${artifactId}`}
                className="rounded-full border border-slate-700 px-4 py-2 text-sm font-semibold text-slate-200 transition hover:border-cyan-300 hover:text-cyan-100"
              >
                Artifact explorer
              </Link>
              {payload.explainability.aiTraceId ? (
                <Link
                  href={`/ai-traces/${payload.explainability.aiTraceId}`}
                  className="rounded-full border border-slate-700 px-4 py-2 text-sm font-semibold text-slate-200 transition hover:border-cyan-300 hover:text-cyan-100"
                >
                  AI trace
                </Link>
              ) : null}
            </div>
          </div>
          <div className="mt-6 flex flex-wrap gap-3">
            <ActionForm action={markReviewedAction} artifactId={artifactId} versionId={versionId} label="Mark reviewed" />
            <ActionForm action={markReadyAction} artifactId={artifactId} versionId={versionId} label="Mark ready" />
            <ActionForm action={publishAction} artifactId={artifactId} versionId={versionId} label="Publish" />
          </div>
        </section>

        <section className="grid gap-6 lg:grid-cols-2">
          <div className="rounded-3xl border border-slate-800 bg-slate-900 p-6">
            <h2 className="text-2xl font-semibold">Trust and readiness</h2>
            <ul className="mt-4 space-y-2 text-sm text-slate-300">
              <li>Trust: {payload.trustState}</li>
              <li>Conflict: {payload.conflictState}</li>
              <li>Risk: {payload.riskState}</li>
              <li>Capability: {payload.capabilityState}</li>
              <li>Creation source: {payload.creationSource}</li>
              <li>Stored readiness: {readiness.storedReadinessState}</li>
              <li>Recalculated readiness: {readiness.recalculatedReadinessState}</li>
              <li>Dependencies: {dependencyCount}</li>
            </ul>
            {readiness.blockingReasons.length > 0 ? (
              <ul className="mt-4 space-y-2 text-sm text-amber-200">
                {readiness.blockingReasons.map((reason) => (
                  <li key={reason}>{reason}</li>
                ))}
              </ul>
            ) : null}
          </div>

          <div className="rounded-3xl border border-slate-800 bg-slate-900 p-6">
            <h2 className="text-2xl font-semibold">Evidence links</h2>
            <ul className="mt-4 space-y-3 text-sm text-slate-300">
              {payload.evidenceLinks.map((link) => (
                <li key={link.linkId} className="rounded-2xl border border-slate-800 bg-slate-950 p-4">
                  <p className="font-semibold text-slate-100">{link.evidenceType}</p>
                  <p className="mt-1">{link.safeSummary}</p>
                  <p className="mt-2 text-xs uppercase text-slate-500">
                    trust {link.trustState}
                    {link.permissionFiltered ? " · filtered" : ""}
                  </p>
                </li>
              ))}
            </ul>
          </div>
        </section>

        <section className="rounded-3xl border border-slate-800 bg-slate-900 p-6">
          <h2 className="text-2xl font-semibold">Suggested actions</h2>
          <div className="mt-6 overflow-x-auto">
            <table className="min-w-full text-left text-sm text-slate-300">
              <thead className="text-xs uppercase text-slate-500">
                <tr>
                  <th className="px-3 py-2">Title</th>
                  <th className="px-3 py-2">Kind</th>
                  <th className="px-3 py-2">Risk</th>
                  <th className="px-3 py-2">Status</th>
                  <th className="px-3 py-2">Action</th>
                </tr>
              </thead>
              <tbody>
                {payload.suggestedActions.map((action) => (
                  <tr key={action.actionId} className="border-t border-slate-800">
                    <td className="px-3 py-3">{action.title}</td>
                    <td className="px-3 py-3">{action.kind}</td>
                    <td className="px-3 py-3">{action.riskScore}</td>
                    <td className="px-3 py-3">{action.status}</td>
                    <td className="px-3 py-3">
                      <ActionForm
                        action={selectActionForReviewAction}
                        artifactId={artifactId}
                        versionId={versionId}
                        label="Select for review"
                      >
                        <input type="hidden" name="actionId" value={action.actionId} />
                      </ActionForm>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          <p className="mt-4 text-xs text-slate-500">
            Review task creation from suggested actions is deferred to Issue 19. Status transitions are audited.
          </p>
        </section>
      </div>
    </main>
  );
}

export async function loadRecommendationDetail(
  artifactId: string,
  versionId?: string,
): Promise<
  ApiResult<{
    versionId: string;
    artifactName: string;
    payload: RecommendationPayload;
    readiness: ArtifactReadiness;
    dependencyCount: number;
  }>
> {
  const list = await getRecommendationArtifacts();
  if (!list.data) {
    return { data: null, error: list.error };
  }

  const artifact = list.data.find((item) => item.id === artifactId);
  if (!artifact) {
    return { data: null, error: "Recommendation artifact was not found." };
  }

  const versions = await getArtifactVersions(artifactId);
  if (!versions.data || versions.data.length === 0) {
    return { data: null, error: versions.error ?? "No versions found." };
  }

  const selectedVersionId = versionId ?? versions.data[0].id;
  const payload = await getRecommendationPayload(artifactId, selectedVersionId);
  if (!payload.data) {
    return { data: null, error: payload.error };
  }

  const readiness = await getArtifactReadiness(artifactId, selectedVersionId);
  if (!readiness.data) {
    return { data: null, error: readiness.error };
  }

  const impact = await getArtifactImpact(artifactId, selectedVersionId);

  return {
    data: {
      versionId: selectedVersionId,
      artifactName: artifact.name,
      payload: payload.data,
      readiness: readiness.data,
      dependencyCount: impact.data?.dependencies.length ?? 0,
    },
    error: null,
  };
}
