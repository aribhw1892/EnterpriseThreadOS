import Link from "next/link";
import { WorkflowCanvas } from "@/components/workflows/WorkflowCanvas";
import { Button } from "@/components/ui/Button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/Card";
import { ErrorState } from "@/components/ui/ErrorState";
import { Notice } from "@/components/ui/Notice";
import { PageHeader } from "@/components/ui/PageHeader";
import { PillStack, SidePanel } from "@/components/ui/SidePanel";
import { getArtifactVersions, loadWorkflowVersionByKey } from "@/lib/etos-api";

export const dynamic = "force-dynamic";

type PageProps = {
  params: Promise<{ workflowKey: string }>;
  searchParams: Promise<{ versionId?: string; error?: string; notice?: string }>;
};

export default async function WorkflowEditPage({ params, searchParams }: PageProps) {
  const { workflowKey } = await params;
  const { versionId, error, notice } = await searchParams;
  const decodedKey = decodeURIComponent(workflowKey);
  const loaded = await loadWorkflowVersionByKey(decodedKey, versionId);

  if (!loaded.data) {
    return (
      <main className="px-6 py-8 lg:px-8">
        <PageHeader title="Workflow editor" description="Workflow not found." />
        <ErrorState error={loaded.error ?? "Workflow was not found."} />
      </main>
    );
  }

  const { detail, readiness, artifactId, versionId: selectedVersionId } = loaded.data;
  const versions = await getArtifactVersions(artifactId);

  return (
    <main className="px-6 py-8 lg:px-8">
      <PageHeader
        title={detail.displayName}
        description={`${detail.workflowKey} · ${detail.versionLabel} · ${detail.artifactReadinessState} · ${detail.workflowScope}`}
        actions={
          <>
            <Link
              href={`/workflows/${encodeURIComponent(detail.workflowKey)}/publish?versionId=${encodeURIComponent(selectedVersionId)}`}
            >
              <Button type="button">Publish review</Button>
            </Link>
            <Link href="/workflows">
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

      {versions.data && versions.data.length > 1 ? (
        <div className="mb-4 flex flex-wrap gap-2">
          {versions.data.map((version) => {
            const selected = version.id === selectedVersionId;
            return (
              <Link
                key={version.id}
                href={`/workflows/${encodeURIComponent(detail.workflowKey)}/edit?versionId=${encodeURIComponent(version.id)}`}
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

      <div className="grid gap-4 lg:grid-cols-3">
        <Card className="lg:col-span-2">
          <CardHeader>
            <CardTitle>Workflow canvas</CardTitle>
          </CardHeader>
          <CardContent>
            <WorkflowCanvas
              artifactId={artifactId}
              versionId={selectedVersionId}
              workflowKey={detail.workflowKey}
              initialSteps={detail.steps}
              versionLabel={detail.versionLabel}
            />
          </CardContent>
        </Card>

        <div className="space-y-4">
          <SidePanel title="Step library">
            <PillStack
              items={[
                { label: "Steps", value: String(detail.steps.length), variant: "info" },
                { label: "Agents", value: String(detail.referencedAgents.length), variant: "neutral" },
                { label: "Tools", value: String(detail.referencedTools.length), variant: "neutral" },
                {
                  label: "Safe mode",
                  value: detail.safeModeEnabled ? "On" : "Off",
                  variant: detail.safeModeEnabled ? "warning" : "success",
                },
              ]}
            />
            <p className="mt-4 text-xs leading-relaxed text-etos-ink-muted">
              Drag nodes to rearrange. Delete removes the step from the next draft version. Add step is disabled —
              typed step payloads are not authored in UI yet.
            </p>
          </SidePanel>

          <SidePanel title="Readiness">
            {readiness.blockingReasons.length > 0 ? (
              <ul className="space-y-2 text-xs text-etos-warning-fg">
                {readiness.blockingReasons.map((reason) => (
                  <li key={reason} className="rounded-xl border border-etos-border bg-etos-panel-muted px-2 py-1.5">
                    {reason}
                  </li>
                ))}
              </ul>
            ) : (
              <p className="text-xs text-etos-ink-muted">No blocking readiness notes.</p>
            )}
            {detail.derivedCapabilityRisk ? (
              <p className="mt-3 text-xs text-etos-ink-muted">
                Risk: {detail.derivedCapabilityRisk.effectiveRiskLevel} · ceiling{" "}
                {detail.derivedCapabilityRisk.permissionCeiling}
              </p>
            ) : null}
          </SidePanel>
        </div>
      </div>

      <details className="mt-6 rounded-etos-card border border-etos-border bg-etos-panel-muted p-4 text-sm text-etos-ink-muted">
        <summary className="cursor-pointer font-extrabold text-etos-ink">Advanced / Debug</summary>
        <pre className="mt-4 overflow-x-auto rounded-xl border border-etos-border bg-etos-panel p-3 text-xs">
          {JSON.stringify({ detail, readiness }, null, 2)}
        </pre>
      </details>
    </main>
  );
}
