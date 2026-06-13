import Link from "next/link";
import { ContextView360 } from "@/components/explorers/ContextView360";
import { GovernanceFlowPanel } from "@/components/explorers/GovernanceFlowPanel";
import { ExplorerErrorState, ExplorerNavLink } from "@/components/explorers/ExplorerListShell";
import { getContextView360, getGovernanceFlow, getGraphExplorerNode } from "@/lib/etos-api";

export const dynamic = "force-dynamic";

export default async function GraphNodeDetailPage({ params }: { params: Promise<{ nodeId: string }> }) {
  const { nodeId } = await params;
  const [node, view, flow] = await Promise.all([
    getGraphExplorerNode(nodeId),
    getContextView360("GraphNode", nodeId),
    getGovernanceFlow("GraphNode", nodeId),
  ]);

  return (
    <main className="min-h-screen bg-slate-950 px-6 py-10 text-slate-100">
      <div className="mx-auto flex max-w-6xl flex-col gap-8">
        <section className="rounded-3xl border border-slate-800 bg-slate-900 p-8">
          <div className="flex flex-wrap items-start justify-between gap-4">
            <div>
              <h1 className="text-4xl font-semibold">Graph node 360°</h1>
              <p className="mt-3 font-mono text-sm text-cyan-200">{nodeId}</p>
              {node.data ? <p className="mt-2 text-sm text-slate-400">{node.data.safeSummary}</p> : null}
            </div>
            <div className="flex flex-wrap gap-2">
              <ExplorerNavLink href="/graph">Graph</ExplorerNavLink>
              {node.data ? (
                <Link href={node.data.chatRoute} className="rounded-full bg-cyan-300 px-4 py-2 text-sm font-semibold text-slate-950">
                  Open chat
                </Link>
              ) : null}
            </div>
          </div>
        </section>

        {view.error || !view.data ? <ExplorerErrorState error={view.error ?? "Context view unavailable."} /> : <ContextView360 view={view.data} />}
        {flow.data ? <GovernanceFlowPanel flow={flow.data} /> : null}
      </div>
    </main>
  );
}
