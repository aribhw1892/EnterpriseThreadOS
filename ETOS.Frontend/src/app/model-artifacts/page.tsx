import {
  ApiResult,
  AttributeSchemaVersion,
  LifecycleVocabularyVersion,
  ModelPackageVersion,
  OntologyVersion,
  SemanticLayerVersion,
  adminUserId,
  createCanonicalModelSeed,
  getOntologyLists,
  selectedTenantId,
} from "@/lib/etos-api";
import { revalidatePath } from "next/cache";
import type { ReactNode } from "react";

export const dynamic = "force-dynamic";

async function publishSeedModelPackage() {
  "use server";

  await createCanonicalModelSeed();
  revalidatePath("/model-artifacts");
}

function StatusBadge({ status }: { status: string }) {
  const normalized = status.toLowerCase();
  const className =
    normalized === "published"
      ? "bg-emerald-100 text-emerald-800 dark:bg-emerald-950 dark:text-emerald-200"
      : normalized === "draft"
        ? "bg-cyan-100 text-cyan-800 dark:bg-cyan-950 dark:text-cyan-200"
        : "bg-slate-100 text-slate-800 dark:bg-slate-800 dark:text-slate-200";

  return (
    <span className={`rounded-full px-3 py-1 text-xs font-semibold uppercase tracking-wide ${className}`}>
      {status}
    </span>
  );
}

function EmptyState({ message }: { message: string }) {
  return (
    <div className="rounded-2xl border border-slate-800 bg-slate-950 p-4 text-sm text-slate-400">
      {message}
    </div>
  );
}

function ErrorState({ error }: { error: string }) {
  return (
    <div className="rounded-2xl border border-amber-500/30 bg-amber-500/10 p-4 text-sm text-amber-100">
      {error}
    </div>
  );
}

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
    <section className="rounded-3xl border border-slate-800 bg-slate-900 p-6">
      <div className="mb-5">
        <h2 className="text-2xl font-semibold">{title}</h2>
        <p className="mt-1 text-sm text-slate-400">{description}</p>
      </div>

      {result.error ? (
        <ErrorState error={result.error} />
      ) : result.data && result.data.length > 0 ? (
        <div className="grid gap-3">{result.data.map(renderItem)}</div>
      ) : (
        <EmptyState message={emptyMessage} />
      )}
    </section>
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
    <article key={id} className="rounded-2xl border border-slate-800 bg-slate-950 p-4">
      <div className="flex items-center justify-between gap-3">
        <div>
          <h3 className="font-semibold">{title}</h3>
          <p className="mt-1 text-sm text-slate-400">{subtitle}</p>
        </div>
        <StatusBadge status={status} />
      </div>
      <div className="mt-3 grid gap-1 text-xs text-slate-500">
        <p>{summary ?? "No summary."}</p>
        <p>{new Date(createdAt).toLocaleString()}</p>
        {footer}
      </div>
    </article>
  );
}

function OntologyCard(version: OntologyVersion) {
  return (
    <VersionCard
      key={version.id}
      id={version.id}
      title={`${version.key} ${version.versionLabel}`}
      subtitle={`${version.objectTypeCount} object types, ${version.relationshipTypeCount} relationships, ${version.bomRelationshipCount} BOM definitions`}
      status={version.state}
      summary={version.summary}
      createdAt={version.createdAt}
    />
  );
}

function SemanticLayerCard(version: SemanticLayerVersion) {
  return (
    <VersionCard
      key={version.id}
      id={version.id}
      title={`${version.key} ${version.versionLabel}`}
      subtitle={`Ontology ${version.ontologyVersionLabel ?? version.ontologyVersionId}`}
      status={version.state}
      summary={version.summary}
      createdAt={version.createdAt}
    />
  );
}

function LifecycleCard(version: LifecycleVocabularyVersion) {
  return (
    <VersionCard
      key={version.id}
      id={version.id}
      title={`${version.key} ${version.versionLabel}`}
      subtitle={`${version.stateCount} states, ${version.transitionCount} transitions`}
      status={version.state}
      summary={version.summary}
      createdAt={version.createdAt}
    />
  );
}

