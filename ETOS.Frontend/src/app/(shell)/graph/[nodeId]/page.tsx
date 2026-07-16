import Link from "next/link";
import { ContextView360 } from "@/components/explorers/ContextView360";
import { GovernanceFlowPanel } from "@/components/explorers/GovernanceFlowPanel";
import { ExplorerErrorState, ExplorerNavLink } from "@/components/explorers/ExplorerListShell";
import { Button } from "@/components/ui/Button";
import { PageHeader } from "@/components/ui/PageHeader";
import {
  getContextView360,
  getGovernanceFlow,
  getGraphExplorerNode,
  getGraphExplorerRelationships,
} from "@/lib/etos-api";

export const dynamic = "force-dynamic";

export default async function GraphNodeDetailPage({
  params,
}: {
  params: Promise<{ nodeId: string }>;
}) {
  const { nodeId } = await params;
  const [node, view, flow, relationships] = await Promise.all([
    getGraphExplorerNode(nodeId),
    getContextView360("GraphNode", nodeId),
    getGovernanceFlow("GraphNode", nodeId),
    getGraphExplorerRelationships(nodeId, "both"),
  ]);

  return (
    <main className="px-6 py-8 lg:px-8">
      <PageHeader
        eyebrow="Graph"
        title="Graph node 360°"
        description={node.data?.safeSummary ?? `Node ${nodeId}`}
        actions={
          <>
            <ExplorerNavLink href="/graph">Graph</ExplorerNavLink>
            <ExplorerNavLink href={`/explorers/360/${nodeId}?kind=GraphNode`}>
              360 alias
            </ExplorerNavLink>
            {node.data ? (
              <Link href={node.data.chatRoute}>
                <Button type="button">Open chat</Button>
              </Link>
            ) : null}
          </>
        }
      />
      <p className="mb-6 font-mono text-sm text-etos-accent-cyan">{nodeId}</p>

      {view.error || !view.data ? (
        <ExplorerErrorState error={view.error ?? "Context view unavailable."} />
      ) : (
        <ContextView360
          view={view.data}
          graphCenter={node.data}
          relationships={relationships.data}
          relationshipsError={relationships.error}
        />
      )}
      {flow.data ? (
        <div className="mt-6">
          <GovernanceFlowPanel flow={flow.data} />
        </div>
      ) : null}
    </main>
  );
}
