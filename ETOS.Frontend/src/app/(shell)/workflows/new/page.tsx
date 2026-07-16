import Link from "next/link";
import { createWorkflowAction } from "@/app/(shell)/workflows/actions";
import { Button } from "@/components/ui/Button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/Card";
import { ErrorState } from "@/components/ui/ErrorState";
import { Notice } from "@/components/ui/Notice";
import { PageHeader } from "@/components/ui/PageHeader";
import { getOntologyLists } from "@/lib/etos-api";

export const dynamic = "force-dynamic";

type PageProps = {
  searchParams: Promise<{ error?: string }>;
};

const fieldClass =
  "mt-2 w-full rounded-xl border border-etos-border bg-etos-panel px-3.5 py-2.5 text-sm text-etos-ink";

export default async function NewWorkflowPage({ searchParams }: PageProps) {
  const { error } = await searchParams;
  const ontologyLists = await getOntologyLists();
  const modelPackages = ontologyLists.modelPackages.data ?? [];
  const activePackage = ontologyLists.activeModelPackage.data;

  return (
    <main className="px-6 py-8 lg:px-8">
      <PageHeader
        title="Create workflow"
        description="Create a draft WorkflowVersion with governed scope, safe mode defaults, and a compatible model package. Opens the canvas editor on success."
        actions={
          <Link href="/workflows">
            <Button type="button" variant="ghost">
              Registry
            </Button>
          </Link>
        }
      />

      {error ? (
        <div className="mb-4">
          <Notice variant="danger">{error}</Notice>
        </div>
      ) : null}
      {ontologyLists.modelPackages.error ? (
        <div className="mb-4">
          <ErrorState error={ontologyLists.modelPackages.error} />
        </div>
      ) : null}

      {modelPackages.length === 0 ? (
        <Card>
          <CardHeader>
            <CardTitle>Model package required</CardTitle>
          </CardHeader>
          <CardContent className="space-y-4 text-sm text-etos-ink-muted">
            <p>
              Workflow definitions require at least one compatible model package. Install the manufacturing
              reference package or create a model package first.
            </p>
            <Link href="/model-artifacts" className="text-etos-accent hover:underline">
              Model artifacts
            </Link>
          </CardContent>
        </Card>
      ) : (
        <Card>
          <CardHeader>
            <CardTitle>Workflow metadata</CardTitle>
          </CardHeader>
          <CardContent>
            <form action={createWorkflowAction} className="space-y-4">
              <div className="grid gap-4 md:grid-cols-2">
                <label className="block text-sm">
                  <span className="font-semibold text-etos-ink">Artifact name</span>
                  <input
                    name="name"
                    type="text"
                    required
                    placeholder="Manufacturing investigation workflow"
                    className={fieldClass}
                  />
                </label>
                <label className="block text-sm">
                  <span className="font-semibold text-etos-ink">Workflow key</span>
                  <input
                    name="workflowKey"
                    type="text"
                    required
                    placeholder="manufacturing-investigation"
                    className={fieldClass}
                  />
                </label>
              </div>
              <label className="block text-sm">
                <span className="font-semibold text-etos-ink">Display name</span>
                <input
                  name="displayName"
                  type="text"
                  required
                  placeholder="Manufacturing investigation"
                  className={fieldClass}
                />
              </label>
              <label className="block text-sm">
                <span className="font-semibold text-etos-ink">Description (optional)</span>
                <textarea
                  name="description"
                  rows={3}
                  placeholder="Governed multi-step workflow for BOM discrepancy investigation."
                  className={fieldClass}
                />
              </label>
              <div className="grid gap-4 md:grid-cols-2">
                <label className="block text-sm">
                  <span className="font-semibold text-etos-ink">Workflow scope</span>
                  <select name="workflowScope" required defaultValue="tenant" className={fieldClass}>
                    <option value="tenant">tenant</option>
                    <option value="platform">platform</option>
                    <option value="personal">personal</option>
                  </select>
                </label>
                <label className="block text-sm">
                  <span className="font-semibold text-etos-ink">Compatible model package</span>
                  <select
                    name="compatibleModelPackageVersionId"
                    required
                    defaultValue={activePackage?.id ?? modelPackages[0]?.id ?? ""}
                    className={fieldClass}
                  >
                    {modelPackages.map((pkg) => (
                      <option key={pkg.id} value={pkg.id}>
                        {pkg.name} · {pkg.versionLabel}
                        {pkg.key ? ` · ${pkg.key}` : ""}
                      </option>
                    ))}
                  </select>
                </label>
              </div>
              <Notice variant="info">
                Initial create uses empty steps (`steps: []`). Edit the canvas to rearrange/delete existing steps,
                or install package definitions that include steps.
              </Notice>
              <Button type="submit">Create draft workflow</Button>
            </form>
          </CardContent>
        </Card>
      )}
    </main>
  );
}
