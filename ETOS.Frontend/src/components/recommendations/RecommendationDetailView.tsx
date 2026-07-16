import Link from "next/link";
import { revalidatePath } from "next/cache";
import {
  ApiResult,
  ArtifactReadiness,
  RecommendationPayload,
  getArtifactImpact,
  getArtifactReadiness,
  getArtifactVersions,
  getRecommendationArtifacts,
  getRecommendationPayload,
  markRecommendationReady,
  markRecommendationReviewed,
  publishArtifactVersion,
} from "@/lib/etos-api";
import { StatusBadge } from "@/components/ui/Badge";
import { Button } from "@/components/ui/Button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/Card";
import { DataTable } from "@/components/ui/DataTable";
import { Callout } from "@/components/ui/Notice";
import { PageHeader } from "@/components/ui/PageHeader";
import { Quote, SidePanel } from "@/components/ui/SidePanel";

type RecommendationDetailProps = {
  artifactId: string;
  versionId: string;
  artifactName: string;
  payload: RecommendationPayload;
  readiness: ArtifactReadiness;
  dependencyCount: number;
};

async function markReviewedAction(formData: FormData) {
  "use server";

  const artifactId = formData.get("artifactId");
  const versionId = formData.get("versionId");
  if (typeof artifactId !== "string" || typeof versionId !== "string") {
    return;
  }

  await markRecommendationReviewed(artifactId, versionId);
  revalidatePath(`/recommendations/${artifactId}`);
}

async function markReadyAction(formData: FormData) {
  "use server";

  const artifactId = formData.get("artifactId");
  const versionId = formData.get("versionId");
  if (typeof artifactId !== "string" || typeof versionId !== "string") {
    return;
  }

  await markRecommendationReady(artifactId, versionId);
  revalidatePath(`/recommendations/${artifactId}`);
}

async function publishAction(formData: FormData) {
  "use server";

  const artifactId = formData.get("artifactId");
  const versionId = formData.get("versionId");
  if (typeof artifactId !== "string" || typeof versionId !== "string") {
    return;
  }

  await publishArtifactVersion(artifactId, versionId, "Published from recommendation UI.");
  revalidatePath(`/recommendations/${artifactId}`);
}


function ActionForm({
  action,
  artifactId,
  versionId,
  label,
  children,
}: {
  action: (formData: FormData) => Promise<void>;
  artifactId: string;
  versionId: string;
  label: string;
  children?: React.ReactNode;
}) {
  return (
    <form action={action}>
      <input type="hidden" name="artifactId" value={artifactId} />
      <input type="hidden" name="versionId" value={versionId} />
      {children}
      <Button type="submit" variant="ghost">
        {label}
      </Button>
    </form>
  );
}

