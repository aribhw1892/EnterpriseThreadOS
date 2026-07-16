import Link from "next/link";
import { Fragment, type ReactNode } from "react";
import type { ApiResult } from "@/lib/etos-api";
import { EmptyState } from "@/components/ui/EmptyState";
import { ErrorState } from "@/components/ui/ErrorState";

export function ExplorerListShell<T>({
  title,
  description,
  result,
  emptyMessage,
  renderItem,
  getItemKey,
}: {
  title: string;
  description: string;
  result: ApiResult<T[]>;
  emptyMessage: string;
  renderItem: (item: T) => ReactNode;
  getItemKey: (item: T) => string;
}) {
  return (
    <section className="rounded-etos-card border border-etos-border-panel bg-etos-panel-elevated p-6 shadow-etos">
      <div className="mb-5">
        <h2 className="text-2xl font-semibold text-etos-ink">{title}</h2>
        <p className="mt-1 text-sm text-etos-ink-muted">{description}</p>
      </div>

      {result.error ? (
        <ErrorState error={result.error} />
      ) : result.data && result.data.length > 0 ? (
        <div className="grid gap-3">
          {result.data.map((item) => (
            <Fragment key={getItemKey(item)}>{renderItem(item)}</Fragment>
          ))}
        </div>
      ) : (
        <EmptyState message={emptyMessage} />
      )}
    </section>
  );
}

export function ExplorerErrorState({ error }: { error: string }) {
  return <ErrorState error={error} />;
}

export function ExplorerEmptyState({ message }: { message: string }) {
  return <EmptyState message={message} />;
}

export function ExplorerNavLink({ href, children }: { href: string; children: ReactNode }) {
  return (
    <Link
      href={href}
      className="inline-flex items-center rounded-etos-button border border-etos-info-border bg-etos-info-bg px-4 py-2 text-sm font-semibold text-etos-info-fg transition hover:opacity-90"
    >
      {children}
    </Link>
  );
}
