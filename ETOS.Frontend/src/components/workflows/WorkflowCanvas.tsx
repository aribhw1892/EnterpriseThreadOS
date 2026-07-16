"use client";

import { useCallback, useMemo, useState } from "react";
import {
  Background,
  Controls,
  Handle,
  MarkerType,
  MiniMap,
  Position,
  ReactFlow,
  type Edge,
  type Node,
  type NodeProps,
} from "@xyflow/react";
import "@xyflow/react/dist/style.css";
import type { WorkflowStepDefinition } from "@/lib/etos-api";
import { Button } from "@/components/ui/Button";
import { Notice } from "@/components/ui/Notice";
import {
  saveWorkflowDraftAction,
  validateWorkflowPreviewAction,
} from "@/app/(shell)/workflows/actions";

type WorkflowCanvasProps = {
  artifactId: string;
  versionId: string;
  workflowKey: string;
  initialSteps: WorkflowStepDefinition[];
  versionLabel: string;
};

type StepNodeData = {
  label: string;
  stepType: string;
  safeModeOnBlock: string;
  step: WorkflowStepDefinition;
};

function StepNode({ data }: NodeProps) {
  const nodeData = data as StepNodeData;
  return (
    <div className="min-w-[180px] rounded-etos-card border border-etos-border bg-etos-panel px-3 py-2 shadow-etos">
      <Handle type="target" position={Position.Left} className="!bg-etos-accent" />
      <p className="text-xs font-extrabold text-etos-ink">{nodeData.label}</p>
      <p className="mt-1 text-[11px] text-etos-ink-muted">{nodeData.stepType}</p>
      <p className="mt-1 text-[10px] text-etos-ink-subtle">safe: {nodeData.safeModeOnBlock}</p>
      <Handle type="source" position={Position.Right} className="!bg-etos-accent" />
    </div>
  );
}

const nodeTypes = { step: StepNode };

function stepsToGraph(steps: WorkflowStepDefinition[]): { nodes: Node[]; edges: Edge[] } {
  const nodes: Node[] = steps.map((step, index) => ({
    id: step.stepKey,
    type: "step",
    position: { x: 40 + index * 240, y: 80 + (index % 2) * 40 },
    data: {
      label: step.stepKey,
      stepType: step.stepType,
      safeModeOnBlock: step.safeModeOnBlock,
      step,
    } satisfies StepNodeData,
  }));

  const edges: Edge[] = [];
  for (const step of steps) {
    if (step.dependsOnStepKeys.length > 0) {
      for (const dep of step.dependsOnStepKeys) {
        edges.push({
          id: `${dep}->${step.stepKey}`,
          source: dep,
          target: step.stepKey,
          markerEnd: { type: MarkerType.ArrowClosed },
          style: { stroke: "var(--etos-border-strong, #94a3b8)" },
        });
      }
    }
  }

  if (edges.length === 0 && steps.length > 1) {
    for (let i = 0; i < steps.length - 1; i += 1) {
      edges.push({
        id: `${steps[i].stepKey}->${steps[i + 1].stepKey}`,
        source: steps[i].stepKey,
        target: steps[i + 1].stepKey,
        markerEnd: { type: MarkerType.ArrowClosed },
        style: { stroke: "var(--etos-border-strong, #94a3b8)" },
      });
    }
  }

  return { nodes, edges };
}

export function WorkflowCanvas({
  artifactId,
  versionId,
  workflowKey,
  initialSteps,
  versionLabel,
}: WorkflowCanvasProps) {
  const [steps, setSteps] = useState<WorkflowStepDefinition[]>(initialSteps);
  const { nodes, edges } = useMemo(() => stepsToGraph(steps), [steps]);

  const onNodesDelete = useCallback((deleted: Node[]) => {
    const deletedIds = new Set(deleted.map((n) => n.id));
    setSteps((current) =>
      current
        .filter((step) => !deletedIds.has(step.stepKey))
        .map((step) => ({
          ...step,
          dependsOnStepKeys: step.dependsOnStepKeys.filter((dep) => !deletedIds.has(dep)),
        })),
    );
  }, []);

  const stepsJson = useMemo(() => JSON.stringify(steps), [steps]);

  return (
    <div className="space-y-4">
      {steps.length === 0 ? (
        <Notice variant="info">
          Empty canvas — this version has no steps. Create with steps via package install or API, then rearrange /
          delete here. Add step is disabled until a typed step picker exists.
        </Notice>
      ) : null}

      <div className="h-[420px] overflow-hidden rounded-etos-card border border-etos-border bg-etos-panel-muted">
        <ReactFlow
          nodes={nodes}
          edges={edges}
          nodeTypes={nodeTypes}
          fitView
          nodesDraggable
          nodesConnectable={false}
          elementsSelectable
          onNodesDelete={onNodesDelete}
          proOptions={{ hideAttribution: true }}
        >
          <Background gap={16} size={1} />
          <Controls />
          <MiniMap pannable zoomable />
        </ReactFlow>
      </div>

      <div className="flex flex-wrap gap-3">
        <form action={validateWorkflowPreviewAction}>
          <input type="hidden" name="artifactId" value={artifactId} />
          <input type="hidden" name="versionId" value={versionId} />
          <input type="hidden" name="workflowKey" value={workflowKey} />
          <Button type="submit" variant="ghost">
            Validate (preview)
          </Button>
        </form>
        <form action={saveWorkflowDraftAction}>
          <input type="hidden" name="workflowKey" value={workflowKey} />
          <input type="hidden" name="versionId" value={versionId} />
          <input type="hidden" name="stepsJson" value={stepsJson} />
          <input type="hidden" name="versionLabel" value={`${versionLabel}-canvas`} />
          <Button type="submit">Save draft</Button>
        </form>
        <Button type="button" disabled title="Typed step picker not implemented — avoid inventing step payloads.">
          Add step
        </Button>
      </div>
    </div>
  );
}
