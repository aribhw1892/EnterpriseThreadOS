"use client";

import { useEffect, useRef } from "react";
import type { DigitalThreadEvent } from "@/lib/etos-api";
import {
  resolveDigitalThreadStreamAuth,
  startDigitalThreadEventStream,
  type DigitalThreadStreamAuth,
} from "@/lib/digital-thread-stream";

type Props = {
  enabled: boolean;
  auth?: Partial<DigitalThreadStreamAuth>;
  since?: string;
  onEvent: (event: DigitalThreadEvent) => void;
  onStatus?: (status: "connecting" | "live" | "error" | "stopped", detail?: string) => void;
};

export function DigitalThreadLiveClient({
  enabled,
  auth,
  since,
  onEvent,
  onStatus,
}: Props) {
  const userId = auth?.userId;
  const tenantId = auth?.tenantId;
  const onEventRef = useRef(onEvent);
  const onStatusRef = useRef(onStatus);

  useEffect(() => {
    onEventRef.current = onEvent;
    onStatusRef.current = onStatus;
  }, [onEvent, onStatus]);

  useEffect(() => {
    if (!enabled) {
      onStatusRef.current?.("stopped");
      return;
    }

    const resolved = resolveDigitalThreadStreamAuth({ userId, tenantId });
    if (!resolved) {
      onStatusRef.current?.(
        "error",
        "Missing tenant/user headers for digital-thread stream.",
      );
      return;
    }

    onStatusRef.current?.("connecting");
    const handle = startDigitalThreadEventStream(
      resolved,
      {
        onEvent: (event) => {
          onStatusRef.current?.("live");
          onEventRef.current(event);
        },
        onHeartbeat: () => onStatusRef.current?.("live"),
        onError: (message) => onStatusRef.current?.("error", message),
      },
      { since },
    );

    return () => {
      handle.stop();
      onStatusRef.current?.("stopped");
    };
  }, [enabled, userId, tenantId, since]);

  return null;
}
