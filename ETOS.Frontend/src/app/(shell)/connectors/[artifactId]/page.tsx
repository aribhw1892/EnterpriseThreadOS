import Link from "next/link";
import { ExplorerNavLink } from "@/components/explorers/ExplorerListShell";
import {
  ApiResult,
  ConnectorDefinitionDetail,
  getArtifactVersions,
  getConnectorDefinitionArtifacts,
  getConnectorDefinitionDetail,
} from "@/lib/etos-api";

export const dynamic = "force-dynamic";

type PageProps = {
  params: Promise<{ artifactId: string }>;
  searchParams: Promise<{ versionId?: string }>;
};

async function loadConnectorDefinitionDetail(
  artifactId: string,
  versionId?: string,
): Promise<
  ApiResult<{
    versionId: string;
    artifactName: string;
    detail: ConnectorDefinitionDetail;
  }>
> {
  const list = await getConnectorDefinitionArtifacts();
  if (!list.data) {
    return { data: null, error: list.error };
  }

  const artifact = list.data.find((item) => item.id === artifactId);
  if (!artifact) {
    return { data: null, error: "Connector definition artifact was not found." };
  }

  const versions = await getArtifactVersions(artifactId);
  if (!versions.data || versions.data.length === 0) {
    return { data: null, error: versions.error ?? "No versions found." };
  }

  const selectedVersionId = versionId ?? versions.data[0].id;
  const detail = await getConnectorDefinitionDetail(artifactId, selectedVersionId);
  if (!detail.data) {
    return { data: null, error: detail.error };
  }

  return {
    data: {
      versionId: selectedVersionId,
      artifactName: artifact.name,
      detail: detail.data,
    },
    error: null,
  };
}

export default async function ConnectorDefinitionDetailPage({ params, searchParams }: PageProps) {
  const { artifactId } = await params;
  const { versionId } = await searchParams;
  const loaded = await loadConnectorDefinitionDetail(artifactId, versionId);

  if (!loaded.data) {
    return (
      <main className="min-h-screen bg-slate-950 px-6 py-10 text-slate-100">
        <div className="mx-auto max-w-3xl rounded-3xl border border-amber-500/30 bg-amber-500/10 p-6 text-sm text-amber-100">
          {loaded.error ?? "Connector definition was not found."}
        </div>
      </main>
    );
  }

  const { detail } = loaded.data;

  return (
    <main className="min-h-screen bg-slate-950 px-6 py-10 text-slate-100">
      <div className="mx-auto flex max-w-6xl flex-col gap-8">
        <section className="rounded-3xl border border-slate-800 bg-slate-900 p-8">
          <div className="flex flex-wrap items-start justify-between gap-4">
            <div>
              <p className="text-sm uppercase tracking-wide text-cyan-300">Issue 22 · Connector</p>
              <h1 className="mt-2 text-4xl font-semibold">{loaded.data.artifactName}</h1>
              <p className="mt-3 text-slate-400">
                {detail.connectorKey} · {detail.connectorKind} · {detail.versionLabel}
              </p>
            </div>
            <div className="flex flex-wrap gap-3">
              <ExplorerNavLink href="/tools">Tools</ExplorerNavLink>
              <ExplorerNavLink href="/explorers">Explorers</ExplorerNavLink>
            </div>
          </div>
        </section>

        <section className="rounded-3xl border border-slate-800 bg-slate-900 p-6">
          <h2 className="text-2xl font-semibold">Execution boundary</h2>
          <div className="mt-4 flex flex-wrap items-center gap-3">
            <span
              className={`rounded-full px-3 py-1 text-xs font-semibold uppercase tracking-wide ${
                detail.executionEnabled
                  ? "bg-emerald-500/20 text-emerald-200"
                  : "bg-amber-500/20 text-amber-200"
              }`}
            >
              {detail.executionEnabled ? "Execution enabled" : "Execution disabled"}
            </span>
            {detail.writesExternalSystem ? (
              <span className="rounded-full bg-rose-500/20 px-3 py-1 text-xs font-semibold uppercase tracking-wide text-rose-200">
                Write-capable contract
              </span>
            ) : null}
          </div>
          {detail.disabledReason ? (
            <p className="mt-4 text-sm text-amber-100">{detail.disabledReason}</p>
          ) : null}
        </section>

        <section className="rounded-3xl border border-slate-800 bg-slate-900 p-6">
          <h2 className="text-2xl font-semibold">Credential boundary</h2>
          <ul className="mt-4 space-y-2 text-sm text-slate-300">
            <li>Credential scope key: {detail.credentialScopeKey}</li>
            <li>Secret reference key: {detail.secretReferenceKey}</li>
            <li className="text-slate-400">
              Raw secret material is never returned through tool runs or connector APIs.
            </li>
          </ul>
        </section>

        <section className="rounded-3xl border border-slate-800 bg-slate-900 p-6">
          <h2 className="text-2xl font-semibold">Supported operations</h2>
          {detail.supportedOperations.length > 0 ? (
            <ul className="mt-4 space-y-1 text-sm text-slate-300">
              {detail.supportedOperations.map((operation) => (
                <li key={operation}>{operation}</li>
              ))}
            </ul>
          ) : (
            <p className="mt-4 text-sm text-slate-500">No supported operations declared.</p>
          )}
        </section>

        <p className="text-sm text-slate-500">
          <Link href="/tools" className="text-cyan-300 hover:text-cyan-100">
            Back to tool registry
          </Link>
        </p>
      </div>
    </main>
  );
}
