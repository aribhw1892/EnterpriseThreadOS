import {
  ApiResult,
  GovernedChatSessionSummary,
  GovernedChatTurn,
  adminUserId,
  askGovernedChatTurn,
  createGovernedChatSession,
  getGovernedChatLists,
  getGovernedChatSession,
  getGovernedChatTurn,
  selectedTenantId,
} from "@/lib/etos-api";
import Link from "next/link";
import { draftArtifactDetailHref } from "@/lib/etos-api";
import { revalidatePath } from "next/cache";
import type { ReactNode } from "react";

export const dynamic = "force-dynamic";

async function createSessionAction() {
  "use server";

  await createGovernedChatSession("Frontend governed chat", "33333333-3333-3333-3333-333333333333");
  revalidatePath("/chat");
}

async function askTurnAction(formData: FormData) {
  "use server";

  const sessionId = formData.get("sessionId");
  const message = formData.get("message");
  const intentKey = formData.get("intentKey");
  const draftKind = formData.get("draftArtifactKind");

  if (typeof sessionId !== "string" || sessionId.length === 0) {
    return;
  }

  if (typeof message !== "string" || message.trim().length === 0) {
    return;
  }

  const draftArtifactKind =
    draftKind === "QueryIntent" || draftKind === "Dashboard" || draftKind === "Report"
      ? draftKind
      : undefined;

  await askGovernedChatTurn(
    sessionId,
    message.trim(),
    typeof intentKey === "string" && intentKey.length > 0 ? intentKey : "object-360-context",
    draftArtifactKind,
  );
  revalidatePath("/chat");
}

function ErrorState({ error }: { error: string }) {
  return (
    <div className="rounded-2xl border border-amber-500/30 bg-amber-500/10 p-4 text-sm text-amber-100">
      {error}
    </div>
  );
}

function EmptyState({ message }: { message: string }) {
  return (
    <div className="rounded-2xl border border-slate-800 bg-slate-950 p-4 text-sm text-slate-400">
      {message}
    </div>
  );
}

function SessionCard(session: GovernedChatSessionSummary) {
  return (
    <article key={session.id} className="rounded-2xl border border-slate-800 bg-slate-950 p-4">
      <h3 className="font-semibold">{session.title}</h3>
      <p className="mt-1 text-sm text-slate-400">
        {session.turnCount} turn(s) · {new Date(session.createdAt).toLocaleString()}
      </p>
      {session.startGraphNodeId ? (
        <p className="mt-2 font-mono text-xs text-slate-500">Anchor node: {session.startGraphNodeId}</p>
      ) : null}
    </article>
  );
}

function TurnPanel({ turn }: { turn: GovernedChatTurn }) {
  return (
    <section className="rounded-3xl border border-slate-800 bg-slate-900 p-6">
      <div className="mb-5">
        <h2 className="text-2xl font-semibold">Latest response</h2>
        <p className="mt-2 text-sm text-slate-300">{turn.assistantSafeSummary}</p>
      </div>

      <div className="grid gap-4 lg:grid-cols-2">
        <div className="rounded-2xl border border-slate-800 bg-slate-950 p-4">
          <h3 className="font-semibold">Evidence</h3>
          {turn.evidence.length > 0 ? (
            <ul className="mt-3 space-y-2 text-sm text-slate-300">
              {turn.evidence.map((item) => (
                <li key={item.contextId}>
                  <span className="text-xs uppercase text-slate-500">{item.contextType}</span>
                  <p>{item.safeSummary}</p>
                </li>
              ))}
            </ul>
          ) : (
            <p className="mt-3 text-sm text-slate-500">No evidence returned.</p>
          )}
        </div>
        <div className="rounded-2xl border border-slate-800 bg-slate-950 p-4">
          <h3 className="font-semibold">Confidence</h3>
          <ul className="mt-3 space-y-2 text-sm text-slate-300">
            <li>Overall: {turn.confidence.overall.toFixed(2)}</li>
            <li>Retrieved: {turn.confidence.retrievalCount}</li>
            <li>Allowed: {turn.confidence.allowedCount}</li>
            <li>Denied: {turn.confidence.deniedCount}</li>
            <li>Trust filtered: {turn.confidence.trustFilteredCount}</li>
            <li>Denied summaries: {turn.deniedSummaryCount}</li>
          </ul>
          <p className="mt-3 text-xs text-slate-500">{turn.confidence.notes}</p>
        </div>
      </div>

      <div className="mt-6 flex flex-wrap gap-3 text-sm">
        <Link
          href={`/ai-traces?traceId=${turn.aiTraceRecordId}`}
          className="rounded-full border border-cyan-300 px-4 py-2 font-semibold text-cyan-100 transition hover:bg-cyan-300 hover:text-slate-950"
        >
          View AI Trace
        </Link>
        {turn.draftArtifact ? (
          draftArtifactDetailHref(turn.draftArtifact.artifactType, turn.draftArtifact.artifactId) ? (
            <Link
              href={draftArtifactDetailHref(turn.draftArtifact.artifactType, turn.draftArtifact.artifactId)!}
              className="rounded-full border border-cyan-300 px-4 py-2 font-semibold text-cyan-100 transition hover:bg-cyan-300 hover:text-slate-950"
            >
              Open draft {turn.draftArtifact.artifactType} · {turn.draftArtifact.readinessState}
            </Link>
          ) : (
            <span className="rounded-full border border-slate-700 px-4 py-2 text-slate-300">
              Draft {turn.draftArtifact.artifactType} · {turn.draftArtifact.readinessState}
            </span>
          )
        ) : null}
      </div>
    </section>
  );
}

