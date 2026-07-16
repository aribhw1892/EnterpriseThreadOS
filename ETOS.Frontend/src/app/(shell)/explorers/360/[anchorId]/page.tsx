import Link from "next/link";
import { ContextView360 } from "@/components/explorers/ContextView360";
import { GovernanceFlowPanel } from "@/components/explorers/GovernanceFlowPanel";
import { ExplorerErrorState } from "@/components/explorers/ExplorerListShell";
import { Button } from "@/components/ui/Button";
import {
  getContextView360,
  getGovernanceFlow,
  getGraphExplorerNode,
  getGraphExplorerRelationships,
} from "@/lib/etos-api";

export const dynamic = "force-dynamic";

const ANCHOR_KINDS = [
  "GraphNode",
  "Artifact",
  "Document",
  "ContextPackage",
  "AiTrace",
] as const;

type PageProps = {
  params: Promise<{ anchorId: string }>;
  searchParams: Promise<{ kind?: string }>;
};

export default async function Explorer360AliasPage({
  params,
  searchParams,
}: PageProps) {
  const { anchorId } = await params;
  const { kind: kindParam } = await searchParams;
  const kind = ANCHOR_KINDS.includes(kindParam as (typeof ANCHOR_KINDS)[number])
    ? (kindParam as (typeof ANCHOR_KINDS)[number])
    : "GraphNode";

  const [node, view, flow, relationships] = await Promise.all([
    kind === "GraphNode"
      ? getGraphExplorerNode(anchorId)
      : Promise.resolve({ data: null, error: null as string | null }),
    getContextView360(kind, anchorId),
    getGovernanceFlow(kind, anchorId),
    kind === "GraphNode"
      ? getGraphExplorerRelationships(anchorId, "both")
      : Promise.resolve({ data: null, error: null as string | null }),
  ]);

  return (
    <main className="px-6 py-8 lg:px-8">
      <div className="mb-[18px] flex flex-wrap items-start justify-between gap-4">
        <div>
          <h1 className="text-[30px] font-bold tracking-tight text-etos-ink">
            Graph explorer & 360° context
          </h1>
          <p className="mt-2 max-w-[900px] text-sm text-etos-ink-muted">
            Connected enterprise context with evidence, quality issues,
            recommendations, traces, documents, and relationship navigation.
          </p>
        </div>
        <div className="flex flex-wrap gap-2.5">
          <Link href="/explorers">
            <Button variant="ghost">Explorers</Button>
          </Link>
          {node.data?.chatRoute ? (
            <Link href={node.data.chatRoute}>
              <Button variant="primary">Open chat</Button>
            </Link>
          ) : null}
        </div>
      </div>

      {view.error || !view.data ? (
        <ExplorerErrorState
          error={view.error ?? "Context view unavailable."}
        />
      ) : (
        <ContextView360
          view={view.data}
          graphCenter={node.data}
          relationships={relationships.data}
          relationshipsError={relationships.error}
        />
      )}

      <details className="mt-6 rounded-etos-card border border-etos-border bg-etos-panel-muted p-4">
        <summary className="cursor-pointer text-sm font-extrabold text-etos-ink">
          Advanced / Debug
        </summary>
        <p className="mt-2 text-xs text-etos-ink-muted">
          Kind={kind} · Anchor={anchorId}. Pass ?kind=GraphNode|Artifact|Document|ContextPackage|AiTrace
        </p>
        {node.error ? <ExplorerErrorState error={node.error} /> : null}
        {flow.data ? (
          <div className="mt-4">
            <GovernanceFlowPanel flow={flow.data} />
          </div>
        ) : null}
      </details>
    </main>
  );
}
