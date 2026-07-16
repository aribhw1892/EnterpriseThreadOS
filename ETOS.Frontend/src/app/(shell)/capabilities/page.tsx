import Link from "next/link";
import { DefinitionLibraryPage } from "@/components/model/DefinitionLibraryPage";
import { Button } from "@/components/ui/Button";
import { getCapabilityDefinitionArtifacts } from "@/lib/etos-api";

export const dynamic = "force-dynamic";

export default async function CapabilitiesPage() {
  const artifacts = await getCapabilityDefinitionArtifacts();
  const rows = (artifacts.data ?? []).map((artifact) => ({
    id: artifact.id,
    name: artifact.capabilityKey ?? artifact.name,
    secondary: artifact.name,
    versionLabel: artifact.latestVersionLabel,
    readinessState: artifact.readinessState,
    dependencyHint: "etos-manufacturing-reference",
  }));

  return (
    <DefinitionLibraryPage
      title="Capability definitions"
      description="Business outcomes are versioned separately from ontology and agent runtime capability profiles."
      hrefBase="/capabilities"
      rows={rows}
      error={artifacts.error}
      emptyMessage="No capability definitions yet."
      primaryActionLabel="New capability"
      registryTitle="Capability registry"
      secondaryAction={
        <Link href="/business-policies">
          <Button variant="ghost">Publish selected</Button>
        </Link>
      }
      columnLabels={{
        name: "Capability",
        secondary: "Outcome",
        deps: "Compatible package",
      }}
      previewTitle="Definition preview"
      previewPills={[
        { label: "Depends on", value: "Published ontology", variant: "info" },
        { label: "Allowed outputs", value: "Recommendation / task", variant: "purple" },
        { label: "Immutable after publish", value: "Yes", variant: "warning" },
      ]}
    />
  );
}
