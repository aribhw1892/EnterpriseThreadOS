"use client";

import type { AgentVersionArtifactSummary, ImportPreview } from "@/lib/etos-api";
import { getAgentDefinitionArtifacts } from "@/lib/etos-api";
import Link from "next/link";
import { useEffect, useState } from "react";

type MappingAgentDebugPanelProps = {
  batchId?: string | null;
  evidenceId?: string | null;
  runPreview: (input: {
    batchId: string;
    evidenceId?: string | null;
    suggestionProviderKey: string;
    mappingAssistantAgentKey?: string | null;
  }) => Promise<{ preview: ImportPreview | null; error: string | null }>;
};

function formatJson(value: string | null | undefined): string {
  if (!value) {
    return "";
  }

  try {
    return JSON.stringify(JSON.parse(value), null, 2);
  } catch {
    return value;
  }
}

function DebugBlock({ title, value }: { title: string; value: string | null | undefined }) {
  if (!value) {
    return null;
  }

  return (
    <details className="rounded-xl border border-slate-800 bg-slate-950 p-4">
      <summary className="cursor-pointer text-sm font-semibold text-cyan-200">{title}</summary>
      <pre className="mt-3 max-h-80 overflow-auto whitespace-pre-wrap break-all text-xs text-slate-300">
        {formatJson(value)}
      </pre>
    </details>
  );
}

function StatusPill({ label, tone }: { label: string; tone: "ok" | "warn" | "error" | "neutral" }) {
  const toneClass =
    tone === "ok"
      ? "border-emerald-500/40 bg-emerald-500/10 text-emerald-200"
      : tone === "warn"
        ? "border-amber-500/40 bg-amber-500/10 text-amber-200"
        : tone === "error"
          ? "border-rose-500/40 bg-rose-500/10 text-rose-200"
          : "border-slate-700 bg-slate-900 text-slate-300";

  return <span className={`rounded-full border px-3 py-1 text-xs font-medium ${toneClass}`}>{label}</span>;
}

