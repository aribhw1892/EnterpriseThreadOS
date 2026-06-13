import Link from "next/link";
import { ExplorerListShell, ExplorerNavLink } from "@/components/explorers/ExplorerListShell";
import { ArtifactExplorerSummary, getExplorerArtifacts } from "@/lib/etos-api";

export const dynamic = "force-dynamic";

function ArtifactCard(artifact: ArtifactExplorerSummary) {
  return (
    <article className="rounded-2xl border border-slate-800 bg-slate-950 p-4">
      <div className="flex items-start justify-between gap-3">
        <div>
          <h3 className="font-semibold">{artifact.name}</h3>
          <p className="mt-1 text-sm text-slate-400">{artifact.safeSummary}</p>
          <p className="mt-2 text-xs text-slate-500">
            {artifact.artifactType} · {artifact.lifecycleState}
            {artifact.latestVersionLabel ? ` · ${artifact.latestVersionLabel}` : ""}
          </p>
        </div>
        <Link href={`/artifacts/${artifact.id}`} className="text-sm font-semibold text-cyan-300 hover:text-cyan-200">
          360°
        </Link>
      </div>
    </article>
  );
}

export default async function ArtifactsExplorerPage() {
  const artifacts = await getExplorerArtifacts();

  return (
    <main className="min-h-screen bg-slate-950 px-6 py-10 text-slate-100">
      <div className="mx-auto flex max-w-6xl flex-col gap-8">
        <section className="rounded-3xl border border-slate-800 bg-slate-900 p-8">
          <div className="flex flex-wrap items-start justify-between gap-4">
            <div>
              <h1 className="text-4xl font-semibold">Artifact explorer</h1>
              <p className="mt-3 text-slate-400">Governed artifact list with drill-down into 360° context views.</p>
            </div>
            <div className="flex flex-wrap gap-2">
              <ExplorerNavLink href="/explorers">Explorers</ExplorerNavLink>
              <ExplorerNavLink href="/">Home</ExplorerNavLink>
            </div>
          </div>
        </section>

        <ExplorerListShell
          title="Artifacts"
          description="Tenant-scoped artifact registry summaries."
          result={artifacts}
          emptyMessage="No artifacts are available for the selected tenant."
          renderItem={ArtifactCard}
        />
      </div>
    </main>
  );
}
