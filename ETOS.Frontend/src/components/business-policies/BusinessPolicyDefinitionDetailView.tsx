import Link from "next/link";
import { revalidatePath } from "next/cache";
import {
  ApiResult,
  ArtifactReadiness,
  BusinessPolicyDefinitionDetail,
  getArtifactReadiness,
  getArtifactVersions,
  getBusinessPolicyDefinitionArtifacts,
  getBusinessPolicyDefinitionDetail,
  markBusinessPolicyDefinitionReady,
  publishBusinessPolicyDefinition,
} from "@/lib/etos-api";
import { ExplorerNavLink } from "@/components/explorers/ExplorerListShell";

type BusinessPolicyDefinitionDetailProps = {
  artifactId: string;
  versionId: string;
  artifactName: string;
  detail: BusinessPolicyDefinitionDetail;
  readiness: ArtifactReadiness;
};

async function markReadyAction(formData: FormData) {
  "use server";

  const artifactId = formData.get("artifactId");
  const versionId = formData.get("versionId");
  if (typeof artifactId !== "string" || typeof versionId !== "string") {
    return;
  }

  await markBusinessPolicyDefinitionReady(artifactId, versionId);
  revalidatePath(`/business-policies/${artifactId}`);
}

async function publishAction(formData: FormData) {
  "use server";

  const artifactId = formData.get("artifactId");
  const versionId = formData.get("versionId");
  if (typeof artifactId !== "string" || typeof versionId !== "string") {
    return;
  }

  await publishBusinessPolicyDefinition(artifactId, versionId, "Published from business policy definition UI.");
  revalidatePath(`/business-policies/${artifactId}`);
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

export function BusinessPolicyDefinitionDetailView({
  artifactId,
  versionId,
  artifactName,
  detail,
  readiness,
}: BusinessPolicyDefinitionDetailProps) {
  return (
    <main className="">
      <div className="flex flex-col gap-6">
        <section className="rounded-etos-card border border-etos-border-panel bg-etos-panel-elevated p-6 shadow-etos">
          <div className="flex flex-wrap items-start justify-between gap-4">
            <div>
              <p className="text-sm uppercase tracking-wide text-etos-accent-cyan">Issue 18.3 · Layer 4</p>
              <h1 className="mt-2 text-4xl font-semibold">{artifactName}</h1>
              <p className="mt-3 max-w-3xl text-etos-ink-muted">
                Business constraint policy version {detail.versionLabel} · {readiness.storedReadinessState}
              </p>
              {detail.description ? <p className="mt-2 text-sm text-etos-ink-subtle">{detail.description}</p> : null}
            </div>
            <div className="flex flex-wrap gap-3">
              <ExplorerNavLink href="/business-policies">Business policies</ExplorerNavLink>
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
            <h2 className="text-2xl font-semibold">Constraint</h2>
            <dl className="mt-4 space-y-3 text-sm text-etos-ink">
              <div>
                <dt className="text-etos-ink-subtle">Policy key</dt>
                <dd>{detail.policyKey}</dd>
              </div>
              <div>
                <dt className="text-etos-ink-subtle">Constraint category</dt>
                <dd>{detail.constraintCategory}</dd>
              </div>
              <div>
                <dt className="text-etos-ink-subtle">Constraint summary</dt>
                <dd>{detail.constraintSummary}</dd>
              </div>
            </dl>
            {Object.keys(detail.constraintRules).length > 0 ? (
              <div className="mt-4">
                <h3 className="text-sm font-semibold text-etos-ink-muted">Constraint rules</h3>
                <ul className="mt-2 space-y-1 text-sm text-etos-ink">
                  {Object.entries(detail.constraintRules).map(([key, value]) => (
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
        </section>

        <section className="rounded-etos-card border border-etos-border-panel bg-etos-panel-elevated p-6 shadow-etos">
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
        </section>

        {detail.futureExtensionPlaceholders.length > 0 && (
          <section className="rounded-etos-card border border-etos-border-panel bg-etos-panel-elevated p-6 shadow-etos">
            <h2 className="text-2xl font-semibold">Future wiring placeholders</h2>
            <ul className="mt-4 space-y-1 text-sm text-etos-ink">
              {detail.futureExtensionPlaceholders.map((item) => (
                <li key={item}>{item}</li>
              ))}
            </ul>
          </section>
        )}
      </div>
    </main>
  );
}

export async function loadBusinessPolicyDefinitionDetail(
  artifactId: string,
  versionId?: string,
): Promise<
  ApiResult<{
    versionId: string;
    artifactName: string;
    detail: BusinessPolicyDefinitionDetail;
    readiness: ArtifactReadiness;
  }>
> {
  const list = await getBusinessPolicyDefinitionArtifacts();
  if (!list.data) {
    return { data: null, error: list.error };
  }

  const artifact = list.data.find((item) => item.id === artifactId);
  if (!artifact) {
    return { data: null, error: "Business policy definition artifact was not found." };
  }

  const versions = await getArtifactVersions(artifactId);
  if (!versions.data || versions.data.length === 0) {
    return { data: null, error: versions.error ?? "No versions found." };
  }

  const selectedVersionId = versionId ?? versions.data[0].id;
  const detail = await getBusinessPolicyDefinitionDetail(artifactId, selectedVersionId);
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