function ActionButton({ action, children }: { action: () => Promise<void>; children: ReactNode }) {
  return (
    <form action={action}>
      <button
        type="submit"
        className="rounded-2xl bg-cyan-300 px-5 py-3 text-sm font-semibold text-slate-950 transition hover:bg-cyan-200"
      >
        {children}
      </button>
    </form>
  );
}

function renderApiError(result: ApiResult<unknown>) {
  return result.error ? <ErrorState error={result.error} /> : null;
}

async function loadLatestTurn(session: GovernedChatSessionSummary): Promise<ApiResult<GovernedChatTurn>> {
  if (session.turnCount === 0) {
    return { data: null, error: null };
  }

  const detail = await getGovernedChatSession(session.id);
  if (!detail.data || detail.data.turns.length === 0) {
    return { data: null, error: detail.error };
  }

  return await getGovernedChatTurn(detail.data.turns[0].id);
}

export default async function ChatPage() {
  const { sessions } = await getGovernedChatLists();
  const activeSession = sessions.data?.[0] ?? null;
  const latestTurn = activeSession ? await loadLatestTurn(activeSession) : { data: null, error: null };

  return (
    <main className="min-h-screen bg-slate-950 text-slate-100">
      <div className="mx-auto flex max-w-6xl flex-col gap-6 px-6 py-10">
        <section className="rounded-3xl border border-slate-800 bg-slate-900 p-6">
          <div className="flex flex-wrap items-start justify-between gap-4">
            <div>
              <p className="text-sm uppercase tracking-[0.2em] text-cyan-300">Issue 15</p>
              <h1 className="mt-2 text-3xl font-semibold">Governed Chat</h1>
              <p className="mt-2 max-w-3xl text-sm text-slate-400">
                Ask natural-language questions over governed retrieval context, review evidence and confidence, link to AI
                Trace records, and optionally draft query intents, dashboards, or reports.
              </p>
              <p className="mt-2 text-xs text-slate-500">
                Tenant {selectedTenantId} · User {adminUserId}
              </p>
            </div>
            <div className="flex flex-wrap gap-3">
              <Link
                href="/"
                className="rounded-full border border-slate-700 px-4 py-2 text-sm font-semibold text-slate-200 transition hover:border-cyan-300 hover:text-cyan-100"
              >
                Home
              </Link>
              <ActionButton action={createSessionAction}>New session</ActionButton>
            </div>
          </div>
        </section>

        {renderApiError(sessions)}

        <section className="rounded-3xl border border-slate-800 bg-slate-900 p-6">
          <div className="mb-5">
            <h2 className="text-2xl font-semibold">Sessions</h2>
            <p className="mt-1 text-sm text-slate-400">Recent governed chat sessions for the active tenant.</p>
          </div>
          {sessions.data && sessions.data.length > 0 ? (
            <div className="grid gap-3">{sessions.data.map((session) => SessionCard(session))}</div>
          ) : (
            <EmptyState message="No chat sessions yet. Create one to start asking governed questions." />
          )}
        </section>

        {activeSession ? (
          <section className="rounded-3xl border border-slate-800 bg-slate-900 p-6">
            <div className="mb-5">
              <h2 className="text-2xl font-semibold">Ask</h2>
              <p className="mt-1 text-sm text-slate-400">Active session: {activeSession.title}</p>
            </div>
            <form action={askTurnAction} className="grid gap-4">
              <input type="hidden" name="sessionId" value={activeSession.id} />
              <label className="grid gap-2 text-sm">
                <span className="text-slate-400">Message</span>
                <textarea
                  name="message"
                  rows={3}
                  required
                  className="rounded-2xl border border-slate-800 bg-slate-950 px-4 py-3 text-slate-100"
                  placeholder="What parts are linked to this assembly?"
                />
              </label>
              <label className="grid gap-2 text-sm">
                <span className="text-slate-400">Intent</span>
                <select
                  name="intentKey"
                  defaultValue="object-360-context"
                  className="rounded-2xl border border-slate-800 bg-slate-950 px-4 py-3 text-slate-100"
                >
                  <option value="object-360-context">object-360-context</option>
                  <option value="bom-impact-context">bom-impact-context</option>
                  <option value="document-evidence-context">document-evidence-context</option>
                </select>
              </label>
              <label className="grid gap-2 text-sm">
                <span className="text-slate-400">Optional draft artifact</span>
                <select
                  name="draftArtifactKind"
                  defaultValue=""
                  className="rounded-2xl border border-slate-800 bg-slate-950 px-4 py-3 text-slate-100"
                >
                  <option value="">None</option>
                  <option value="QueryIntent">Draft query intent</option>
                  <option value="Dashboard">Draft dashboard</option>
                  <option value="Report">Draft report</option>
                </select>
              </label>
              <button
                type="submit"
                className="w-fit rounded-2xl bg-cyan-300 px-5 py-3 text-sm font-semibold text-slate-950 transition hover:bg-cyan-200"
              >
                Send governed chat turn
              </button>
            </form>
          </section>
        ) : null}

        {renderApiError(latestTurn)}
        {latestTurn.data ? <TurnPanel turn={latestTurn.data} /> : null}
      </div>
    </main>
  );
}
