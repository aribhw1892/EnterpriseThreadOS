import type { AgentVersionDetail } from "@/lib/etos-api";
import { AgentModelConfigForm } from "@/components/agents/AgentModelConfigForm";

type AgentModelConfigPanelProps = {
  artifactId: string;
  versionId: string;
  agentKey: string;
  detail: AgentVersionDetail;
  errorMessage?: string | null;
};

export function AgentModelConfigPanel({
  artifactId,
  versionId,
  agentKey,
  detail,
  errorMessage,
}: AgentModelConfigPanelProps) {
  return (
    <section className="rounded-3xl border border-slate-800 bg-slate-900 p-6">
      <h2 className="text-2xl font-semibold">Model routing</h2>
      <p className="mt-2 text-sm text-slate-400">
        Runtime adapter: {detail.preferredRuntimeAdapterKey} · Current readiness:{" "}
        {detail.artifactReadinessState}
      </p>
      <div className="mt-6">
        <AgentModelConfigForm
          artifactId={artifactId}
          versionId={versionId}
          agentKey={agentKey}
          readinessState={detail.artifactReadinessState}
          primaryModelProviderKey={detail.primaryModelProviderKey}
          primaryModelId={detail.primaryModelId}
          fallbackModels={detail.fallbackModels}
          errorMessage={errorMessage}
        />
      </div>
    </section>
  );
}
