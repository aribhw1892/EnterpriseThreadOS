import { PlaceholderPage } from "@/components/placeholders/PlaceholderPage";

export default function AgentTeamsPage() {
  return (
    <PlaceholderPage
      title="Agent Teams"
      description="Multi-agent teams with coordinator synthesis, delegation rules, and consensus tracking. Backend AgentTeamVersion and AgentTeamRun records are deferred until after the MVP demonstration (Issue 25)."
      issueBlocker="Issue 25"
      mockupSrc="/mockups/35-agent-team-builder.png"
      mockupAlt="Agent team builder mockup (35)"
      primaryAction={{
        label: "Create agent team",
        reason: "Requires Issue 25 multi-agent team backend",
      }}
    />
  );
}
