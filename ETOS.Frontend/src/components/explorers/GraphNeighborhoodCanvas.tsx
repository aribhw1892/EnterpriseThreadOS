"use client";

import { useEffect, useMemo, useRef, useState, useTransition } from "react";
import { useRouter } from "next/navigation";
import Graph from "graphology";
import forceAtlas2 from "graphology-layout-forceatlas2";
import Sigma from "sigma";
import { EdgeArrowProgram } from "sigma/rendering";
import type { GraphExplorerNodeDetail, GraphExplorerRelationship } from "@/lib/etos-api";
import { loadGraphNeighborhood } from "@/app/(shell)/explorers/actions";
import { Button } from "@/components/ui/Button";

export type GraphNeighborhoodCanvasProps = {
  center: Pick<
    GraphExplorerNodeDetail,
    "nodeId" | "objectType" | "trustState" | "graphSpace" | "safeSummary"
  >;
  relationships: GraphExplorerRelationship[];
  filterSummary?: {
    visibleSectionCount: number;
    deniedSectionCount: number;
  };
};

type GraphNodeModel = {
  nodeId: string;
  objectType: string;
  trustState: string;
  safeSummary: string;
  isCenter?: boolean;
};

type GraphEdgeModel = {
  relationshipId: string;
  relationshipType: string;
  fromNodeId: string;
  toNodeId: string;
};

type NodeAttrs = {
  label: string;
  size: number;
  color: string;
  x: number;
  y: number;
  objectType: string;
  trustState: string;
  safeSummary: string;
  isCenter?: boolean;
};

type EdgeAttrs = {
  label: string;
  size: number;
  color: string;
  type?: string;
};

const TYPE_COLORS: Record<string, string> = {
  part: "#0ea5e9",
  partversion: "#2563eb",
  document: "#7c3aed",
  file: "#8b5cf6",
  bom: "#059669",
  supplier: "#d97706",
  organization: "#ea580c",
  default: "#64748b",
};

function colorForObjectType(objectType: string): string {
  const key = objectType.replace(/[^a-z0-9]/gi, "").toLowerCase();
  for (const [token, color] of Object.entries(TYPE_COLORS)) {
    if (token !== "default" && key.includes(token)) {
      return color;
    }
  }
  return TYPE_COLORS.default;
}

function shortLabel(text: string, max = 28): string {
  const trimmed = text.trim();
  if (trimmed.length <= max) {
    return trimmed;
  }
  return `${trimmed.slice(0, max - 1)}…`;
}

function readThemeColors(container: HTMLElement) {
  const styles = getComputedStyle(container);
  return {
    label: styles.getPropertyValue("--etos-ink").trim() || "#0f172a",
    edge: styles.getPropertyValue("--etos-ink-subtle").trim() || "#94a3b8",
  };
}

function relationshipsToModels(
  focusNodeId: string,
  relationships: GraphExplorerRelationship[],
): { nodes: GraphNodeModel[]; edges: GraphEdgeModel[] } {
  const nodes = new Map<string, GraphNodeModel>();
  const edges: GraphEdgeModel[] = [];

  for (const rel of relationships) {
    const adjacentId = rel.adjacentNodeId;
    if (!nodes.has(adjacentId)) {
      nodes.set(adjacentId, {
        nodeId: adjacentId,
        objectType: rel.adjacentObjectType,
        trustState: rel.trustState,
        safeSummary: rel.safeSummary,
      });
    }

    const fromNodeId = rel.direction === "in" ? adjacentId : focusNodeId;
    const toNodeId = rel.direction === "in" ? focusNodeId : adjacentId;
    edges.push({
      relationshipId: rel.relationshipId,
      relationshipType: rel.relationshipType,
      fromNodeId,
      toNodeId,
    });
  }

  return { nodes: Array.from(nodes.values()), edges };
}

function mergeGraph(
  baseNodes: GraphNodeModel[],
  baseEdges: GraphEdgeModel[],
  extraNodes: GraphNodeModel[],
  extraEdges: GraphEdgeModel[],
) {
  const nodeMap = new Map(baseNodes.map((n) => [n.nodeId, n]));
  for (const node of extraNodes) {
    if (!nodeMap.has(node.nodeId)) {
      nodeMap.set(node.nodeId, node);
    }
  }
  const edgeMap = new Map(baseEdges.map((e) => [e.relationshipId, e]));
  for (const edge of extraEdges) {
    edgeMap.set(edge.relationshipId, edge);
  }
  return {
    nodes: Array.from(nodeMap.values()),
    edges: Array.from(edgeMap.values()),
  };
}

