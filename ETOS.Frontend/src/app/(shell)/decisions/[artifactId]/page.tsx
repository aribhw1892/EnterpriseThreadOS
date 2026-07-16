import Link from "next/link";
import { notFound } from "next/navigation";
import { DecisionDetailPanel } from "@/components/decisions/DecisionDetailPanel";
import { ExplorerNavLink } from "@/components/explorers/ExplorerListShell";
import { getArtifactVersions, getDecisionDetail, listDecisions } from "@/lib/etos-api";

export const dynamic = "force-dynamic";

type DecisionDetailPageProps = {
  params: Promise<{ artifactId: string }>;
};

export default async function DecisionDetailPage({ params }: DecisionDetailPageProps) {
  const { artifactId } = await params;
  const list = await listDecisions();
  const summary = list.data?.find((item) => item.artifactId === artifactId);
  if (!summary && list.error) {
    return (
      <main className="min-h-screen bg-slate-950 px-6 py-10 text-slate-100">
        <div className="mx-auto max-w-4xl rounded-2xl border border-amber-500/30 bg-amber-500/10 p-4 text-sm text-amber-100">
          {list.error}
        </div>
      </main>
    );
  }

  const versions = await getArtifactVersions(artifactId);
  const versionId = versions.data?.[0]?.id;
  if (!versionId) {
    notFound();
  }

  const detailResult = await getDecisionDetail(artifactId, versionId);
  if (!detailResult.data) {
    return (
      <main className="min-h-screen bg-slate-950 px-6 py-10 text-slate-100">
        <div className="mx-auto max-w-4xl rounded-2xl border border-amber-500/30 bg-amber-500/10 p-4 text-sm text-amber-100">
          {detailResult.error ?? "Decision detail could not be loaded."}
        </div>
      </main>
    );
  }

  const detail = detailResult.data;

  return (
    <main className="min-h-screen bg-slate-950 px-6 py-10 text-slate-100">
      <div className="mx-auto flex max-w-6xl flex-col gap-8">
        <section className="rounded-3xl border border-slate-800 bg-slate-900 p-8">
          <div className="flex flex-wrap items-start justify-between gap-4">
            <div>
              <p className="text-sm uppercase tracking-wide text-cyan-300">Decision</p>
              <h1 className="mt-2 text-4xl font-semibold">{detail.title}</h1>
              <p className="mt-3 text-slate-400">
                {detail.status} · outcome {detail.outcomeKey} · conflict {detail.conflictState}
              </p>
              <p className="mt-2 text-sm text-slate-300">{detail.outcomeSummary}</p>
            </div>
            <div className="flex flex-wrap gap-3">
              <ExplorerNavLink href="/decisions">Decision explorer</ExplorerNavLink>
              <Link href={`/tasks/${detail.reviewTaskArtifactId}`} className="text-sm font-semibold text-cyan-300 hover:text-cyan-200">
                Source review task
              </Link>
            </div>
          </div>
        </section>

        <DecisionDetailPanel artifactId={artifactId} versionId={versionId} initialDetail={detail} />
      </div>
    </main>
  );
}
