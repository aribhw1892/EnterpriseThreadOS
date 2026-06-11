import {
  ApiResult,
  CadParsingStatus,
  DataQualityIssue,
  DocumentArtifact,
  DocumentArtifactDetail,
  DocumentObjectLink,
  DocumentVectorIndexRecord,
  DocumentVersion,
  adminUserId,
  createDemoDocumentFlow,
  createExtractionIssueForLatestDocument,
  getDocumentLists,
  requestLatestDocumentVectorIndex,
  selectedTenantId,
} from "@/lib/etos-api";
import { revalidatePath } from "next/cache";
import type { ReactNode } from "react";

export const dynamic = "force-dynamic";

async function createDemoDocument() {
  "use server";

  await createDemoDocumentFlow();
  revalidatePath("/documents");
}

async function requestVectorIndex() {
  "use server";

  await requestLatestDocumentVectorIndex();
  revalidatePath("/documents");
}

async function createExtractionIssue() {
  "use server";

  await createExtractionIssueForLatestDocument();
  revalidatePath("/documents");
}

function StatusBadge({ status }: { status: string }) {
  const normalized = status.toLowerCase();
  const className =
    normalized === "completed" || normalized === "metadataimported" || normalized === "indexed"
      ? "bg-emerald-100 text-emerald-800 dark:bg-emerald-950 dark:text-emerald-200"
      : normalized === "failed"
        ? "bg-rose-100 text-rose-800 dark:bg-rose-950 dark:text-rose-200"
        : normalized === "uncertain" || normalized === "disabledplaceholder"
          ? "bg-amber-100 text-amber-800 dark:bg-amber-950 dark:text-amber-200"
          : "bg-cyan-100 text-cyan-800 dark:bg-cyan-950 dark:text-cyan-200";

  return (
    <span className={`rounded-full px-3 py-1 text-xs font-semibold uppercase tracking-wide ${className}`}>
      {status}
    </span>
  );
}

function ErrorState({ error }: { error: string }) {
  return (
    <div className="rounded-2xl border border-amber-500/30 bg-amber-500/10 p-4 text-sm text-amber-100">
      {error}
    </div>
  );
}

function EmptyState({ message }: { message: string }) {
  return (
    <div className="rounded-2xl border border-slate-800 bg-slate-950 p-4 text-sm text-slate-400">
      {message}
    </div>
  );
}

function ActionButton({ action, children }: { action: () => Promise<void>; children: ReactNode }) {
  return (
    <form action={action}>
      <button
        type="submit"
        className="rounded-2xl bg-cyan-300 px-5 py-3 text-sm font-semibold text-slate-950 transition hover:bg-cyan-200"
      >
        {children}
      </button>
    </form>
  );
}

function ListSection<T>({
  title,
  description,
  items,
  emptyMessage,
  renderItem,
}: {
  title: string;
  description: string;
  items: T[];
  emptyMessage: string;
  renderItem: (item: T) => ReactNode;
}) {
  return (
    <section className="rounded-3xl border border-slate-800 bg-slate-900 p-6">
      <div className="mb-5">
        <h2 className="text-2xl font-semibold">{title}</h2>
        <p className="mt-1 text-sm text-slate-400">{description}</p>
      </div>
      {items.length > 0 ? <div className="grid gap-3">{items.map(renderItem)}</div> : <EmptyState message={emptyMessage} />}
    </section>
  );
}

function DocumentCard(document: DocumentArtifact) {
  return (
    <article key={document.id} className="rounded-2xl border border-slate-800 bg-slate-950 p-4">
      <div className="flex items-start justify-between gap-3">
        <div>
          <h3 className="font-semibold">{document.title}</h3>
          <p className="mt-1 text-sm text-slate-400">{document.description ?? "No description."}</p>
        </div>
        <StatusBadge status={document.classificationKey} />
      </div>
      <div className="mt-3 grid gap-1 text-xs text-slate-500 md:grid-cols-2">
        <p>Type: {document.documentType}</p>
        <p>Links: {document.linkCount}</p>
        <p>Artifact: {document.artifactId}</p>
        <p>Latest: {document.latestVersion?.versionLabel ?? "none"}</p>
      </div>
    </article>
  );
}

