import Link from "next/link";
import { revalidatePath } from "next/cache";
import { redirect } from "next/navigation";
import { ExplorerNavLink } from "@/components/explorers/ExplorerListShell";
import { getOntologyLists, postWorkflowDefinition } from "@/lib/etos-api";

export const dynamic = "force-dynamic";

type PageProps = {
  searchParams: Promise<{ error?: string }>;
};

async function createWorkflowAction(formData: FormData) {
  "use server";

  const name = formData.get("name");
  const workflowKey = formData.get("workflowKey");
  const displayName = formData.get("displayName");
  const workflowScope = formData.get("workflowScope");
  const description = formData.get("description");
  const compatibleModelPackageVersionId = formData.get("compatibleModelPackageVersionId");

  if (typeof name !== "string" || name.trim().length === 0) {
    redirect("/workflows/new?error=Name%20is%20required.");
  }

  if (typeof workflowKey !== "string" || workflowKey.trim().length === 0) {
    redirect("/workflows/new?error=Workflow%20key%20is%20required.");
  }

  if (typeof displayName !== "string" || displayName.trim().length === 0) {
    redirect("/workflows/new?error=Display%20name%20is%20required.");
  }

  if (typeof workflowScope !== "string" || workflowScope.trim().length === 0) {
    redirect("/workflows/new?error=Workflow%20scope%20is%20required.");
  }

  if (
    typeof compatibleModelPackageVersionId !== "string" ||
    compatibleModelPackageVersionId.trim().length === 0
  ) {
    redirect("/workflows/new?error=A%20compatible%20model%20package%20is%20required.");
  }

  const result = await postWorkflowDefinition({
    name: name.trim(),
    description: typeof description === "string" && description.trim().length > 0 ? description.trim() : null,
    workflowKey: workflowKey.trim(),
    displayName: displayName.trim(),
    workflowDescription:
      typeof description === "string" && description.trim().length > 0 ? description.trim() : null,
    workflowScope: workflowScope.trim(),
    steps: [],
    compatibleModelPackageVersionIds: [compatibleModelPackageVersionId.trim()],
    safeModeEnabled: true,
    previewModeDefault: true,
    allowPartialCompletion: false,
    defaultStepSafeModeBehavior: "skip",
    triggerConfig: {
      manualEnabled: true,
      scheduledEnabled: false,
      eventDrivenEnabled: false,
    },
  });

  if (result.error || !result.data) {
    redirect(`/workflows/new?error=${encodeURIComponent(result.error ?? "Could not create workflow.")}`);
  }

  revalidatePath("/workflows");
  redirect(`/workflows/${encodeURIComponent(workflowKey.trim())}/edit`);
}

