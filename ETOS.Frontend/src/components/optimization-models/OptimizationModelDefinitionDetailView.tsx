import Link from "next/link";
import { revalidatePath } from "next/cache";
import {
  ApiResult,
  ArtifactReadiness,
  getArtifactReadiness,
  getArtifactVersions,
  getOptimizationModelDefinitionArtifacts,
  getOptimizationModelDefinitionDetail,
  markOptimizationModelDefinitionReady,
  OptimizationModelDefinitionDetail,
  publishOptimizationModelDefinition,
} from "@/lib/etos-api";
import { ExplorerNavLink } from "@/components/explorers/ExplorerListShell";

type OptimizationModelDefinitionDetailProps = {
  artifactId: string;
  versionId: string;
  artifactName: string;
  detail: OptimizationModelDefinitionDetail;
  readiness: ArtifactReadiness;
};

async function markReadyAction(formData: FormData) {
  "use server";

  const artifactId = formData.get("artifactId");
  const versionId = formData.get("versionId");
  if (typeof artifactId !== "string" || typeof versionId !== "string") {
    return;
  }

  await markOptimizationModelDefinitionReady(artifactId, versionId);
  revalidatePath(`/optimization-models/${artifactId}`);
}

async function publishAction(formData: FormData) {
  "use server";

  const artifactId = formData.get("artifactId");
  const versionId = formData.get("versionId");
  if (typeof artifactId !== "string" || typeof versionId !== "string") {
    return;
  }

  await publishOptimizationModelDefinition(artifactId, versionId, "Published from optimization model UI.");
  revalidatePath(`/optimization-models/${artifactId}`);
}

function ActionForm({
  action,
  artifactId,
  versionId,
  label,
}: {
  action: (formData: FormData) => Promise<void>;
  artifactId: string;
  versionId: string;
  label: string;
}) {
  return (
    <form action={action}>
      <input type="hidden" name="artifactId" value={artifactId} />
      <input type="hidden" name="versionId" value={versionId} />
      <button
        type="submit"
        className="inline-flex items-center rounded-etos-button border border-etos-border px-4 py-2 text-sm font-semibold text-etos-ink transition hover:bg-etos-panel-muted"
      >
        {label}
      </button>
    </form>
  );
}

