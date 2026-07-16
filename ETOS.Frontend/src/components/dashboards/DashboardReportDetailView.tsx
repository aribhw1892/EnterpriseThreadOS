import Link from "next/link";
import { revalidatePath } from "next/cache";
import {
  ApiResult,
  ArtifactReadiness,
  DashboardReportPreview,
  DashboardReportTemplate,
  exportDashboardReport,
  getArtifactImpact,
  getArtifactReadiness,
  getDashboardReportTemplate,
  markDashboardReportReady,
  previewDashboardReport,
  publishArtifactVersion,
} from "@/lib/etos-api";
import { ExplorerNavLink } from "@/components/explorers/ExplorerListShell";

type DashboardReportKind = "dashboard" | "report";

type DashboardReportDetailProps = {
  kind: DashboardReportKind;
  artifactId: string;
  versionId: string;
  artifactName: string;
  template: DashboardReportTemplate;
  preview: DashboardReportPreview;
  readiness: ArtifactReadiness;
  dependencyCount: number;
};

async function markReadyAction(formData: FormData) {
  "use server";

  const kind = formData.get("kind");
  const artifactId = formData.get("artifactId");
  const versionId = formData.get("versionId");
  if (typeof kind !== "string" || typeof artifactId !== "string" || typeof versionId !== "string") {
    return;
  }

  await markDashboardReportReady(kind as DashboardReportKind, artifactId, versionId);
  revalidatePath(kind === "dashboard" ? `/dashboards/${artifactId}` : `/reports/${artifactId}`);
}

async function publishAction(formData: FormData) {
  "use server";

  const kind = formData.get("kind");
  const artifactId = formData.get("artifactId");
  const versionId = formData.get("versionId");
  if (typeof kind !== "string" || typeof artifactId !== "string" || typeof versionId !== "string") {
    return;
  }

  await publishArtifactVersion(artifactId, versionId, "Published from dashboard/report UI.");
  revalidatePath(kind === "dashboard" ? `/dashboards/${artifactId}` : `/reports/${artifactId}`);
}

async function exportAction(formData: FormData) {
  "use server";

  const kind = formData.get("kind");
  const artifactId = formData.get("artifactId");
  const versionId = formData.get("versionId");
  if (typeof kind !== "string" || typeof artifactId !== "string" || typeof versionId !== "string") {
    return;
  }

  await exportDashboardReport(kind as DashboardReportKind, artifactId, versionId);
  revalidatePath(kind === "dashboard" ? `/dashboards/${artifactId}` : `/reports/${artifactId}`);
}

function ActionForm({
  action,
  kind,
  artifactId,
  versionId,
  label,
}: {
  action: (formData: FormData) => Promise<void>;
  kind: DashboardReportKind;
  artifactId: string;
  versionId: string;
  label: string;
}) {
  return (
    <form action={action}>
      <input type="hidden" name="kind" value={kind} />
      <input type="hidden" name="artifactId" value={artifactId} />
      <input type="hidden" name="versionId" value={versionId} />
      <button
        type="submit"
        className="inline-flex items-center rounded-etos-button border border-etos-border px-4 py-2 text-sm font-semibold text-etos-ink transition hover:bg-etos-panel-muted"
      >
        {label}
      </button>
    </form>
  );
}

