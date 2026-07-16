import Link from "next/link";
import { executeToolAction } from "@/app/(shell)/tools/actions";
import { Button } from "@/components/ui/Button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/Card";
import { ErrorState } from "@/components/ui/ErrorState";
import { KpiCard } from "@/components/ui/KpiCard";
import { Notice } from "@/components/ui/Notice";
import { PageHeader } from "@/components/ui/PageHeader";
import { PillStack, SidePanel } from "@/components/ui/SidePanel";
import { TraceTimeline, type TraceTimelineStep } from "@/components/ui/TraceTimeline";
import {
  getArtifactVersions,
  getConnectorDefinitionDetail,
  getToolDefinitionArtifacts,
  getToolDefinitionDetail,
  getToolRunDetail,
  type ConnectorDefinitionDetail,
  type ToolDefinitionDetail,
  type ToolRunDetail,
} from "@/lib/etos-api";

export const dynamic = "force-dynamic";

type PageProps = {
  params: Promise<{ runId: string }>;
  searchParams: Promise<{ error?: string; notice?: string }>;
};

type LinkedTool = {
  artifactId: string;
  versionId: string;
  detail: ToolDefinitionDetail;
  connector?: ConnectorDefinitionDetail | null;
};

async function resolveLinkedTool(toolDefinitionVersionId: string): Promise<LinkedTool | null> {
  const tools = await getToolDefinitionArtifacts();
  if (!tools.data) {
    return null;
  }

  for (const tool of tools.data) {
    const versions = await getArtifactVersions(tool.id);
    if (!versions.data) {
      continue;
    }

    const match = versions.data.find((version) => version.id === toolDefinitionVersionId);
    if (!match) {
      continue;
    }

    const detail = await getToolDefinitionDetail(tool.id, match.id);
    if (!detail.data) {
      return null;
    }

    let connector: ConnectorDefinitionDetail | null = null;
    const ref = detail.data.referencedConnector;
    if (ref) {
      const connectorDetail = await getConnectorDefinitionDetail(
        ref.connectorArtifactId,
        ref.connectorDefinitionVersionId,
      );
      connector = connectorDetail.data;
    }

    return {
      artifactId: tool.id,
      versionId: match.id,
      detail: detail.data,
      connector,
    };
  }

  return null;
}

function parseNotes(json?: string | null): string[] {
  if (!json) {
    return [];
  }

  try {
    const parsed = JSON.parse(json) as unknown;
    if (Array.isArray(parsed)) {
      return parsed.map((item) => String(item));
    }
    if (parsed && typeof parsed === "object") {
      return Object.entries(parsed as Record<string, unknown>).map(
        ([key, value]) => `${key}: ${String(value)}`,
      );
    }
    return [String(parsed)];
  } catch {
    return [json];
  }
}

function buildTimeline(detail: ToolRunDetail): TraceTimelineStep[] {
  const validation = parseNotes(detail.validationResultJson);
  const compatibility = parseNotes(detail.compatibilityNotesJson);
  const statusLower = detail.status.toLowerCase();
  const failed = statusLower.includes("fail") || statusLower.includes("error");

  return [
    {
      id: "registry",
      title: "Tool registry lookup",
      description: `Version ${detail.toolDefinitionVersionId}`,
      status: "Pass",
    },
    {
      id: "validation",
      title: "Schema / validation check",
      description:
        validation.length > 0
          ? validation.slice(0, 3).join("; ")
          : detail.errorSafeSummary ?? "No validation notes recorded",
      status: failed && validation.length === 0 ? "Fail" : validation.length > 0 ? "Reviewed" : "Pass",
    },
    {
      id: "compatibility",
      title: "Compatibility / policy notes",
      description:
        compatibility.length > 0
          ? compatibility.slice(0, 3).join("; ")
          : "No compatibility notes recorded",
      status: compatibility.some((n) => /block|fail|incompat/i.test(n))
        ? "Filtered"
        : "Pass",
    },
    {
      id: "status",
      title: detail.isDryRun ? "Dry-run completion" : "Execution completion",
      description: detail.errorSafeSummary ?? `Status: ${detail.status}`,
      status: detail.status,
      meta: detail.completedAt
        ? `Completed ${new Date(detail.completedAt).toLocaleString()}`
        : `Created ${new Date(detail.createdAt).toLocaleString()}`,
      href: detail.aiTraceRecordId ? `/ai-traces/${detail.aiTraceRecordId}` : null,
    },
  ];
}

