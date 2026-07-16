import Link from "next/link";
import { Badge } from "@/components/ui/Badge";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/Card";
import { DataTable } from "@/components/ui/DataTable";
import { ErrorState } from "@/components/ui/ErrorState";
import { Notice } from "@/components/ui/Notice";
import { PageHeader } from "@/components/ui/PageHeader";
import { TraceTimeline } from "@/components/ui/TraceTimeline";
import {
  ApiResult,
  ConnectorDefinitionDetail,
  getArtifactVersions,
  getConnectorDefinitionArtifacts,
  getConnectorDefinitionDetail,
} from "@/lib/etos-api";

export const dynamic = "force-dynamic";

type PageProps = {
  params: Promise<{ artifactId: string }>;
  searchParams: Promise<{ versionId?: string }>;
};

type CapabilityRow = {
  id: string;
  capability: string;
  mode: string;
  credentialBehavior: string;
  status: string;
};

async function loadConnectorDefinitionDetail(
  artifactId: string,
  versionId?: string,
): Promise<
  ApiResult<{
    versionId: string;
    artifactName: string;
    detail: ConnectorDefinitionDetail;
  }>
> {
  const list = await getConnectorDefinitionArtifacts();
  if (!list.data) {
    return { data: null, error: list.error };
  }

  const artifact = list.data.find((item) => item.id === artifactId);
  if (!artifact) {
    return { data: null, error: "Connector definition artifact was not found." };
  }

  const versions = await getArtifactVersions(artifactId);
  if (!versions.data || versions.data.length === 0) {
    return { data: null, error: versions.error ?? "No versions found." };
  }

  const selectedVersionId = versionId ?? versions.data[0].id;
  const detail = await getConnectorDefinitionDetail(artifactId, selectedVersionId);
  if (!detail.data) {
    return { data: null, error: detail.error };
  }

  return {
    data: {
      versionId: selectedVersionId,
      artifactName: artifact.name,
      detail: detail.data,
    },
    error: null,
  };
}

function buildCapabilityRows(detail: ConnectorDefinitionDetail): CapabilityRow[] {
  const operations =
    detail.supportedOperations.length > 0
      ? detail.supportedOperations
      : ["Declared connector surface"];

  return operations.map((operation, index) => {
    const writeLike =
      detail.writesExternalSystem ||
      /create|update|write|delete|mutate/i.test(operation);
    const enabled = detail.executionEnabled && !writeLike;

    return {
      id: `${operation}-${index}`,
      capability: operation,
      mode: writeLike
        ? "Write connector"
        : detail.callsExternalSystem
          ? "Read connector"
          : "Internal",
      credentialBehavior: writeLike
        ? "Disabled contract only"
        : detail.callsExternalSystem
          ? "Short-lived scoped token"
          : "No source mutation",
      status: writeLike
        ? "Disabled"
        : enabled
          ? "Enabled"
          : detail.disabledReason ?? "Disabled",
    };
  });
}

export default async function ConnectorDefinitionDetailPage({
  params,
  searchParams,
}: PageProps) {
  const { artifactId } = await params;
  const { versionId } = await searchParams;
  const loaded = await loadConnectorDefinitionDetail(artifactId, versionId);

  if (!loaded.data) {
    return (
      <main className="px-6 py-8 lg:px-8">
        <ErrorState error={loaded.error ?? "Connector definition was not found."} />
        <p className="mt-4 text-sm">
          <Link href="/tools" className="text-etos-accent hover:underline">
            Back to registry
          </Link>
        </p>
      </main>
    );
  }

  const { detail } = loaded.data;
  const rows = buildCapabilityRows(detail);

  return (
    <main className="px-6 py-8 lg:px-8">
      <PageHeader
        title="Connector detail & credential boundary"
        description={`${loaded.data.artifactName} — read-only connector design with tenant-aware scoped credential issuance and disabled write-capable contracts.`}
      />

      {!detail.executionEnabled || detail.writesExternalSystem ? (
        <div className="mb-4">
          <Notice variant="warning">
            {detail.disabledReason ??
              (detail.writesExternalSystem
                ? "Write-capable connector contracts stay disabled in MVP."
                : "Connector execution is currently disabled.")}
          </Notice>
        </div>
      ) : null}

      <div className="grid gap-4 lg:grid-cols-3">
        <Card className="lg:col-span-2">
          <CardHeader>
            <CardTitle>Connector definition</CardTitle>
          </CardHeader>
          <CardContent>
            <p className="mb-4 text-sm text-etos-ink-muted">
              {detail.connectorKey} · {detail.connectorKind} · {detail.versionLabel} ·{" "}
              {detail.artifactReadinessState}
            </p>
            <DataTable<CapabilityRow>
              rows={rows}
              rowKey={(row) => row.id}
              emptyMessage="No supported operations declared."
              columns={[
                {
                  key: "capability",
                  header: "Capability",
                  render: (row) => row.capability,
                },
                {
                  key: "mode",
                  header: "Mode",
                  render: (row) => (
                    <span className="text-etos-ink-muted">{row.mode}</span>
                  ),
                },
                {
                  key: "credential",
                  header: "Credential behavior",
                  render: (row) => (
                    <span className="text-etos-ink-muted">{row.credentialBehavior}</span>
                  ),
                },
                {
                  key: "status",
                  header: "Status",
                  render: (row) => (
                    <Badge
                      variant={
                        row.status.toLowerCase().includes("enable") ? "success" : "danger"
                      }
                    >
                      {row.status}
                    </Badge>
                  ),
                },
              ]}
            />
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>Credential issuance</CardTitle>
          </CardHeader>
          <CardContent>
            <TraceTimeline
              steps={[
                {
                  id: "gateway",
                  title: "Tool Gateway",
                  description: "Policy, tenant, tool, connector checks",
                  status: "Gate",
                  meta: `Scope: ${detail.credentialScopeKey}`,
                },
                {
                  id: "secret",
                  title: "Secret Provider",
                  description: "Issues scoped token; never raw secret",
                  status: "Scoped",
                  meta: `Ref: ${detail.secretReferenceKey}`,
                },
                {
                  id: "connector",
                  title: "Connector",
                  description: detail.writesExternalSystem
                    ? "Write path remains contract-only"
                    : "Receives token for read-only call",
                  status: detail.writesExternalSystem ? "Write disabled" : "Read only",
                },
              ]}
            />
          </CardContent>
        </Card>
      </div>

      <Notice className="mt-4" variant="info">
        Secret values, API keys, passwords, and tokens are never stored in AgentRun,
        WorkflowRun, ToolRun, AI Trace, or audit payloads.
      </Notice>

      <details className="mt-6 rounded-etos-card border border-etos-border bg-etos-panel-muted p-4 text-sm text-etos-ink-muted">
        <summary className="cursor-pointer font-extrabold text-etos-ink">
          Advanced / Debug
        </summary>
        <div className="mt-4 space-y-3">
          <Link href="/tools" className="text-etos-accent hover:underline">
            Back to registry
          </Link>
          <pre className="overflow-x-auto rounded-xl border border-etos-border bg-etos-panel p-3 text-xs">
            {JSON.stringify(detail, null, 2)}
          </pre>
        </div>
      </details>
    </main>
  );
}
