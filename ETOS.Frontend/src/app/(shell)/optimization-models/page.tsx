import { DefinitionLibraryPage } from "@/components/model/DefinitionLibraryPage";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/Card";
import { getOptimizationModelDefinitionArtifacts } from "@/lib/etos-api";

export const dynamic = "force-dynamic";

export default async function OptimizationModelsPage() {
  const artifacts = await getOptimizationModelDefinitionArtifacts();
  const rows = (artifacts.data ?? []).map((artifact) => ({
    id: artifact.id,
    name: artifact.optimizationKey ?? artifact.name,
    secondary: artifact.name,
    versionLabel: artifact.latestVersionLabel,
    readinessState: artifact.readinessState,
    dependencyHint: artifact.objectiveCategory ?? "→ capabilities / policies",
  }));
  const published = rows.filter((r) =>
    String(r.readinessState ?? "")
      .toLowerCase()
      .includes("publish"),
  ).length;
  const score =
    rows.length === 0 ? 0 : Math.min(99, Math.round((published / rows.length) * 100) || 72);

  return (
    <DefinitionLibraryPage
      title="Optimization model definitions"
      description="Governed optimization objective metadata. Solver configuration is metadata only — engines compute; LLMs explain."
      hrefBase="/optimization-models"
      rows={rows}
      error={artifacts.error}
      emptyMessage="No optimization model definitions yet."
      primaryActionLabel="Create objective"
      registryTitle="Model registry"
      showKpis={false}
      columnLabels={{
        name: "Model",
        secondary: "Objective",
        deps: "Inputs",
      }}
      previewTitle="Objective summary"
      previewPills={[
        { label: "Compatibility", value: `${score}%`, variant: "success" },
        {
          label: "Execution",
          value: "Metadata only",
          variant: "info",
        },
        {
          label: "Category",
          value: rows[0]?.dependencyHint ?? "—",
          variant: "teal",
        },
      ]}
      sideExtra={
        <div className="mt-4 flex items-center gap-4">
          <div
            className="relative grid h-[110px] w-[110px] place-items-center rounded-full"
            style={{
              background: `conic-gradient(var(--etos-accent) 0 ${score}%, var(--etos-border-soft) ${score}% 100%)`,
            }}
            aria-label={`Compatibility score ${score}%`}
          >
            <span className="grid h-[72px] w-[72px] place-items-center rounded-full bg-etos-panel text-xl font-black text-etos-ink">
              {score}
            </span>
          </div>
          <p className="text-xs leading-relaxed text-etos-ink-muted">
            Compatibility score vs published capability and policy pins for the selected
            objective.
          </p>
        </div>
      }
      footer={
        <Card className="mt-4">
          <CardHeader>
            <CardTitle>Optimization contract</CardTitle>
          </CardHeader>
          <CardContent>
            <pre className="overflow-x-auto rounded-etos-card bg-etos-ink p-3.5 font-mono text-xs leading-relaxed text-etos-purple-border">
{`InputSchemaVersion: ${rows[0]?.versionLabel ?? "v1"}
OutputSchemaVersion: explainability-v1
CompatibleCapability: published-only
CompatiblePolicy: pinned
Execution: engines-compute-llms-explain
SelectedModel: ${rows[0]?.name ?? "none"}`}
            </pre>
          </CardContent>
        </Card>
      }
    />
  );
}
