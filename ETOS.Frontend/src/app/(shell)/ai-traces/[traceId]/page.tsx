import {
  AiTraceDetail,
  exportAiTrace,
  getAiTraceDetail,
} from "@/lib/etos-api";
import { Button } from "@/components/ui/Button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/Card";
import { ErrorState } from "@/components/ui/ErrorState";
import { PageHeader } from "@/components/ui/PageHeader";
import { PillStack, SidePanel } from "@/components/ui/SidePanel";
import { TraceTimeline, type TraceTimelineStep } from "@/components/ui/TraceTimeline";
import Link from "next/link";
import { revalidatePath } from "next/cache";

export const dynamic = "force-dynamic";

async function exportTraceAction(formData: FormData) {
  "use server";

  const traceId = formData.get("traceId");
  if (typeof traceId !== "string" || traceId.length === 0) {
    return;
  }

  await exportAiTrace(traceId);
  revalidatePath(`/ai-traces/${traceId}`);
  revalidatePath("/ai-traces");
}

function buildTimeline(trace: AiTraceDetail): TraceTimelineStep[] {
  const steps: TraceTimelineStep[] = [
    {
      id: "query",
      title: "Query received",
      description: trace.queryText,
      status: trace.status,
      meta: `${trace.intentKey} · ${trace.strategyKey}`,
    },
    {
      id: "retrieval",
      title: "Retrieval",
      description: `Retrieved ${trace.confidenceImpact.retrievedCount}, filtered ${trace.confidenceImpact.filteredCount}`,
      meta: trace.strategyKey,
    },
    {
      id: "policy",
      title: "Policy filter",
      description: trace.confidenceImpact.policyKey
        ? `Policy ${trace.confidenceImpact.policyKey}`
        : "No policy key recorded",
      meta: `${trace.confidenceImpact.deniedCount} denied · ${trace.confidenceImpact.trustFilteredCount} trust-filtered`,
    },
  ];

  for (const source of trace.sourcesSummary) {
    steps.push({
      id: `source-${source.sourceKind}`,
      title: `Source · ${source.sourceKind}`,
      description: `${source.count} reference(s)`,
      meta: source.safeReferences.slice(0, 3).join(", ") || undefined,
    });
  }

  for (const item of trace.filteredSummaries.slice(0, 5)) {
    steps.push({
      id: `filtered-${item.contextId}`,
      title: "Filtered context",
      description: item.safeSummary,
      meta: item.contextType,
    });
  }

  for (const item of trace.deniedSafeSummaries.slice(0, 5)) {
    steps.push({
      id: `denied-${item.contextId}`,
      title: "Denied summary",
      description: item.safeSummary,
      status: "denied",
      meta: item.reason,
    });
  }

  for (const link of trace.artifactLinks) {
    steps.push({
      id: link.id,
      title: `Artifact link · ${link.linkKind}`,
      description: `${link.objectType} (${link.objectId})`,
    });
  }

  if (trace.contextPackageId) {
    steps.push({
      id: "context-package",
      title: "Context package",
      description: "Linked governed context package",
      href: `/context-packages/${trace.contextPackageId}`,
    });
  }

  return steps;
}

export default async function AiTraceDetailPage({
  params,
}: {
  params: Promise<{ traceId: string }>;
}) {
  const { traceId } = await params;
  const result = await getAiTraceDetail(traceId);

  if (result.error || !result.data) {
    return (
      <main className="px-6 py-8 lg:px-8">
        <PageHeader title="Trace detail" description="Governed retrieval audit record." />
        <ErrorState error={result.error ?? "Trace not found."} />
        <Link href="/ai-traces" className="mt-4 inline-block text-sm font-semibold text-etos-accent">
          Back to AI traces
        </Link>
      </main>
    );
  }

  const trace = result.data;
  const steps = buildTimeline(trace);
  const docSources =
    trace.sourcesSummary.find((s) =>
      s.sourceKind.toLowerCase().includes("doc"),
    )?.count ?? 0;

  return (
    <main className="px-6 py-8 lg:px-8">
      <PageHeader
        title="AI Trace detail"
        description="Permission-filtered evidence chain from query intent through retrieval, context package, generated output, and artifact links."
        actions={
          <>
            <form action={exportTraceAction}>
              <input type="hidden" name="traceId" value={trace.id} />
              <Button type="submit" variant="primary">
                Export trace package
              </Button>
            </form>
            {trace.contextPackageId ? (
              <Link href={`/context-packages/${trace.contextPackageId}`}>
                <Button variant="ghost">Open context package</Button>
              </Link>
            ) : null}
          </>
        }
      />

      <div className="grid gap-4 lg:grid-cols-[minmax(0,2fr)_minmax(280px,1fr)]">
        <Card>
          <CardHeader>
            <CardTitle>Trace timeline</CardTitle>
          </CardHeader>
          <CardContent>
            <TraceTimeline steps={steps} />
          </CardContent>
        </Card>

        <aside className="lg:sticky lg:top-6 lg:self-start">
          <SidePanel title="Context access decisions">
            <PillStack
              items={[
                {
                  label: "Graph nodes visible",
                  value: trace.confidenceImpact.retrievedCount.toString(),
                  variant: "success",
                },
                {
                  label: "Documents visible",
                  value: String(docSources),
                  variant: "success",
                },
                {
                  label: "Denied references",
                  value: trace.confidenceImpact.deniedCount.toString(),
                  variant: trace.confidenceImpact.deniedCount > 0 ? "danger" : "neutral",
                },
                { label: "Export permission", value: "Requires approval", variant: "warning" },
              ]}
            />
            <div className="mt-4 h-px bg-etos-border" />
            <div className="mt-4 rounded-etos-card bg-etos-ink p-3.5 font-mono text-xs leading-relaxed text-etos-purple-border">
              <div>TraceExportMode: on-demand</div>
              <div>RedactionPolicy: default-sensitive</div>
              <div>ExportHash: generated only on request</div>
              {trace.promptTemplateVersionLabel ? (
                <div>PromptTemplate: {trace.promptTemplateVersionLabel}</div>
              ) : null}
              {trace.outputSchemaVersionLabel ? (
                <div>OutputSchema: {trace.outputSchemaVersionLabel}</div>
              ) : null}
            </div>
          </SidePanel>
        </aside>
      </div>

      <details className="mt-6 rounded-etos-card border border-etos-border bg-etos-panel-muted p-4 text-sm text-etos-ink-muted">
        <summary className="cursor-pointer font-extrabold text-etos-ink">Advanced / Debug</summary>
        <div className="mt-4 space-y-3 text-xs">
          <p>
            <span className="font-semibold">Intent:</span> {trace.intentKey}
          </p>
          <p>
            <span className="font-semibold">Strategy:</span> {trace.strategyKey}
          </p>
          <p>
            <span className="font-semibold">Status:</span> {trace.status}
          </p>
          <p>
            <span className="font-semibold">Confidence notes:</span>{" "}
            {trace.confidenceImpact.notes || "—"}
          </p>
          <p>
            <span className="font-semibold">Safe summary:</span> {trace.safeSummary}
          </p>
        </div>
      </details>
    </main>
  );
}