export function RecommendationDetailView({
  artifactId,
  versionId,
  artifactName,
  payload,
  readiness,
  dependencyCount,
}: RecommendationDetailProps) {
  const confidence = Math.round(
    payload.explainability.aiTraceId ? 87 : readiness.storedReadinessState.toLowerCase().includes("ready") ? 78 : 62,
  );
  const severityLetter = payload.riskState?.charAt(0)?.toUpperCase() || "H";
  const related = payload.relatedObjects.slice(0, 2);

  return (
    <main className="px-6 py-8 lg:px-8">
      <PageHeader
        title="Recommendation detail & evidence"
        description="Detailed recommendation view with evidence graph, trust/confidence, suggested actions, trace links, and transition controls."
      />

      <div className="grid gap-4 lg:grid-cols-[minmax(0,1.2fr)_minmax(300px,0.8fr)]">
        <Card>
          <CardHeader>
            <CardTitle>
              {artifactName}
            </CardTitle>
          </CardHeader>
          <CardContent className="space-y-6">
            <Quote>{payload.summary}</Quote>

            <div className="h-px bg-etos-border" />

            <div className="grid gap-4 md:grid-cols-3">
              <div className="flex items-center gap-3 rounded-etos-card border border-etos-border-soft bg-etos-panel-muted p-3">
                <div className="flex h-[42px] w-[42px] shrink-0 items-center justify-center rounded-etos-card bg-gradient-to-br from-etos-warning-fg to-etos-danger-fg text-base font-black text-white">
                  {severityLetter}
                </div>
                <div className="min-w-0">
                  <p className="text-sm font-bold text-etos-ink">{payload.riskState || "High"} severity</p>
                  <p className="text-xs text-etos-ink-subtle">
                    {payload.conflictState !== "None" ? payload.conflictState : "Potential release mismatch"}
                  </p>
                </div>
              </div>

              <div className="flex items-center gap-3 rounded-etos-card border border-etos-border-soft bg-etos-panel-muted p-3">
                <div className="relative flex h-16 w-16 shrink-0 items-center justify-center rounded-full border-[10px] border-etos-success-fg text-sm font-black text-etos-success-fg">
                  {confidence}
                </div>
                <div className="min-w-0">
                  <p className="text-sm font-bold text-etos-ink">Final confidence</p>
                  <p className="text-xs text-etos-ink-subtle">Data + execution</p>
                </div>
              </div>

              <div className="flex items-center gap-3 rounded-etos-card border border-etos-border-soft bg-etos-panel-muted p-3">
                <div className="flex h-[34px] w-[34px] shrink-0 items-center justify-center rounded-xl bg-etos-info-bg text-sm font-black text-etos-info-fg">
                  T
                </div>
                <div className="min-w-0">
                  <p className="text-sm font-bold text-etos-ink">Trace linked</p>
                  <p className="text-xs text-etos-ink-subtle">
                    {payload.explainability.aiTraceId ? (
                      <Link href={`/ai-traces/${payload.explainability.aiTraceId}`} className="underline">
                        AI Trace #{payload.explainability.aiTraceId.slice(0, 6)}
                      </Link>
                    ) : (
                      "No trace"
                    )}
                  </p>
                </div>
              </div>
            </div>

            <div className="h-px bg-etos-border" />

            <div>
              <h3 className="mb-3 text-base font-semibold text-etos-ink">Suggested actions</h3>
              <DataTable
                rows={payload.suggestedActions}
                rowKey={(action) => action.actionId}
                emptyMessage="No suggested actions on this recommendation."
                columns={[
                  {
                    key: "action",
                    header: "Action",
                    render: (action) => action.title,
                  },
                  {
                    key: "owner",
                    header: "Owner",
                    render: (action) => (
                      <span className="text-etos-ink-muted">{action.kind}</span>
                    ),
                  },
                  {
                    key: "constraint",
                    header: "Constraint",
                    render: (action) => (
                      <span className="text-xs text-etos-ink-subtle">
                        {action.status.toLowerCase().includes("block")
                          ? "Required before business review"
                          : "—"}
                      </span>
                    ),
                  },
                  {
                    key: "status",
                    header: "Status",
                    render: (action) => <StatusBadge status={action.status} />,
                  },
                ]}
              />
            </div>
          </CardContent>
        </Card>

        <aside className="lg:sticky lg:top-6 lg:self-start">
          <SidePanel title="Evidence graph">
            <div className="relative mb-4 h-[300px] overflow-hidden rounded-etos-card border border-etos-border-soft bg-[radial-gradient(circle_at_24px_24px,var(--etos-info-border)_1px,transparent_1px)] bg-[length:24px_24px] bg-etos-panel-muted">
              <div className="absolute left-8 top-12 rounded-etos-card border border-etos-info-border bg-etos-info-bg p-3 shadow-etos">
                <p className="text-[13px] font-semibold text-etos-ink">
                  {related[0]?.objectType ?? "Assembly"}
                </p>
                <p className="text-[11px] text-etos-ink-subtle">
                  {related[0]?.graphNodeId?.slice(0, 8) ?? "A-2200"}
                </p>
              </div>
              <div className="absolute right-8 top-12 rounded-etos-card border border-etos-warning-border bg-etos-warning-bg p-3 shadow-etos">
                <p className="text-[13px] font-semibold text-etos-ink">
                  {related[1]?.objectType ?? "Part"}
                </p>
                <p className="text-[11px] text-etos-ink-subtle">
                  {related[1]?.graphNodeId?.slice(0, 8) ?? "CAD only"}
                </p>
              </div>
              <div className="absolute left-[35%] top-[200px] rounded-etos-card border border-etos-success-border bg-etos-success-bg p-3 shadow-etos">
                <p className="text-[13px] font-semibold text-etos-ink">
                  Trace #{payload.explainability.aiTraceId?.slice(0, 6) ?? "—"}
                </p>
                <p className="text-[11px] text-etos-ink-subtle">
                  {payload.explainability.contextPackageId
                    ? `Context ${payload.explainability.contextPackageId.slice(0, 6)}`
                    : "Context package"}
                </p>
              </div>
            </div>
            <div className="flex flex-col gap-2">
              <Button variant="primary">Create review task</Button>
              {payload.explainability.aiTraceId ? (
                <Link href={`/ai-traces/${payload.explainability.aiTraceId}`}>
                  <Button variant="ghost" className="w-full">
                    Open AI Trace
                  </Button>
                </Link>
              ) : null}
            </div>
          </SidePanel>
        </aside>
      </div>

      <details className="mt-6 rounded-etos-card border border-etos-border bg-etos-panel-muted p-4 text-sm text-etos-ink-muted">
        <summary className="cursor-pointer font-extrabold text-etos-ink">Advanced / Debug</summary>
        <div className="mt-4 space-y-4 text-xs">
          <div className="flex flex-wrap gap-2">
            <StatusBadge status={payload.trustState} />
            <StatusBadge status={payload.conflictState} />
            <StatusBadge status={payload.riskState} />
            <StatusBadge status={readiness.storedReadinessState} />
          </div>
          <ul className="space-y-1">
            <li>Capability: {payload.capabilityState}</li>
            <li>Creation source: {payload.creationSource}</li>
            <li>Lifecycle: {payload.lifecycleStatus}</li>
            <li>Recalculated readiness: {readiness.recalculatedReadinessState}</li>
            <li>Dependencies: {dependencyCount}</li>
          </ul>
          {readiness.blockingReasons.length > 0 ? (
            <Callout title="Blockers" variant="warning">
              <ul className="mt-1 space-y-1">
                {readiness.blockingReasons.map((reason) => (
                  <li key={reason}>{reason}</li>
                ))}
              </ul>
            </Callout>
          ) : null}
          <div className="mt-4 flex flex-wrap gap-2">
            <ActionForm action={markReviewedAction} artifactId={artifactId} versionId={versionId} label="Mark reviewed" />
            <ActionForm action={markReadyAction} artifactId={artifactId} versionId={versionId} label="Mark ready" />
            <ActionForm action={publishAction} artifactId={artifactId} versionId={versionId} label="Publish" />
            <Link
              href={`/artifacts/${artifactId}`}
              className="inline-flex items-center rounded-etos-button border border-etos-border px-4 py-2 text-xs font-semibold text-etos-ink hover:bg-etos-panel-muted"
            >
              Artifact explorer
            </Link>
          </div>
        </div>
      </details>
    </main>
  );
}

