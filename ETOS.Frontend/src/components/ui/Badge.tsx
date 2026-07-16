import type { ReactNode } from "react";

export type BadgeVariant =
  | "success"
  | "warning"
  | "danger"
  | "info"
  | "purple"
  | "teal"
  | "neutral";

const variantClasses: Record<BadgeVariant, string> = {
  success:
    "bg-etos-success-bg text-etos-success-fg border-etos-success-border",
  warning:
    "bg-etos-warning-bg text-etos-warning-fg border-etos-warning-border",
  danger: "bg-etos-danger-bg text-etos-danger-fg border-etos-danger-border",
  info: "bg-etos-info-bg text-etos-info-fg border-etos-info-border",
  purple: "bg-etos-purple-bg text-etos-purple-fg border-etos-purple-border",
  teal: "bg-etos-teal-bg text-etos-teal-fg border-etos-teal-border",
  neutral:
    "bg-etos-neutral-bg text-etos-neutral-fg border-etos-neutral-border",
};

/** Maps common backend status strings to a badge variant. */
export function badgeVariantForStatus(status: string): BadgeVariant {
  const value = status.toLowerCase();
  if (
    ["healthy", "published", "active", "trusted", "approved", "succeeded", "completed", "ready", "ok", "running"].includes(value)
  ) {
    return "success";
  }
  if (["staged", "pending", "review", "draft-review", "warning", "degraded", "deferred"].includes(value)) {
    return "warning";
  }
  if (["blocked", "conflicted", "denied", "failed", "high", "critical", "rejected", "error", "inactive"].includes(value)) {
    return "danger";
  }
  if (["info", "trace", "schema", "intent"].includes(value)) {
    return "info";
  }
  if (["agent", "workflow"].includes(value)) {
    return "purple";
  }
  return "neutral";
}

export function Badge({
  variant = "neutral",
  children,
  className = "",
}: {
  variant?: BadgeVariant;
  children: ReactNode;
  className?: string;
}) {
  return (
    <span
      className={`inline-flex items-center gap-1 rounded-full border px-3 py-1 text-xs font-semibold uppercase tracking-wide ${variantClasses[variant]} ${className}`}
    >
      {children}
    </span>
  );
}

/** Drop-in replacement for the legacy page-local StatusBadge. */
export function StatusBadge({ status }: { status: string }) {
  return <Badge variant={badgeVariantForStatus(status)}>{status}</Badge>;
}