function AttributeSchemaCard(version: AttributeSchemaVersion) {
  return (
    <VersionCard
      key={version.id}
      id={version.id}
      title={`${version.key} ${version.versionLabel}`}
      subtitle={`${version.attributeCount} attributes for ontology ${version.ontologyVersionLabel ?? version.ontologyVersionId}`}
      status={version.state}
      summary={version.summary}
      createdAt={version.createdAt}
    />
  );
}

function ModelPackageCard(version: ModelPackageVersion) {
  return (
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
          <p>Semantic layer: {version.semanticLayerVersionLabel ?? version.semanticLayerVersionId}</p>
          <p>Lifecycle: {version.lifecycleVocabularyVersionLabel ?? version.lifecycleVocabularyVersionId}</p>
          <p>Attributes: {version.attributeSchemaVersionLabel ?? version.attributeSchemaVersionId}</p>
        </>
      }
    />
  );
}

export default async function ModelArtifactsPage() {
  const lists = await getOntologyLists();
  const activePackage = lists.activeModelPackage.data;

  return (
    <main className="min-h-screen bg-slate-950 px-6 py-10 text-slate-100">
      <div className="mx-auto grid max-w-7xl gap-8">
        <header className="rounded-3xl border border-slate-800 bg-slate-900 p-8">
          <p className="text-sm font-semibold uppercase tracking-[0.3em] text-cyan-300">
            EnterpriseThreadOS
          </p>
          <div className="mt-4 flex flex-col gap-6 lg:flex-row lg:items-end lg:justify-between">
            <div>
              <h1 className="text-4xl font-bold tracking-tight">Canonical Model Artifacts</h1>
              <p className="mt-3 max-w-3xl text-slate-300">
                Draft, preview, publish, and inspect tenant ontology versions, semantic graph mappings,
                lifecycle vocabularies, attribute schemas, and model packages.
              </p>
            </div>
            <form action={publishSeedModelPackage}>
              <button
                type="submit"
                className="rounded-2xl bg-cyan-300 px-5 py-3 text-sm font-semibold text-slate-950 transition hover:bg-cyan-200"
              >
                Create seed model package
              </button>
            </form>
          </div>
          <div className="mt-5 grid gap-2 text-xs text-slate-500 md:grid-cols-2">
            <p>Admin user: {adminUserId}</p>
            <p>Tenant: {selectedTenantId}</p>
          </div>
        </header>

        <section className="rounded-3xl border border-cyan-400/30 bg-cyan-400/10 p-6">
          <h2 className="text-2xl font-semibold">Active Published Package</h2>
          {lists.activeModelPackage.error ? (
            <div className="mt-4">
              <ErrorState error={lists.activeModelPackage.error} />
            </div>
          ) : activePackage ? (
            <div className="mt-4">
              {ModelPackageCard(activePackage)}
            </div>
          ) : (
            <div className="mt-4">
              <EmptyState message="No published model package is active yet." />
            </div>
          )}
        </section>

        <div className="grid gap-6 xl:grid-cols-2">
          <ListSection
            title="Ontology Versions"
            description="Canonical object types, semantic relationships, and BOM relationship metadata."
            result={lists.ontologyVersions}
            emptyMessage="No ontology versions have been created."
            renderItem={OntologyCard}
          />

          <ListSection
            title="Semantic Layers"
            description="Graph memory mapping metadata for canonical object and relationship names."
            result={lists.semanticLayers}
            emptyMessage="No semantic layer versions have been created."
            renderItem={SemanticLayerCard}
          />

          <ListSection
            title="Lifecycle Vocabularies"
            description="Normalized lifecycle states and approval-aware transitions."
            result={lists.lifecycleVocabularies}
            emptyMessage="No lifecycle vocabulary versions have been created."
            renderItem={LifecycleCard}
          />

          <ListSection
            title="Attribute Schemas"
            description="Tenant-safe attribute definitions with validation, permissions, search, and AI metadata."
            result={lists.attributeSchemas}
            emptyMessage="No attribute schema versions have been created."
            renderItem={AttributeSchemaCard}
          />
        </div>

        <ListSection
          title="Model Packages"
          description="Published packages bind ontology, semantic layer, lifecycle, and attributes for later imports and graph records."
          result={lists.modelPackages}
          emptyMessage="No model package versions have been created."
          renderItem={ModelPackageCard}
        />
      </div>
    </main>
  );
}
