import {
  ApiResult,
  CadParsingStatus,
  DataQualityIssue,
  DocumentArtifact,
  DocumentArtifactDetail,
  DocumentObjectLink,
  DocumentVectorIndexRecord,
  DocumentVersion,
  createDemoDocumentFlow,
  getDocumentLists,
  requestLatestDocumentVectorIndex,
} from "@/lib/etos-api";
import { Badge, StatusBadge } from "@/components/ui/Badge";
import { Button } from "@/components/ui/Button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/Card";
import { DataTable } from "@/components/ui/DataTable";
import { EmptyState } from "@/components/ui/EmptyState";
import { ErrorState } from "@/components/ui/ErrorState";
import { PageHeader } from "@/components/ui/PageHeader";
import { SidePanel, PillStack } from "@/components/ui/SidePanel";
import Link from "next/link";
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

function ActionButton({ action, children }: { action: () => Promise<void>; children: ReactNode }) {
  return (
    <form action={action}>
      <Button type="submit">{children}</Button>
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
    <Card>
      <CardHeader>
        <CardTitle>{title}</CardTitle>
        <CardDescription>{description}</CardDescription>
      </CardHeader>
      <CardContent>
        {items.length > 0 ? <div className="grid gap-3">{items.map(renderItem)}</div> : <EmptyState message={emptyMessage} />}
      </CardContent>
    </Card>
  );
}