function buildSigmaGraph(
  nodes: GraphNodeModel[],
  edges: GraphEdgeModel[],
  theme: { label: string; edge: string },
): Graph<NodeAttrs, EdgeAttrs> {
  const graph = new Graph<NodeAttrs, EdgeAttrs>({ multi: true, type: "directed" });

  nodes.forEach((node, index) => {
    const angle = (index * 2 * Math.PI) / Math.max(nodes.length, 1);
    graph.addNode(node.nodeId, {
      label: shortLabel(node.objectType || node.safeSummary || node.nodeId),
      size: node.isCenter ? 18 : 11,
      color: node.isCenter ? "#2563eb" : colorForObjectType(node.objectType),
      x: node.isCenter ? 0 : Math.cos(angle) * 140,
      y: node.isCenter ? 0 : Math.sin(angle) * 140,
      objectType: node.objectType,
      trustState: node.trustState,
      safeSummary: node.safeSummary,
      isCenter: node.isCenter,
    });
  });

  for (const edge of edges) {
    if (!graph.hasNode(edge.fromNodeId) || !graph.hasNode(edge.toNodeId)) {
      continue;
    }
    if (graph.hasEdge(edge.relationshipId)) {
      continue;
    }
    graph.addEdgeWithKey(edge.relationshipId, edge.fromNodeId, edge.toNodeId, {
      label: edge.relationshipType,
      size: 1.4,
      color: theme.edge,
      type: "arrow",
    });
  }

  if (graph.order > 1) {
    forceAtlas2.assign(graph, {
      iterations: 90,
      settings: {
        gravity: 1.2,
        scalingRatio: 12,
        strongGravityMode: true,
        slowDown: 2,
        barnesHutOptimize: graph.order > 40,
      },
    });
  }

  return graph;
}

