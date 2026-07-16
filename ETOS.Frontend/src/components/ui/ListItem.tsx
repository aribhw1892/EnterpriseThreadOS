import type { ReactNode } from "react";
import { Button } from "@/components/ui/Button";

/** Numbered action row matching mockup `.list-item`. */
export function ListItem({
  index,
  title,
  description,
  action,
  actionLabel,
  actionVariant = "ghost",
}: {
  index: number | string;
  title: string;
  description: string;
  action?: () => Promise<void>;
  actionLabel?: string;
  actionVariant?: "primary" | "ghost" | "danger" | "good";
}) {
  return (
    <div className="flex items-start gap-3 rounded-[14px] border border-etos-border-soft bg-etos-panel-muted p-3">
      <div className="flex h-[34px] w-[34px] shrink-0 items-center justify-center rounded-xl bg-etos-info-bg text-sm font-black text-etos-info-fg">
        {index}
      </div>
      <div className="min-w-0 flex-1">
        <p className="text-[13px] font-extrabold text-etos-ink">{title}</p>
        <p className="mt-1 text-xs leading-snug text-etos-ink-muted">{description}</p>
      </div>
      {action && actionLabel ? (
        <form action={action} className="shrink-0">
          <Button type="submit" variant={actionVariant}>
            {actionLabel}
          </Button>
        </form>
      ) : null}
    </div>
  );
}

export function ListStack({ children }: { children: ReactNode }) {
  return <div className="flex flex-col gap-2.5">{children}</div>;
}
