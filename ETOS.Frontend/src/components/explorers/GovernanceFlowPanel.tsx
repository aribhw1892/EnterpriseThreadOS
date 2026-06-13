import Link from "next/link";
import type { GovernanceFlow } from "@/lib/etos-api";

export function GovernanceFlowPanel({ flow }: { flow: GovernanceFlow }) {
  return (
    <section className="rounded-3xl border border-slate-800 bg-slate-900 p-6">
      <div className="mb-5">
        <h2 className="text-2xl font-semibold">Governance flow</h2>
        <p className="mt-1 text-sm text-slate-400">
          Relationship, dependency, trace, and audit edges with Milestone 4 review-chain placeholders.
        </p>
      </div>

      <div className="mb-6 grid gap-3">
        <h3 className="text-sm font-semibold uppercase tracking-wide text-slate-500">Nodes</h3>
        {flow.nodes.map((node) => (
          <article key={node.nodeId} className="rounded-2xl border border-slate-800 bg-slate-950 p-4">
            <div className="flex items-start justify-between gap-3">
              <div>
                <p className="font-medium">{node.title}</p>
                <p className="mt-1 text-sm text-slate-400">{node.safeSummary}</p>
                <p className="mt-2 text-xs text-slate-500">
                  {node.kind} · {node.status}
                </p>
              </div>
              {node.linkRoute ? (
                <Link href={node.linkRoute} className="text-sm font-semibold text-cyan-300 hover:text-cyan-200">
                  Open
                </Link>
              ) : null}
            </div>
          </article>
        ))}
      </div>

      <div className="mb-6 grid gap-3">
        <h3 className="text-sm font-semibold uppercase tracking-wide text-slate-500">Edges</h3>
        {flow.edges.map((edge) => (
          <p key={edge.edgeId} className="rounded-xl border border-slate-800 bg-slate-950 px-4 py-3 text-sm text-slate-300">
            {edge.label} ({edge.kind})
          </p>
        ))}
      </div>

      <div className="grid gap-3">
        <h3 className="text-sm font-semibold uppercase tracking-wide text-slate-500">Future chain placeholders</h3>
        {flow.futureChainPlaceholders.map((placeholder) => (
          <article key={placeholder.kind} className="rounded-2xl border border-dashed border-amber-500/40 bg-amber-500/5 p-4">
            <div className="flex items-center justify-between gap-3">
              <div>
                <p className="font-medium">{placeholder.title}</p>
                <p className="mt-1 text-sm text-slate-400">{placeholder.safeSummary}</p>
              </div>
              <span className="rounded-full bg-amber-500/20 px-3 py-1 text-xs font-semibold uppercase text-amber-100">
                {placeholder.status}
              </span>
            </div>
          </article>
        ))}
      </div>
    </section>
  );
}
