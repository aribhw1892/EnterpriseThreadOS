import type { AgentVersionDetail } from "@/lib/etos-api";
import { AgentModelConfigForm } from "@/components/agents/AgentModelConfigForm";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/Card";

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
    <Card>
      <CardHeader>
        <CardTitle>Model routing</CardTitle>
      </CardHeader>
      <CardContent>
        <p className="mb-4 text-sm text-etos-ink-muted">
          Runtime adapter: {detail.preferredRuntimeAdapterKey} · Current readiness:{" "}
          {detail.artifactReadinessState}
        </p>
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
      </CardContent>
    </Card>
  );
}
