import { ContextView360 } from "@/components/explorers/ContextView360";
import { GovernanceFlowPanel } from "@/components/explorers/GovernanceFlowPanel";
import { ExplorerErrorState, ExplorerNavLink } from "@/components/explorers/ExplorerListShell";
import { getContextView360, getGovernanceFlow } from "@/lib/etos-api";

export const dynamic = "force-dynamic";

export default async function DocumentDetailPage({ params }: { params: Promise<{ documentId: string }> }) {
  const { documentId } = await params;
  const [view, flow] = await Promise.all([
    getContextView360("Document", documentId),
    getGovernanceFlow("Document", documentId),
  ]);

  return (
    <main className="min-h-screen bg-slate-950 px-6 py-10 text-slate-100">
      <div className="mx-auto flex max-w-6xl flex-col gap-8">
        <section className="rounded-3xl border border-slate-800 bg-slate-900 p-8">
          <div className="flex flex-wrap items-start justify-between gap-4">
            <div>
              <h1 className="text-4xl font-semibold">Document 360°</h1>
              <p className="mt-3 font-mono text-sm text-cyan-200">{documentId}</p>
            </div>
            <div className="flex flex-wrap gap-2">
              <ExplorerNavLink href="/documents">Documents</ExplorerNavLink>
              <ExplorerNavLink href="/explorers">Explorers</ExplorerNavLink>
            </div>
          </div>
        </section>

        {view.error || !view.data ? <ExplorerErrorState error={view.error ?? "Context view unavailable."} /> : <ContextView360 view={view.data} />}
        {flow.data ? <GovernanceFlowPanel flow={flow.data} /> : null}
      </div>
    </main>
  );
}
