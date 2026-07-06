"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useState } from "react";
import {
  addDecisionComment,
  adminUserId,
  castDecisionVote,
  getDecisionDetail,
  recordManualOutcome,
  type DecisionDetail,
  type DecisionVoteKind,
} from "@/lib/etos-api";
import {
  DebugButton,
  DebugFieldLabel,
  DebugJsonBlock,
  DebugSelect,
  DebugStatusPill,
  DebugTextInput,
} from "@/components/review-tasks/review-task-debug-shared";

type DecisionDetailPanelProps = {
  artifactId: string;
  versionId: string;
  initialDetail: DecisionDetail;
};

export function DecisionDetailPanel({ artifactId, versionId, initialDetail }: DecisionDetailPanelProps) {
  const router = useRouter();
  const [detail, setDetail] = useState(initialDetail);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [voteKind, setVoteKind] = useState<DecisionVoteKind>("approve");
  const [voteComment, setVoteComment] = useState("");
  const [commentBody, setCommentBody] = useState("");
  const [checkType, setCheckType] = useState("manual-check");
  const [expectedOutcome, setExpectedOutcome] = useState("approved");
  const [actualOutcome, setActualOutcome] = useState("approved");
  const [outcomeSummary, setOutcomeSummary] = useState("Manual outcome recorded from decision detail.");

  async function refreshDetail() {
    const result = await getDecisionDetail(artifactId, versionId);
    if (result.data) {
      setDetail(result.data);
    }
    return result;
  }

  async function runOperation(action: () => Promise<{ error: string | null }>) {
    setBusy(true);
    setError(null);
    const result = await action();
    if (result.error) {
      setError(result.error);
    } else {
      await refreshDetail();
      router.refresh();
    }
    setBusy(false);
  }

  const isClosed = detail.status === "finalized" || detail.status === "superseded";

  return (
    <section className="rounded-3xl border border-cyan-500/30 bg-slate-900 p-6">
      <div className="flex flex-wrap items-center gap-3">
        <h2 className="text-2xl font-semibold">Decision operations</h2>
        {busy ? <DebugStatusPill label="Running…" tone="warn" /> : null}
        {detail.conflictState === "blocked" ? <DebugStatusPill label="Conflict" tone="error" /> : null}
      </div>

      {error ? (
        <p className="mt-4 rounded-2xl border border-rose-500/30 bg-rose-500/10 p-3 text-sm text-rose-100">{error}</p>
      ) : null}

      <div className="mt-6 grid gap-6 lg:grid-cols-2">
        <div className="space-y-3 rounded-2xl border border-slate-800 bg-slate-950 p-4">
          <h3 className="font-semibold text-cyan-200">Cast vote</h3>
          <DebugFieldLabel>Vote</DebugFieldLabel>
          <DebugSelect value={voteKind} onChange={(event) => setVoteKind(event.target.value as DecisionVoteKind)}>
            <option value="approve">approve</option>
            <option value="reject">reject</option>
            <option value="abstain">abstain</option>
            <option value="dissent">dissent</option>
          </DebugSelect>
          <DebugFieldLabel>Comment</DebugFieldLabel>
          <DebugTextInput value={voteComment} onChange={(event) => setVoteComment(event.target.value)} />
          <DebugButton
            disabled={busy || isClosed}
            onClick={() =>
              void runOperation(async () => {
                const result = await castDecisionVote(artifactId, versionId, voteKind, voteComment.trim() || undefined);
                return { error: result.error };
              })
            }
          >
            POST /votes
          </DebugButton>
        </div>

        <div className="space-y-3 rounded-2xl border border-slate-800 bg-slate-950 p-4">
          <h3 className="font-semibold text-cyan-200">Comment</h3>
          <textarea
            value={commentBody}
            onChange={(event) => setCommentBody(event.target.value)}
            rows={3}
            className="w-full rounded-xl border border-slate-700 bg-slate-950 px-3 py-2 text-sm text-slate-100"
          />
          <DebugButton
            disabled={busy || !commentBody.trim()}
            onClick={() =>
              void runOperation(async () => {
                const result = await addDecisionComment(artifactId, versionId, commentBody.trim());
                if (!result.error) {
                  setCommentBody("");
                }
                return { error: result.error };
              })
            }
          >
            POST /comments
          </DebugButton>
        </div>

        <div className="space-y-3 rounded-2xl border border-slate-800 bg-slate-950 p-4 lg:col-span-2">
          <h3 className="font-semibold text-cyan-200">Manual outcome</h3>
          <div className="grid gap-3 md:grid-cols-2">
            <div>
              <DebugFieldLabel>Check type</DebugFieldLabel>
              <DebugTextInput value={checkType} onChange={(event) => setCheckType(event.target.value)} />
            </div>
            <div>
              <DebugFieldLabel>Expected outcome</DebugFieldLabel>
              <DebugTextInput value={expectedOutcome} onChange={(event) => setExpectedOutcome(event.target.value)} />
            </div>
            <div>
              <DebugFieldLabel>Actual outcome</DebugFieldLabel>
              <DebugTextInput value={actualOutcome} onChange={(event) => setActualOutcome(event.target.value)} />
            </div>
            <div>
              <DebugFieldLabel>Evidence summary</DebugFieldLabel>
              <DebugTextInput value={outcomeSummary} onChange={(event) => setOutcomeSummary(event.target.value)} />
            </div>
          </div>
          <DebugButton
            disabled={busy}
            onClick={() =>
              void runOperation(async () => {
                const result = await recordManualOutcome(artifactId, versionId, {
                  checkType: checkType.trim(),
                  expectedOutcome: expectedOutcome.trim(),
                  actualOutcome: actualOutcome.trim(),
                  outcomeStatus: "successful",
                  outcomeConfidence: 0.9,
                  evidenceSummary: outcomeSummary.trim(),
                  recommendationArtifactId: detail.recommendationArtifactId ?? undefined,
                });
                return { error: result.error };
              })
            }
          >
            POST /outcomes
          </DebugButton>
        </div>
      </div>

      <div className="mt-6 grid gap-4 lg:grid-cols-2">
        <div className="rounded-2xl border border-slate-800 bg-slate-950 p-4">
          <h3 className="font-semibold">Votes</h3>
          <ul className="mt-3 space-y-2 text-sm text-slate-300">
            {detail.votes.length > 0 ? (
              detail.votes.map((vote) => (
                <li key={vote.id}>
                  {vote.vote} · {vote.userId}
                  {vote.comment ? ` — ${vote.comment}` : ""}
                </li>
              ))
            ) : (
              <li className="text-slate-500">No votes recorded.</li>
            )}
          </ul>
        </div>
        <div className="rounded-2xl border border-slate-800 bg-slate-950 p-4">
          <h3 className="font-semibold">Links</h3>
          <ul className="mt-3 space-y-2 text-sm">
            <li>
              <Link href={`/tasks/${detail.reviewTaskArtifactId}`} className="text-cyan-300 hover:underline">
                Review task
              </Link>
            </li>
            {detail.recommendationArtifactId ? (
              <li>
                <Link href={`/recommendations/${detail.recommendationArtifactId}`} className="text-cyan-300 hover:underline">
                  Recommendation
                </Link>
              </li>
            ) : null}
          </ul>
          <p className="mt-3 text-xs text-slate-500">Acting user: {adminUserId ?? "configure NEXT_PUBLIC_ETOS_ADMIN_USER_ID"}</p>
        </div>
      </div>

      <div className="mt-6">
        <DebugJsonBlock title="Decision detail" value={detail} />
      </div>
    </section>
  );
}