function VersionCard(version: DocumentVersion) {
  return (
    <article key={version.id} className="rounded-2xl border border-slate-800 bg-slate-950 p-4">
      <div className="flex items-center justify-between gap-3">
        <h3 className="font-semibold">{version.versionLabel}</h3>
        <StatusBadge status={version.extractionStatus} />
      </div>
      <div className="mt-3 grid gap-1 text-xs text-slate-500">
        <p>File: {version.originalFileName}</p>
        <p>Content type: {version.contentType}</p>
        <p>Size: {version.sizeBytes} bytes</p>
        <p>Checksum: {version.sha256Checksum}</p>
        <p>Failure: {version.extractionFailureSummary ?? "none"}</p>
      </div>
    </article>
  );
}

function LinkCard(link: DocumentObjectLink) {
  return (
    <article key={link.id} className="rounded-2xl border border-slate-800 bg-slate-950 p-4">
      <div className="flex items-center justify-between gap-3">
        <h3 className="font-semibold">Object link</h3>
        <StatusBadge status={link.extractionStatus} />
      </div>
      <div className="mt-3 grid gap-1 text-xs text-slate-500">
        <p>Confidence: {(link.confidenceScore * 100).toFixed(1)}%</p>
        <p>Graph node: {link.graphNodeId ?? "n/a"}</p>
        <p>Import batch: {link.importBatchId ?? "n/a"}</p>
        <p>Source: {link.sourceSystem ?? "n/a"} {link.sourceRecordId ?? ""}</p>
        <p>{link.evidenceSummary}</p>
      </div>
    </article>
  );
}

function VectorCard(record: DocumentVectorIndexRecord) {
  return (
    <article key={record.id} className="rounded-2xl border border-slate-800 bg-slate-950 p-4">
      <div className="flex items-center justify-between gap-3">
        <h3 className="font-semibold">{record.providerName}</h3>
        <StatusBadge status={record.status} />
      </div>
      <div className="mt-3 grid gap-1 text-xs text-slate-500">
        <p>Tenant filter: {record.tenantFilter}</p>
        <p>Policy: {record.policyFilterSummary}</p>
        <p>{record.safeSummary}</p>
        <p>Failure: {record.failureSummary ?? "none"}</p>
      </div>
    </article>
  );
}

function DataQualityIssueCard(issue: DataQualityIssue) {
  const sources = issue.sourceLinks
    .map((link) => `${link.sourceType}${link.label ? ` (${link.label})` : ""}`)
    .join(", ");

  return (
    <article key={issue.id} className="rounded-2xl border border-slate-800 bg-slate-950 p-4">
      <div className="flex items-center justify-between gap-3">
        <div>
          <h3 className="font-semibold">{issue.title}</h3>
          <p className="mt-1 text-sm text-slate-400">{issue.issueCode}</p>
        </div>
        <div className="flex flex-wrap justify-end gap-2">
          <StatusBadge status={issue.severity} />
          <StatusBadge status={issue.status} />
        </div>
      </div>
      <p className="mt-3 text-sm text-slate-300">{issue.evidenceSummary}</p>
      <div className="mt-3 grid gap-1 text-xs text-slate-500">
        <p>Priority: {issue.reviewPriority}</p>
        <p>Review hook: {issue.reviewTaskReady ? issue.reviewTaskHint ?? "ready" : "not ready"}</p>
        <p>Sources: {sources || "none"}</p>
      </div>
    </article>
  );
}

function CadStatus({ result }: { result: ApiResult<CadParsingStatus> }) {
  if (result.error) {
    return <ErrorState error={result.error} />;
  }

  if (!result.data) {
    return <EmptyState message="CAD parsing status is not available." />;
  }

  return (
    <section className="rounded-3xl border border-cyan-400/30 bg-cyan-400/10 p-6">
      <div className="flex items-center justify-between gap-3">
        <div>
          <h2 className="text-2xl font-semibold">CAD Parsing Placeholder</h2>
          <p className="mt-2 text-sm text-slate-300">{result.data.safeSummary}</p>
        </div>
        <StatusBadge status={result.data.isEnabled ? "enabled" : "disabled"} />
      </div>
      <p className="mt-3 font-mono text-xs text-cyan-100">{result.data.providerName}</p>
    </section>
  );
}