export function DashboardReportDetailView({
  kind,
  artifactId,
  versionId,
  artifactName,
  template,
  preview,
  readiness,
  dependencyCount,
}: DashboardReportDetailProps) {
  const listHref = kind === "dashboard" ? "/dashboards" : "/reports";
  const title = kind === "dashboard" ? "Dashboard" : "Report";

  return (
    <main className="">
      <div className="flex flex-col gap-6">
        <section className="rounded-etos-card border border-etos-border-panel bg-etos-panel-elevated p-6 shadow-etos">
          <div className="flex flex-wrap items-start justify-between gap-4">
            <div>
              <p className="text-sm uppercase tracking-wide text-etos-accent-cyan">Issue 17</p>
              <h1 className="mt-2 text-4xl font-semibold">{artifactName}</h1>
              <p className="mt-3 max-w-3xl text-etos-ink-muted">
                {title} version {template.versionLabel} · {readiness.storedReadinessState}
              </p>
              {template.summary ? <p className="mt-2 text-sm text-etos-ink-subtle">{template.summary}</p> : null}
            </div>
            <div className="flex flex-wrap gap-3">
              <ExplorerNavLink href={listHref}>{title}s</ExplorerNavLink>
              <ExplorerNavLink href="/explorers">Explorers</ExplorerNavLink>
              <Link
                href={`/artifacts/${artifactId}`}
                className="rounded-full border border-etos-border px-4 py-2 text-sm font-semibold text-etos-ink transition hover:border-etos-accent hover:text-etos-accent"
              >
                Artifact explorer
              </Link>
            </div>
          </div>
          <div className="mt-6 flex flex-wrap gap-3">
            <ActionForm
              action={markReadyAction}
              kind={kind}
              artifactId={artifactId}
              versionId={versionId}
              label="Save draft / Mark ready"
            />
            <ActionForm
              action={publishAction}
              kind={kind}
              artifactId={artifactId}
              versionId={versionId}
              label="Request publish"
            />
            <ActionForm action={exportAction} kind={kind} artifactId={artifactId} versionId={versionId} label="Export JSON" />
          </div>
        </section>

        <section className="rounded-etos-card border border-etos-border-panel bg-etos-panel-elevated p-6 shadow-etos">
          <h2 className="text-2xl font-semibold text-etos-ink">Widget grid preview</h2>
          <p className="mt-2 text-sm text-etos-ink-muted">
            Layout placeholder for builder canvas. Blocks map from template definitions.
          </p>
          <div className="mt-6 grid gap-4 sm:grid-cols-2 xl:grid-cols-3">
            {template.blocks.length > 0 ? (
              template.blocks.map((block) => (
                <div
                  key={block.blockId}
                  className="flex min-h-[120px] flex-col justify-between rounded-etos-card border border-dashed border-etos-border bg-etos-panel-muted p-4"
                >
                  <div>
                    <p className="text-xs uppercase tracking-wide text-etos-ink-subtle">{block.kind}</p>
                    <p className="mt-1 font-semibold text-etos-ink">{block.title}</p>
                  </div>
                  <p className="text-xs text-etos-ink-muted">
                    {block.kpiKey ? `KPI ${block.kpiKey}` : block.queryIntentRef ?? "Widget slot"}
                  </p>
                </div>
              ))
            ) : (
              <p className="text-sm text-etos-ink-muted sm:col-span-2 xl:col-span-3">No template blocks yet.</p>
            )}
          </div>
        </section>

        <section className="grid gap-6 lg:grid-cols-2">
          <div className="rounded-etos-card border border-etos-border-panel bg-etos-panel-elevated p-6 shadow-etos">
            <h2 className="text-2xl font-semibold">Readiness</h2>
            <ul className="mt-4 space-y-2 text-sm text-etos-ink">
              <li>Stored: {readiness.storedReadinessState}</li>
              <li>Recalculated: {readiness.recalculatedReadinessState}</li>
              <li>Compatibility: {readiness.compatibilityStatus}</li>
              <li>Policy risk: {readiness.policyRiskStatus}</li>
              <li>Dependencies: {dependencyCount}</li>
            </ul>
            {readiness.blockingReasons.length > 0 ? (
              <ul className="mt-4 space-y-2 text-sm text-etos-warning-fg">
                {readiness.blockingReasons.map((reason) => (
                  <li key={reason}>{reason}</li>
                ))}
              </ul>
            ) : (
              <p className="mt-4 text-sm text-etos-ink-subtle">No publish blockers from readiness recalculation.</p>
            )}
          </div>

          <div className="rounded-etos-card border border-etos-border-panel bg-etos-panel-elevated p-6 shadow-etos">
            <h2 className="text-2xl font-semibold">Template blocks</h2>
            <ul className="mt-4 space-y-3 text-sm text-etos-ink">
              {template.blocks.map((block) => (
                <li key={block.blockId} className="rounded-etos-card border border-etos-border-soft bg-etos-panel p-4">
                  <p className="font-semibold text-etos-ink">{block.title}</p>
                  <p className="text-xs uppercase text-etos-ink-subtle">{block.kind}</p>
                  {block.queryIntentRef ? <p className="mt-1 text-xs text-etos-ink-muted">Intent: {block.queryIntentRef}</p> : null}
                  {block.kpiKey ? <p className="mt-1 text-xs text-etos-ink-muted">KPI: {block.kpiKey}</p> : null}
                </li>
              ))}
            </ul>
          </div>
        </section>

        <section className="rounded-etos-card border border-etos-border-panel bg-etos-panel-elevated p-6 shadow-etos">
          <h2 className="text-2xl font-semibold">Governed preview</h2>
          <p className="mt-2 text-sm text-etos-ink-muted">
            Preview runs through governed query only. Allowed {preview.filterSummary.allowedContextTotal} · Denied{" "}
            {preview.filterSummary.deniedContextTotal}
          </p>
          <div className="mt-6 grid gap-4 md:grid-cols-2">
            {preview.blocks.map((block) => (
              <article key={block.blockId} className="rounded-etos-card border border-etos-border-soft bg-etos-panel p-4">
                <div className="flex items-center justify-between gap-3">
                  <h3 className="font-semibold">{block.title}</h3>
                  <span className="rounded-full border border-etos-border px-3 py-1 text-xs uppercase text-etos-ink-muted">
                    {block.status}
                  </span>
                </div>
                <p className="mt-3 text-sm text-etos-ink">{block.safeSummary}</p>
                {block.kind === "governed_query" ? (
                  <p className="mt-2 text-xs text-etos-ink-subtle">
                    Allowed {block.allowedCount} · Denied {block.deniedCount}
                  </p>
                ) : null}
              </article>
            ))}
          </div>
        </section>

        {kind === "report" ? (
          <section className="rounded-etos-card border border-etos-border-panel bg-etos-panel-elevated p-6 shadow-etos">
            <h2 className="text-2xl font-semibold text-etos-ink">Evidence appendix</h2>
            <p className="mt-2 text-sm text-etos-ink-muted">
              Report evidence drawn from governed preview blocks and filter summary.
            </p>
            <ul className="mt-4 space-y-3 text-sm">
              {preview.blocks.map((block) => (
                <li
                  key={`evidence-${block.blockId}`}
                  className="rounded-etos-card border border-etos-border-soft bg-etos-panel p-4"
                >
                  <p className="font-semibold text-etos-ink">{block.title}</p>
                  <p className="mt-1 text-etos-ink-muted">{block.safeSummary}</p>
                  <p className="mt-2 text-xs text-etos-ink-subtle">
                    Status {block.status}
                    {block.kind === "governed_query"
                      ? ` · allowed ${block.allowedCount} · denied ${block.deniedCount}`
                      : ""}
                  </p>
                </li>
              ))}
            </ul>
            <p className="mt-4 text-xs text-etos-ink-subtle">
              Filter totals: allowed {preview.filterSummary.allowedContextTotal} · denied{" "}
              {preview.filterSummary.deniedContextTotal}
            </p>
          </section>
        ) : null}
      </div>
    </main>
  );
}

