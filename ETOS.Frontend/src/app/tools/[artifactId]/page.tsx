import Link from "next/link";
import { ExplorerNavLink } from "@/components/explorers/ExplorerListShell";
import {
  ApiResult,
  ArtifactReadiness,
  ToolDefinitionDetail,
  getArtifactReadiness,
  getArtifactVersions,
  getToolDefinitionArtifacts,
  getToolDefinitionDetail,
} from "@/lib/etos-api";

export const dynamic = "force-dynamic";

type PageProps = {
  params: Promise<{ artifactId: string }>;
  searchParams: Promise<{ versionId?: string }>;
};

async function loadToolDefinitionDetail(
  artifactId: string,
  versionId?: string,
): Promise<
  ApiResult<{
    versionId: string;
    artifactName: string;
    detail: ToolDefinitionDetail;
    readiness: ArtifactReadiness;
  }>
> {
  const list = await getToolDefinitionArtifacts();
  if (!list.data) {
    return { data: null, error: list.error };
  }

  const artifact = list.data.find((item) => item.id === artifactId);
  if (!artifact) {
    return { data: null, error: "Tool definition artifact was not found." };
  }

  const versions = await getArtifactVersions(artifactId);
  if (!versions.data || versions.data.length === 0) {
    return { data: null, error: versions.error ?? "No versions found." };
  }

  const selectedVersionId = versionId ?? versions.data[0].id;
  const detail = await getToolDefinitionDetail(artifactId, selectedVersionId);
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

export default async function ToolDefinitionDetailPage({ params, searchParams }: PageProps) {
  const { artifactId } = await params;
  const { versionId } = await searchParams;
  const loaded = await loadToolDefinitionDetail(artifactId, versionId);

  if (!loaded.data) {
    return (
      <main className="min-h-screen bg-slate-950 px-6 py-10 text-slate-100">
        <div className="mx-auto max-w-3xl rounded-3xl border border-amber-500/30 bg-amber-500/10 p-6 text-sm text-amber-100">
          {loaded.error ?? "Tool definition was not found."}
        </div>
      </main>
    );
  }

  const { detail, readiness } = loaded.data;

  return (
    <main className="min-h-screen bg-slate-950 px-6 py-10 text-slate-100">
      <div className="mx-auto flex max-w-6xl flex-col gap-8">
        <section className="rounded-3xl border border-slate-800 bg-slate-900 p-8">
          <div className="flex flex-wrap items-start justify-between gap-4">
            <div>
              <p className="text-sm uppercase tracking-wide text-cyan-300">Issue 22 · Tool</p>
              <h1 className="mt-2 text-4xl font-semibold">{loaded.data.artifactName}</h1>
              <p className="mt-3 text-slate-400">
                {detail.toolKey} · {detail.versionLabel} · {detail.artifactReadinessState}
              </p>
            </div>
            <div className="flex flex-wrap gap-3">
              <ExplorerNavLink href="/tools">Tools</ExplorerNavLink>
              <ExplorerNavLink href="/explorers">Explorers</ExplorerNavLink>
            </div>
          </div>
        </section>

        <section className="rounded-3xl border border-slate-800 bg-slate-900 p-6">
          <h2 className="text-2xl font-semibold">Capability flags</h2>
          <ul className="mt-4 grid gap-2 text-sm text-slate-300 md:grid-cols-2">
            <li>Read-only: {detail.capabilityFlags.readOnly ? "Yes" : "No"}</li>
            <li>Calls external system: {detail.capabilityFlags.callsExternalSystem ? "Yes" : "No"}</li>
            <li>Writes external system: {detail.capabilityFlags.writesExternalSystem ? "Yes" : "No"}</li>
            <li>Supports dry-run: {detail.capabilityFlags.supportsDryRun ? "Yes" : "No"}</li>
            <li>Risk level: {detail.riskLevel}</li>
            <li>Handler: {detail.internalHandlerKey ?? "Connector-backed"}</li>
          </ul>
        </section>

        <section className="rounded-3xl border border-slate-800 bg-slate-900 p-6">
          <h2 className="text-2xl font-semibold">Compatibility and dependencies</h2>
          {readiness.blockingReasons.length > 0 ? (
            <ul className="mt-4 space-y-2 text-sm text-amber-100">
              {readiness.blockingReasons.map((reason) => (
                <li key={reason} className="rounded-xl border border-amber-500/30 bg-amber-500/10 px-3 py-2">
                  {reason}
                </li>
              ))}
            </ul>
          ) : (
            <p className="mt-4 text-sm text-slate-400">No blocking compatibility notes for this version.</p>
          )}

          {detail.compatibleModelPackages.length > 0 ? (
            <div className="mt-6">
              <h3 className="text-sm font-semibold text-slate-400">Compatible model packages</h3>
              <ul className="mt-2 space-y-1 text-sm text-slate-300">
                {detail.compatibleModelPackages.map((item) => (
                  <li key={item.modelPackageVersionId}>
                    {item.key} · {item.versionLabel} · {item.state}
                  </li>
                ))}
              </ul>
            </div>
          ) : null}

          {detail.referencedConnector ? (
            <div className="mt-6">
              <h3 className="text-sm font-semibold text-slate-400">Referenced connector</h3>
              <Link
                href={`/connectors/${detail.referencedConnector.connectorArtifactId}`}
                className="mt-2 inline-block text-sm text-cyan-300 hover:text-cyan-100"
              >
                {detail.referencedConnector.connectorKey} · {detail.referencedConnector.versionLabel}
              </Link>
            </div>
          ) : null}
        </section>

        <section className="rounded-3xl border border-slate-800 bg-slate-900 p-6">
          <h2 className="text-2xl font-semibold">Schemas</h2>
          <div className="mt-4 grid gap-4 lg:grid-cols-2">
            <pre className="overflow-x-auto rounded-2xl border border-slate-800 bg-slate-950 p-4 text-xs text-slate-300">
              {detail.inputSchemaJson}
            </pre>
            <pre className="overflow-x-auto rounded-2xl border border-slate-800 bg-slate-950 p-4 text-xs text-slate-300">
              {detail.outputSchemaJson}
            </pre>
          </div>
        </section>
      </div>
    </main>
  );
}
