"use client";

import { useMemo, useState } from "react";
import type { AgentFallbackModel } from "@/lib/etos-api";
import {
  markAgentReadyAction,
  publishAgentAction,
  saveAgentModelConfigAction,
} from "@/components/agents/agent-configure-actions";

const PROVIDER_OPTIONS = ["openai", "openai-compatible", "openai-v1"] as const;

type AgentModelConfigFormProps = {
  artifactId: string;
  versionId: string;
  agentKey: string;
  readinessState: string;
  primaryModelProviderKey: string;
  primaryModelId: string;
  fallbackModels: AgentFallbackModel[];
  errorMessage?: string | null;
};

type FallbackRow = AgentFallbackModel & { key: string };

function readinessIncludes(state: string, token: string): boolean {
  return state.toLowerCase().includes(token.toLowerCase());
}

function toRows(models: AgentFallbackModel[]): FallbackRow[] {
  return models.map((model, index) => ({
    ...model,
    key: `${model.providerKey}-${model.modelId}-${index}`,
  }));
}

function emptyRow(): FallbackRow {
  return {
    key: `new-${Date.now()}`,
    providerKey: "openai",
    modelId: "",
    triggerReason: "",
  };
}

export function AgentModelConfigForm({
  artifactId,
  versionId,
  agentKey,
  readinessState,
  primaryModelProviderKey,
  primaryModelId,
  fallbackModels,
  errorMessage,
}: AgentModelConfigFormProps) {
  const [providerKey, setProviderKey] = useState(primaryModelProviderKey);
  const [modelId, setModelId] = useState(primaryModelId);
  const [rows, setRows] = useState<FallbackRow[]>(() => toRows(fallbackModels));

  const fallbackModelsJson = useMemo(
    () =>
      JSON.stringify(
        rows
          .filter(
            (row) =>
              row.providerKey.trim().length > 0 &&
              row.modelId.trim().length > 0 &&
              row.triggerReason.trim().length > 0,
          )
          .map((row) => ({
            providerKey: row.providerKey.trim(),
            modelId: row.modelId.trim(),
            triggerReason: row.triggerReason.trim(),
          })),
      ),
    [rows],
  );

  const isDraft = readinessIncludes(readinessState, "draft");
  const isReady = readinessIncludes(readinessState, "ready");
  const isPublished = readinessIncludes(readinessState, "published");

  return (
    <div className="space-y-6">
      {errorMessage ? (
        <p className="rounded-xl border border-amber-500/30 bg-amber-500/10 px-3 py-2 text-sm text-amber-100">
          {errorMessage}
        </p>
      ) : null}

      <p className="text-sm text-slate-400">
        OpenAI cloud uses provider <code className="text-cyan-200">openai</code> with{" "}
        <code className="text-cyan-200">OPENAI_API_KEY</code>. LM Studio uses{" "}
        <code className="text-cyan-200">openai-compatible</code> with{" "}
        <code className="text-cyan-200">OPENAI_BASE_URL</code> in your local <code className="text-cyan-200">.env</code>{" "}
        (see <span className="text-slate-300">docs/local-development.md</span>).
      </p>

      <form action={saveAgentModelConfigAction} className="space-y-4">
        <input type="hidden" name="artifactId" value={artifactId} />
        <input type="hidden" name="versionId" value={versionId} />
        <input type="hidden" name="agentKey" value={agentKey} />
        <input type="hidden" name="fallbackModelsJson" value={fallbackModelsJson} />

        <div className="grid gap-4 md:grid-cols-2">
          <label className="block text-sm">
            <span className="font-semibold text-slate-300">Primary model provider</span>
            <select
              name="primaryModelProviderKey"
              required
              value={providerKey}
              onChange={(event) => setProviderKey(event.target.value)}
              className="mt-2 w-full rounded-2xl border border-slate-700 bg-slate-950 px-4 py-3 text-slate-100"
            >
              {PROVIDER_OPTIONS.map((option) => (
                <option key={option} value={option}>
                  {option}
                </option>
              ))}
            </select>
          </label>
          <label className="block text-sm">
            <span className="font-semibold text-slate-300">Primary model id</span>
            <input
              name="primaryModelId"
              type="text"
              required
              value={modelId}
              onChange={(event) => setModelId(event.target.value)}
              placeholder="gpt-4o-mini"
              className="mt-2 w-full rounded-2xl border border-slate-700 bg-slate-950 px-4 py-3 text-slate-100"
            />
          </label>
        </div>

        <div>
          <div className="flex items-center justify-between gap-3">
            <h3 className="text-sm font-semibold text-slate-300">Fallback models</h3>
            <button
              type="button"
              onClick={() => setRows((current) => [...current, emptyRow()])}
              className="rounded-xl border border-slate-700 px-3 py-1 text-xs font-semibold text-slate-200 transition hover:border-cyan-300 hover:text-cyan-100"
            >
              Add fallback
            </button>
          </div>
          {rows.length === 0 ? (
            <p className="mt-3 text-sm text-slate-500">No fallback models configured.</p>
          ) : (
            <ul className="mt-3 space-y-3">
              {rows.map((row, index) => (
                <li
                  key={row.key}
                  className="grid gap-3 rounded-2xl border border-slate-800 bg-slate-950 p-4 md:grid-cols-[1fr_1fr_1fr_auto]"
                >
                  <label className="block text-xs">
                    <span className="font-semibold text-slate-400">Provider</span>
                    <select
                      value={row.providerKey}
                      onChange={(event) =>
                        setRows((current) =>
                          current.map((item, itemIndex) =>
                            itemIndex === index ? { ...item, providerKey: event.target.value } : item,
                          ),
                        )
                      }
                      className="mt-1 w-full rounded-xl border border-slate-700 bg-slate-900 px-3 py-2 text-sm text-slate-100"
                    >
                      {PROVIDER_OPTIONS.map((option) => (
                        <option key={option} value={option}>
                          {option}
                        </option>
                      ))}
                    </select>
                  </label>
                  <label className="block text-xs">
                    <span className="font-semibold text-slate-400">Model id</span>
                    <input
                      type="text"
                      value={row.modelId}
                      onChange={(event) =>
                        setRows((current) =>
                          current.map((item, itemIndex) =>
                            itemIndex === index ? { ...item, modelId: event.target.value } : item,
                          ),
                        )
                      }
                      className="mt-1 w-full rounded-xl border border-slate-700 bg-slate-900 px-3 py-2 text-sm text-slate-100"
                    />
                  </label>
                  <label className="block text-xs">
                    <span className="font-semibold text-slate-400">Trigger reason</span>
                    <input
                      type="text"
                      value={row.triggerReason}
                      onChange={(event) =>
                        setRows((current) =>
                          current.map((item, itemIndex) =>
                            itemIndex === index ? { ...item, triggerReason: event.target.value } : item,
                          ),
                        )
                      }
                      className="mt-1 w-full rounded-xl border border-slate-700 bg-slate-900 px-3 py-2 text-sm text-slate-100"
                    />
                  </label>
                  <button
                    type="button"
                    onClick={() => setRows((current) => current.filter((_, itemIndex) => itemIndex !== index))}
                    className="self-end rounded-xl border border-rose-500/40 px-3 py-2 text-xs font-semibold text-rose-100 transition hover:bg-rose-500/10"
                  >
                    Remove
                  </button>
                </li>
              ))}
            </ul>
          )}
        </div>

        <button
          type="submit"
          className="rounded-2xl border border-cyan-500/40 bg-cyan-500/10 px-4 py-2 text-sm font-semibold text-cyan-100 transition hover:border-cyan-300"
        >
          Save model config
        </button>
      </form>

      <div className="border-t border-slate-800 pt-4">
        <h3 className="text-sm font-semibold text-slate-300">Version lifecycle</h3>
        {isPublished ? (
          <p className="mt-2 text-sm text-slate-500">
            This version is published. Save model changes to create a draft version, then mark ready and publish.
          </p>
        ) : null}

        <div className="mt-4 flex flex-wrap gap-3">
          {isDraft ? (
            <form action={markAgentReadyAction}>
              <input type="hidden" name="artifactId" value={artifactId} />
              <input type="hidden" name="versionId" value={versionId} />
              <input type="hidden" name="agentKey" value={agentKey} />
              <button
                type="submit"
                className="rounded-2xl border border-slate-700 px-4 py-2 text-sm font-semibold text-slate-200 transition hover:border-cyan-300 hover:text-cyan-100"
              >
                Mark ready
              </button>
            </form>
          ) : null}

          {isReady ? (
            <form action={publishAgentAction}>
              <input type="hidden" name="artifactId" value={artifactId} />
              <input type="hidden" name="versionId" value={versionId} />
              <input type="hidden" name="agentKey" value={agentKey} />
              <button
                type="submit"
                className="rounded-2xl border border-emerald-500/40 bg-emerald-500/10 px-4 py-2 text-sm font-semibold text-emerald-100 transition hover:border-emerald-300"
              >
                Publish
              </button>
            </form>
          ) : null}
        </div>
      </div>
    </div>
  );
}
