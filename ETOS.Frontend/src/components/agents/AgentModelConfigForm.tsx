"use client";

import { useMemo, useState } from "react";
import type { AgentFallbackModel } from "@/lib/etos-api";
import {
  markAgentReadyAction,
  publishAgentAction,
  saveAgentModelConfigAction,
} from "@/components/agents/agent-configure-actions";
import { Button } from "@/components/ui/Button";
import { Notice } from "@/components/ui/Notice";

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

const fieldClass =
  "mt-2 w-full rounded-xl border border-etos-border bg-etos-panel px-3.5 py-2.5 text-sm text-etos-ink";

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
      {errorMessage ? <Notice variant="danger">{errorMessage}</Notice> : null}

      <p className="text-sm text-etos-ink-muted">
        OpenAI cloud uses provider <code className="font-mono text-etos-accent">openai</code> with{" "}
        <code className="font-mono text-etos-accent">OPENAI_API_KEY</code>. LM Studio uses{" "}
        <code className="font-mono text-etos-accent">openai-compatible</code> with{" "}
        <code className="font-mono text-etos-accent">OPENAI_BASE_URL</code> in local{" "}
        <code className="font-mono text-etos-accent">.env</code>.
      </p>

      <form action={saveAgentModelConfigAction} className="space-y-4">
        <input type="hidden" name="artifactId" value={artifactId} />
        <input type="hidden" name="versionId" value={versionId} />
        <input type="hidden" name="agentKey" value={agentKey} />
        <input type="hidden" name="fallbackModelsJson" value={fallbackModelsJson} />

        <div className="grid gap-4 md:grid-cols-2">
          <label className="block text-sm">
            <span className="font-semibold text-etos-ink">Primary model provider</span>
            <select
              name="primaryModelProviderKey"
              required
              value={providerKey}
              onChange={(event) => setProviderKey(event.target.value)}
              className={fieldClass}
            >
              {PROVIDER_OPTIONS.map((option) => (
                <option key={option} value={option}>
                  {option}
                </option>
              ))}
            </select>
          </label>
          <label className="block text-sm">
            <span className="font-semibold text-etos-ink">Primary model id</span>
            <input
              name="primaryModelId"
              type="text"
              required
              value={modelId}
              onChange={(event) => setModelId(event.target.value)}
              placeholder="gpt-4o-mini"
              className={fieldClass}
            />
          </label>
        </div>

        <div>
          <div className="flex items-center justify-between gap-3">
            <h3 className="text-sm font-semibold text-etos-ink">Fallback models</h3>
            <Button type="button" variant="ghost" onClick={() => setRows((current) => [...current, emptyRow()])}>
              Add fallback
            </Button>
          </div>
          {rows.length === 0 ? (
            <p className="mt-3 text-sm text-etos-ink-muted">No fallback models configured.</p>
          ) : (
            <ul className="mt-3 space-y-3">
              {rows.map((row, index) => (
                <li
                  key={row.key}
                  className="grid gap-3 rounded-etos-card border border-etos-border bg-etos-panel-muted p-4 md:grid-cols-[1fr_1fr_1fr_auto]"
                >
                  <label className="block text-xs">
                    <span className="font-semibold text-etos-ink-muted">Provider</span>
                    <select
                      value={row.providerKey}
                      onChange={(event) =>
                        setRows((current) =>
                          current.map((item, itemIndex) =>
                            itemIndex === index ? { ...item, providerKey: event.target.value } : item,
                          ),
                        )
                      }
                      className="mt-1 w-full rounded-xl border border-etos-border bg-etos-panel px-3 py-2 text-sm text-etos-ink"
                    >
                      {PROVIDER_OPTIONS.map((option) => (
                        <option key={option} value={option}>
                          {option}
                        </option>
                      ))}
                    </select>
                  </label>
                  <label className="block text-xs">
                    <span className="font-semibold text-etos-ink-muted">Model id</span>
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
                      className="mt-1 w-full rounded-xl border border-etos-border bg-etos-panel px-3 py-2 text-sm text-etos-ink"
                    />
                  </label>
                  <label className="block text-xs">
                    <span className="font-semibold text-etos-ink-muted">Trigger reason</span>
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
                      className="mt-1 w-full rounded-xl border border-etos-border bg-etos-panel px-3 py-2 text-sm text-etos-ink"
                    />
                  </label>
                  <Button
                    type="button"
                    variant="ghost"
                    onClick={() => setRows((current) => current.filter((_, itemIndex) => itemIndex !== index))}
                  >
                    Remove
                  </Button>
                </li>
              ))}
            </ul>
          )}
        </div>

        <Button type="submit">Save model config</Button>
      </form>

      <div className="border-t border-etos-border pt-4">
        <h3 className="text-sm font-semibold text-etos-ink">Version lifecycle</h3>
        {isPublished ? (
          <p className="mt-2 text-sm text-etos-ink-muted">
            This version is published. Save model changes to create a draft version, then mark ready and publish.
          </p>
        ) : null}

        <div className="mt-4 flex flex-wrap gap-3">
          {isDraft ? (
            <form action={markAgentReadyAction}>
              <input type="hidden" name="artifactId" value={artifactId} />
              <input type="hidden" name="versionId" value={versionId} />
              <input type="hidden" name="agentKey" value={agentKey} />
              <Button type="submit" variant="ghost">
                Mark ready
              </Button>
            </form>
          ) : null}

          {isReady ? (
            <form action={publishAgentAction}>
              <input type="hidden" name="artifactId" value={artifactId} />
              <input type="hidden" name="versionId" value={versionId} />
              <input type="hidden" name="agentKey" value={agentKey} />
              <Button type="submit">Publish</Button>
            </form>
          ) : null}
        </div>
      </div>
    </div>
  );
}