function VersionCard(version: DocumentVersion) {
  return (
    <article key={version.id} className="rounded-etos-card border border-etos-border-soft bg-etos-panel p-4">
      <div className="flex items-center justify-between gap-3">
        <h3 className="font-semibold">{version.versionLabel}</h3>
        <StatusBadge status={version.extractionStatus} />
      </div>
      <div className="mt-3 grid gap-1 text-xs text-etos-ink-subtle">
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
    <article key={link.id} className="rounded-etos-card border border-etos-border-soft bg-etos-panel p-4">
      <div className="flex items-center justify-between gap-3">
        <h3 className="font-semibold">Object link</h3>
        <StatusBadge status={link.extractionStatus} />
      </div>
      <div className="mt-3 grid gap-1 text-xs text-etos-ink-subtle">
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
    <article key={record.id} className="rounded-etos-card border border-etos-border-soft bg-etos-panel p-4">
      <div className="flex items-center justify-between gap-3">
        <h3 className="font-semibold">{record.providerName}</h3>
        <StatusBadge status={record.status} />
      </div>
      <div className="mt-3 grid gap-1 text-xs text-etos-ink-subtle">
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
    <article key={issue.id} className="rounded-etos-card border border-etos-border-soft bg-etos-panel p-4">
      <div className="flex items-center justify-between gap-3">
        <div>
          <h3 className="font-semibold">{issue.title}</h3>
          <p className="mt-1 text-sm text-etos-ink-muted">{issue.issueCode}</p>
        </div>
        <div className="flex flex-wrap justify-end gap-2">
          <StatusBadge status={issue.severity} />
          <StatusBadge status={issue.status} />
        </div>
      </div>
      <p className="mt-3 text-sm text-etos-ink">{issue.evidenceSummary}</p>
      <div className="mt-3 grid gap-1 text-xs text-etos-ink-subtle">
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
    <section className="rounded-etos-card border border-etos-info-border bg-etos-panel-elevated p-6 shadow-etos">
      <div className="flex items-center justify-between gap-3">
        <div>
          <h2 className="text-2xl font-semibold">CAD Parsing Placeholder</h2>
          <p className="mt-2 text-sm text-etos-ink">{result.data.safeSummary}</p>
        </div>
        <StatusBadge status={result.data.isEnabled ? "enabled" : "disabled"} />
      </div>
      <p className="mt-3 font-mono text-xs text-etos-accent-cyan">{result.data.providerName}</p>
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

  const docs = lists.documents.data ?? [];
  const vectorByDoc = new Map<string, string>();
  for (const record of lists.firstDocumentDetail.data?.vectorIndexRecords ?? []) {
    vectorByDoc.set(record.documentArtifactId, record.status);
  }

  return (
    <main className="px-6 py-8 lg:px-8">
      <PageHeader
        title="Document memory explorer"
        description="Document artifacts and versions linked to imports, graph objects, quality issues, vector indexing hooks, and AI traces."
        actions={
          <>
            <ActionButton action={createDemoDocument}>Create demo document</ActionButton>
            <ActionButton action={requestVectorIndex}>Index vectors</ActionButton>
          </>
        }
      />

      {lists.documents.error ? (
        <ErrorState error={lists.documents.error} />
      ) : null}

      <div className="grid gap-4 lg:grid-cols-[1.2fr_0.8fr]">
        <Card>
          <CardHeader>
            <CardTitle>Documents</CardTitle>
          </CardHeader>
          <CardContent>
            <DataTable<DocumentArtifact>
              rows={docs}
              rowKey={(row) => row.id}
              emptyMessage="No document artifacts have been created."
              columns={[
                {
                  key: "title",
                  header: "Document",
                  render: (row) => (
                    <Link href={`/documents/${row.id}`} className="font-extrabold text-etos-accent hover:underline">
                      {row.title}
                    </Link>
                  ),
                },
                {
                  key: "version",
                  header: "Version",
                  render: () => <span className="text-etos-ink-muted">v1</span>,
                },
                {
                  key: "linked",
                  header: "Linked object",
                  render: (row) => (
                    <span className="text-xs text-etos-ink-subtle">
                      {row.linkCount > 0 ? `${row.linkCount} links` : "—"}
                    </span>
                  ),
                },
                {
                  key: "extraction",
                  header: "Extraction",
                  render: (row) => {
                    const status = vectorByDoc.get(row.id);
                    return status ? <StatusBadge status={status} /> : <Badge variant="neutral">Pending</Badge>;
                  },
                },
              ]}
            />
          </CardContent>
        </Card>

        <SidePanel title="Document detail">
          {lists.firstDocumentDetail.data ? (
            <>
              <div className="mb-4 rounded-xl border border-etos-border-soft bg-etos-panel-muted p-3">
                <p className="text-xs font-semibold uppercase tracking-wide text-etos-ink-muted">Filtered summary</p>
                <p className="mt-2 text-[13px] leading-relaxed text-etos-ink">
                  Specification applies to imported parts. Restricted supplier pricing section excluded from LLM-visible context. 
                  Evidence references retained in AI Trace.
                </p>
              </div>
              <PillStack
                items={[
                  {
                    label: "Qdrant chunks",
                    value: String(lists.firstDocumentDetail.data.vectorIndexRecords.length),
                    variant: "info",
                  },
                  {
                    label: "Graph links",
                    value: String(lists.firstDocumentDetail.data.objectLinks.length),
                    variant: "success",
                  },
                  {
                    label: "Access policy",
                    value: "Restricted attributes filtered",
                    variant: "warning",
                  },
                ]}
              />
              <details className="mt-4">
                <summary className="cursor-pointer text-xs font-semibold text-etos-accent">Advanced / Debug</summary>
                <div className="mt-2 space-y-1 text-xs text-etos-ink-subtle">
                  <p>Versions: {lists.firstDocumentDetail.data.versions.length}</p>
                  <p>Classification: {lists.firstDocumentDetail.data.classificationKey}</p>
                  <p>Type: {lists.firstDocumentDetail.data.documentType}</p>
                </div>
              </details>
            </>
          ) : (
            <EmptyState message="Create a demo document to inspect details." />
          )}
        </SidePanel>
      </div>

      <details className="mt-6">
        <summary className="mb-4 cursor-pointer text-lg font-semibold text-etos-ink">Advanced / Debug</summary>
        <div className="grid gap-6">
          <CadStatus result={lists.cadParsing} />
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
      </details>
    </main>
  );
}
