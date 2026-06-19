import Link from "next/link";
import { revalidatePath } from "next/cache";
import { redirect } from "next/navigation";
import { ExplorerNavLink } from "@/components/explorers/ExplorerListShell";
import {
  getAgentTemplateDefinitionArtifacts,
  getAgentTypeDefinitionArtifacts,
  getArtifactVersions,
  postAgentFromPrompt,
  postAgentFromTemplate,
} from "@/lib/etos-api";

export const dynamic = "force-dynamic";

type PageProps = {
  searchParams: Promise<{ error?: string }>;
};

async function createFromTemplateAction(formData: FormData) {
  "use server";

  const templateArtifactId = formData.get("templateArtifactId");
  const agentKey = formData.get("agentKey");
  const displayName = formData.get("displayName");
  const primaryModelProviderKey = formData.get("primaryModelProviderKey");
  const primaryModelId = formData.get("primaryModelId");

  if (typeof templateArtifactId !== "string" || templateArtifactId.length === 0) {
    redirect("/agents/new?error=Agent%20template%20is%20required.");
  }

  const versions = await getArtifactVersions(templateArtifactId);
  if (!versions.data || versions.data.length === 0) {
    redirect("/agents/new?error=Selected%20template%20has%20no%20versions.");
  }

  const result = await postAgentFromTemplate({
    sourceAgentTemplateVersionId: versions.data[0].id,
    agentKey: typeof agentKey === "string" && agentKey.length > 0 ? agentKey : null,
    displayName: typeof displayName === "string" && displayName.length > 0 ? displayName : null,
    primaryModelProviderKey:
      typeof primaryModelProviderKey === "string" && primaryModelProviderKey.length > 0
        ? primaryModelProviderKey
        : "openai",
    primaryModelId:
      typeof primaryModelId === "string" && primaryModelId.length > 0 ? primaryModelId : "gpt-4o-mini",
  });

  if (result.error || !result.data) {
    redirect(`/agents/new?error=${encodeURIComponent(result.error ?? "Could not create agent from template.")}`);
  }

  revalidatePath("/agents");
  redirect("/agents");
}

async function createFromPromptAction(formData: FormData) {
  "use server";

  const prompt = formData.get("prompt");
  const primaryModelProviderKey = formData.get("primaryModelProviderKey");
  const primaryModelId = formData.get("primaryModelId");

  if (typeof prompt !== "string" || prompt.trim().length === 0) {
    redirect("/agents/new?error=Prompt%20is%20required.");
  }

  const result = await postAgentFromPrompt({
    prompt: prompt.trim(),
    primaryModelProviderKey:
      typeof primaryModelProviderKey === "string" && primaryModelProviderKey.length > 0
        ? primaryModelProviderKey
        : "openai",
    primaryModelId:
      typeof primaryModelId === "string" && primaryModelId.length > 0 ? primaryModelId : "gpt-4o-mini",
  });

  if (result.error || !result.data) {
    redirect(`/agents/new?error=${encodeURIComponent(result.error ?? "Could not create agent from prompt.")}`);
  }

  revalidatePath("/agents");
  redirect("/agents");
}