export async function loadDashboardReportDetail(
  kind: DashboardReportKind,
  artifactId: string,
  versionId?: string,
): Promise<
  ApiResult<{
    versionId: string;
    artifactName: string;
    template: DashboardReportTemplate;
    preview: DashboardReportPreview;
    readiness: ArtifactReadiness;
    dependencyCount: number;
  }>
> {
  const { getArtifactVersions, getDashboardArtifacts, getReportArtifacts } = await import("@/lib/etos-api");
  const list = kind === "dashboard" ? await getDashboardArtifacts() : await getReportArtifacts();
  if (!list.data) {
    return { data: null, error: list.error };
  }

  const artifact = list.data.find((item) => item.id === artifactId);
  if (!artifact) {
    return { data: null, error: "Dashboard or report artifact was not found." };
  }

  const versions = await getArtifactVersions(artifactId);
  if (!versions.data || versions.data.length === 0) {
    return { data: null, error: versions.error ?? "No versions found." };
  }

  const selectedVersionId = versionId ?? versions.data[0].id;
  const template = await getDashboardReportTemplate(kind, artifactId, selectedVersionId);
  if (!template.data) {
    return { data: null, error: template.error };
  }

  const preview = await previewDashboardReport(kind, artifactId, selectedVersionId, {
    startGraphNodeId: template.data.defaultAnchor.startGraphNodeId ?? null,
    documentArtifactId: template.data.defaultAnchor.documentArtifactId ?? null,
    policyKey: "published-policy",
  });
  if (!preview.data) {
    return { data: null, error: preview.error };
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
      template: template.data,
      preview: preview.data,
      readiness: readiness.data,
      dependencyCount: impact.data?.dependencies.length ?? 0,
    },
    error: null,
  };
}
