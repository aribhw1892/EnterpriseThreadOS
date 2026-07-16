import Link from "next/link";
import { revalidatePath } from "next/cache";
import {
  ApiResult,
  ArtifactReadiness,
  CapabilityDefinitionDetail,
  getArtifactReadiness,
  getArtifactVersions,
  getCapabilityDefinitionArtifacts,
  getCapabilityDefinitionDetail,
  markCapabilityDefinitionReady,
  publishCapabilityDefinition,
} from "@/lib/etos-api";
import { ExplorerNavLink } from "@/components/explorers/ExplorerListShell";

type CapabilityDefinitionDetailProps = {
  artifactId: string;
  versionId: string;
  artifactName: string;
  detail: CapabilityDefinitionDetail;
  readiness: ArtifactReadiness;
};

async function markReadyAction(formData: FormData) {
  "use server";

  const artifactId = formData.get("artifactId");
  const versionId = formData.get("versionId");
  if (typeof artifactId !== "string" || typeof versionId !== "string") {
    return;
  }

  await markCapabilityDefinitionReady(artifactId, versionId);
  revalidatePath(`/capabilities/${artifactId}`);
}

async function publishAction(formData: FormData) {
  "use server";

  const artifactId = formData.get("artifactId");
  const versionId = formData.get("versionId");
  if (typeof artifactId !== "string" || typeof versionId !== "string") {
    return;
  }

  await publishCapabilityDefinition(artifactId, versionId, "Published from capability definition UI.");
  revalidatePath(`/capabilities/${artifactId}`);
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

export function CapabilityDefinitionDetailView({
  artifactId,
  versionId,
  artifactName,
  detail,
  readiness,
}: CapabilityDefinitionDetailProps) {
  return (
    <main className="">
      <div className="flex flex-col gap-6">
        <section className="rounded-etos-card border border-etos-border-panel bg-etos-panel-elevated p-6 shadow-etos">
          <div className="flex flex-wrap items-start justify-between gap-4">
            <div>
              <p className="text-sm uppercase tracking-wide text-etos-accent-cyan">Issue 18.2</p>
              <h1 className="mt-2 text-4xl font-semibold">{artifactName}</h1>
              <p className="mt-3 max-w-3xl text-etos-ink-muted">
                Capability version {detail.versionLabel} · {readiness.storedReadinessState}
              </p>
              {detail.description ? <p className="mt-2 text-sm text-etos-ink-subtle">{detail.description}</p> : null}
            </div>
            <div className="flex flex-wrap gap-3">
              <ExplorerNavLink href="/capabilities">Capabilities</ExplorerNavLink>
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
            <h2 className="text-2xl font-semibold">Outcome</h2>
            <dl className="mt-4 space-y-3 text-sm text-etos-ink">
              <div>
                <dt className="text-etos-ink-subtle">Capability key</dt>
                <dd>{detail.capabilityKey}</dd>
              </div>
              <div>
                <dt className="text-etos-ink-subtle">Outcome category</dt>
                <dd>{detail.outcomeCategory}</dd>
              </div>
              <div>
                <dt className="text-etos-ink-subtle">Outcome summary</dt>
                <dd>{detail.outcomeSummary}</dd>
              </div>
            </dl>
            {Object.keys(detail.outcomeMetadata).length > 0 ? (
              <div className="mt-4">
                <h3 className="text-sm font-semibold text-etos-ink-muted">Outcome metadata</h3>
                <ul className="mt-2 space-y-1 text-sm text-etos-ink">
                  {Object.entries(detail.outcomeMetadata).map(([key, value]) => (
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
            {readiness.blockingReasons.length > 0 ? (
              <ul className="mt-4 space-y-1 text-sm text-etos-warning-fg">
                {readiness.blockingReasons.map((reason) => (
                  <li key={reason}>{reason}</li>
                ))}
              </ul>
            ) : null}
          </div>
        </section>

        <section className="grid gap-6 lg:grid-cols-2">
          <div className="rounded-etos-card border border-etos-border-panel bg-etos-panel-elevated p-6 shadow-etos">
            <h2 className="text-2xl font-semibold">Compatible model packages</h2>
            {detail.compatibleModelPackages.length > 0 ? (
              <ul className="mt-4 space-y-3 text-sm text-etos-ink">
                {detail.compatibleModelPackages.map((item) => (
                  <li key={item.modelPackageVersionId} className="rounded-etos-card border border-etos-border-soft bg-etos-panel p-3">
                    <p className="font-semibold">{item.name}</p>
                    <p className="text-etos-ink-muted">
                      {item.key} · {item.versionLabel} · {item.state}
                    </p>
                  </li>
                ))}
              </ul>
            ) : (
              <p className="mt-4 text-sm text-etos-ink-subtle">No model package references.</p>
            )}
          </div>

          <div className="rounded-etos-card border border-etos-border-panel bg-etos-panel-elevated p-6 shadow-etos">
            <h2 className="text-2xl font-semibold">Compatible ontologies</h2>
            {detail.compatibleOntologies.length > 0 ? (
              <ul className="mt-4 space-y-3 text-sm text-etos-ink">
                {detail.compatibleOntologies.map((item) => (
                  <li key={item.ontologyVersionId} className="rounded-etos-card border border-etos-border-soft bg-etos-panel p-3">
                    <p className="font-semibold">{item.key}</p>
                    <p className="text-etos-ink-muted">
                      {item.versionLabel} · {item.state}
                    </p>
                  </li>
                ))}
              </ul>
            ) : (
              <p className="mt-4 text-sm text-etos-ink-subtle">No ontology references.</p>
            )}
          </div>
        </section>

        {(detail.suggestedQueryIntentRefs.length > 0 || detail.futureExtensionPlaceholders.length > 0) && (
          <section className="rounded-etos-card border border-etos-border-panel bg-etos-panel-elevated p-6 shadow-etos">
            <h2 className="text-2xl font-semibold">Future wiring placeholders</h2>
            {detail.suggestedQueryIntentRefs.length > 0 ? (
              <div className="mt-4">
                <h3 className="text-sm font-semibold text-etos-ink-muted">Suggested query intents</h3>
                <ul className="mt-2 space-y-1 text-sm text-etos-ink">
                  {detail.suggestedQueryIntentRefs.map((item) => (
                    <li key={item}>{item}</li>
                  ))}
                </ul>
              </div>
            ) : null}
            {detail.futureExtensionPlaceholders.length > 0 ? (
              <div className="mt-4">
                <h3 className="text-sm font-semibold text-etos-ink-muted">Extension placeholders</h3>
                <ul className="mt-2 space-y-1 text-sm text-etos-ink">
                  {detail.futureExtensionPlaceholders.map((item) => (
                    <li key={item}>{item}</li>
                  ))}
                </ul>
              </div>
            ) : null}
          </section>
        )}
      </div>
    </main>
  );
}

export async function loadCapabilityDefinitionDetail(
  artifactId: string,
  versionId?: string,
): Promise<
  ApiResult<{
    versionId: string;
    artifactName: string;
    detail: CapabilityDefinitionDetail;
    readiness: ArtifactReadiness;
  }>
> {
  const list = await getCapabilityDefinitionArtifacts();
  if (!list.data) {
    return { data: null, error: list.error };
  }

  const artifact = list.data.find((item) => item.id === artifactId);
  if (!artifact) {
    return { data: null, error: "Capability definition artifact was not found." };
  }

  const versions = await getArtifactVersions(artifactId);
  if (!versions.data || versions.data.length === 0) {
    return { data: null, error: versions.error ?? "No versions found." };
  }

  const selectedVersionId = versionId ?? versions.data[0].id;
  const detail = await getCapabilityDefinitionDetail(artifactId, selectedVersionId);
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