export function OptimizationModelDefinitionDetailView({
  artifactId,
  versionId,
  artifactName,
  detail,
  readiness,
}: OptimizationModelDefinitionDetailProps) {
  return (
    <main className="">
      <div className="flex flex-col gap-6">
        <section className="rounded-etos-card border border-etos-border-panel bg-etos-panel-elevated p-6 shadow-etos">
          <div className="flex flex-wrap items-start justify-between gap-4">
            <div>
              <p className="text-sm uppercase tracking-wide text-etos-accent-cyan">Issue 18.4 · Layer 5</p>
              <h1 className="mt-2 text-4xl font-semibold">{artifactName}</h1>
              <p className="mt-3 max-w-3xl text-etos-ink-muted">
                Optimization model version {detail.versionLabel} · {readiness.storedReadinessState}
              </p>
            </div>
            <div className="flex flex-wrap gap-3">
              <ExplorerNavLink href="/optimization-models">Optimization models</ExplorerNavLink>
              <ExplorerNavLink href="/explorers">Explorers</ExplorerNavLink>
              <Link
                href={`/artifacts/${artifactId}`}
                className="rounded-full border border-etos-border px-4 py-2 text-sm font-semibold text-etos-ink transition hover:border-etos-accent hover:text-etos-accent"
              >
                Artifact explorer
              </Link>
            </div>
          </div>
          <div className="mt-6 flex flex-wrap gap-3">
            <ActionForm action={markReadyAction} artifactId={artifactId} versionId={versionId} label="Mark ready" />
            <ActionForm action={publishAction} artifactId={artifactId} versionId={versionId} label="Publish" />
          </div>
        </section>

        <section className="grid gap-6 lg:grid-cols-2">
          <div className="rounded-etos-card border border-etos-border-panel bg-etos-panel-elevated p-6 shadow-etos">
            <h2 className="text-2xl font-semibold">Objective</h2>
            <dl className="mt-4 space-y-3 text-sm text-etos-ink">
              <div>
                <dt className="text-etos-ink-subtle">Optimization key</dt>
                <dd>{detail.optimizationKey}</dd>
              </div>
              <div>
                <dt className="text-etos-ink-subtle">Objective category</dt>
                <dd>{detail.objectiveCategory}</dd>
              </div>
              <div>
                <dt className="text-etos-ink-subtle">Objective summary</dt>
                <dd>{detail.objectiveSummary}</dd>
              </div>
            </dl>
            {detail.inputRequirements.length > 0 ? (
              <div className="mt-4">
                <h3 className="text-sm font-semibold text-etos-ink-muted">Input requirements</h3>
                <ul className="mt-2 space-y-1 text-sm text-etos-ink">
                  {detail.inputRequirements.map((item) => (
                    <li key={item}>{item}</li>
                  ))}
                </ul>
              </div>
            ) : null}
            {Object.keys(detail.solverConfiguration).length > 0 ? (
              <div className="mt-4">
                <h3 className="text-sm font-semibold text-etos-ink-muted">Solver configuration (metadata only)</h3>
                <ul className="mt-2 space-y-1 text-sm text-etos-ink">
                  {Object.entries(detail.solverConfiguration).map(([key, value]) => (
                    <li key={key}>
                      {key}: {value}
                    </li>
                  ))}
                </ul>
              </div>
            ) : null}
          </div>

          <div className="rounded-etos-card border border-etos-border-panel bg-etos-panel-elevated p-6 shadow-etos">
            <h2 className="text-2xl font-semibold">Readiness</h2>
            <ul className="mt-4 space-y-2 text-sm text-etos-ink">
              <li>Stored: {readiness.storedReadinessState}</li>
              <li>Recalculated: {readiness.recalculatedReadinessState}</li>
              <li>Policy risk: {readiness.policyRiskStatus}</li>
            </ul>
          </div>
        </section>

        <section className="grid gap-6 lg:grid-cols-2">
          <div className="rounded-etos-card border border-etos-border-panel bg-etos-panel-elevated p-6 shadow-etos">
            <h2 className="text-2xl font-semibold">Referenced capabilities</h2>
            {detail.referencedCapabilities.length > 0 ? (
              <ul className="mt-4 space-y-3 text-sm text-etos-ink">
                {detail.referencedCapabilities.map((item) => (
                  <li key={item.capabilityDefinitionVersionId} className="rounded-etos-card border border-etos-border-soft bg-etos-panel p-3">
                    <Link
                      href={`/capabilities/${item.capabilityArtifactId}`}
                      className="font-semibold text-etos-accent hover:underline"
                    >
                      {item.capabilityArtifactName}
                    </Link>
                    <p className="text-etos-ink-muted">
                      {item.capabilityKey} · {item.versionLabel} · {item.readinessState}
                    </p>
                  </li>
                ))}
              </ul>
            ) : (
              <p className="mt-4 text-sm text-etos-ink-subtle">No capability references.</p>
            )}
          </div>

          <div className="rounded-etos-card border border-etos-border-panel bg-etos-panel-elevated p-6 shadow-etos">
            <h2 className="text-2xl font-semibold">Referenced business policies</h2>
            {detail.referencedBusinessPolicies.length > 0 ? (
              <ul className="mt-4 space-y-3 text-sm text-etos-ink">
                {detail.referencedBusinessPolicies.map((item) => (
                  <li key={item.businessPolicyDefinitionVersionId} className="rounded-etos-card border border-etos-border-soft bg-etos-panel p-3">
                    <Link
                      href={`/business-policies/${item.businessPolicyArtifactId}`}
                      className="font-semibold text-etos-accent hover:underline"
                    >
                      {item.businessPolicyArtifactName}
                    </Link>
                    <p className="text-etos-ink-muted">
                      {item.policyKey} · {item.versionLabel} · {item.readinessState}
                    </p>
                  </li>
                ))}
              </ul>
            ) : (
              <p className="mt-4 text-sm text-etos-ink-subtle">No business policy references.</p>
            )}
          </div>
        </section>
      </div>
    </main>
  );
}

export async function loadOptimizationModelDefinitionDetail(
  artifactId: string,
  versionId?: string,
): Promise<
  ApiResult<{
    versionId: string;
    artifactName: string;
    detail: OptimizationModelDefinitionDetail;
    readiness: ArtifactReadiness;
  }>
> {
  const list = await getOptimizationModelDefinitionArtifacts();
  if (!list.data) {
    return { data: null, error: list.error };
  }

  const artifact = list.data.find((item) => item.id === artifactId);
  if (!artifact) {
    return { data: null, error: "Optimization model definition artifact was not found." };
  }

  const versions = await getArtifactVersions(artifactId);
  if (!versions.data || versions.data.length === 0) {
    return { data: null, error: versions.error ?? "No versions found." };
  }

  const selectedVersionId = versionId ?? versions.data[0].id;
  const detail = await getOptimizationModelDefinitionDetail(artifactId, selectedVersionId);
  if (!detail.data) {
    return { data: null, error: detail.error };
  }

  const readiness = await getArtifactReadiness(artifactId, selectedVersionId);
  if (!readiness.data) {
    return { data: null, error: readiness.error };
  }

  return {
    data: {
      versionId: selectedVersionId,
      artifactName: artifact.name,
      detail: detail.data,
      readiness: readiness.data,
    },
    error: null,
  };
}
