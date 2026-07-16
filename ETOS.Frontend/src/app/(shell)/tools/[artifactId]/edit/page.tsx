import Link from "next/link";
import {
  compatibilityScanToolAction,
  dryRunToolAction,
  markToolReadyAction,
  publishToolAction,
} from "@/app/(shell)/tools/actions";
import { Badge } from "@/components/ui/Badge";
import { Button } from "@/components/ui/Button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/Card";
import { ErrorState } from "@/components/ui/ErrorState";
import { Notice } from "@/components/ui/Notice";
import { PageHeader } from "@/components/ui/PageHeader";
import {
  ApiResult,
  ArtifactReadiness,
  ToolDefinitionDetail,
  getArtifactReadiness,
  getArtifactVersions,
  getToolDefinitionArtifacts,
  getToolDefinitionDetail,
} from "@/lib/etos-api";

export const dynamic = "force-dynamic";

type PageProps = {
  params: Promise<{ artifactId: string }>;
  searchParams: Promise<{ versionId?: string; error?: string; notice?: string }>;
};

async function loadToolDefinitionDetail(
  artifactId: string,
  versionId?: string,
): Promise<
  ApiResult<{
    versionId: string;
    artifactName: string;
    detail: ToolDefinitionDetail;
    readiness: ArtifactReadiness;
    versions: { id: string; versionLabel: string }[];
  }>
> {
  const list = await getToolDefinitionArtifacts();
  if (!list.data) {
    return { data: null, error: list.error };
  }

  const artifact = list.data.find((item) => item.id === artifactId);
  if (!artifact) {
    return { data: null, error: "Tool definition artifact was not found." };
  }

  const versions = await getArtifactVersions(artifactId);
  if (!versions.data || versions.data.length === 0) {
    return { data: null, error: versions.error ?? "No versions found." };
  }

  const selectedVersionId = versionId ?? versions.data[0].id;
  const detail = await getToolDefinitionDetail(artifactId, selectedVersionId);
  if (!detail.data) {
    return { data: null, error: detail.error };
  }

  const readiness = await getArtifactReadiness(artifactId, selectedVersionId);
  if (!readiness.data) {
    return { data: null, error: readiness.error };
  }

  return {
    data: {
      versionId: selectedVersionId,
      artifactName: artifact.name,
      detail: detail.data,
      readiness: readiness.data,
      versions: versions.data.map((v) => ({ id: v.id, versionLabel: v.versionLabel })),
    },
    error: null,
  };
}

function ActionButton({
  action,
  artifactId,
  versionId,
  label,
  variant = "ghost",
}: {
  action: (formData: FormData) => Promise<void>;
  artifactId: string;
  versionId: string;
  label: string;
  variant?: "primary" | "ghost" | "good";
}) {
  return (
    <form action={action}>
      <input type="hidden" name="artifactId" value={artifactId} />
      <input type="hidden" name="versionId" value={versionId} />
      <Button type="submit" variant={variant}>
        {label}
      </Button>
    </form>
  );
}

