import {
  ApiResult,
  GovernedChatAnchor,
  GovernedChatSessionSummary,
  GovernedChatTurn,
  adminUserId,
  askGovernedChatTurn,
  createGovernedChatSession,
  getGovernedChatLists,
  getGovernedChatSession,
  getGovernedChatTurn,
  resolveGovernedChatAnchor,
  selectedTenantId,
} from "@/lib/etos-api";
import { Badge } from "@/components/ui/Badge";
import { Button } from "@/components/ui/Button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/Card";
import { EmptyState } from "@/components/ui/EmptyState";
import { ErrorState } from "@/components/ui/ErrorState";
import { ListItem, ListStack } from "@/components/ui/ListItem";
import { PageHeader } from "@/components/ui/PageHeader";
import { PillStack, SidePanel } from "@/components/ui/SidePanel";
import Link from "next/link";
import { revalidatePath } from "next/cache";
import { redirect } from "next/navigation";

export const dynamic = "force-dynamic";

async function createSessionAction() {
  "use server";

  const anchor = await resolveGovernedChatAnchor();
  if (anchor.error || !anchor.data) {
    redirect(
      `/chat?error=${encodeURIComponent(anchor.error ?? "Could not resolve a governed chat anchor.")}`,
    );
  }

  const result = await createGovernedChatSession(
    "Frontend governed chat",
    anchor.data,
  );
  if (result.error) {
    redirect(`/chat?error=${encodeURIComponent(result.error)}`);
  }

  revalidatePath("/chat");
  redirect("/chat");
}

async function askTurnAction(formData: FormData) {
  "use server";

  const sessionId = formData.get("sessionId");
  const message = formData.get("message");
  const intentKey = formData.get("intentKey");
  const draftKind = formData.get("draftArtifactKind");

  if (typeof sessionId !== "string" || sessionId.length === 0) {
    redirect("/chat?error=Active%20session%20was%20not%20found.");
  }

  if (typeof message !== "string" || message.trim().length === 0) {
    redirect("/chat?error=Chat%20message%20is%20required.");
  }

  const draftArtifactKind =
    draftKind === "QueryIntent" ||
    draftKind === "Dashboard" ||
    draftKind === "Report" ||
    draftKind === "Recommendation"
      ? draftKind
      : undefined;

  const anchor = await resolveGovernedChatAnchor();
  if (anchor.error || !anchor.data) {
    redirect(
      `/chat?error=${encodeURIComponent(anchor.error ?? "Could not resolve a governed chat anchor.")}`,
    );
  }

  const result = await askGovernedChatTurn(
    sessionId,
    message.trim(),
    typeof intentKey === "string" && intentKey.length > 0
      ? intentKey
      : anchor.data.defaultIntentKey,
    draftArtifactKind,
    anchor.data,
  );
  if (result.error) {
    redirect(`/chat?error=${encodeURIComponent(result.error)}`);
  }

  revalidatePath("/chat");
  redirect("/chat");
}

function renderApiError(result: ApiResult<unknown>) {
  return result.error ? <ErrorState error={result.error} /> : null;
}

function anchorHint(anchor: GovernedChatAnchor | null): string {
  if (!anchor) {
    return "No trusted graph node or document anchor is available yet.";
  }

  if (anchor.startGraphNodeId) {
    return `Using trusted graph node ${anchor.startGraphNodeId}. Default intent: ${anchor.defaultIntentKey}.`;
  }

  return `No trusted graph nodes yet. Using document ${anchor.documentArtifactId}. Choose document-evidence-context or promote an import on /imports for object-360-context.`;
}

async function loadLatestTurn(session: GovernedChatSessionSummary): Promise<{
  turn: ApiResult<GovernedChatTurn>;
  userMessage: string | null;
  intentKey: string | null;
}> {
  if (session.turnCount === 0) {
    return { turn: { data: null, error: null }, userMessage: null, intentKey: null };
  }

  const detail = await getGovernedChatSession(session.id);
  if (!detail.data || detail.data.turns.length === 0) {
    return {
      turn: { data: null, error: detail.error },
      userMessage: null,
      intentKey: null,
    };
  }

  const latest = detail.data.turns[0];
  const turn = await getGovernedChatTurn(latest.id);
  return {
    turn,
    userMessage: latest.userMessage,
    intentKey: null,
  };
}

