import {
  createCanonicalModelSeed,
  getOntologyLists,
} from "@/lib/etos-api";
import { Badge, StatusBadge } from "@/components/ui/Badge";
import { Button } from "@/components/ui/Button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/Card";
import { DataTable } from "@/components/ui/DataTable";
import { EmptyState } from "@/components/ui/EmptyState";
import { ErrorState } from "@/components/ui/ErrorState";
import { PageHeader } from "@/components/ui/PageHeader";
import { SidePanel } from "@/components/ui/SidePanel";
import Link from "next/link";
import { revalidatePath } from "next/cache";

export const dynamic = "force-dynamic";

async function seedAction() {
  "use server";

  await createCanonicalModelSeed();
  revalidatePath("/model-artifacts");
  revalidatePath("/model-artifacts/ontology");
}

export default async function OntologyBrowserPage() {
  const lists = await getOntologyLists();
  const ontologyVersions = lists.ontologyVersions.data ?? [];

  const selectedObject = ontologyVersions[0];

  return (
    <main className="px-6 py-8 lg:px-8">
      <PageHeader
        title="Ontology & semantic layer detail"
        description="AI-native model view showing object types, relationship semantics, synonyms, allowed usage, and publish gates."
        actions={
          <>
            <form action={seedAction} className="inline">
              <Button type="submit" variant="primary">
                Create new version
              </Button>
            </form>
            <Button
              type="button"
              variant="ghost"
              disabled
              title="Impact analysis endpoint not wired"
            >
              Impact analysis
            </Button>
            <Link href="/model-artifacts">
              <Button variant="ghost">Model packages</Button>
            </Link>
          </>
        }
      />

      {lists.ontologyVersions.error ? (
        <ErrorState error={lists.ontologyVersions.error} />
      ) : null}

      <div className="grid gap-4 lg:grid-cols-[1.2fr_0.8fr]">
        <Card>
          <CardHeader>
            <CardTitle>Semantic object catalog</CardTitle>
          </CardHeader>
          <CardContent>
            <DataTable
              rows={ontologyVersions}
              rowKey={(row) => row.id}
              emptyMessage="No ontology versions. Seed a model package to begin."
              columns={[
                {
                  key: "object",
                  header: "Object type",
                  render: (row) => <span className="font-extrabold">{row.key}</span>,
                },
                {
                  key: "desc",
                  header: "AI description",
                  render: (row) => <span className="text-etos-ink-muted">{row.summary}</span>,
                },
                {
                  key: "usage",
                  header: "Allowed usage",
                  render: () => <span className="text-xs text-etos-ink-subtle">Search, dashboard, agent analysis</span>,
                },
                {
                  key: "classification",
                  header: "Classification",
                  render: (row) => <StatusBadge status={row.state} />,
                },
              ]}
            />
          </CardContent>
        </Card>

        <SidePanel title={`Selected object: ${selectedObject?.key ?? "None"}`}>
          {selectedObject ? (
            <>
              <div className="mb-4 rounded-xl border border-etos-border-soft bg-etos-panel-muted p-3">
                <p className="text-xs font-semibold uppercase tracking-wide text-etos-ink-muted">Business description</p>
                <p className="mt-2 text-[13px] leading-relaxed text-etos-ink">
                  Represents a controlled version of an ontology object as interpreted by the active manufacturing package. 
                  It links object types, relationships, semantic mappings, and AI usage metadata.
                </p>
              </div>
              <div className="mb-4 flex flex-wrap gap-2">
                <Badge variant="info">Synonyms: item, object, type</Badge>
                <Badge variant="success">Graph-first retrieval</Badge>
                <Badge variant="purple">Agent usable</Badge>
                <Badge variant="warning">Dashboard dimension</Badge>
              </div>
              <div className="space-y-2">
                <div className="flex items-center justify-between gap-3 text-xs text-etos-ink-subtle">
                  <span className="font-semibold">Semantic completeness</span>
                  <div className="flex flex-1 items-center gap-2">
                    <div className="h-2 flex-1 overflow-hidden rounded-full bg-etos-border-soft">
                      <div className="h-full w-[96%] rounded-full bg-gradient-to-r from-etos-accent-cyan to-etos-purple" />
                    </div>
                    <b className="w-6 text-right">96</b>
                  </div>
                </div>
                <div className="flex items-center justify-between gap-3 text-xs text-etos-ink-subtle">
                  <span className="font-semibold">AI usage safety</span>
                  <div className="flex flex-1 items-center gap-2">
                    <div className="h-2 flex-1 overflow-hidden rounded-full bg-etos-border-soft">
                      <div className="h-full w-[88%] rounded-full bg-gradient-to-r from-etos-accent-cyan to-etos-purple" />
                    </div>
                    <b className="w-6 text-right">88</b>
                  </div>
                </div>
              </div>
              <details className="mt-4">
                <summary className="cursor-pointer text-xs font-semibold text-etos-accent">Advanced / Debug</summary>
                <div className="mt-2 space-y-1 text-xs text-etos-ink-subtle">
                  <p>Types: {selectedObject.objectTypeCount}</p>
                  <p>Relationships: {selectedObject.relationshipTypeCount}</p>
                  <p>State: {selectedObject.state}</p>
                </div>
              </details>
            </>
          ) : (
            <EmptyState message="No ontology object selected." />
          )}
        </SidePanel>
      </div>
    </main>
  );
}
