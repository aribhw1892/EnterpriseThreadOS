import {
  adminUserId,
  apiBaseUrl,
  selectedTenantId,
  type DigitalThreadEvent,
} from "@/lib/etos-api";

export type DigitalThreadStreamAuth = {
  userId: string;
  tenantId: string;
};

export type DigitalThreadStreamHandlers = {
  onEvent: (event: DigitalThreadEvent, cursor: string) => void;
  onHeartbeat?: (cursor: string) => void;
  onError?: (message: string) => void;
};

export type DigitalThreadStreamHandle = {
  stop: () => void;
};

type StreamEnvelope = {
  cursor: string;
  event?: DigitalThreadEvent | null;
  heartbeat?: boolean;
};

/**
 * SSE poll-delta client using fetch ReadableStream so ETOS tenant headers can be sent.
 * Browser EventSource cannot set custom headers.
 */
export function startDigitalThreadEventStream(
  auth: DigitalThreadStreamAuth,
  handlers: DigitalThreadStreamHandlers,
  options?: { since?: string; sinceEventId?: string },
): DigitalThreadStreamHandle {
  const controller = new AbortController();
  let stopped = false;

  const run = async () => {
    const query = new URLSearchParams();
    if (options?.since) query.set("since", options.since);
    if (options?.sinceEventId) query.set("sinceEventId", options.sinceEventId);
    const suffix = query.size > 0 ? `?${query.toString()}` : "";

    try {
      const response = await fetch(
        `${apiBaseUrl}/api/admin/digital-thread/events/stream${suffix}`,
        {
          method: "GET",
          headers: {
            Accept: "text/event-stream",
            "X-ETOS-User-Id": auth.userId,
            "X-ETOS-Tenant-Id": auth.tenantId,
          },
          cache: "no-store",
          signal: controller.signal,
        },
      );

      if (!response.ok || !response.body) {
        handlers.onError?.(
          `${response.status} ${response.statusText || "stream unavailable"}`,
        );
        return;
      }

      const reader = response.body.getReader();
      const decoder = new TextDecoder();
      let buffer = "";

      while (!stopped) {
        const { done, value } = await reader.read();
        if (done) break;
        buffer += decoder.decode(value, { stream: true });
        const chunks = buffer.split("\n\n");
        buffer = chunks.pop() ?? "";

        for (const chunk of chunks) {
          const lines = chunk.split("\n");
          let eventName = "message";
          const dataLines: string[] = [];

          for (const line of lines) {
            if (line.startsWith(":")) {
              const heartbeatCursor = line.replace(/^:\s*heartbeat\s*/i, "").trim();
              handlers.onHeartbeat?.(heartbeatCursor || "heartbeat");
              continue;
            }
            if (line.startsWith("event:")) {
              eventName = line.slice(6).trim();
              continue;
            }
            if (line.startsWith("data:")) {
              dataLines.push(line.slice(5).trim());
            }
          }

          if (eventName !== "digital-thread" || dataLines.length === 0) {
            continue;
          }

          try {
            const envelope = JSON.parse(dataLines.join("\n")) as StreamEnvelope;
            if (envelope.heartbeat || !envelope.event) {
              handlers.onHeartbeat?.(envelope.cursor);
              continue;
            }
            handlers.onEvent(envelope.event, envelope.cursor);
          } catch {
            handlers.onError?.("Failed to parse digital-thread stream payload.");
          }
        }
      }
    } catch (error) {
      if (stopped || controller.signal.aborted) {
        return;
      }
      handlers.onError?.(
        error instanceof Error ? error.message : "Digital-thread stream failed.",
      );
    }
  };

  void run();

  return {
    stop: () => {
      stopped = true;
      controller.abort();
    },
  };
}

export function resolveDigitalThreadStreamAuth(
  override?: Partial<DigitalThreadStreamAuth>,
): DigitalThreadStreamAuth | null {
  const userId = override?.userId ?? adminUserId;
  const tenantId = override?.tenantId ?? selectedTenantId;
  if (!userId || !tenantId) {
    return null;
  }
  return { userId, tenantId };
}