type PageProps = {
  searchParams: Promise<{ error?: string }>;
};

export default async function ChatPage({ searchParams }: PageProps) {
  const { error: actionError } = await searchParams;
  const { sessions } = await getGovernedChatLists();
  const anchor = await resolveGovernedChatAnchor();
  const activeSession = sessions.data?.[0] ?? null;
  const latest = activeSession
    ? await loadLatestTurn(activeSession)
    : { turn: { data: null, error: null }, userMessage: null, intentKey: null };
  const defaultIntent = anchor.data?.defaultIntentKey ?? "object-360-context";
  const turn = latest.turn.data;
  const userMessage = latest.userMessage;

  return (
    <main className="px-6 py-8 lg:px-8">
      <PageHeader
        title="Governed chat over digital thread"
        description="Natural-language query over trusted graph and document context with evidence, confidence, and AI Trace links."
      />

      {actionError ? <ErrorState error={actionError} /> : null}
      {renderApiError(sessions)}
      {renderApiError(anchor)}
      {renderApiError(latest.turn)}

      <div className="grid gap-4 lg:grid-cols-[minmax(0,1.2fr)_minmax(280px,0.8fr)]">
        <Card>
          <CardHeader>
            <CardTitle>Conversation</CardTitle>
          </CardHeader>
          <CardContent className="space-y-4">
            {turn ? (
              <ListStack>
                {userMessage ? (
                  <ListItem
                    index="U"
                    title={userMessage}
                    description="User question routed to approved query intent."
                  />
                ) : null}
                <div className="rounded-[14px] border border-etos-border-soft bg-etos-panel-muted p-3">
                  <div className="flex items-start gap-3">
                    <div className="flex h-[34px] w-[34px] shrink-0 items-center justify-center rounded-xl bg-etos-info-bg text-sm font-black text-etos-info-fg">
                      AI
                    </div>
                    <div className="min-w-0 flex-1">
                      <p className="text-[13px] font-extrabold text-etos-ink">
                        {turn.assistantSafeSummary}
                      </p>
                      <p className="mt-1 text-xs leading-snug text-etos-ink-muted">
                        Retrieved {turn.confidence.retrievalCount} · Confidence{" "}
                        {Math.round(turn.confidence.overall * 100)}%
                      </p>
                      <div className="mt-2 flex flex-wrap gap-2">
                        {turn.confidence.retrievalCount > 0 ? (
                          <Badge variant="success">Graph → Docs → LLM</Badge>
                        ) : null}
                        {turn.aiTraceRecordId ? (
                          <Link href={`/ai-traces/${turn.aiTraceRecordId}`}>
                            <Badge variant="info">
                              Trace #{turn.aiTraceRecordId.slice(0, 6)}
                            </Badge>
                          </Link>
                        ) : null}
                        {turn.confidence.deniedCount > 0 ? (
                          <Badge variant="warning">
                            {turn.confidence.deniedCount} denied context
                          </Badge>
                        ) : null}
                      </div>
                    </div>
                  </div>
                </div>
              </ListStack>
            ) : (
              <EmptyState message="Create a session and send a turn to start governed chat." />
            )}

            {activeSession ? (
              <form action={askTurnAction} className="grid gap-3 border-t border-etos-border pt-4">
                <input type="hidden" name="sessionId" value={activeSession.id} />
                <input type="hidden" name="intentKey" value={defaultIntent} />
                <label className="grid gap-2 text-sm">
                  <span className="text-xs font-semibold uppercase tracking-wide text-etos-ink-muted">
                    Ask follow-up
                  </span>
                  <textarea
                    name="message"
                    rows={3}
                    required
                    className="rounded-etos-card border border-etos-border bg-etos-panel px-4 py-3 text-etos-ink"
                    placeholder="Ask about BOM impact, evidence gaps, or draft a dashboard…"
                  />
                </label>
                <div className="flex flex-wrap gap-2">
                  <Button type="submit" variant="primary">
                    Send
                  </Button>
                </div>
              </form>
            ) : (
              <form action={createSessionAction}>
                <Button type="submit" variant="primary">
                  Start session
                </Button>
              </form>
            )}
          </CardContent>
        </Card>

        <aside className="lg:sticky lg:top-6 lg:self-start">
          {turn ? (
            <SidePanel title="Answer governance">
              <PillStack
                items={[
                  { label: "Query intent", value: defaultIntent, variant: "info" },
                  {
                    label: "Retrieval strategy",
                    value: "Graph → Docs → LLM",
                    variant: "success",
                  },
                  {
                    label: "Confidence",
                    value: `${Math.round(turn.confidence.overall * 100)}%`,
                    variant: turn.confidence.overall >= 0.85 ? "success" : "warning",
                  },
                  {
                    label: "Evidence visibility",
                    value: "Filtered before LLM",
                    variant: "success",
                  },
                ]}
              />
              <div className="mt-4 h-px bg-etos-border" />
              {activeSession ? (
                <div className="mt-4 flex flex-col gap-2">
                  <form action={askTurnAction}>
                    <input type="hidden" name="sessionId" value={activeSession.id} />
                    <input type="hidden" name="intentKey" value={defaultIntent} />
                    <input
                      type="hidden"
                      name="message"
                      value="Create a dashboard draft summarizing the recurring gaps from this conversation."
                    />
                    <Button
                      type="submit"
                      name="draftArtifactKind"
                      value="Dashboard"
                      variant="primary"
                      className="w-full"
                    >
                      Create dashboard draft
                    </Button>
                  </form>
                  <form action={askTurnAction}>
                    <input type="hidden" name="sessionId" value={activeSession.id} />
                    <input type="hidden" name="intentKey" value={defaultIntent} />
                    <input
                      type="hidden"
                      name="message"
                      value="Create a recommendation from the evidence in this conversation."
                    />
                    <Button
                      type="submit"
                      name="draftArtifactKind"
                      value="Recommendation"
                      variant="ghost"
                      className="w-full"
                    >
                      Create recommendation
                    </Button>
                  </form>
                  {turn.draftArtifact ? (
                    <Link
                      href={
                        turn.draftArtifact.artifactType.toLowerCase().includes("dashboard")
                          ? `/dashboards/${turn.draftArtifact.artifactId}`
                          : turn.draftArtifact.artifactType
                                .toLowerCase()
                                .includes("recommend")
                            ? `/recommendations/${turn.draftArtifact.artifactId}`
                            : `/artifacts/${turn.draftArtifact.artifactId}`
                      }
                      className="text-xs font-extrabold text-etos-accent-cyan underline-offset-2 hover:underline"
                    >
                      Open draft {turn.draftArtifact.artifactType} →
                    </Link>
                  ) : null}
                </div>
              ) : null}
            </SidePanel>
          ) : (
            <SidePanel title="Answer governance">
              <p className="text-sm text-etos-ink-muted">
                Send a turn to populate intent, retrieval, confidence, and draft CTAs.
              </p>
            </SidePanel>
          )}
        </aside>
      </div>

      <details className="mt-6 rounded-etos-card border border-etos-border bg-etos-panel-muted p-4 text-sm text-etos-ink-muted">
        <summary className="cursor-pointer font-extrabold text-etos-ink">
          Advanced / Debug
        </summary>
        <div className="mt-4 space-y-4 text-xs">
          <p>
            Tenant {selectedTenantId} · User {adminUserId}
          </p>
          <p>{anchorHint(anchor.data)}</p>
          <form action={createSessionAction}>
            <Button type="submit" variant="ghost">
              New session
            </Button>
          </form>
          {sessions.data && sessions.data.length > 0 ? (
            <div className="space-y-2">
              <p className="font-semibold text-etos-ink">Sessions</p>
              {sessions.data.map((session) => (
                <div
                  key={session.id}
                  className="rounded-etos-card border border-etos-border-soft bg-etos-panel p-3"
                >
                  <p className="font-semibold text-etos-ink">{session.title}</p>
                  <p className="text-etos-ink-muted">
                    {session.turnCount} turn(s) ·{" "}
                    {new Date(session.createdAt).toLocaleString()}
                  </p>
                </div>
              ))}
            </div>
          ) : null}
          {turn?.evidence && turn.evidence.length > 0 ? (
            <div>
              <p className="font-semibold text-etos-ink">Evidence dump</p>
              <ul className="mt-2 space-y-1">
                {turn.evidence.map((item) => (
                  <li key={item.contextId}>
                    {item.contextType}: {item.safeSummary}
                  </li>
                ))}
              </ul>
            </div>
          ) : null}
        </div>
      </details>
    </main>
  );
}