export default async function NewWorkflowPage({ searchParams }: PageProps) {
  const { error } = await searchParams;
  const ontologyLists = await getOntologyLists();
  const modelPackages = ontologyLists.modelPackages.data ?? [];
  const activePackage = ontologyLists.activeModelPackage.data;

  return (
    <main className="min-h-screen bg-slate-950 px-6 py-10 text-slate-100">
      <div className="mx-auto flex max-w-6xl flex-col gap-8">
        <section className="rounded-3xl border border-slate-800 bg-slate-900 p-8">
          <div className="flex flex-wrap items-start justify-between gap-4">
            <div>
              <p className="text-sm uppercase tracking-wide text-cyan-300">Issue 24 · Create</p>
              <h1 className="mt-2 text-4xl font-semibold">New tenant workflow</h1>
              <p className="mt-3 max-w-3xl text-slate-400">
                Create a draft WorkflowVersion with governed scope, safe mode defaults, and at least one compatible
                model package.
              </p>
            </div>
            <div className="flex flex-wrap gap-3">
              <ExplorerNavLink href="/workflows">Workflows</ExplorerNavLink>
              <ExplorerNavLink href="/model-artifacts">Model artifacts</ExplorerNavLink>
            </div>
          </div>
        </section>

        {error ? (
          <div className="rounded-2xl border border-amber-500/30 bg-amber-500/10 p-4 text-sm text-amber-100">
            {error}
          </div>
        ) : null}

        {ontologyLists.modelPackages.error ? (
          <div className="rounded-2xl border border-amber-500/30 bg-amber-500/10 p-4 text-sm text-amber-100">
            {ontologyLists.modelPackages.error}
          </div>
        ) : null}

        {modelPackages.length === 0 ? (
          <section className="rounded-3xl border border-slate-800 bg-slate-900 p-6">
            <h2 className="text-2xl font-semibold">Model package required</h2>
            <p className="mt-3 text-sm text-slate-400">
              Workflow definitions require at least one compatible model package or ontology. Install the manufacturing
              reference package or create a model package first.
            </p>
            <div className="mt-6 flex flex-wrap gap-3">
              <ExplorerNavLink href="/model-artifacts">Model artifacts</ExplorerNavLink>
              <ExplorerNavLink href="/workflows">Back to workflows</ExplorerNavLink>
            </div>
          </section>
        ) : (
          <section className="rounded-3xl border border-slate-800 bg-slate-900 p-6">
            <h2 className="text-2xl font-semibold">Workflow metadata</h2>
            <form action={createWorkflowAction} className="mt-6 space-y-4">
              <div className="grid gap-4 md:grid-cols-2">
                <label className="block text-sm">
                  <span className="font-semibold text-slate-300">Artifact name</span>
                  <input
                    name="name"
                    type="text"
                    required
                    placeholder="Manufacturing investigation workflow"
                    className="mt-2 w-full rounded-2xl border border-slate-700 bg-slate-950 px-4 py-3 text-slate-100"
                  />
                </label>
                <label className="block text-sm">
                  <span className="font-semibold text-slate-300">Workflow key</span>
                  <input
                    name="workflowKey"
                    type="text"
                    required
                    placeholder="manufacturing-investigation"
                    className="mt-2 w-full rounded-2xl border border-slate-700 bg-slate-950 px-4 py-3 text-slate-100"
                  />
                </label>
              </div>
              <label className="block text-sm">
                <span className="font-semibold text-slate-300">Display name</span>
                <input
                  name="displayName"
                  type="text"
                  required
                  placeholder="Manufacturing investigation"
                  className="mt-2 w-full rounded-2xl border border-slate-700 bg-slate-950 px-4 py-3 text-slate-100"
                />
              </label>
              <label className="block text-sm">
                <span className="font-semibold text-slate-300">Description (optional)</span>
                <textarea
                  name="description"
                  rows={3}
                  placeholder="Governed multi-step workflow for BOM discrepancy investigation."
                  className="mt-2 w-full rounded-2xl border border-slate-700 bg-slate-950 px-4 py-3 text-slate-100"
                />
              </label>
              <div className="grid gap-4 md:grid-cols-2">
                <label className="block text-sm">
                  <span className="font-semibold text-slate-300">Workflow scope</span>
                  <select
                    name="workflowScope"
                    required
                    defaultValue="tenant"
                    className="mt-2 w-full rounded-2xl border border-slate-700 bg-slate-950 px-4 py-3 text-slate-100"
                  >
                    <option value="tenant">tenant</option>
                    <option value="platform">platform</option>
                    <option value="personal">personal</option>
                  </select>
                </label>
                <label className="block text-sm">
                  <span className="font-semibold text-slate-300">Compatible model package</span>
                  <select
                    name="compatibleModelPackageVersionId"
                    required
                    defaultValue={activePackage?.id ?? modelPackages[0]?.id ?? ""}
                    className="mt-2 w-full rounded-2xl border border-slate-700 bg-slate-950 px-4 py-3 text-slate-100"
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
              <button
                type="submit"
                className="rounded-2xl border border-cyan-500/40 bg-cyan-500/10 px-4 py-2 text-sm font-semibold text-cyan-100 transition hover:border-cyan-300"
              >
                Create draft workflow
              </button>
            </form>
          </section>
        )}

        <p className="text-sm text-slate-500">
          Need governed steps first?{" "}
          <Link href="/agents" className="text-cyan-300 hover:text-cyan-100">
            Configure agents
          </Link>{" "}
          and{" "}
          <Link href="/tools" className="text-cyan-300 hover:text-cyan-100">
            tools
          </Link>{" "}
          before wiring workflow steps.
        </p>
      </div>
    </main>
  );
}
