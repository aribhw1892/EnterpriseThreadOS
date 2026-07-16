"use client";

import Link from "next/link";
import { useEffect, useState } from "react";
import {
  getDigitalThreadEventDetail,
  type DigitalThreadEventDetail,
} from "@/lib/etos-api";
import { PillStack, SidePanel } from "@/components/ui/SidePanel";
import { ErrorState } from "@/components/ui/ErrorState";

type Props = {
  eventId: string | null;
  /** When set (preview mode), skip live detail API. */
  previewDetail?: DigitalThreadEventDetail | null;
  onClose: () => void;
};

type LiveLoadState =
  | { status: "idle" }
  | { status: "loading"; eventId: string }
  | { status: "error"; eventId: string; error: string }
  | { status: "ready"; eventId: string; detail: DigitalThreadEventDetail };

export function DigitalThreadEventInspector({
  eventId,
  previewDetail = null,
  onClose,
}: Props) {
  const [liveState, setLiveState] = useState<LiveLoadState>({ status: "idle" });

  const usingPreview =
    Boolean(eventId) &&
    Boolean(previewDetail) &&
    previewDetail?.eventId === eventId;

  useEffect(() => {
    if (!eventId || usingPreview || eventId.startsWith("preview:")) {
      return;
    }

    let cancelled = false;
    const requestId = eventId;

    void getDigitalThreadEventDetail(requestId).then((result) => {
      if (cancelled) return;
      if (result.error || !result.data) {
        setLiveState({
          status: "error",
          eventId: requestId,
          error: result.error ?? "Event detail not found.",
        });
        return;
      }
      setLiveState({
        status: "ready",
        eventId: requestId,
        detail: result.data,
      });
    });

    return () => {
      cancelled = true;
    };
  }, [eventId, usingPreview]);

  if (!eventId) {
    return (
      <SidePanel
        title="Event inspector"
        className="border-etos-ops-border bg-etos-ops-panel text-etos-ops-ink"
      >
        <p className="text-sm text-etos-ops-ink-muted">
          Select a pulse on the canvas to inspect evidence and drill-through routes.
        </p>
      </SidePanel>
    );
  }

  const detail = usingPreview
    ? previewDetail
    : liveState.status === "ready" && liveState.eventId === eventId
      ? liveState.detail
      : null;

  const error =
    !usingPreview && eventId.startsWith("preview:")
      ? "Preview detail unavailable."
      : !usingPreview &&
          liveState.status === "error" &&
          liveState.eventId === eventId
        ? liveState.error
        : null;

  const awaiting =
    !usingPreview &&
    !eventId.startsWith("preview:") &&
    (liveState.status === "idle" ||
      liveState.status === "loading" ||
      ("eventId" in liveState && liveState.eventId !== eventId) ||
      (liveState.status !== "ready" && liveState.status !== "error"));

  return (
    <SidePanel
      title="Event inspector"
      className="border-etos-ops-border bg-etos-ops-panel text-etos-ops-ink"
    >
      <div className="mb-3 flex items-center justify-between gap-2">
        <p className="truncate font-mono text-[11px] text-etos-ops-ink-muted">{eventId}</p>
        <button
          type="button"
          onClick={onClose}
          className="rounded-md border border-etos-ops-border px-2 py-0.5 text-[10px] uppercase tracking-wide text-etos-ops-ink-muted"
        >
          Close
        </button>
      </div>

      {awaiting ? (
        <p className="text-sm text-etos-ops-ink-muted">Loading detail…</p>
      ) : null}
      {error ? <ErrorState error={error} /> : null}

      {detail ? (
        <div className="grid gap-3">
          <div>
            <h4 className="text-sm font-semibold text-etos-ops-ink">{detail.title}</h4>
            <p className="mt-1 text-xs text-etos-ops-ink-muted">{detail.description}</p>
          </div>
          <PillStack
            items={[
              { label: "Type", value: detail.eventType },
              { label: "System", value: detail.sourceSystemName },
              { label: "Trust", value: detail.trustState },
              { label: "Sync", value: detail.syncStatus },
              ...(detail.severity
                ? [{ label: "Severity", value: detail.severity }]
                : []),
            ]}
          />
          {detail.policySafeSummary ? (
            <p className="rounded-xl border border-etos-ops-border bg-etos-ops-canvas px-3 py-2 text-xs text-etos-ops-ink">
              {detail.policySafeSummary}
            </p>
          ) : null}
          {detail.dataQualitySafeSummary ? (
            <p className="rounded-xl border border-etos-ops-border bg-etos-ops-canvas px-3 py-2 text-xs text-etos-ops-ink">
              {detail.dataQualitySafeSummary}
            </p>
          ) : null}
          {detail.evidenceLinks.length > 0 ? (
            <div>
              <p className="mb-1 text-[10px] font-bold uppercase tracking-wide text-etos-ops-ink-muted">
                Evidence
              </p>
              <ul className="grid gap-1">
                {detail.evidenceLinks.map((link) => (
                  <li key={`${link.linkType}-${link.label}`} className="text-xs">
                    {link.href ? (
                      <Link href={link.href} className="text-sky-300 hover:underline">
                        {link.label}
                      </Link>
                    ) : (
                      <span className="text-etos-ops-ink">{link.label}</span>
                    )}
                    {link.safeSummary ? (
                      <span className="block text-etos-ops-ink-muted">{link.safeSummary}</span>
                    ) : null}
                  </li>
                ))}
              </ul>
            </div>
          ) : null}
          {detail.drillRoutes.length > 0 ? (
            <div>
              <p className="mb-1 text-[10px] font-bold uppercase tracking-wide text-etos-ops-ink-muted">
                Drill-through
              </p>
              <ul className="grid gap-1">
                {detail.drillRoutes.map((route) => (
                  <li key={`${route.routeType}-${route.href}`}>
                    <Link
                      href={route.href}
                      className="text-xs font-semibold text-sky-300 hover:underline"
                    >
                      {route.label}
                    </Link>
                  </li>
                ))}
              </ul>
            </div>
          ) : null}
        </div>
      ) : null}
    </SidePanel>
  );
}
