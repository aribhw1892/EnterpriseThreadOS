import Link from "next/link";
import { Suspense } from "react";
import {
  createAgentFromPromptAction,
  createAgentFromTemplateAction,
} from "@/app/(shell)/agents/actions";
import { Button } from "@/components/ui/Button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/Card";
import { ErrorState } from "@/components/ui/ErrorState";
import { Notice } from "@/components/ui/Notice";
import { PageHeader } from "@/components/ui/PageHeader";
import { PillStack, SidePanel } from "@/components/ui/SidePanel";
import { Tabs } from "@/components/ui/Tabs";
import {
  getAgentTemplateDefinitionArtifacts,
  getAgentTypeDefinitionArtifacts,
} from "@/lib/etos-api";

export const dynamic = "force-dynamic";

type PageProps = {
  searchParams: Promise<{ error?: string; mode?: string }>;
};

const fieldClass =
  "mt-2 w-full rounded-xl border border-etos-border bg-etos-panel px-3.5 py-2.5 text-sm text-etos-ink";

export default async function NewAgentPage({ searchParams }: PageProps) {
  const { error, mode: modeParam } = await searchParams;
  const mode = modeParam === "prompt" ? "prompt" : "template";
  const [templates, agentTypes] = await Promise.all([
    getAgentTemplateDefinitionArtifacts(),
    getAgentTypeDefinitionArtifacts(),
  ]);
  const loadError = templates.error ?? agentTypes.error;
  const templateList = templates.data ?? [];
  const typeList = agentTypes.data ?? [];

  return (
    <main className="px-6 py-8 lg:px-8">
      <PageHeader
        title="Agent builder"
        description="Create a draft AgentVersion from an installed template or a natural-language prompt. Redirects to configure on success."
        actions={
          <Link href="/agents">
            <Button type="button" variant="ghost">
              Back to registry
            </Button>
          </Link>
        }
      />

      {error ? (
        <div className="mb-4">
          <Notice variant="danger">{error}</Notice>
        </div>
      ) : null}
      {loadError ? (
        <div className="mb-4">
          <ErrorState error={loadError} />
        </div>
      ) : null}

      <div className="grid gap-4 lg:grid-cols-3">
        <Card className="lg:col-span-2">
          <CardHeader>
            <CardTitle>Composition</CardTitle>
          </CardHeader>
          <CardContent>
            <Suspense
              fallback={<div className="mb-4 h-10 animate-pulse rounded-xl bg-etos-panel-muted" />}
            >
              <Tabs
                paramName="mode"
                activeId={mode}
                items={[
                  { id: "template", label: "From template" },
                  { id: "prompt", label: "From prompt" },
                ]}
              />
            </Suspense>

            {mode === "template" ? (
              <form action={createAgentFromTemplateAction} className="mt-4 space-y-4">
                <label className="block text-sm">
                  <span className="font-semibold text-etos-ink">Agent template</span>
                  <select name="templateArtifactId" required className={fieldClass}>
                    <option value="">Select a template</option>
                    {templateList.map((template) => (
                      <option key={template.id} value={template.id}>
                        {template.name}
                        {template.templateKey ? ` · ${template.templateKey}` : ""}
                      </option>
                    ))}
                  </select>
                </label>
                <div className="grid gap-4 md:grid-cols-2">
                  <label className="block text-sm">
                    <span className="font-semibold text-etos-ink">Agent key (optional)</span>
                    <input
                      name="agentKey"
                      type="text"
                      placeholder="manufacturing-investigator"
                      className={fieldClass}
                    />
                  </label>
                  <label className="block text-sm">
                    <span className="font-semibold text-etos-ink">Display name (optional)</span>
                    <input
                      name="displayName"
                      type="text"
                      placeholder="Manufacturing investigator"
                      className={fieldClass}
                    />
                  </label>
                </div>
                <div className="grid gap-4 md:grid-cols-2">
                  <label className="block text-sm">
                    <span className="font-semibold text-etos-ink">Primary model provider</span>
                    <input
                      name="primaryModelProviderKey"
                      type="text"
                      defaultValue="openai"
                      className={fieldClass}
                    />
                  </label>
                  <label className="block text-sm">
                    <span className="font-semibold text-etos-ink">Primary model id</span>
                    <input
                      name="primaryModelId"
                      type="text"
                      defaultValue="gpt-4o-mini"
                      className={fieldClass}
                    />
                  </label>
                </div>
                <Button type="submit" disabled={templateList.length === 0}>
                  Create from template
                </Button>
              </form>
            ) : (
              <form action={createAgentFromPromptAction} className="mt-4 space-y-4">
                <label className="block text-sm">
                  <span className="font-semibold text-etos-ink">Prompt</span>
                  <textarea
                    name="prompt"
                    required
                    rows={5}
                    placeholder="Create an agent that investigates manufacturing BOM discrepancies using governed query and evidence-backed recommendations."
                    className={fieldClass}
                  />
                </label>
                <div className="grid gap-4 md:grid-cols-2">
                  <label className="block text-sm">
                    <span className="font-semibold text-etos-ink">Primary model provider</span>
                    <input
                      name="primaryModelProviderKey"
                      type="text"
                      defaultValue="openai"
                      className={fieldClass}
                    />
                  </label>
                  <label className="block text-sm">
                    <span className="font-semibold text-etos-ink">Primary model id</span>
                    <input
                      name="primaryModelId"
                      type="text"
                      defaultValue="gpt-4o-mini"
                      className={fieldClass}
                    />
                  </label>
                </div>
                <Button type="submit">Create from prompt</Button>
              </form>
            )}
          </CardContent>
        </Card>

        <SidePanel title="Draft governance">
          <PillStack
            items={[
              { label: "Lifecycle", value: "Draft on create", variant: "info" },
              { label: "Templates", value: String(templateList.length), variant: "neutral" },
              { label: "Agent types", value: String(typeList.length), variant: "neutral" },
              { label: "Output", value: "Recommendation-only", variant: "purple" },
            ]}
          />
          <p className="mt-4 text-xs leading-relaxed text-etos-ink-muted">
            Created agents land as draft AgentVersion artifacts. Mark ready and publish from configure before
            governed execute.
          </p>
          <div className="mt-4">
            <Link href="/agent-templates" className="text-sm text-etos-accent hover:underline">
              Browse templates
            </Link>
          </div>
        </SidePanel>
      </div>
    </main>
  );
}
