import Link from "next/link";
import { ExplorerListShell, ExplorerNavLink } from "@/components/explorers/ExplorerListShell";
import { GraphExplorerNodeSummary, getGraphExplorerNodes } from "@/lib/etos-api";

export const dynamic = "force-dynamic";

function GraphNodeCard(node: GraphExplorerNodeSummary) {
  return (
    <article className="rounded-2xl border border-slate-800 bg-slate-950 p-4">
      <div className="flex items-start justify-between gap-3">
        <div>
          <h3 className="font-semibold">{node.objectType}</h3>
          <p className="mt-1 text-sm text-slate-400">{node.safeSummary}</p>
          <p className="mt-2 text-xs text-slate-500">
            {node.graphSpace} · {node.trustState}
          </p>
        </div>
        <Link href={`/graph/${node.nodeId}`} className="text-sm font-semibold text-cyan-300 hover:text-cyan-200">
          360°
        </Link>
      </div>
    </article>
  );
}

export default async function GraphExplorerPage() {
  const nodes = await getGraphExplorerNodes();

  return (
    <main className="min-h-screen bg-slate-950 px-6 py-10 text-slate-100">
      <div className="mx-auto flex max-w-6xl flex-col gap-8">
        <section className="rounded-3xl border border-slate-800 bg-slate-900 p-8">
          <div className="flex flex-wrap items-start justify-between gap-4">
            <div>
              <h1 className="text-4xl font-semibold">Graph explorer</h1>
              <p className="mt-3 text-slate-400">Governed graph node browse with trust and policy-filtered summaries.</p>
            </div>
            <div className="flex flex-wrap gap-2">
              <ExplorerNavLink href="/explorers">Explorers</ExplorerNavLink>
              <ExplorerNavLink href="/">Home</ExplorerNavLink>
            </div>
          </div>
        </section>

        <ExplorerListShell
          title="Graph nodes"
          description="Trusted production-space nodes by default."
          result={nodes}
          emptyMessage="No graph nodes matched the current filters."
          renderItem={GraphNodeCard}
        />
      </div>
    </main>
  );
}
