"use client";

import Link from "next/link";
import { useMemo, useState, useTransition } from "react";
import {
  loadGraphSearch,
  loadGraphSubgraph,
  runGraphPatternQuery,
} from "@/app/(shell)/explorers/actions";
import {
  GraphBloomCanvas,
  type BloomGraphEdge,
  type BloomGraphNode,
} from "@/components/explorers/GraphBloomCanvas";
import { Button } from "@/components/ui/Button";
import { SidePanel } from "@/components/ui/SidePanel";
import type { GraphExplorerNodeSummary } from "@/lib/etos-api";

type GraphBloomExplorerProps = {
  initialNodes: GraphExplorerNodeSummary[];
  initialTruncated: boolean;
  initialLimit: number;
};

function toBloomNodes(
  nodes: GraphExplorerNodeSummary[],
  centerId?: string | null,
): BloomGraphNode[] {
  return nodes.map((node) => ({
    nodeId: node.nodeId,
    objectType: node.objectType,
    trustState: node.trustState,
    graphSpace: node.graphSpace,
    safeSummary: node.safeSummary,
    allowedAttributes: node.allowedAttributes,
    isCenter: centerId ? node.nodeId === centerId : false,
  }));
}

export function GraphBloomExplorer({
  initialNodes,
  initialTruncated,
  initialLimit,
}: GraphBloomExplorerProps) {
  const [nodes, setNodes] = useState<BloomGraphNode[]>(() => toBloomNodes(initialNodes));
  const [edges, setEdges] = useState<BloomGraphEdge[]>([]);
  const [selectedId, setSelectedId] = useState<string | null>(initialNodes[0]?.nodeId ?? null);
  const [truncated, setTruncated] = useState(initialTruncated);
  const [status, setStatus] = useState<string | null>(null);
  const [pending, startTransition] = useTransition();

  const [search, setSearch] = useState("");
  const [graphSpace, setGraphSpace] = useState("Trusted");
  const [trustState, setTrustState] = useState("Trusted");
  const [objectType, setObjectType] = useState("");
  const [limit, setLimit] = useState(Math.min(initialLimit || 50, 100));

  const [startObjectType, setStartObjectType] = useState("");
  const [endObjectType, setEndObjectType] = useState("");
  const [relationshipTypes, setRelationshipTypes] = useState("");
  const [maxDepth, setMaxDepth] = useState(2);
  const [patternLimit, setPatternLimit] = useState(50);

  const selected = useMemo(
    () => nodes.find((node) => node.nodeId === selectedId) ?? null,
    [nodes, selectedId],
  );

  function mergeGraph(
    nextNodes: BloomGraphNode[],
    nextEdges: BloomGraphEdge[],
    replace: boolean,
  ) {
    if (replace) {
      setNodes(nextNodes);
      setEdges(nextEdges);
      return;
    }

    setNodes((prev) => {
      const map = new Map(prev.map((node) => [node.nodeId, node]));
      for (const node of nextNodes) {
        const existing = map.get(node.nodeId);
        map.set(node.nodeId, existing ? { ...existing, ...node } : node);
      }
      return Array.from(map.values());
    });
    setEdges((prev) => {
      const map = new Map(prev.map((edge) => [edge.relationshipId, edge]));
      for (const edge of nextEdges) {
        map.set(edge.relationshipId, edge);
      }
      return Array.from(map.values());
    });
  }

  function runSearch() {
    setStatus(null);
    startTransition(async () => {
      const result = await loadGraphSearch({
        search: search.trim() || undefined,
        graphSpace,
        trustState,
        objectType: objectType.trim() || undefined,
        limit: Math.min(Math.max(limit, 1), 100),
      });
      if (result.error || !result.data) {
        setStatus(result.error ?? "Search failed.");
        return;
      }

      const bloomNodes = toBloomNodes(result.data.nodes);
      mergeGraph(bloomNodes, [], true);
      setTruncated(result.data.truncated);
      setSelectedId(bloomNodes[0]?.nodeId ?? null);
      setStatus(
        result.data.truncated
          ? `Showing ${result.data.nodes.length} of ${result.data.matchCount} matches (limit ${result.data.limit}).`
          : `Loaded ${result.data.nodes.length} node(s). Expand a node to load edges.`,
      );
    });
  }

  function runPattern() {
    setStatus(null);
    if (!startObjectType.trim() && !search.trim() && !selectedId) {
      setStatus("Pattern needs start type, search text, or a selected seed node.");
      return;
    }

    startTransition(async () => {
      const types = relationshipTypes
        .split(",")
        .map((value) => value.trim())
        .filter(Boolean);

      const result = await runGraphPatternQuery({
        startNodeId: selectedId,
        startObjectType: startObjectType.trim() || null,
        endObjectType: endObjectType.trim() || null,
        relationshipTypes: types.length > 0 ? types : null,
        maxDepth: Math.min(Math.max(maxDepth, 1), 5),
        graphSpace,
        trustState,
        search: search.trim() || null,
        limit: Math.min(Math.max(patternLimit, 1), 250),
      });

      if (result.error || !result.data) {
        setStatus(result.error ?? "Pattern query failed.");
        return;
      }

      const bloomNodes = toBloomNodes(result.data.nodes, selectedId);
      const bloomEdges: BloomGraphEdge[] = result.data.relationships.map((edge) => ({
        relationshipId: edge.relationshipId,
        relationshipType: edge.relationshipType,
        fromNodeId: edge.fromNodeId,
        toNodeId: edge.toNodeId,
        trustState: edge.trustState,
        safeSummary: edge.safeSummary,
      }));
      mergeGraph(bloomNodes, bloomEdges, true);
      setTruncated(result.data.truncated);
      setStatus(
        result.data.truncated
          ? `Pattern truncated at ${result.data.limit} nodes (seeds ${result.data.seedCount}).`
          : `Pattern returned ${result.data.nodes.length} nodes · ${result.data.relationships.length} edges.`,
      );
    });
  }

  function expandNode(nodeId: string) {
    setStatus(null);
    setSelectedId(nodeId);
    startTransition(async () => {
      const result = await loadGraphSubgraph(nodeId, {
        depth: Math.min(Math.max(maxDepth, 1), 5),
        relationshipTypes: relationshipTypes.trim() || undefined,
        direction: "out",
        graphSpace,
        trustState,
        limit: Math.min(Math.max(patternLimit, 1), 250),
      });
      if (result.error || !result.data) {
        setStatus(result.error ?? "Subgraph expand failed.");
        return;
      }

      const bloomNodes = toBloomNodes(result.data.nodes, nodeId);
      const bloomEdges: BloomGraphEdge[] = result.data.relationships.map((edge) => ({
        relationshipId: edge.relationshipId,
        relationshipType: edge.relationshipType,
        fromNodeId: edge.fromNodeId,
        toNodeId: edge.toNodeId,
        trustState: edge.trustState,
        safeSummary: edge.safeSummary,
      }));
      mergeGraph(bloomNodes, bloomEdges, false);
      setTruncated(result.data.truncated || truncated);
      setStatus(
        result.data.truncated
          ? `Expand truncated at ${result.data.limit} nodes.`
          : `Expanded ${result.data.nodes.length} nodes · ${result.data.relationships.length} edges.`,
      );
    });
  }

  function clearCanvas() {
    setNodes([]);
    setEdges([]);
    setSelectedId(null);
    setTruncated(false);
    setStatus("Canvas cleared.");
  }

  return (
    <div className="space-y-4">
      <div className="rounded-etos-card border border-etos-border bg-etos-panel p-4">
        <div className="flex flex-wrap items-end gap-3">
          <label className="min-w-[220px] flex-1 text-xs font-extrabold text-etos-ink">
            Search
            <input
              value={search}
              onChange={(event) => setSearch(event.target.value)}
              placeholder="Type, id, summary, attribute…"
              className="mt-1 w-full rounded-xl border border-etos-border bg-etos-panel-muted px-3 py-2 text-sm font-normal text-etos-ink"
            />
          </label>
          <Button type="button" disabled={pending} onClick={runSearch}>
            {pending ? "Running…" : "Search"}
          </Button>
          <Button type="button" variant="ghost" disabled={pending} onClick={runPattern}>
            Run pattern
          </Button>
          <Button type="button" variant="ghost" onClick={clearCanvas}>
            Clear
          </Button>
        </div>

        <div className="mt-4 grid gap-3 md:grid-cols-2 xl:grid-cols-4">
          <label className="text-xs font-extrabold text-etos-ink">
            Graph space
            <select
              value={graphSpace}
              onChange={(event) => setGraphSpace(event.target.value)}
              className="mt-1 w-full rounded-xl border border-etos-border bg-etos-panel-muted px-3 py-2 text-sm font-normal"
            >
              <option value="Trusted">Trusted</option>
              <option value="Staging">Staging</option>
            </select>
          </label>
          <label className="text-xs font-extrabold text-etos-ink">
            Trust state
            <select
              value={trustState}
              onChange={(event) => setTrustState(event.target.value)}
              className="mt-1 w-full rounded-xl border border-etos-border bg-etos-panel-muted px-3 py-2 text-sm font-normal"
            >
              <option value="Trusted">Trusted</option>
              <option value="Provisional">Provisional</option>
              <option value="Unverified">Unverified</option>
            </select>
          </label>
          <label className="text-xs font-extrabold text-etos-ink">
            Object type filter
            <input
              value={objectType}
              onChange={(event) => setObjectType(event.target.value)}
              placeholder="partVersion"
              className="mt-1 w-full rounded-xl border border-etos-border bg-etos-panel-muted px-3 py-2 text-sm font-normal"
            />
          </label>
          <label className="text-xs font-extrabold text-etos-ink">
            Search limit (max 100)
            <input
              type="number"
              min={1}
              max={100}
              value={limit}
              onChange={(event) => setLimit(Number(event.target.value) || 50)}
              className="mt-1 w-full rounded-xl border border-etos-border bg-etos-panel-muted px-3 py-2 text-sm font-normal"
            />
          </label>
        </div>

        <details className="mt-4 rounded-xl border border-etos-border-soft bg-etos-panel-muted p-3">
          <summary className="cursor-pointer text-sm font-extrabold text-etos-ink">
            Pattern builder (Bloom-like)
          </summary>
          <div className="mt-3 grid gap-3 md:grid-cols-2 xl:grid-cols-3">
            <label className="text-xs font-extrabold text-etos-ink">
              Start type
              <input
                value={startObjectType}
                onChange={(event) => setStartObjectType(event.target.value)}
                placeholder="partVersion"
                className="mt-1 w-full rounded-xl border border-etos-border bg-etos-panel px-3 py-2 text-sm font-normal"
              />
            </label>
            <label className="text-xs font-extrabold text-etos-ink">
              End type
              <input
                value={endObjectType}
                onChange={(event) => setEndObjectType(event.target.value)}
                placeholder="part"
                className="mt-1 w-full rounded-xl border border-etos-border bg-etos-panel px-3 py-2 text-sm font-normal"
              />
            </label>
            <label className="text-xs font-extrabold text-etos-ink">
              Relationship types (csv)
              <input
                value={relationshipTypes}
                onChange={(event) => setRelationshipTypes(event.target.value)}
                placeholder="HAS_BOM,REFERENCES"
                className="mt-1 w-full rounded-xl border border-etos-border bg-etos-panel px-3 py-2 text-sm font-normal"
              />
            </label>
            <label className="text-xs font-extrabold text-etos-ink">
              Depth (1–5)
              <input
                type="number"
                min={1}
                max={5}
                value={maxDepth}
                onChange={(event) => setMaxDepth(Number(event.target.value) || 2)}
                className="mt-1 w-full rounded-xl border border-etos-border bg-etos-panel px-3 py-2 text-sm font-normal"
              />
            </label>
            <label className="text-xs font-extrabold text-etos-ink">
              Result limit (max 250)
              <input
                type="number"
                min={1}
                max={250}
                value={patternLimit}
                onChange={(event) => setPatternLimit(Number(event.target.value) || 50)}
                className="mt-1 w-full rounded-xl border border-etos-border bg-etos-panel px-3 py-2 text-sm font-normal"
              />
            </label>
          </div>
          <p className="mt-2 text-[11px] text-etos-ink-muted">
            Uses selected node as seed when present. Governed typed query only — no Cypher.
          </p>
        </details>
      </div>

      {truncated ? (
        <div className="rounded-xl border border-etos-warning-border bg-etos-warning-bg px-3 py-2 text-xs font-extrabold text-etos-warning-fg">
          Result truncated by limit. Raise limit or narrow filters.
        </div>
      ) : null}
      {status ? (
        <p className="text-xs text-etos-ink-muted">{status}</p>
      ) : null}

      <div className="grid gap-4 lg:grid-cols-[1.4fr_0.6fr]">
        {nodes.length === 0 ? (
          <div className="flex min-h-[480px] flex-col items-center justify-center gap-3 rounded-etos-card border border-etos-border bg-etos-panel-muted p-8 text-center">
            <p className="text-sm font-extrabold text-etos-ink">No graph loaded</p>
            <p className="max-w-md text-xs text-etos-ink-muted">
              Run search or a pattern query. Promote staged imports if the Trusted space is empty.
            </p>
            <Link href="/graph/promote">
              <Button type="button">Open promotion</Button>
            </Link>
          </div>
        ) : (
          <GraphBloomCanvas
            nodes={nodes}
            edges={edges}
            selectedNodeId={selectedId}
            onSelectNode={setSelectedId}
            onExpandNode={expandNode}
          />
        )}

        <SidePanel title="Metadata inspector">
          {selected ? (
            <div className="space-y-3 text-sm">
              <div>
                <p className="text-[13px] font-extrabold text-etos-ink">{selected.objectType}</p>
                <p className="mt-1 font-mono text-[11px] text-etos-accent-cyan">{selected.nodeId}</p>
              </div>
              <p className="text-xs text-etos-ink-muted">{selected.safeSummary}</p>
              <p className="text-xs text-etos-ink-muted">
                Trust {selected.trustState}
                {selected.graphSpace ? ` · ${selected.graphSpace}` : ""}
              </p>
              {selected.allowedAttributes && Object.keys(selected.allowedAttributes).length > 0 ? (
                <dl className="space-y-1 rounded-xl border border-etos-border-soft bg-etos-panel-muted p-3">
                  {Object.entries(selected.allowedAttributes).map(([key, value]) => (
                    <div key={key} className="grid grid-cols-[0.4fr_0.6fr] gap-2 text-[11px]">
                      <dt className="font-extrabold text-etos-ink-muted">{key}</dt>
                      <dd className="break-all text-etos-ink">{value}</dd>
                    </div>
                  ))}
                </dl>
              ) : (
                <p className="text-xs text-etos-ink-subtle">No allowed attributes on this node.</p>
              )}
              <div className="flex flex-wrap gap-2">
                <Button type="button" variant="ghost" disabled={pending} onClick={() => expandNode(selected.nodeId)}>
                  Expand
                </Button>
                <Link href={`/graph/${selected.nodeId}`}>
                  <Button type="button" variant="ghost">
                    Open 360°
                  </Button>
                </Link>
                <Link href={`/chat?startGraphNodeId=${selected.nodeId}`}>
                  <Button type="button" variant="ghost">
                    Open chat
                  </Button>
                </Link>
              </div>
            </div>
          ) : (
            <p className="text-xs text-etos-ink-muted">Select a node on the canvas.</p>
          )}
        </SidePanel>
      </div>
    </div>
  );
}
