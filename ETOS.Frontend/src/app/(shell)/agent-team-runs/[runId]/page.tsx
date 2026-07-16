import { PlaceholderPage } from "@/components/placeholders/PlaceholderPage";

type PageProps = {
  params: Promise<{ runId: string }>;
};

export default async function AgentTeamRunPage({ params }: PageProps) {
  const { runId } = await params;

  return (
    <PlaceholderPage
      title={`Agent team run · ${runId.slice(0, 8)}…`}
      description="AgentTeamRun detail for multi-agent team executions. Blocked until Issue 25 lands AgentTeamVersion orchestration and team-run persistence."
      issueBlocker="Issue 25"
      mockupSrc="/mockups/35-agent-team-builder.png"
      mockupAlt="Agent team run mockup preview (36 deferred — showing team builder thumb)"
      primaryAction={{
        label: "Replay team run",
        reason: "Requires Issue 25 AgentTeamRun APIs",
      }}
    />
  );
}
