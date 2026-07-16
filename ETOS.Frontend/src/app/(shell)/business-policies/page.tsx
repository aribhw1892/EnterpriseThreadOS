import { DefinitionLibraryPage } from "@/components/model/DefinitionLibraryPage";
import { getBusinessPolicyDefinitionArtifacts } from "@/lib/etos-api";

export const dynamic = "force-dynamic";

export default async function BusinessPoliciesPage() {
  const artifacts = await getBusinessPolicyDefinitionArtifacts();
  const rows = (artifacts.data ?? []).map((artifact) => ({
    id: artifact.id,
    name: artifact.policyKey ?? artifact.name,
    secondary: artifact.name,
    versionLabel: artifact.latestVersionLabel,
    readinessState: artifact.readinessState,
    dependencyHint: artifact.constraintCategory ?? "→ capabilities / packages",
  }));

  return (
    <DefinitionLibraryPage
      title="Business policy definitions"
      description="Governed business constraint policies pinned to published capabilities, ontology, and model packages."
      hrefBase="/business-policies"
      rows={rows}
      error={artifacts.error}
      emptyMessage="No business policy definitions yet."
      primaryActionLabel="New business policy"
      registryTitle="Policy definitions"
      columnLabels={{
        name: "Policy",
        secondary: "Constraint",
        deps: "Applies to",
      }}
      previewTitle="Policy composition"
      previewPills={[
        { label: "Depends on", value: "Published capability", variant: "info" },
        { label: "Applies to", value: rows[0]?.dependencyHint ?? "Package", variant: "warning" },
        { label: "Publish gate", value: "Human approval", variant: "purple" },
      ]}
      sideExtra={
        <div className="mt-4">
          <p className="mb-2 text-xs font-semibold uppercase tracking-wide text-etos-ink-muted">
            Composition flow
          </p>
          <div className="flex flex-wrap items-center gap-2 text-[11px] font-extrabold">
            <span className="rounded-xl border border-etos-border bg-etos-panel px-2.5 py-2">
              Capability
            </span>
            <span className="text-etos-ink-subtle">→</span>
            <span className="rounded-xl border border-etos-warning-border bg-etos-warning-bg px-2.5 py-2 text-etos-warning-fg">
              Business Policy
            </span>
            <span className="text-etos-ink-subtle">→</span>
            <span className="rounded-xl border border-etos-purple-border bg-etos-purple-bg px-2.5 py-2 text-etos-purple-fg">
              Agent Template
            </span>
          </div>
          <p className="mt-3 rounded-xl border-l-4 border-etos-info-border bg-etos-info-bg/30 p-3 text-xs text-etos-ink">
            Policies remain drafts until capability and package pins pass publish governance.
          </p>
        </div>
      }
    />
  );
}