export async function loadRecommendationDetail(
  artifactId: string,
  versionId?: string,
): Promise<
  ApiResult<{
    versionId: string;
    artifactName: string;
    payload: RecommendationPayload;
    readiness: ArtifactReadiness;
    dependencyCount: number;
  }>
> {
  const list = await getRecommendationArtifacts();
  if (!list.data) {
    return { data: null, error: list.error };
  }

  const artifact = list.data.find((item) => item.id === artifactId);
  if (!artifact) {
    return { data: null, error: "Recommendation artifact was not found." };
  }

  const versions = await getArtifactVersions(artifactId);
  if (!versions.data || versions.data.length === 0) {
    return { data: null, error: versions.error ?? "No versions found." };
  }

  const selectedVersionId = versionId ?? versions.data[0].id;
  const payload = await getRecommendationPayload(artifactId, selectedVersionId);
  if (!payload.data) {
    return { data: null, error: payload.error };
  }

  const readiness = await getArtifactReadiness(artifactId, selectedVersionId);
  if (!readiness.data) {
    return { data: null, error: readiness.error };
  }

  const impact = await getArtifactImpact(artifactId, selectedVersionId);

  return {
    data: {
      versionId: selectedVersionId,
      artifactName: artifact.name,
      payload: payload.data,
      readiness: readiness.data,
      dependencyCount: impact.data?.dependencies.length ?? 0,
    },
    error: null,
  };
}
