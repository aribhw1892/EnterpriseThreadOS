import {
  ApiResult,
  ModelPackageVersion,
  createCanonicalModelSeed,
  getOntologyLists,
} from "@/lib/etos-api";
import { Button } from "@/components/ui/Button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/Card";
import { DataTable } from "@/components/ui/DataTable";
import { EmptyState } from "@/components/ui/EmptyState";
import { ErrorState } from "@/components/ui/ErrorState";
import { PageHeader } from "@/components/ui/PageHeader";
import { StatusBadge } from "@/components/ui/Badge";
import { Callout } from "@/components/ui/Notice";
import { PillStack, SidePanel } from "@/components/ui/SidePanel";
import Link from "next/link";
import { revalidatePath } from "next/cache";
import type { ReactNode } from "react";

export const dynamic = "force-dynamic";

async function publishSeedModelPackage() {
  "use server";

  await createCanonicalModelSeed();
  revalidatePath("/model-artifacts");
  revalidatePath("/model-artifacts/ontology");
}

type PackageRow = {
  id: string;
  artifact: string;
  version: string;
  readiness: string;
  health: string;
};

function ListSection<T>({
  title,
  description,
  result,
  emptyMessage,
  renderItem,
}: {
  title: string;
  description: string;
  result: ApiResult<T[]>;
  emptyMessage: string;
  renderItem: (item: T) => ReactNode;
}) {
  return (
    <Card>
      <CardHeader>
        <CardTitle>{title}</CardTitle>
        <p className="text-sm text-etos-ink-muted">{description}</p>
      </CardHeader>
      <CardContent>
        {result.error ? (
          <ErrorState error={result.error} />
        ) : result.data && result.data.length > 0 ? (
          <div className="grid gap-3">{result.data.map(renderItem)}</div>
        ) : (
          <EmptyState message={emptyMessage} />
        )}
      </CardContent>
    </Card>
  );
}

function VersionCard({
  id,
  title,
  subtitle,
  status,
  summary,
  createdAt,
  footer,
}: {
  id: string;
  title: string;
  subtitle: string;
  status: string;
  summary?: string | null;
  createdAt: string;
  footer?: ReactNode;
}) {
  return (
    <article key={id} className="rounded-etos-card border border-etos-border-soft bg-etos-panel p-4">
      <div className="flex items-center justify-between gap-3">
        <div>
          <h3 className="font-semibold text-etos-ink">{title}</h3>
          <p className="mt-1 text-sm text-etos-ink-muted">{subtitle}</p>
        </div>
        <StatusBadge status={status} />
      </div>
      <div className="mt-3 grid gap-1 text-xs text-etos-ink-subtle">
        <p>{summary ?? "No summary."}</p>
        <p>{new Date(createdAt).toLocaleString()}</p>
        {footer}
      </div>
    </article>
  );
}

