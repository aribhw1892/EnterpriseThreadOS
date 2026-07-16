import Link from "next/link";
import type { GovernanceFlow } from "@/lib/etos-api";
import { StatusBadge } from "@/components/ui/Badge";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/Card";

export function GovernanceFlowPanel({ flow }: { flow: GovernanceFlow }) {
  return (
    <Card>
      <CardHeader>
        <CardTitle>Governance flow</CardTitle>
        <CardDescription>
          Relationship, dependency, trace, and audit edges with Milestone 4 review-chain placeholders.
        </CardDescription>
      </CardHeader>
      <CardContent className="space-y-6">
        <div className="grid gap-3">
          <h3 className="text-sm font-semibold uppercase tracking-wide text-etos-ink-subtle">Nodes</h3>
          {flow.nodes.map((node) => (
            <article
              key={node.nodeId}
              className="rounded-etos-card border border-etos-border-soft bg-etos-panel p-4"
            >
              <div className="flex items-start justify-between gap-3">
                <div>
                  <p className="font-medium text-etos-ink">{node.title}</p>
                  <p className="mt-1 text-sm text-etos-ink-muted">{node.safeSummary}</p>
                  <p className="mt-2 text-xs text-etos-ink-subtle">
                    {node.kind} · {node.status}
                  </p>
                </div>
                {node.linkRoute ? (
                  <Link href={node.linkRoute} className="text-sm font-semibold text-etos-accent hover:underline">
                    Open
                  </Link>
                ) : null}
              </div>
            </article>
          ))}
        </div>

        <div className="grid gap-3">
          <h3 className="text-sm font-semibold uppercase tracking-wide text-etos-ink-subtle">Edges</h3>
          {flow.edges.map((edge) => (
            <p
              key={edge.edgeId}
              className="rounded-etos-card border border-etos-border-soft bg-etos-panel px-4 py-3 text-sm text-etos-ink"
            >
              {edge.label} ({edge.kind})
            </p>
          ))}
        </div>

        <div className="grid gap-3">
          <h3 className="text-sm font-semibold uppercase tracking-wide text-etos-ink-subtle">
            Future chain placeholders
          </h3>
          {flow.futureChainPlaceholders.map((placeholder) => (
            <article
              key={placeholder.kind}
              className="rounded-etos-card border border-dashed border-etos-warning-border bg-etos-warning-bg p-4"
            >
              <div className="flex items-center justify-between gap-3">
                <div>
                  <p className="font-medium text-etos-ink">{placeholder.title}</p>
                  <p className="mt-1 text-sm text-etos-ink-muted">{placeholder.safeSummary}</p>
                </div>
                <StatusBadge status={placeholder.status} />
              </div>
            </article>
          ))}
        </div>
      </CardContent>
    </Card>
  );
}
