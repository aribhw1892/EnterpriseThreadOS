import Link from "next/link";
import { revalidatePath } from "next/cache";
import {
  AgentTemplateDefinitionDetail,
  ApiResult,
  ArtifactReadiness,
  getAgentTemplateDefinitionArtifacts,
  getAgentTemplateDefinitionDetail,
  getArtifactReadiness,
  getArtifactVersions,
  markAgentTemplateDefinitionReady,
  publishAgentTemplateDefinition,
} from "@/lib/etos-api";
import { ExplorerNavLink } from "@/components/explorers/ExplorerListShell";

type AgentTemplateDefinitionDetailProps = {
  artifactId: string;
  versionId: string;
  artifactName: string;
  detail: AgentTemplateDefinitionDetail;
  readiness: ArtifactReadiness;
};

async function markReadyAction(formData: FormData) {
  "use server";

  const artifactId = formData.get("artifactId");
  const versionId = formData.get("versionId");
  if (typeof artifactId !== "string" || typeof versionId !== "string") {
    return;
  }

  await markAgentTemplateDefinitionReady(artifactId, versionId);
  revalidatePath(`/agent-templates/${artifactId}`);
}

async function publishAction(formData: FormData) {
  "use server";

  const artifactId = formData.get("artifactId");
  const versionId = formData.get("versionId");
  if (typeof artifactId !== "string" || typeof versionId !== "string") {
    return;
  }

  await publishAgentTemplateDefinition(artifactId, versionId, "Published from agent template UI.");
  revalidatePath(`/agent-templates/${artifactId}`);
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
        className="rounded-2xl border border-slate-700 px-4 py-2 text-sm font-semibold text-slate-200 transition hover:border-cyan-300 hover:text-cyan-100"
      >
        {label}
      </button>
    </form>
  );
}

