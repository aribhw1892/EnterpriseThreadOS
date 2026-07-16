"use client";

import { useEffect, useRef, useState } from "react";
import Graph from "graphology";
import forceAtlas2 from "graphology-layout-forceatlas2";
import Sigma from "sigma";
import { EdgeArrowProgram } from "sigma/rendering";

export type BloomGraphNode = {
  nodeId: string;
  objectType: string;
  trustState: string;
  graphSpace?: string;
  safeSummary: string;
  allowedAttributes?: Record<string, string>;
  isCenter?: boolean;
};

export type BloomGraphEdge = {
  relationshipId: string;
  relationshipType: string;
  fromNodeId: string;
  toNodeId: string;
  trustState?: string;
  safeSummary?: string;
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

function buildSigmaGraph(
  nodes: BloomGraphNode[],
  edges: BloomGraphEdge[],
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

export type GraphBloomCanvasProps = {
  nodes: BloomGraphNode[];
  edges: BloomGraphEdge[];
  selectedNodeId?: string | null;
  onSelectNode?: (nodeId: string) => void;
  onExpandNode?: (nodeId: string) => void;
  className?: string;
};

export function GraphBloomCanvas({
  nodes,
  edges,
  selectedNodeId,
  onSelectNode,
  onExpandNode,
  className = "",
}: GraphBloomCanvasProps) {
  const containerRef = useRef<HTMLDivElement>(null);
  const sigmaRef = useRef<Sigma | null>(null);
  const onSelectRef = useRef(onSelectNode);
  const onExpandRef = useRef(onExpandNode);
  const [hoverId, setHoverId] = useState<string | null>(null);

  useEffect(() => {
    onSelectRef.current = onSelectNode;
  });

  useEffect(() => {
    onExpandRef.current = onExpandNode;
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
      nodeReducer: (node, data) => {
        if (selectedNodeId && node === selectedNodeId) {
          return { ...data, size: (data.size as number) + 4, highlighted: true };
        }
        return data;
      },
    });

    sigmaRef.current = renderer;

    renderer.on("clickNode", ({ node }) => {
      onSelectRef.current?.(node);
    });
    renderer.on("enterNode", ({ node }) => {
      setHoverId(node);
      container.style.cursor = "pointer";
    });
    renderer.on("leaveNode", () => {
      setHoverId(null);
      container.style.cursor = "grab";
    });
    renderer.on("doubleClickNode", ({ node, event }) => {
      event.preventSigmaDefault();
      onExpandRef.current?.(node);
    });

    return () => {
      renderer.kill();
      sigmaRef.current = null;
    };
  }, [nodes, edges, selectedNodeId]);

  function fitView() {
    sigmaRef.current?.getCamera().animatedReset({ duration: 280 });
  }

  return (
    <div className={`relative min-h-[480px] overflow-hidden rounded-etos-card border border-etos-border bg-etos-panel-muted ${className}`}>
      <div
        ref={containerRef}
        className="absolute inset-0 cursor-grab active:cursor-grabbing"
        aria-label="Bloom graph canvas"
      />
      <div className="pointer-events-none absolute bottom-3 left-3 rounded-xl border border-etos-border bg-etos-panel/95 px-3 py-2 text-[11px] text-etos-ink-muted">
        {nodes.length} nodes · {edges.length} edges
        {hoverId ? (
          <span className="ml-2 font-mono text-[10px] text-etos-accent-cyan">
            {hoverId}
          </span>
        ) : null}
      </div>
      <button
        type="button"
        className="absolute bottom-3 right-3 z-10 rounded-etos-button border border-etos-border bg-etos-panel px-3 py-2 text-[12px] font-extrabold text-etos-ink"
        onClick={fitView}
      >
        Fit
      </button>
    </div>
  );
}
