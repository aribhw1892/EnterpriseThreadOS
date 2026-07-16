import Link from "next/link";
import {
  executeWorkflowAction,
  markWorkflowReadyAction,
  publishWorkflowAction,
  workflowTestRunAction,
} from "@/app/(shell)/workflows/actions";
import { Button } from "@/components/ui/Button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/Card";
import { DataTable } from "@/components/ui/DataTable";
import { ErrorState } from "@/components/ui/ErrorState";
import { KpiCard } from "@/components/ui/KpiCard";
import { Notice } from "@/components/ui/Notice";
import { PageHeader } from "@/components/ui/PageHeader";
import { PillStack, SidePanel } from "@/components/ui/SidePanel";
import { getArtifactVersions, loadWorkflowVersionByKey } from "@/lib/etos-api";

export const dynamic = "force-dynamic";

type PageProps = {
  params: Promise<{ workflowKey: string }>;
  searchParams: Promise<{ versionId?: string; error?: string; notice?: string; success?: string }>;
};

type CheckRow = {
  id: string;
  check: string;
  result: string;
};

const fieldClass =
  "mt-2 w-full rounded-xl border border-etos-border bg-etos-panel px-3.5 py-2.5 text-sm text-etos-ink";

export default async function WorkflowPublishPage({ params, searchParams }: PageProps) {
  const { workflowKey } = await params;
  const { versionId, error, notice, success } = await searchParams;
  const decodedKey = decodeURIComponent(workflowKey);
  const loaded = await loadWorkflowVersionByKey(decodedKey, versionId);

  if (!loaded.data) {
    return (
      <main className="px-6 py-8 lg:px-8">
        <PageHeader title="Publish review" description="Workflow not found." />
        <ErrorState error={loaded.error ?? "Workflow was not found."} />
      </main>
    );
  }

  const { detail, readiness, artifactId, versionId: selectedVersionId } = loaded.data;
  const versions = await getArtifactVersions(artifactId);
  const derivedRisk = detail.derivedCapabilityRisk;
  const isPublished = detail.artifactReadinessState.toLowerCase().includes("publish");
  const infoNotice = notice ?? success;

  const checks: CheckRow[] = [
    {
      id: "blockers",
      check: "Readiness blockers",
      result:
        readiness.blockingReasons.length === 0
          ? "Pass"
          : `${readiness.blockingReasons.length} blocking`,
    },
    {
      id: "risk",
      check: "Derived capability risk",
      result: derivedRisk?.effectiveRiskLevel ?? "Pending mark-ready",
    },
    {
      id: "safe",
      check: "Safe mode",
      result: detail.safeModeEnabled ? "Enabled" : "Disabled",
    },
    {
      id: "steps",
      check: "Step count",
      result: String(detail.steps.length),
    },
    {
      id: "compat",
      check: "Compatibility notes",
      result:
        detail.compatibilityTestNotes.length > 0
          ? `${detail.compatibilityTestNotes.length} notes`
          : "None",
    },
  ];

  return (
    <main className="px-6 py-8 lg:px-8">
      <PageHeader
        title={`Publish · ${detail.displayName}`}
        description={`${detail.workflowKey} · ${detail.versionLabel} · ${detail.artifactReadinessState}`}
        actions={
          <Link
            href={`/workflows/${encodeURIComponent(detail.workflowKey)}/edit?versionId=${encodeURIComponent(selectedVersionId)}`}
          >
            <Button type="button" variant="ghost">
              Edit canvas
            </Button>
          </Link>
        }
      />

      {error ? (
        <div className="mb-4">
          <Notice variant="danger">{error}</Notice>
        </div>
      ) : null}
      {infoNotice ? (
        <div className="mb-4">
          <Notice variant="info">{infoNotice}</Notice>
        </div>
      ) : null}

      {versions.data && versions.data.length > 1 ? (
        <div className="mb-4 flex flex-wrap gap-2">
          {versions.data.map((version) => {
            const selected = version.id === selectedVersionId;
            return (
              <Link
                key={version.id}
                href={`/workflows/${encodeURIComponent(detail.workflowKey)}/publish?versionId=${encodeURIComponent(version.id)}`}
                className={`rounded-full px-3 py-1 text-xs font-semibold ${
                  selected
                    ? "bg-etos-accent text-etos-accent-fg"
                    : "border border-etos-border text-etos-ink-muted hover:border-etos-accent"
                }`}
              >
                {version.versionLabel} · {version.readinessState}
              </Link>
            );
          })}
        </div>
      ) : null}

      <div className="grid gap-4 md:grid-cols-4">
        <KpiCard
          label="Effective risk"
          value={derivedRisk?.effectiveRiskLevel ?? "—"}
          hint="From mark-ready"
        />
        <KpiCard
          label="Permission ceiling"
          value={derivedRisk?.permissionCeiling ?? "—"}
          hint="Inherited trust"
        />
        <KpiCard label="Steps" value={detail.steps.length} hint="Governed step graph" />
        <KpiCard
          label="Publish state"
          value={isPublished ? "Published" : detail.artifactReadinessState}
          hint="Lifecycle"
        />
      </div>

      <div className="mt-4 grid gap-4 lg:grid-cols-3">
        <Card className="lg:col-span-2">
          <CardHeader>
            <CardTitle>Publish checks</CardTitle>
          </CardHeader>
          <CardContent className="space-y-4">
            <DataTable<CheckRow>
              rows={checks}
              rowKey={(row) => row.id}
              emptyMessage="No checks."
              columns={[
                {
                  key: "check",
                  header: "Check",
                  render: (row) => <span className="font-semibold text-etos-ink">{row.check}</span>,
                },
                {
                  key: "result",
                  header: "Result",
                  render: (row) => <span className="text-etos-ink-muted">{row.result}</span>,
                },
              ]}
            />

            {readiness.blockingReasons.length > 0 ? (
              <ul className="space-y-2 text-sm text-etos-warning-fg">
                {readiness.blockingReasons.map((reason) => (
                  <li key={reason} className="rounded-xl border border-etos-border bg-etos-panel-muted px-3 py-2">
                    {reason}
                  </li>
                ))}
              </ul>
            ) : null}

            <div className="flex flex-wrap gap-3">
              <form action={markWorkflowReadyAction}>
                <input type="hidden" name="artifactId" value={artifactId} />
                <input type="hidden" name="versionId" value={selectedVersionId} />
                <input type="hidden" name="workflowKey" value={detail.workflowKey} />
                <Button type="submit" variant="ghost">
                  Mark ready
                </Button>
              </form>
              <form action={publishWorkflowAction} className="flex flex-wrap items-end gap-3">
                <input type="hidden" name="artifactId" value={artifactId} />
                <input type="hidden" name="versionId" value={selectedVersionId} />
                <input type="hidden" name="workflowKey" value={detail.workflowKey} />
                <label className="block text-sm">
                  <span className="font-semibold text-etos-ink">Summary</span>
                  <input
                    name="summary"
                    type="text"
                    placeholder="Initial publish"
                    className={fieldClass}
                  />
                </label>
                <Button type="submit">Publish</Button>
              </form>
              <Button
                type="button"
                disabled
                title="Request changes has no backend endpoint yet."
              >
                Request changes
              </Button>
            </div>
          </CardContent>
        </Card>

        <SidePanel title="Runtime trust">
          <PillStack
            items={[
              {
                label: "Manual trigger",
                value: detail.triggerConfig.manualEnabled ? "On" : "Off",
                variant: "info",
              },
              {
                label: "Partial completion",
                value: detail.allowPartialCompletion ? "Allowed" : "Off",
              },
              {
                label: "Default safe mode",
                value: detail.defaultStepSafeModeBehavior,
                variant: "warning",
              },
              {
                label: "Tools",
                value: String(detail.referencedTools.length),
              },
            ]}
          />
          <p className="mt-4 text-xs text-etos-ink-muted">
            Request changes is disabled — no backend support. Use edit + mark-ready cycle instead.
          </p>
        </SidePanel>
      </div>

      {isPublished ? (
        <Card className="mt-4">
          <CardHeader>
            <CardTitle>Execute published workflow</CardTitle>
          </CardHeader>
          <CardContent>
            <form action={executeWorkflowAction} className="space-y-4">
              <input type="hidden" name="artifactId" value={artifactId} />
              <input type="hidden" name="versionId" value={selectedVersionId} />
              <input type="hidden" name="workflowKey" value={detail.workflowKey} />
              <label className="block text-sm">
                <span className="font-semibold text-etos-ink">Structured input JSON (optional)</span>
                <textarea
                  name="structuredInputJson"
                  rows={4}
                  defaultValue={`{"intentKey":"bom-impact-context","queryText":"Investigate BOM impact for assembly A-100."}`}
                  className={`${fieldClass} font-mono text-xs`}
                />
              </label>
              <Button type="submit">Execute workflow</Button>
            </form>
          </CardContent>
        </Card>
      ) : (
        <div className="mt-4">
          <Notice variant="info">Execute is available only after the workflow version is published.</Notice>
        </div>
      )}

      <details className="mt-6 rounded-etos-card border border-etos-border bg-etos-panel-muted p-4 text-sm text-etos-ink-muted">
        <summary className="cursor-pointer font-extrabold text-etos-ink">Advanced / Debug</summary>
        <div className="mt-4 space-y-4">
          <form action={workflowTestRunAction}>
            <input type="hidden" name="artifactId" value={artifactId} />
            <input type="hidden" name="versionId" value={selectedVersionId} />
            <input type="hidden" name="workflowKey" value={detail.workflowKey} />
            <Button type="submit" variant="ghost">
              Workflow test-run
            </Button>
          </form>
          <pre className="overflow-x-auto rounded-xl border border-etos-border bg-etos-panel p-3 text-xs">
            {JSON.stringify({ detail, readiness, derivedRisk }, null, 2)}
          </pre>
        </div>
      </details>
    </main>
  );
}