function FirstDocumentDetail({ result }: { result: ApiResult<DocumentArtifactDetail> }) {
  if (result.error) {
    return <ErrorState error={result.error} />;
  }

  if (!result.data) {
    return <EmptyState message="Create a demo document to inspect versions, object links, and vector index hooks." />;
  }

  return (
    <div className="grid gap-6 xl:grid-cols-3">
      <ListSection
        title="Versions"
        description="Immutable document version metadata. Raw payloads stay behind storage keys."
        items={result.data.versions}
        emptyMessage="No document versions have been uploaded."
        renderItem={VersionCard}
      />
      <ListSection
        title="Object Links"
        description="Evidence-backed links to graph nodes or import batches."
        items={result.data.objectLinks}
        emptyMessage="No object links have been created."
        renderItem={LinkCard}
      />
      <ListSection
        title="Vector Hooks"
        description="Recorded vector indexing requests with tenant and policy filter metadata."
        items={result.data.vectorIndexRecords}
        emptyMessage="No vector indexing requests have been recorded."
        renderItem={VectorCard}
      />
    </div>
  );
}

export default async function DocumentsPage() {
  const lists = await getDocumentLists();
  const firstDocumentId = lists.firstDocumentDetail.data?.id;
  const documentIssues = (lists.dataQualityIssues.data ?? []).filter((issue) =>
    firstDocumentId
      ? issue.sourceLinks.some(
          (link) =>
            link.sourceType === "DocumentArtifact" && link.sourceId === firstDocumentId,
        )
      : issue.sourceLinks.some(
          (link) =>
            link.sourceType === "DocumentArtifact" ||
            link.sourceType === "DocumentVersion" ||
            link.sourceType === "DocumentObjectLink",
        ),
  );

  return (
    <main className="min-h-screen bg-slate-950 px-6 py-10 text-slate-100">
      <div className="mx-auto grid max-w-7xl gap-8">
        <header className="rounded-3xl border border-slate-800 bg-slate-900 p-8">
          <p className="text-sm font-semibold uppercase tracking-[0.3em] text-cyan-300">
            EnterpriseThreadOS
          </p>
          <div className="mt-4 flex flex-col gap-6 lg:flex-row lg:items-end lg:justify-between">
            <div>
              <h1 className="text-4xl font-bold tracking-tight">Document Memory</h1>
              <p className="mt-3 max-w-3xl text-slate-300">
                Inspect governed document artifacts, immutable versions, object links, extraction review hooks, and vector indexing contracts.
              </p>
            </div>
            <div className="flex flex-wrap gap-3">
              <ActionButton action={createDemoDocument}>Create demo document</ActionButton>
              <ActionButton action={requestVectorIndex}>Request vector index</ActionButton>
              <ActionButton action={createExtractionIssue}>Create extraction issue</ActionButton>
            </div>
          </div>
          <div className="mt-5 grid gap-2 text-xs text-slate-500 md:grid-cols-2">
            <p>Admin user: {adminUserId}</p>
            <p>Tenant: {selectedTenantId}</p>
          </div>
        </header>

        <CadStatus result={lists.cadParsing} />

        {lists.documents.error ? (
          <ErrorState error={lists.documents.error} />
        ) : (
          <ListSection
            title="Documents"
            description="Tenant-scoped document artifacts backed by the artifact registry."
            items={lists.documents.data ?? []}
            emptyMessage="No document artifacts have been created."
            renderItem={DocumentCard}
          />
        )}

        <FirstDocumentDetail result={lists.firstDocumentDetail} />

        {lists.dataQualityIssues.error ? (
          <ErrorState error={lists.dataQualityIssues.error} />
        ) : (
          <ListSection
            title="Document Quality Issues"
            description="Reviewable extraction failures and uncertain document-object links."
            items={documentIssues}
            emptyMessage="No document extraction or link issues have been created."
            renderItem={DataQualityIssueCard}
          />
        )}
      </div>
    </main>
  );
}