export function GraphNeighborhoodCanvas({
  center,
  relationships: initialRelationships,
  filterSummary,
}: GraphNeighborhoodCanvasProps) {
  const router = useRouter();
  const containerRef = useRef<HTMLDivElement>(null);
  const sigmaRef = useRef<Sigma | null>(null);

  const base = useMemo(() => {
    const mapped = relationshipsToModels(center.nodeId, initialRelationships);
    const nodeMap = new Map<string, GraphNodeModel>();
    nodeMap.set(center.nodeId, {
      nodeId: center.nodeId,
      objectType: center.objectType,
      trustState: center.trustState,
      safeSummary: center.safeSummary,
      isCenter: true,
    });
    for (const node of mapped.nodes) {
      if (!nodeMap.has(node.nodeId)) {
        nodeMap.set(node.nodeId, node);
      }
    }
    return {
      nodes: Array.from(nodeMap.values()),
      edges: mapped.edges,
    };
  }, [center, initialRelationships]);

  const [seedNodeId, setSeedNodeId] = useState(center.nodeId);
  const [extraNodes, setExtraNodes] = useState<GraphNodeModel[]>([]);
  const [extraEdges, setExtraEdges] = useState<GraphEdgeModel[]>([]);
  const [selectedId, setSelectedId] = useState(center.nodeId);
  const [hoverId, setHoverId] = useState<string | null>(null);
  const [status, setStatus] = useState<string | null>(null);
  const [pending, startTransition] = useTransition();

  if (seedNodeId !== center.nodeId) {
    setSeedNodeId(center.nodeId);
    setExtraNodes([]);
    setExtraEdges([]);
    setSelectedId(center.nodeId);
    setStatus(null);
  }

  const { nodes, edges } = useMemo(
    () => mergeGraph(base.nodes, base.edges, extraNodes, extraEdges),
    [base, extraNodes, extraEdges],
  );

  const selected = nodes.find((n) => n.nodeId === selectedId) ?? nodes[0];

  function expandAround(nodeId: string) {
    setStatus(null);
    startTransition(async () => {
      const result = await loadGraphNeighborhood(nodeId, "both");
      if (result.error || !result.data) {
        setStatus(result.error ?? "Could not load neighborhood.");
        return;
      }

      const mapped = relationshipsToModels(nodeId, result.data);
      setExtraNodes((prev) => {
        const map = new Map(prev.map((n) => [n.nodeId, n]));
        for (const node of mapped.nodes) {
          if (!map.has(node.nodeId)) {
            map.set(node.nodeId, node);
          }
        }
        return Array.from(map.values());
      });
      setExtraEdges((prev) => {
        const map = new Map(prev.map((e) => [e.relationshipId, e]));
        for (const edge of mapped.edges) {
          map.set(edge.relationshipId, edge);
        }
        return Array.from(map.values());
      });
      setSelectedId(nodeId);
      setStatus(
        result.data.length === 0
          ? "No additional relationships in Trusted/Provisional space."
          : `Expanded ${result.data.length} relationship(s).`,
      );
    });
  }

  const expandAroundRef = useRef(expandAround);

  useEffect(() => {
    expandAroundRef.current = expandAround;
  });

  useEffect(() => {
    const container = containerRef.current;
    if (!container) {
      return;
    }

    const theme = readThemeColors(container);
    const graph = buildSigmaGraph(nodes, edges, theme);
    const renderer = new Sigma(graph, container, {
      allowInvalidContainer: true,
      renderLabels: true,
      renderEdgeLabels: edges.length <= 48,
      labelFont: "Inter, ui-sans-serif, system-ui, sans-serif",
      labelSize: 11,
      labelWeight: "700",
      labelColor: { color: theme.label },
      defaultEdgeType: "arrow",
      defaultEdgeColor: theme.edge,
      stagePadding: 36,
      edgeProgramClasses: {
        arrow: EdgeArrowProgram,
      },
    });

    sigmaRef.current = renderer;

    const onClickNode = ({ node }: { node: string }) => {
      setSelectedId(node);
    };
    const onEnterNode = ({ node }: { node: string }) => {
      setHoverId(node);
      container.style.cursor = "pointer";
    };
    const onLeaveNode = () => {
      setHoverId(null);
      container.style.cursor = "grab";
    };
    const onDoubleClickNode = ({
      node,
      event,
    }: {
      node: string;
      event: { preventSigmaDefault: () => void };
    }) => {
      event.preventSigmaDefault();
      expandAroundRef.current(node);
    };

    renderer.on("clickNode", onClickNode);
    renderer.on("enterNode", onEnterNode);
    renderer.on("leaveNode", onLeaveNode);
    renderer.on("doubleClickNode", onDoubleClickNode);

    return () => {
      renderer.kill();
      sigmaRef.current = null;
    };
  }, [nodes, edges]);

  function fitView() {
    sigmaRef.current?.getCamera().animatedReset({ duration: 280 });
  }

  return (
    <div className="space-y-3">
      <div className="relative min-h-[420px] overflow-hidden rounded-etos-card border border-etos-border bg-etos-panel-muted">
        <div
          ref={containerRef}
          className="absolute inset-0 cursor-grab active:cursor-grabbing"
          aria-label="Graph neighborhood canvas"
        />

        <div className="pointer-events-none absolute left-3 top-3 z-10 flex flex-wrap gap-2">
          <span className="rounded-xl border border-etos-border bg-etos-panel/95 px-2.5 py-1 text-[11px] font-extrabold text-etos-ink">
            {center.objectType}
          </span>
          <span className="rounded-xl border border-etos-border bg-etos-panel/95 px-2.5 py-1 text-[11px] text-etos-ink-muted">
            {center.graphSpace} · {center.trustState}
          </span>
          <span className="rounded-xl border border-etos-border bg-etos-panel/95 px-2.5 py-1 text-[11px] text-etos-ink-muted">
            {nodes.length} nodes · {edges.length} edges
          </span>
        </div>

        <div className="absolute bottom-3 left-3 right-3 z-10 flex flex-wrap items-end justify-between gap-2">
          <div className="max-w-[70%] rounded-xl border border-etos-border bg-etos-panel/95 px-3 py-2 text-[11px] text-etos-ink-muted">
            {filterSummary ? (
              <p>
                Permission-filtered · Trust-aware · Tenant scoped · Visible{" "}
                {filterSummary.visibleSectionCount} · Denied{" "}
                {filterSummary.deniedSectionCount}
              </p>
            ) : (
              <p>Drag · scroll zoom · click select · double-click expand</p>
            )}
            {hoverId ? (
              <p className="mt-1 font-mono text-[10px] text-etos-accent-cyan">
                Hover {hoverId}
              </p>
            ) : null}
            {status ? <p className="mt-1 text-etos-warning-fg">{status}</p> : null}
          </div>
          <div className="pointer-events-auto flex gap-2">
            <Button
              type="button"
              variant="ghost"
              disabled={pending}
              onClick={() => expandAround(selectedId)}
            >
              {pending ? "Loading…" : "Expand"}
            </Button>
            <Button type="button" variant="ghost" onClick={fitView}>
              Fit
            </Button>
          </div>
        </div>
      </div>

      <div className="flex flex-wrap items-start justify-between gap-3 rounded-xl border border-etos-border-soft bg-etos-panel-muted px-3 py-2.5">
        <div className="min-w-0 flex-1">
          <p className="text-[13px] font-extrabold text-etos-ink">
            {selected?.objectType ?? "Node"}{" "}
            <span className="font-mono text-[11px] font-normal text-etos-accent-cyan">
              {selected?.nodeId}
            </span>
          </p>
          <p className="mt-1 text-xs text-etos-ink-muted">
            Trust {selected?.trustState ?? "—"} · {selected?.safeSummary}
          </p>
        </div>
        {selected && selected.nodeId !== center.nodeId ? (
          <Button
            type="button"
            variant="ghost"
            onClick={() => router.push(`/graph/${selected.nodeId}`)}
          >
            Open node
          </Button>
        ) : null}
      </div>
    </div>
  );
}