export default async function ToolDefinitionEditorPage({ params, searchParams }: PageProps) {
  const { artifactId } = await params;
  const { versionId, error, notice } = await searchParams;
  const loaded = await loadToolDefinitionDetail(artifactId, versionId);

  if (!loaded.data) {
    return (
      <main className="px-6 py-8 lg:px-8">
        <ErrorState error={loaded.error ?? "Tool definition was not found."} />
        <p className="mt-4 text-sm">
          <Link href="/tools" className="text-etos-accent hover:underline">
            Back to registry
          </Link>
        </p>
      </main>
    );
  }

  const { detail, readiness, versions, versionId: selectedVersionId, artifactName } = loaded.data;
  const schemaCode = [
    `InputSchema:`,
    detail.inputSchemaJson,
    "",
    `OutputSchema:`,
    detail.outputSchemaJson,
    "",
    detail.referencedOutputSchema
      ? `ReferencedOutputSchema: ${detail.referencedOutputSchema.outputSchemaArtifactName} · ${detail.referencedOutputSchema.versionLabel}`
      : "ReferencedOutputSchema: (inline)",
    `OutputValidation: strict`,
    `InvalidOutputBehavior: block_downstream`,
  ].join("\n");

  return (
    <main className="px-6 py-8 lg:px-8">
      <PageHeader
        title="Tool definition editor"
        description="Schema-first tool registration with intent, permission, and classification governance. Schema fields are read-only in this slice."
        actions={
          <>
            <Button type="button" disabled title="Save draft requires create/update POST not wired in UI.">
              Save draft
            </Button>
            <ActionButton
              action={markToolReadyAction}
              artifactId={artifactId}
              versionId={selectedVersionId}
              label="Mark ready"
              variant="primary"
            />
            <ActionButton
              action={publishToolAction}
              artifactId={artifactId}
              versionId={selectedVersionId}
              label="Publish"
              variant="good"
            />
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

      <div className="grid gap-4 lg:grid-cols-[1.2fr_0.8fr]">
        <Card>
          <CardHeader>
            <CardTitle>Definition</CardTitle>
          </CardHeader>
          <CardContent>
            <div className="grid gap-3 sm:grid-cols-2">
              <Field label="Tool name" value={artifactName} />
              <Field label="Tool category" value={detail.toolCategory} />
              <Field
                label="Risk level"
                value={`${detail.riskLevel}${detail.capabilityFlags.callsExternalSystem ? " · can expose external context" : ""}`}
              />
              <Field
                label="Dry run support"
                value={detail.capabilityFlags.supportsDryRun ? "Supported" : "Not supported"}
              />
            </div>
            <div className="my-4 h-px bg-etos-border" />
            <div className="flex flex-wrap gap-2">
              {detail.allowedQueryIntentKeys.map((intent) => (
                <Badge key={intent} variant="info">
                  Allowed intent: {intent}
                </Badge>
              ))}
              {detail.requiredPermissionKeys.map((permission) => (
                <Badge key={permission} variant="purple">
                  Permission: {permission}
                </Badge>
              ))}
              {detail.capabilityFlags.readOnly ? (
                <Badge variant="success">Read-only</Badge>
              ) : null}
              {detail.capabilityFlags.writesExternalSystem ? (
                <Badge variant="danger">Writes external</Badge>
              ) : (
                <Badge variant="warning">Classification filtered</Badge>
              )}
              {detail.referencedConnector ? (
                <Link href={`/connectors/${detail.referencedConnector.connectorArtifactId}`}>
                  <Badge variant="teal">
                    Connector: {detail.referencedConnector.connectorKey}
                  </Badge>
                </Link>
              ) : null}
            </div>
            <p className="mt-4 text-sm text-etos-ink-muted">
              {detail.toolKey} · {detail.versionLabel} · {detail.artifactReadinessState}
            </p>
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>Schema references</CardTitle>
          </CardHeader>
          <CardContent>
            <pre className="overflow-x-auto whitespace-pre-wrap rounded-xl border border-etos-ops-border bg-etos-ops-panel p-3.5 font-mono text-xs leading-relaxed text-etos-ops-ink">
              {schemaCode}
            </pre>
            <div className="mt-4 flex flex-wrap gap-2">
              <ActionButton
                action={compatibilityScanToolAction}
                artifactId={artifactId}
                versionId={selectedVersionId}
                label="Validate schema compatibility"
                variant="primary"
              />
              {detail.capabilityFlags.supportsDryRun ? (
                <ActionButton
                  action={dryRunToolAction}
                  artifactId={artifactId}
                  versionId={selectedVersionId}
                  label="Dry-run"
                  variant="ghost"
                />
              ) : (
                <Button type="button" disabled title="Tool does not support dry-run.">
                  Dry-run
                </Button>
              )}
            </div>
          </CardContent>
        </Card>
      </div>

      <details className="mt-6 rounded-etos-card border border-etos-border bg-etos-panel-muted p-4 text-sm text-etos-ink-muted">
        <summary className="cursor-pointer font-extrabold text-etos-ink">
          Advanced / Debug
        </summary>
        <div className="mt-4 space-y-4">
          <p>
            Save draft / register create flows are disabled until a create/update UI binds{" "}
            <code className="font-mono text-xs">POST /api/admin/tools</code>. Schema JSON is
            read-only from the live definition.
          </p>
          <div>
            <p className="font-semibold text-etos-ink">Versions</p>
            <ul className="mt-2 space-y-1">
              {versions.map((version) => (
                <li key={version.id}>
                  <Link
                    href={`/tools/${artifactId}/edit?versionId=${version.id}`}
                    className="text-etos-accent hover:underline"
                  >
                    {version.versionLabel}
                  </Link>
                  {version.id === selectedVersionId ? " · selected" : ""}
                </li>
              ))}
            </ul>
          </div>
          {readiness.blockingReasons.length > 0 ? (
            <div>
              <p className="font-semibold text-etos-ink">Readiness blockers</p>
              <ul className="mt-2 list-disc space-y-1 pl-5 text-etos-warning-fg">
                {readiness.blockingReasons.map((reason) => (
                  <li key={reason}>{reason}</li>
                ))}
              </ul>
            </div>
          ) : (
            <p>No blocking readiness reasons.</p>
          )}
          <pre className="overflow-x-auto rounded-xl border border-etos-border bg-etos-panel p-3 text-xs">
            {JSON.stringify({ detail, readiness }, null, 2)}
          </pre>
          <Link href="/tools" className="text-etos-accent hover:underline">
            Back to registry
          </Link>
        </div>
      </details>
    </main>
  );
}

function Field({ label, value }: { label: string; value: string }) {
  return (
    <div>
      <p className="mb-1.5 text-xs font-extrabold uppercase tracking-[0.06em] text-etos-ink-muted">
        {label}
      </p>
      <div className="rounded-xl border border-etos-border bg-etos-panel px-3 py-2.5 text-[13px] text-etos-ink">
        {value}
      </div>
    </div>
  );
}
