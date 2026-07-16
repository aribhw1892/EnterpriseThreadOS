"use server";

import {
  getGraphExplorerNode,
  getGraphExplorerRelationships,
  getGraphExplorerSubgraph,
  postGraphExplorerPatternQuery,
  searchGraphExplorerNodes,
  type GraphExplorerNodeList,
  type GraphExplorerNodeQuery,
  type GraphExplorerPatternQuery,
  type GraphExplorerPatternQueryRequest,
  type GraphExplorerRelationship,
  type GraphExplorerSubgraph,
} from "@/lib/etos-api";

export async function loadGraphNeighborhood(
  nodeId: string,
  direction: "in" | "out" | "both" = "both",
): Promise<{ data: GraphExplorerRelationship[] | null; error: string | null }> {
  return getGraphExplorerRelationships(nodeId, direction);
}

export async function loadGraphSearch(
  query: GraphExplorerNodeQuery,
): Promise<{ data: GraphExplorerNodeList | null; error: string | null }> {
  return searchGraphExplorerNodes(query);
}

export async function loadGraphSubgraph(
  nodeId: string,
  options: {
    depth?: number;
    relationshipTypes?: string;
    direction?: string;
    graphSpace?: string;
    trustState?: string;
    limit?: number;
  } = {},
): Promise<{ data: GraphExplorerSubgraph | null; error: string | null }> {
  return getGraphExplorerSubgraph(nodeId, options);
}

export async function runGraphPatternQuery(
  body: GraphExplorerPatternQueryRequest,
): Promise<{ data: GraphExplorerPatternQuery | null; error: string | null }> {
  return postGraphExplorerPatternQuery(body);
}

export async function loadGraphNodeDetail(nodeId: string) {
  return getGraphExplorerNode(nodeId);
}
