import type { ContextViewSectionVisibility } from "@/lib/etos-api";
import { Badge, type BadgeVariant } from "@/components/ui/Badge";

export function SectionVisibilityBadge({ visibility }: { visibility: ContextViewSectionVisibility }) {
  const normalized = visibility.toLowerCase();
  const variant: BadgeVariant =
    normalized === "visible" ? "success" : normalized === "denied" ? "danger" : "neutral";

  return <Badge variant={variant}>{visibility}</Badge>;
}
