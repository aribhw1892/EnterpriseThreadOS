import { DefinitionLibraryPage } from "@/components/model/DefinitionLibraryPage";
import { getAgentTemplateDefinitionArtifacts } from "@/lib/etos-api";

export const dynamic = "force-dynamic";

export default async function AgentTemplatesPage() {
  const artifacts = await getAgentTemplateDefinitionArtifacts();
  const rows = (artifacts.data ?? []).map((artifact) => ({
    id: artifact.id,
    name: artifact.templateKey ?? artifact.name,
    secondary: artifact.name,
    versionLabel: artifact.latestVersionLabel,
    readinessState: artifact.readinessState,
    dependencyHint: artifact.patternCategory ?? "→ capability / policy",
  }));

  return (
    <DefinitionLibraryPage
      title="Agent template library"
      description="Reusable agent pattern definitions composing ontology, capability, policy, prompt, and retrieval references."
      hrefBase="/agent-templates"
      rows={rows}
      error={artifacts.error}
      emptyMessage="No agent template definitions yet."
      primaryActionLabel="New template"
      registryTitle="Templates"
      showKpis={false}
      columnLabels={{
        name: "Template",
        secondary: "Pattern",
        deps: "Pinned dependencies",
      }}
      previewTitle="Template detail"
      previewPills={[
        {
          label: "Prompt template",
          value: rows[0]?.versionLabel ?? "Draft",
          variant: "info",
        },
        { label: "Output schema", value: "Recommendation-only", variant: "purple" },
        { label: "Allowed tools", value: "Governed gateway", variant: "teal" },
        { label: "Safe mode", value: "Partial", variant: "warning" },
      ]}
      sideExtra={
        <p className="mt-4 rounded-xl border-l-4 border-etos-info-border bg-etos-info-bg/30 p-3 text-xs text-etos-ink">
          Templates remain drafts until publish governance passes capability, policy, and
          package pins.
        </p>
      }
    />
  );
}