export function AgentTemplateDefinitionDetailView({
  artifactId,
  versionId,
  artifactName,
  detail,
  readiness,
}: AgentTemplateDefinitionDetailProps) {
  return (
    <main className="min-h-screen bg-slate-950 px-6 py-10 text-slate-100">
      <div className="mx-auto flex max-w-6xl flex-col gap-8">
        <section className="rounded-3xl border border-slate-800 bg-slate-900 p-8">
          <div className="flex flex-wrap items-start justify-between gap-4">
            <div>
              <p className="text-sm uppercase tracking-wide text-cyan-300">Issue 18.4 · Layer 6</p>
              <h1 className="mt-2 text-4xl font-semibold">{artifactName}</h1>
              <p className="mt-3 max-w-3xl text-slate-400">
                Agent template version {detail.versionLabel} · {readiness.storedReadinessState} · not AgentVersion
              </p>
            </div>
            <div className="flex flex-wrap gap-3">
              <ExplorerNavLink href="/agent-templates">Agent templates</ExplorerNavLink>
              <ExplorerNavLink href="/explorers">Explorers</ExplorerNavLink>
              <Link
                href={`/artifacts/${artifactId}`}
                className="rounded-full border border-slate-700 px-4 py-2 text-sm font-semibold text-slate-200 transition hover:border-cyan-300 hover:text-cyan-100"
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
          <div className="rounded-3xl border border-slate-800 bg-slate-900 p-6">
            <h2 className="text-2xl font-semibold">Pattern</h2>
            <dl className="mt-4 space-y-3 text-sm text-slate-300">
              <div>
                <dt className="text-slate-500">Template key</dt>
                <dd>{detail.templateKey}</dd>
              </div>
              <div>
                <dt className="text-slate-500">Pattern category</dt>
                <dd>{detail.patternCategory}</dd>
              </div>
              <div>
                <dt className="text-slate-500">Pattern summary</dt>
                <dd>{detail.patternSummary}</dd>
              </div>
              <div>
                <dt className="text-slate-500">Preferred runtime adapter</dt>
                <dd>{detail.preferredRuntimeAdapterKey}</dd>
              </div>
            </dl>
          </div>

          <div className="rounded-3xl border border-slate-800 bg-slate-900 p-6">
            <h2 className="text-2xl font-semibold">Composition refs</h2>
            <dl className="mt-4 space-y-3 text-sm text-slate-300">
              <div>
                <dt className="text-slate-500">Prompt template</dt>
                <dd>
                  {detail.promptTemplate
                    ? `${detail.promptTemplate.artifactName} · ${detail.promptTemplate.versionLabel} · ${detail.promptTemplate.readinessState}`
                    : "None"}
                </dd>
              </div>
              <div>
                <dt className="text-slate-500">Output schema</dt>
                <dd>
                  {detail.outputSchema
                    ? `${detail.outputSchema.artifactName} · ${detail.outputSchema.versionLabel} · ${detail.outputSchema.readinessState}`
                    : "None"}
                </dd>
              </div>
              <div>
                <dt className="text-slate-500">Query intent</dt>
                <dd>
                  {detail.queryIntent
                    ? `${detail.queryIntent.intentKey} · ${detail.queryIntent.versionLabel} · enabled=${detail.queryIntent.isEnabled}`
                    : "None"}
                </dd>
              </div>
              <div>
                <dt className="text-slate-500">Retrieval strategy</dt>
                <dd>
                  {detail.retrievalStrategy
                    ? `${detail.retrievalStrategy.strategyKey} · ${detail.retrievalStrategy.versionLabel} · enabled=${detail.retrievalStrategy.isEnabled}`
                    : "None"}
                </dd>
              </div>
            </dl>
          </div>
        </section>

        <section className="grid gap-6 lg:grid-cols-2">
          <div className="rounded-3xl border border-slate-800 bg-slate-900 p-6">
            <h2 className="text-2xl font-semibold">Referenced capabilities</h2>
            {detail.referencedCapabilities.length > 0 ? (
              <ul className="mt-4 space-y-3 text-sm text-slate-300">
                {detail.referencedCapabilities.map((item) => (
                  <li key={item.capabilityDefinitionVersionId} className="rounded-2xl border border-slate-800 bg-slate-950 p-3">
                    <p className="font-semibold">{item.capabilityArtifactName}</p>
                    <p className="text-slate-400">
                      {item.capabilityKey} · {item.versionLabel}
                    </p>
                  </li>
                ))}
              </ul>
            ) : (
              <p className="mt-4 text-sm text-slate-500">No capability references.</p>
            )}
          </div>

          <div className="rounded-3xl border border-slate-800 bg-slate-900 p-6">
            <h2 className="text-2xl font-semibold">Referenced optimization models</h2>
            {detail.referencedOptimizationModels.length > 0 ? (
              <ul className="mt-4 space-y-3 text-sm text-slate-300">
                {detail.referencedOptimizationModels.map((item) => (
                  <li key={item.optimizationModelVersionId} className="rounded-2xl border border-slate-800 bg-slate-950 p-3">
                    <p className="font-semibold">{item.optimizationModelArtifactName}</p>
                    <p className="text-slate-400">
                      {item.optimizationKey} · {item.versionLabel}
                    </p>
                  </li>
                ))}
              </ul>
            ) : (
              <p className="mt-4 text-sm text-slate-500">No optimization model references.</p>
            )}
          </div>
        </section>
      </div>
    </main>
  );
}

export async function loadAgentTemplateDefinitionDetail(
  artifactId: string,
  versionId?: string,
): Promise<
  ApiResult<{
    versionId: string;
    artifactName: string;
    detail: AgentTemplateDefinitionDetail;
    readiness: ArtifactReadiness;
  }>
> {
  const list = await getAgentTemplateDefinitionArtifacts();
  if (!list.data) {
    return { data: null, error: list.error };
  }

  const artifact = list.data.find((item) => item.id === artifactId);
  if (!artifact) {
    return { data: null, error: "Agent template definition artifact was not found." };
  }

  const versions = await getArtifactVersions(artifactId);
  if (!versions.data || versions.data.length === 0) {
    return { data: null, error: versions.error ?? "No versions found." };
  }

  const selectedVersionId = versionId ?? versions.data[0].id;
  const detail = await getAgentTemplateDefinitionDetail(artifactId, selectedVersionId);
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