function summarizeJson(json?: string | null, fallback = "No summary.") {
  if (!json) {
    return fallback;
  }

  try {
    const parsed = JSON.parse(json) as unknown;
    if (typeof parsed === "string") {
      return parsed;
    }
    if (parsed && typeof parsed === "object") {
      const record = parsed as Record<string, unknown>;
      const preferred =
        record.summary ?? record.message ?? record.status ?? record.result;
      if (preferred != null) {
        return String(preferred);
      }
      return JSON.stringify(parsed, null, 2).slice(0, 600);
    }
    return String(parsed);
  } catch {
    return json.slice(0, 600);
  }
}

function executeGate(linked: LinkedTool | null): { enabled: boolean; reason: string } {
  if (!linked) {
    return { enabled: false, reason: "Linked tool definition not found for this run." };
  }

  if (linked.detail.capabilityFlags.writesExternalSystem) {
    return { enabled: false, reason: "Tool declares external writes — blocked in MVP." };
  }

  if (linked.connector) {
    if (!linked.connector.executionEnabled) {
      return {
        enabled: false,
        reason: linked.connector.disabledReason ?? "Linked connector execution is disabled.",
      };
    }
    if (linked.connector.writesExternalSystem) {
      return {
        enabled: false,
        reason: "Linked connector is write-capable and stays disabled.",
      };
    }
  }

  return { enabled: true, reason: "Execute allowed for this governed tool." };
}

