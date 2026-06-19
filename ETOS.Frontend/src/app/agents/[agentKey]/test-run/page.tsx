import Link from "next/link";
import { revalidatePath } from "next/cache";
import { redirect } from "next/navigation";
import { ExplorerNavLink } from "@/components/explorers/ExplorerListShell";
import { loadAgentVersionByKey, postAgentPreview, postAgentTestRun } from "@/lib/etos-api";

export const dynamic = "force-dynamic";

type PageProps = {
  params: Promise<{ agentKey: string }>;
  searchParams: Promise<{ error?: string; runId?: string; toolRunIds?: string; versionId?: string }>;
};

async function testRunAction(formData: FormData) {
  "use server";

  const artifactId = formData.get("artifactId");
  const versionId = formData.get("versionId");
  const agentKey = formData.get("agentKey");
  const queryText = formData.get("queryText");

  if (
    typeof artifactId !== "string" ||
    typeof versionId !== "string" ||
    typeof agentKey !== "string" ||
    artifactId.length === 0 ||
    versionId.length === 0
  ) {
    redirect("/agents?error=Agent%20context%20was%20missing.");
  }

  if (typeof queryText !== "string" || queryText.trim().length === 0) {
    redirect(
      `/agents/${encodeURIComponent(agentKey)}/test-run?error=${encodeURIComponent("Query text is required.")}`,
    );
  }

  const result = await postAgentTestRun(artifactId, versionId, { queryText: queryText.trim() });
  if (result.error || !result.data) {
    redirect(
      `/agents/${encodeURIComponent(agentKey)}/test-run?error=${encodeURIComponent(result.error ?? "Test run failed.")}`,
    );
  }

  revalidatePath("/agent-runs");
  const toolRunQuery =
    result.data.toolRunIds.length > 0
      ? `&toolRunIds=${result.data.toolRunIds.map((id) => encodeURIComponent(id)).join(",")}`
      : "";
  redirect(
    `/agents/${encodeURIComponent(agentKey)}/test-run?runId=${encodeURIComponent(result.data.agentRunId)}${toolRunQuery}`,
  );
}

async function previewAction(formData: FormData) {
  "use server";

  const artifactId = formData.get("artifactId");
  const versionId = formData.get("versionId");
  const agentKey = formData.get("agentKey");
  const queryText = formData.get("queryText");

  if (
    typeof artifactId !== "string" ||
    typeof versionId !== "string" ||
    typeof agentKey !== "string" ||
    artifactId.length === 0 ||
    versionId.length === 0
  ) {
    redirect("/agents?error=Agent%20context%20was%20missing.");
  }

  if (typeof queryText !== "string" || queryText.trim().length === 0) {
    redirect(
      `/agents/${encodeURIComponent(agentKey)}/test-run?error=${encodeURIComponent("Query text is required.")}`,
    );
  }

  const result = await postAgentPreview(artifactId, versionId, { queryText: queryText.trim() });
  if (result.error || !result.data) {
    redirect(
      `/agents/${encodeURIComponent(agentKey)}/test-run?error=${encodeURIComponent(result.error ?? "Preview failed.")}`,
    );
  }

  revalidatePath("/agent-runs");
  const toolRunQuery =
    result.data.toolRunIds.length > 0
      ? `&toolRunIds=${result.data.toolRunIds.map((id) => encodeURIComponent(id)).join(",")}`
      : "";
  redirect(
    `/agents/${encodeURIComponent(agentKey)}/test-run?runId=${encodeURIComponent(result.data.agentRunId)}${toolRunQuery}`,
  );
}

