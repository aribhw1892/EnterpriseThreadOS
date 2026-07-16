import Link from "next/link";
import { ensureMappingAgentSeedAction } from "@/components/agents/agent-configure-actions";
import { AgentModelConfigPanel } from "@/components/agents/AgentModelConfigPanel";
import { Badge, StatusBadge } from "@/components/ui/Badge";
import { Button } from "@/components/ui/Button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/Card";
import { DataTable } from "@/components/ui/DataTable";
import { ErrorState } from "@/components/ui/ErrorState";
import { Notice } from "@/components/ui/Notice";
import { PageHeader } from "@/components/ui/PageHeader";
import { PillStack, SidePanel } from "@/components/ui/SidePanel";
import { getArtifactVersions, loadAgentVersionByKey } from "@/lib/etos-api";

export const dynamic = "force-dynamic";

type PageProps = {
  params: Promise<{ agentKey: string }>;
  searchParams: Promise<{ versionId?: string; error?: string }>;
};

type CompositionRow = {
  id: string;
  aspect: string;
  value: string;
  state: string;
};

export default async function AgentConfigurePage({ params, searchParams }: PageProps) {
  const { agentKey } = await params;
  const { versionId, error } = await searchParams;
  const decodedKey = decodeURIComponent(agentKey);
  const loaded = await loadAgentVersionByKey(decodedKey, versionId);

  if (!loaded.data) {
    return (
      <main className="px-6 py-8 lg:px-8">
        <PageHeader
          title={`Agent · ${decodedKey}`}
          description="Tenant agent was not found. Seed from the manufacturing reference package or create from a template."
        />
        <div className="mb-4">
          <ErrorState error={loaded.error ?? "Agent was not found."} />
        </div>
        <Card>
          <CardHeader>
            <CardTitle>Recover local mapping assistant</CardTitle>
          </CardHeader>
          <CardContent className="space-y-4 text-sm text-etos-ink-muted">
            <p>
              Tenant agent <code className="font-mono text-etos-accent">{decodedKey}</code> is seeded when the
              manufacturing reference package installs.
            </p>
            <div className="flex flex-wrap gap-3">
              <form action={ensureMappingAgentSeedAction}>
                <input type="hidden" name="agentKey" value={decodedKey} />
                <Button type="submit">Install / ensure reference package</Button>
              </form>
              <Link href="/agents/new">
                <Button type="button" variant="ghost">
                  Create agent
                </Button>
              </Link>
            </div>
            {error ? <Notice variant="danger">{error}</Notice> : null}
          </CardContent>
        </Card>
      </main>
    );
  }

  const { detail, readiness, artifactId, versionId: selectedVersionId } = loaded.data;
  const versions = await getArtifactVersions(artifactId);
  const isPublished = detail.artifactReadinessState.toLowerCase().includes("publish");
  const canExecute = isPublished && !detail.safeModeEnabled;

  const compositionRows: CompositionRow[] = [
    {
      id: "prompt",
      aspect: "Prompt template",
      value: detail.promptTemplate
        ? `${detail.promptTemplate.artifactName} · ${detail.promptTemplate.versionLabel}`
        : "—",
      state: detail.promptTemplate?.readinessState ?? "Unset",
    },
    {
      id: "model",
      aspect: "Primary model",
      value: `${detail.primaryModelProviderKey} / ${detail.primaryModelId}`,
      state: detail.artifactReadinessState,
    },
    {
      id: "retrieval",
      aspect: "Retrieval",
      value: detail.retrievalStrategy
        ? `${detail.retrievalStrategy.strategyKey} · ${detail.retrievalStrategy.versionLabel}`
        : "—",
      state: detail.retrievalStrategy?.isEnabled ? "Enabled" : "Unset",
    },
    {
      id: "tools",
      aspect: "Tools",
      value:
        detail.referencedTools.length > 0
          ? detail.referencedTools.map((t) => t.toolArtifactName).join(", ")
          : "None",
      state: `${detail.referencedTools.length} linked`,
    },
    {
      id: "schema",
      aspect: "Output schema",
      value: detail.outputSchema
        ? `${detail.outputSchema.artifactName} · ${detail.outputSchema.versionLabel}`
        : "—",
      state: detail.outputSchema?.readinessState ?? "Unset",
    },
    {
      id: "fallback",
      aspect: "Fallback models",
      value:
        detail.fallbackModels.length > 0
          ? detail.fallbackModels.map((m) => m.modelId).join(", ")
          : "None",
      state: `${detail.fallbackModels.length}`,
    },
    {
      id: "safe",
      aspect: "Safe mode",
      value: detail.safeModeEnabled ? "Enabled" : "Disabled",
      state: detail.previewModeDefault ? "Preview default" : "Execute default",
    },
    {
      id: "preview",
      aspect: "Preview mode default",
      value: detail.previewModeDefault ? "Yes" : "No",
      state: detail.preferredRuntimeAdapterKey,
    },
  ];

  return (
    <main className="px-6 py-8 lg:px-8">
      <PageHeader
        title={detail.displayName}
        description={`${detail.agentKey} · ${detail.versionLabel} · ${detail.artifactReadinessState}`}
        actions={
          <>
            <Link
              href={`/agents/${encodeURIComponent(detail.agentKey)}/test-run?versionId=${encodeURIComponent(selectedVersionId)}`}
            >
              <Button type="button">Run test fixture</Button>
            </Link>
            <Link href="/agents">
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

      {versions.data && versions.data.length > 1 ? (
        <div className="mb-4 flex flex-wrap gap-2">
          {versions.data.map((version) => {
            const selected = version.id === selectedVersionId;
            return (
              <Link
                key={version.id}
                href={`/agents/${encodeURIComponent(detail.agentKey)}/configure?versionId=${encodeURIComponent(version.id)}`}
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
        <div className="space-y-4 lg:col-span-2">
          <Card>
            <CardHeader>
              <CardTitle>Composition</CardTitle>
            </CardHeader>
            <CardContent>
              <DataTable<CompositionRow>
                rows={compositionRows}
                rowKey={(row) => row.id}
                emptyMessage="No composition rows."
                columns={[
                  {
                    key: "aspect",
                    header: "Aspect",
                    render: (row) => <span className="font-semibold text-etos-ink">{row.aspect}</span>,
                  },
                  {
                    key: "value",
                    header: "Value",
                    render: (row) => (
                      <span className="text-sm text-etos-ink-muted">{row.value}</span>
                    ),
                  },
                  {
                    key: "state",
                    header: "State",
                    render: (row) => <StatusBadge status={row.state} />,
                  },
                ]}
              />
            </CardContent>
          </Card>

          <AgentModelConfigPanel
            artifactId={artifactId}
            versionId={selectedVersionId}
            agentKey={detail.agentKey}
            detail={detail}
            errorMessage={error ?? null}
          />
        </div>

        <div className="space-y-4">
          <SidePanel title="Publish rail">
            <PillStack
              items={[
                {
                  label: "Readiness",
                  value: detail.artifactReadinessState,
                  variant: isPublished ? "success" : "warning",
                },
                {
                  label: "Safe mode",
                  value: detail.safeModeEnabled ? "On" : "Off",
                  variant: detail.safeModeEnabled ? "warning" : "info",
                },
                {
                  label: "Risk",
                  value: detail.derivedCapabilityRisk?.effectiveRiskLevel ?? "Pending",
                  variant: "purple",
                },
                {
                  label: "Execute",
                  value: canExecute ? "Allowed" : "Gated",
                  variant: canExecute ? "success" : "neutral",
                },
              ]}
            />
            {readiness.blockingReasons.length > 0 ? (
              <ul className="mt-4 space-y-2 text-xs text-etos-warning-fg">
                {readiness.blockingReasons.map((reason) => (
                  <li key={reason} className="rounded-xl border border-etos-border bg-etos-panel-muted px-2 py-1.5">
                    {reason}
                  </li>
                ))}
              </ul>
            ) : (
              <p className="mt-4 text-xs text-etos-ink-muted">No blocking readiness notes.</p>
            )}
            <p className="mt-4 text-xs text-etos-ink-muted">
              Mark ready / Publish live in Model routing below. Execute stays gated until published and safe mode is
              off.
            </p>
          </SidePanel>

          {detail.derivedCapabilityRisk ? (
            <SidePanel title="Risk profile">
              <PillStack
                items={[
                  {
                    label: "Effective",
                    value: detail.derivedCapabilityRisk.effectiveRiskLevel,
                    variant: "warning",
                  },
                  {
                    label: "Permission",
                    value: detail.derivedCapabilityRisk.permissionCeiling,
                    variant: "info",
                  },
                  {
                    label: "Semantic fallback",
                    value: detail.derivedCapabilityRisk.retrievalRisk.allowsSemanticFallback
                      ? "Allowed"
                      : "Blocked",
                  },
                  {
                    label: "Vector fallback",
                    value: detail.derivedCapabilityRisk.retrievalRisk.allowsVectorFallback
                      ? "Allowed"
                      : "Blocked",
                  },
                ]}
              />
            </SidePanel>
          ) : null}
        </div>
      </div>

      <details className="mt-6 rounded-etos-card border border-etos-border bg-etos-panel-muted p-4 text-sm text-etos-ink-muted">
        <summary className="cursor-pointer font-extrabold text-etos-ink">Advanced / Debug</summary>
        <div className="mt-4 space-y-4">
          <form action={ensureMappingAgentSeedAction} className="flex flex-wrap gap-3">
            <input type="hidden" name="agentKey" value={detail.agentKey} />
            <Button type="submit" variant="ghost">
              Re-seed mapping agent package
            </Button>
            <Link href="/agent-templates" className="text-etos-accent hover:underline self-center">
              Templates
            </Link>
          </form>
          {detail.referencedTools.length > 0 ? (
            <div className="flex flex-wrap gap-2">
              {detail.referencedTools.map((tool) => (
                <Badge key={tool.toolDefinitionVersionId} variant="info">
                  {tool.toolArtifactName}
                </Badge>
              ))}
            </div>
          ) : null}
          <pre className="overflow-x-auto rounded-xl border border-etos-border bg-etos-panel p-3 text-xs">
            {JSON.stringify({ detail, readiness }, null, 2)}
          </pre>
        </div>
      </details>
    </main>
  );
}