export default async function ModelArtifactsPage() {
  const lists = await getOntologyLists();
  const activePackage = lists.activeModelPackage.data;
  const ontology = lists.ontologyVersions.data?.[0];
  const semantic = lists.semanticLayers.data?.[0];

  const packageRows: PackageRow[] = activePackage
    ? [
        {
          id: "ontology",
          artifact: "OntologyVersion",
          version: activePackage.ontologyVersionLabel ?? "—",
          readiness: ontology?.state ?? "Published",
          health: ontology
            ? `${ontology.objectTypeCount} object types · ${ontology.relationshipTypeCount} relationships`
            : "Bound in package",
        },
        {
          id: "semantic",
          artifact: "SemanticLayerVersion",
          version: activePackage.semanticLayerVersionLabel ?? "—",
          readiness: semantic?.state ?? "Published",
          health: "AI descriptions complete",
        },
        {
          id: "package",
          artifact: "ModelPackageVersion",
          version: activePackage.versionLabel,
          readiness: activePackage.state,
          health: "Bound to new import batches",
        },
        {
          id: "lifecycle",
          artifact: "LifecycleVocabularyVersion",
          version: activePackage.lifecycleVocabularyVersionLabel ?? "—",
          readiness: "Seeded",
          health: "Approval-aware transitions",
        },
        {
          id: "attributes",
          artifact: "AttributeSchemaVersion",
          version: activePackage.attributeSchemaVersionLabel ?? "—",
          readiness: "Seeded",
          health: "Search + AI metadata",
        },
      ]
    : [];

  return (
    <main className="px-6 py-8 lg:px-8">
      <PageHeader
        title="Model package & reference seed"
        description="Publish and inspect the active manufacturing reference package extracted from platform core assumptions."
        actions={
          <>
            <form action={publishSeedModelPackage}>
              <Button type="submit" variant="primary">
                Create seed model package
              </Button>
            </form>
            <Link href="/model-artifacts/ontology">
              <Button variant="ghost">View dependencies</Button>
            </Link>
          </>
        }
      />

      <div className="grid gap-4 lg:grid-cols-3">
        <Card className="lg:col-span-2">
          <CardHeader>
            <CardTitle>Active package</CardTitle>
          </CardHeader>
          <CardContent>
            {lists.activeModelPackage.error ? (
              <ErrorState error={lists.activeModelPackage.error} />
            ) : activePackage ? (
              <>
                <Callout title={activePackage.key} variant="info">
                  contains ontology, semantic layer, import/query profiles, capability
                  seeds, business policies, optimization objectives, agent templates, and
                  demo fixtures.
                </Callout>
                <div className="my-4 h-px bg-etos-border" />
                <DataTable<PackageRow>
                  rows={packageRows}
                  rowKey={(row) => row.id}
                  columns={[
                    {
                      key: "artifact",
                      header: "Artifact",
                      render: (row) => row.artifact,
                    },
                    {
                      key: "version",
                      header: "Version",
                      render: (row) => (
                        <span className="text-etos-ink-muted">{row.version}</span>
                      ),
                    },
                    {
                      key: "readiness",
                      header: "Readiness",
                      render: (row) => <StatusBadge status={row.readiness} />,
                    },
                    {
                      key: "health",
                      header: "Dependency health",
                      render: (row) => (
                        <span className="text-xs text-etos-ink-subtle">{row.health}</span>
                      ),
                    },
                  ]}
                />
              </>
            ) : (
              <EmptyState message="No published model package is active yet. Create a seed package." />
            )}
          </CardContent>
        </Card>

        <SidePanel title="Package boundaries">
          <PillStack
            items={[
              { label: "Platform core", value: "Generic", variant: "neutral" },
              { label: "Ontology brain", value: "Domain meaning", variant: "info" },
              { label: "Capabilities", value: "Outcomes", variant: "purple" },
              { label: "Policies", value: "Constraints", variant: "warning" },
              { label: "Agent templates", value: "Patterns", variant: "teal" },
            ]}
          />
          <div className="mt-4 rounded-xl border-l-4 border-etos-info-border bg-etos-info-bg/30 p-3 text-xs text-etos-ink">
            New imports bind to the active published package at batch creation time.
          </div>
          <div className="mt-4 flex flex-wrap gap-2">
            <Link href="/capabilities">
              <Button variant="ghost">Capabilities</Button>
            </Link>
            <Link href="/business-policies">
              <Button variant="ghost">Policies</Button>
            </Link>
          </div>
        </SidePanel>
      </div>

      <details className="mt-6 rounded-etos-card border border-etos-border bg-etos-panel-muted p-4">
        <summary className="cursor-pointer text-sm font-extrabold text-etos-ink">
          Advanced / Debug — version lists
        </summary>
        <div className="mt-4 grid gap-6 xl:grid-cols-2">
          <ListSection
            title="Ontology versions"
            description="Canonical object types, semantic relationships, and BOM relationship metadata."
            result={lists.ontologyVersions}
            emptyMessage="No ontology versions have been created."
            renderItem={(version) => (
              <VersionCard
                key={version.id}
                id={version.id}
                title={`${version.key} ${version.versionLabel}`}
                subtitle={`${version.objectTypeCount} object types, ${version.relationshipTypeCount} relationships`}
                status={version.state}
                summary={version.summary}
                createdAt={version.createdAt}
              />
            )}
          />
          <ListSection
            title="Semantic layers"
            description="Graph memory mapping metadata for canonical object and relationship names."
            result={lists.semanticLayers}
            emptyMessage="No semantic layer versions have been created."
            renderItem={(version) => (
              <VersionCard
                key={version.id}
                id={version.id}
                title={`${version.key} ${version.versionLabel}`}
                subtitle={`Ontology ${version.ontologyVersionLabel ?? version.ontologyVersionId}`}
                status={version.state}
                summary={version.summary}
                createdAt={version.createdAt}
              />
            )}
          />
          <ListSection
            title="Lifecycle vocabularies"
            description="Normalized lifecycle states and approval-aware transitions."
            result={lists.lifecycleVocabularies}
            emptyMessage="No lifecycle vocabulary versions have been created."
            renderItem={(version) => (
              <VersionCard
                key={version.id}
                id={version.id}
                title={`${version.key} ${version.versionLabel}`}
                subtitle={`${version.stateCount} states, ${version.transitionCount} transitions`}
                status={version.state}
                summary={version.summary}
                createdAt={version.createdAt}
              />
            )}
          />
          <ListSection
            title="Attribute schemas"
            description="Tenant-safe attribute definitions with validation, permissions, search, and AI metadata."
            result={lists.attributeSchemas}
            emptyMessage="No attribute schema versions have been created."
            renderItem={(version) => (
              <VersionCard
                key={version.id}
                id={version.id}
                title={`${version.key} ${version.versionLabel}`}
                subtitle={`${version.attributeCount} attributes`}
                status={version.state}
                summary={version.summary}
                createdAt={version.createdAt}
              />
            )}
          />
        </div>
        <div className="mt-6">
          <ListSection
            title="Model packages"
            description="Published packages bind ontology, semantic layer, lifecycle, and attributes."
            result={lists.modelPackages}
            emptyMessage="No model package versions have been created."
            renderItem={(version: ModelPackageVersion) => (
              <VersionCard
                key={version.id}
                id={version.id}
                title={`${version.name} ${version.versionLabel}`}
                subtitle={version.key}
                status={version.state}
                summary={version.summary}
                createdAt={version.createdAt}
                footer={
                  <>
                    <p>Ontology: {version.ontologyVersionLabel ?? version.ontologyVersionId}</p>
                    <p>
                      Semantic:{" "}
                      {version.semanticLayerVersionLabel ?? version.semanticLayerVersionId}
                    </p>
                  </>
                }
              />
            )}
          />
        </div>
      </details>
    </main>
  );
}