export default async function AgentTestRunPage({ params, searchParams }: PageProps) {
  const { agentKey } = await params;
  const { error, runId, toolRunIds, versionId } = await searchParams;
  const decodedKey = decodeURIComponent(agentKey);
  const loaded = await loadAgentVersionByKey(decodedKey, versionId);
  const linkedToolRunIds = toolRunIds
    ? toolRunIds.split(",").map((id) => id.trim()).filter((id) => id.length > 0)
    : [];

  if (!loaded.data) {
    return (
      <main className="min-h-screen bg-slate-950 px-6 py-10 text-slate-100">
        <div className="mx-auto max-w-3xl rounded-3xl border border-amber-500/30 bg-amber-500/10 p-6 text-sm text-amber-100">
          {loaded.error ?? "Agent was not found."}
        </div>
      </main>
    );
  }

  const { artifactId, versionId: selectedVersionId, detail } = loaded.data;

  return (
    <main className="min-h-screen bg-slate-950 px-6 py-10 text-slate-100">
      <div className="mx-auto flex max-w-6xl flex-col gap-8">
        <section className="rounded-3xl border border-slate-800 bg-slate-900 p-8">
          <div className="flex flex-wrap items-start justify-between gap-4">
            <div>
              <p className="text-sm uppercase tracking-wide text-cyan-300">Issue 23 · Test run</p>
              <h1 className="mt-2 text-4xl font-semibold">{detail.displayName}</h1>
              <p className="mt-3 text-slate-400">
                {detail.agentKey} · {detail.versionLabel} · draft test trigger with governed dry-run boundaries
              </p>
            </div>
            <div className="flex flex-wrap gap-3">
              <ExplorerNavLink href={`/agents/${encodeURIComponent(detail.agentKey)}/configure`}>
                Configure
              </ExplorerNavLink>
              <ExplorerNavLink href="/agents">Agents</ExplorerNavLink>
            </div>
          </div>
        </section>

        {error ? (
          <div className="rounded-2xl border border-amber-500/30 bg-amber-500/10 p-4 text-sm text-amber-100">
            {error}
          </div>
        ) : null}

        {detail.safeModeEnabled ? (
          <div className="rounded-2xl border border-amber-500/30 bg-amber-500/10 p-4 text-sm text-amber-100">
            Safe mode is enabled. Non-preview runs may be blocked until safe mode is disabled in a future admin flow.
          </div>
        ) : null}

        <section className="rounded-3xl border border-slate-800 bg-slate-900 p-6">
          <h2 className="text-2xl font-semibold">Trigger draft test</h2>
          <form className="mt-6 space-y-4">
            <input type="hidden" name="artifactId" value={artifactId} />
            <input type="hidden" name="versionId" value={selectedVersionId} />
            <input type="hidden" name="agentKey" value={detail.agentKey} />
            <label className="block text-sm">
              <span className="font-semibold text-slate-300">Query text</span>
              <textarea
                name="queryText"
                required
                rows={4}
                placeholder="Investigate BOM discrepancies for assembly A-100."
                className="mt-2 w-full rounded-2xl border border-slate-700 bg-slate-950 px-4 py-3 text-slate-100"
              />
            </label>
            <div className="flex flex-wrap gap-3">
              <button
                formAction={previewAction}
                type="submit"
                className="rounded-2xl border border-slate-700 px-4 py-2 text-sm font-semibold text-slate-200 transition hover:border-cyan-300 hover:text-cyan-100"
              >
                Preview
              </button>
              <button
                formAction={testRunAction}
                type="submit"
                className="rounded-2xl border border-cyan-500/40 bg-cyan-500/10 px-4 py-2 text-sm font-semibold text-cyan-100 transition hover:border-cyan-300"
              >
                Test run (dry-run)
              </button>
            </div>
          </form>
        </section>

        {runId ? (
          <section className="rounded-3xl border border-slate-800 bg-slate-900 p-6">
            <h2 className="text-2xl font-semibold">Latest run links</h2>
            <ul className="mt-4 space-y-2 text-sm text-slate-300">
              <li>
                Agent run:{" "}
                <Link href={`/agent-runs/${runId}`} className="text-cyan-300 hover:text-cyan-100">
                  {runId}
                </Link>
              </li>
              {linkedToolRunIds.map((toolRunId) => (
                <li key={toolRunId}>
                  Tool run:{" "}
                  <Link href={`/tool-runs/${toolRunId}`} className="text-cyan-300 hover:text-cyan-100">
                    {toolRunId}
                  </Link>
                </li>
              ))}
              <li>
                Explorer:{" "}
                <Link href="/agent-runs" className="text-cyan-300 hover:text-cyan-100">
                  All agent runs
                </Link>
              </li>
              <li>
                AI traces:{" "}
                <Link href="/ai-traces" className="text-cyan-300 hover:text-cyan-100">
                  Trace explorer
                </Link>
              </li>
            </ul>
          </section>
        ) : null}
      </div>
    </main>
  );
}