export default async function NewAgentPage({ searchParams }: PageProps) {
  const { error } = await searchParams;
  const [templates, agentTypes] = await Promise.all([
    getAgentTemplateDefinitionArtifacts(),
    getAgentTypeDefinitionArtifacts(),
  ]);

  const loadError = templates.error ?? agentTypes.error;

  return (
    <main className="min-h-screen bg-slate-950 px-6 py-10 text-slate-100">
      <div className="mx-auto flex max-w-6xl flex-col gap-8">
        <section className="rounded-3xl border border-slate-800 bg-slate-900 p-8">
          <div className="flex flex-wrap items-start justify-between gap-4">
            <div>
              <p className="text-sm uppercase tracking-wide text-cyan-300">Issue 23 · Create</p>
              <h1 className="mt-2 text-4xl font-semibold">New tenant agent</h1>
              <p className="mt-3 max-w-3xl text-slate-400">
                Create a draft AgentVersion from an installed AgentTemplateVersion or from a natural-language prompt.
              </p>
            </div>
            <div className="flex flex-wrap gap-3">
              <ExplorerNavLink href="/agents">Agents</ExplorerNavLink>
              <ExplorerNavLink href="/agent-templates">Templates</ExplorerNavLink>
            </div>
          </div>
        </section>

        {error ? (
          <div className="rounded-2xl border border-amber-500/30 bg-amber-500/10 p-4 text-sm text-amber-100">
            {error}
          </div>
        ) : null}

        {loadError ? (
          <div className="rounded-2xl border border-amber-500/30 bg-amber-500/10 p-4 text-sm text-amber-100">
            {loadError}
          </div>
        ) : null}

        <section className="rounded-3xl border border-slate-800 bg-slate-900 p-6">
          <h2 className="text-2xl font-semibold">From template</h2>
          <p className="mt-2 text-sm text-slate-400">
            Copies governed references from the selected template into a new draft AgentVersion.
          </p>
          <form action={createFromTemplateAction} className="mt-6 space-y-4">
            <label className="block text-sm">
              <span className="font-semibold text-slate-300">Agent template</span>
              <select
                name="templateArtifactId"
                required
                className="mt-2 w-full rounded-2xl border border-slate-700 bg-slate-950 px-4 py-3 text-slate-100"
              >
                <option value="">Select a template</option>
                {(templates.data ?? []).map((template) => (
                  <option key={template.id} value={template.id}>
                    {template.name}
                    {template.templateKey ? ` · ${template.templateKey}` : ""}
                  </option>
                ))}
              </select>
            </label>
            <div className="grid gap-4 md:grid-cols-2">
              <label className="block text-sm">
                <span className="font-semibold text-slate-300">Agent key (optional)</span>
                <input
                  name="agentKey"
                  type="text"
                  placeholder="manufacturing-investigator"
                  className="mt-2 w-full rounded-2xl border border-slate-700 bg-slate-950 px-4 py-3 text-slate-100"
                />
              </label>
              <label className="block text-sm">
                <span className="font-semibold text-slate-300">Display name (optional)</span>
                <input
                  name="displayName"
                  type="text"
                  placeholder="Manufacturing investigator"
                  className="mt-2 w-full rounded-2xl border border-slate-700 bg-slate-950 px-4 py-3 text-slate-100"
                />
              </label>
            </div>
            <div className="grid gap-4 md:grid-cols-2">
              <label className="block text-sm">
                <span className="font-semibold text-slate-300">Primary model provider</span>
                <input
                  name="primaryModelProviderKey"
                  type="text"
                  defaultValue="openai"
                  className="mt-2 w-full rounded-2xl border border-slate-700 bg-slate-950 px-4 py-3 text-slate-100"
                />
              </label>
              <label className="block text-sm">
                <span className="font-semibold text-slate-300">Primary model id</span>
                <input
                  name="primaryModelId"
                  type="text"
                  defaultValue="gpt-4o-mini"
                  className="mt-2 w-full rounded-2xl border border-slate-700 bg-slate-950 px-4 py-3 text-slate-100"
                />
              </label>
            </div>
            <button
              type="submit"
              className="rounded-2xl border border-cyan-500/40 bg-cyan-500/10 px-4 py-2 text-sm font-semibold text-cyan-100 transition hover:border-cyan-300"
            >
              Create from template
            </button>
          </form>
        </section>

        <section className="rounded-3xl border border-slate-800 bg-slate-900 p-6">
          <h2 className="text-2xl font-semibold">From prompt</h2>
          <p className="mt-2 text-sm text-slate-400">
            Uses governed LLM drafting to propose a draft AgentVersion payload. Review on the agents list before
            publish.
          </p>
          {agentTypes.data && agentTypes.data.length > 0 ? (
            <p className="mt-2 text-xs text-slate-500">
              {agentTypes.data.length} agent type definition
              {agentTypes.data.length === 1 ? "" : "s"} available for backend defaulting.
            </p>
          ) : null}
          <form action={createFromPromptAction} className="mt-6 space-y-4">
            <label className="block text-sm">
              <span className="font-semibold text-slate-300">Prompt</span>
              <textarea
                name="prompt"
                required
                rows={5}
                placeholder="Create an agent that investigates manufacturing BOM discrepancies using governed query and evidence-backed recommendations."
                className="mt-2 w-full rounded-2xl border border-slate-700 bg-slate-950 px-4 py-3 text-slate-100"
              />
            </label>
            <div className="grid gap-4 md:grid-cols-2">
              <label className="block text-sm">
                <span className="font-semibold text-slate-300">Primary model provider</span>
                <input
                  name="primaryModelProviderKey"
                  type="text"
                  defaultValue="openai"
                  className="mt-2 w-full rounded-2xl border border-slate-700 bg-slate-950 px-4 py-3 text-slate-100"
                />
              </label>
              <label className="block text-sm">
                <span className="font-semibold text-slate-300">Primary model id</span>
                <input
                  name="primaryModelId"
                  type="text"
                  defaultValue="gpt-4o-mini"
                  className="mt-2 w-full rounded-2xl border border-slate-700 bg-slate-950 px-4 py-3 text-slate-100"
                />
              </label>
            </div>
            <button
              type="submit"
              className="rounded-2xl border border-cyan-500/40 bg-cyan-500/10 px-4 py-2 text-sm font-semibold text-cyan-100 transition hover:border-cyan-300"
            >
              Create from prompt
            </button>
          </form>
        </section>

        <p className="text-sm text-slate-500">
          Need a reusable pattern first?{" "}
          <Link href="/agent-templates" className="text-cyan-300 hover:text-cyan-100">
            Browse agent templates
          </Link>
          .
        </p>
      </div>
    </main>
  );
}