export default async function ToolRunDetailPage({ params, searchParams }: PageProps) {
  const { runId } = await params;
  const { error, notice } = await searchParams;
  const run = await getToolRunDetail(runId);

  if (!run.data) {
    return (
      <main className="px-6 py-8 lg:px-8">
        <ErrorState error={run.error ?? "Tool run was not found."} />
        <p className="mt-4 text-sm">
          <Link href="/tool-runs" className="text-etos-accent hover:underline">
            Back to tool runs
          </Link>
        </p>
      </main>
    );
  }

  const detail = run.data;
  const linked = await resolveLinkedTool(detail.toolDefinitionVersionId);
  const gate = executeGate(linked);
  const timeline = buildTimeline(detail);

  return (
    <main className="px-6 py-8 lg:px-8">
      <PageHeader
        title="Tool run & dry-run trace"
        description="Runtime detail for dry-run and execution mode, schema validation, policy checks, output summary, and audit links."
        actions={
          <>
            <Link href="/tool-runs">
              <Button type="button" variant="ghost">
                All runs
              </Button>
            </Link>
            <Link href="/tools">
              <Button type="button" variant="ghost">
                Registry
              </Button>
            </Link>
          </>
        }
      />

      {error ? (
        <div className="mb-4">
          <Notice variant="danger">{error}</Notice>
        </div>
      ) : null}
      {notice ? (
        <div className="mb-4">
          <Notice variant="info">{notice}</Notice>
        </div>
      ) : null}

      <div className="grid gap-4 md:grid-cols-4">
        <KpiCard
          label="Mode"
          value={detail.isDryRun ? "Dry run" : "Execute"}
          hint="Governance validation only"
        />
        <KpiCard
          label="Input schema"
          value={linked?.detail.versionLabel ?? "—"}
          hint={detail.validationResultJson ? "Validated" : "No validation payload"}
        />
        <KpiCard
          label="Output schema"
          value={
            linked?.detail.referencedOutputSchema?.versionLabel ??
            (linked ? "Inline" : "—")
          }
          hint="Expected"
        />
        <KpiCard
          label="Risk"
          value={linked?.detail.riskLevel ?? "—"}
          hint="Context exposure controlled"
        />
      </div>

      <div className="mt-4 grid gap-4 lg:grid-cols-3">
        <Card className="lg:col-span-2">
          <CardHeader>
            <CardTitle>{detail.isDryRun ? "Dry-run preview" : "Execution trace"}</CardTitle>
          </CardHeader>
          <CardContent>
            <TraceTimeline steps={timeline} />
            <div className="mt-6 grid gap-4 md:grid-cols-2">
              <div className="rounded-etos-card border border-etos-border-soft bg-etos-panel-muted p-4">
                <p className="text-xs font-extrabold uppercase tracking-wide text-etos-ink-muted">
                  Expected / input
                </p>
                <pre className="mt-2 max-h-48 overflow-auto whitespace-pre-wrap font-mono text-xs text-etos-ink">
                  {summarizeJson(detail.inputSafeSummaryJson)}
                </pre>
              </div>
              <div className="rounded-etos-card border border-etos-border-soft bg-etos-panel-muted p-4">
                <p className="text-xs font-extrabold uppercase tracking-wide text-etos-ink-muted">
                  Actual / output
                </p>
                <pre className="mt-2 max-h-48 overflow-auto whitespace-pre-wrap font-mono text-xs text-etos-ink">
                  {summarizeJson(detail.outputSafeSummaryJson, "No output summary.")}
                </pre>
              </div>
            </div>
          </CardContent>
        </Card>

        <SidePanel title="Execution preview">
          <PillStack
            items={[
              {
                label: "Status",
                value: detail.status,
                variant: "info",
              },
              {
                label: "Mode",
                value: detail.isDryRun ? "Dry-run" : "Execute",
                variant: "purple",
              },
              {
                label: "Tool",
                value: linked?.detail.toolKey ?? detail.toolDefinitionVersionId.slice(0, 8),
                variant: "teal",
              },
              {
                label: "Downstream",
                value: gate.enabled ? "Allowed" : "Blocked",
                variant: gate.enabled ? "success" : "danger",
              },
            ]}
          />
          <div className="my-3 h-px bg-etos-border" />
          {linked && gate.enabled ? (
            <form action={executeToolAction}>
              <input type="hidden" name="artifactId" value={linked.artifactId} />
              <input type="hidden" name="versionId" value={linked.versionId} />
              <input type="hidden" name="returnTo" value={`/tool-runs/${runId}`} />
              <Button type="submit" variant="primary" className="w-full justify-center">
                Execute tool
              </Button>
            </form>
          ) : (
            <Button type="button" disabled className="w-full justify-center" title={gate.reason}>
              Execute tool
            </Button>
          )}
          <p className="mt-2 text-xs text-etos-ink-subtle">{gate.reason}</p>
          {detail.aiTraceRecordId ? (
            <p className="mt-3 text-sm">
              <Link
                href={`/ai-traces/${detail.aiTraceRecordId}`}
                className="text-etos-accent hover:underline"
              >
                Open AI Trace
              </Link>
            </p>
          ) : null}
          {linked ? (
            <p className="mt-2 text-sm">
              <Link
                href={`/tools/${linked.artifactId}/edit`}
                className="text-etos-accent hover:underline"
              >
                Open tool editor
              </Link>
            </p>
          ) : null}
        </SidePanel>
      </div>

      <details className="mt-6 rounded-etos-card border border-etos-border bg-etos-panel-muted p-4 text-sm text-etos-ink-muted">
        <summary className="cursor-pointer font-extrabold text-etos-ink">
          Advanced / Debug
        </summary>
        <div className="mt-4 space-y-3">
          <ul className="space-y-1">
            <li>Run id: {detail.id}</li>
            <li>Tool definition version: {detail.toolDefinitionVersionId}</li>
            {detail.connectorDefinitionVersionId ? (
              <li>Connector definition version: {detail.connectorDefinitionVersionId}</li>
            ) : null}
            {detail.auditRecordId ? <li>Audit record: {detail.auditRecordId}</li> : null}
            {detail.retrievalRunId ? <li>Retrieval run: {detail.retrievalRunId}</li> : null}
          </ul>
          {detail.connectorCredentialSafeSummaryJson ? (
            <div>
              <p className="font-semibold text-etos-ink">Scoped credential summary</p>
              <pre className="mt-2 overflow-x-auto rounded-xl border border-etos-border bg-etos-panel p-3 text-xs">
                {detail.connectorCredentialSafeSummaryJson}
              </pre>
            </div>
          ) : null}
          <pre className="overflow-x-auto rounded-xl border border-etos-border bg-etos-panel p-3 text-xs">
            {JSON.stringify(detail, null, 2)}
          </pre>
        </div>
      </details>
    </main>
  );
}