export function MappingAgentDebugPanel({ batchId, evidenceId, runPreview }: MappingAgentDebugPanelProps) {
  const [providerKey, setProviderKey] = useState("pydantic-ai-v1");
  const [mappingAgentKey, setMappingAgentKey] = useState("");
  const [mappingAgents, setMappingAgents] = useState<AgentVersionArtifactSummary[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [preview, setPreview] = useState<ImportPreview | null>(null);

  useEffect(() => {
    void getAgentDefinitionArtifacts().then((result) => {
      if (!result.data) {
        return;
      }

      const candidates = result.data.filter(
        (agent) =>
          agent.readinessState?.toLowerCase() === "published" &&
          (agent.agentKey?.includes("mapping") ||
            agent.displayName?.toLowerCase().includes("mapping") ||
            agent.agentKey === "import-mapping-assistant"),
      );
      setMappingAgents(candidates);
      const defaultAgent = candidates.find((agent) => agent.agentKey === "import-mapping-assistant") ?? candidates[0];
      if (defaultAgent?.agentKey) {
        setMappingAgentKey(defaultAgent.agentKey);
      }
    });
  }, []);

  async function handleRun() {
    if (!batchId) {
      setError("Create an import batch with uploaded CSV evidence first.");
      return;
    }

    setLoading(true);
    setError(null);
    try {
      const result = await runPreview({
        batchId,
        evidenceId,
        suggestionProviderKey: providerKey,
        mappingAssistantAgentKey: mappingAgentKey || null,
      });
      if (result.error) {
        setError(result.error);
        setPreview(null);
        return;
      }

      setPreview(result.preview);
    } finally {
      setLoading(false);
    }
  }

  const diagnostics = preview?.diagnostics;
  const resolvedAgentKey = diagnostics?.resolvedAgentKey ?? mappingAgentKey;
  const runtimeOk =
    diagnostics?.runtimeStatus?.toLowerCase() === "succeeded" ||
    (providerKey === "rule-based-v1" && !diagnostics?.runtimeCalled);

  return (
    <section className="rounded-3xl border border-violet-400/30 bg-violet-400/5 p-6">
      <h2 className="text-2xl font-semibold">Mapping Agent Debug</h2>
      <p className="mt-2 text-sm text-slate-300">
        Run mapping preview without saving a draft mapping version. Model routing comes from the published mapping
        assistant agent configuration, not appsettings.
      </p>

      <div className="mt-4 flex flex-wrap items-end gap-3">
        <label className="grid gap-1 text-sm text-slate-300">
          Provider
          <select
            className="rounded-lg border border-slate-700 bg-slate-950 px-3 py-2 text-sm"
            value={providerKey}
            onChange={(event) => setProviderKey(event.target.value)}
          >
            <option value="pydantic-ai-v1">pydantic-ai-v1 (LLM runtime)</option>
            <option value="rule-based-v1">rule-based-v1 (deterministic)</option>
          </select>
        </label>
        {providerKey === "pydantic-ai-v1" ? (
          <label className="grid gap-1 text-sm text-slate-300">
            Mapping assistant agent
            <select
              className="rounded-lg border border-slate-700 bg-slate-950 px-3 py-2 text-sm"
              value={mappingAgentKey}
              onChange={(event) => setMappingAgentKey(event.target.value)}
            >
              {mappingAgents.length === 0 ? (
                <option value="">import-mapping-assistant (default)</option>
              ) : (
                mappingAgents.map((agent) => (
                  <option key={agent.id} value={agent.agentKey ?? agent.name}>
                    {agent.displayName ?? agent.agentKey ?? agent.name}
                  </option>
                ))
              )}
            </select>
          </label>
        ) : null}
        <button
          type="button"
          className="rounded-lg bg-violet-500 px-4 py-2 text-sm font-semibold text-white disabled:opacity-50"
          disabled={loading || !batchId}
          onClick={() => void handleRun()}
        >
          {loading ? "Running preview..." : "Run mapping preview debug"}
        </button>
      </div>

      {resolvedAgentKey ? (
        <p className="mt-3 text-xs text-slate-400">
          Resolved agent:{" "}
          <Link className="text-cyan-300 underline" href={`/agents/${resolvedAgentKey}/configure`}>
            {resolvedAgentKey}
          </Link>
          {diagnostics?.primaryModelProviderKey ? (
            <>
              {" "}
              · {diagnostics.primaryModelProviderKey}/{diagnostics.primaryModelId}
            </>
          ) : null}
        </p>
      ) : null}

      {!batchId ? (
        <p className="mt-4 text-sm text-amber-200">
          No batch with evidence yet. Click Create CAD/PDM draft batch or upload CSV to an existing batch first.
        </p>
      ) : null}

      {error ? (
        <pre className="mt-4 overflow-auto rounded-xl border border-rose-500/40 bg-rose-500/10 p-4 text-xs text-rose-100">
          {error}
        </pre>
      ) : null}

      {preview ? (
        <div className="mt-6 grid gap-4">
          <div className="flex flex-wrap gap-2">
            <StatusPill label={`Provider: ${preview.suggestionProvider}`} tone="neutral" />
            {diagnostics ? (
              <>
                <StatusPill
                  label={diagnostics.runtimeCalled ? "Runtime called" : "Runtime not called"}
                  tone={diagnostics.runtimeCalled ? "ok" : "neutral"}
                />
                <StatusPill
                  label={
                    diagnostics.prefetchAttempted
                      ? diagnostics.prefetchSucceeded
                        ? "Prefetch OK"
                        : "Prefetch failed/skipped"
                      : "Prefetch off"
                  }
                  tone={
                    !diagnostics.prefetchAttempted
                      ? "neutral"
                      : diagnostics.prefetchSucceeded
                        ? "ok"
                        : "warn"
                  }
                />
                <StatusPill
                  label={runtimeOk ? "Runtime succeeded" : `Runtime: ${diagnostics.runtimeStatus ?? "n/a"}`}
                  tone={runtimeOk ? "ok" : diagnostics.usedRuleBasedFallback ? "warn" : "error"}
                />
                {diagnostics.modelUsed ? (
                  <StatusPill label={`Model: ${diagnostics.modelUsed}`} tone="neutral" />
                ) : null}
                {diagnostics.primaryModelProviderKey ? (
                  <StatusPill
                    label={`Config: ${diagnostics.primaryModelProviderKey}/${diagnostics.primaryModelId ?? "n/a"}`}
                    tone="neutral"
                  />
                ) : null}
                {diagnostics.usedRuleBasedFallback ? (
                  <StatusPill label="Rule-based fallback used" tone="warn" />
                ) : null}
              </>
            ) : null}
          </div>

          {diagnostics?.traceNotes?.length ? (
            <div className="rounded-xl border border-slate-800 bg-slate-950 p-4 text-xs text-slate-300">
              <p className="font-semibold text-cyan-200">Runtime trace notes</p>
              <ul className="mt-2 list-disc pl-5">
                {diagnostics.traceNotes.map((note) => (
                  <li key={note}>{note}</li>
                ))}
              </ul>
            </div>
          ) : null}

          {diagnostics?.errorMessage ? (
            <div className="rounded-xl border border-amber-500/40 bg-amber-500/10 p-4 text-sm text-amber-100">
              {diagnostics.errorMessage}
            </div>
          ) : null}

          <DebugBlock title="Prefetch tool output" value={diagnostics?.prefetchToolOutputJson} />
          <DebugBlock title="Tool summaries sent to runtime" value={diagnostics?.toolOutputSummariesJson} />
          <DebugBlock title="Governed ontology context" value={diagnostics?.governedContextJson} />
          <DebugBlock title="Structured CSV input" value={diagnostics?.structuredInputJson} />
          <DebugBlock title="Prompt template body" value={diagnostics?.promptTemplateBody} />
          <DebugBlock title="Runtime structured output" value={diagnostics?.runtimeStructuredOutputJson} />

          <details className="rounded-xl border border-slate-800 bg-slate-950 p-4" open>
            <summary className="cursor-pointer text-sm font-semibold text-cyan-200">Column suggestions</summary>
            <pre className="mt-3 max-h-80 overflow-auto whitespace-pre-wrap break-all text-xs text-slate-300">
              {JSON.stringify(preview.columnSuggestions, null, 2)}
            </pre>
          </details>

          <details className="rounded-xl border border-slate-800 bg-slate-950 p-4">
            <summary className="cursor-pointer text-sm font-semibold text-cyan-200">Lifecycle suggestions</summary>
            <pre className="mt-3 max-h-80 overflow-auto whitespace-pre-wrap break-all text-xs text-slate-300">
              {JSON.stringify(preview.lifecycleSuggestions, null, 2)}
            </pre>
          </details>
        </div>
      ) : null}
    </section>
  );
}
