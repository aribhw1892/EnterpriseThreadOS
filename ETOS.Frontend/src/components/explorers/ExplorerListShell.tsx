import Link from "next/link";
import type { ReactNode } from "react";
import type { ApiResult } from "@/lib/etos-api";

export function ExplorerListShell<T>({
  title,
  description,
  result,
  emptyMessage,
  renderItem,
}: {
  title: string;
  description: string;
  result: ApiResult<T[]>;
  emptyMessage: string;
  renderItem: (item: T) => ReactNode;
}) {
  return (
    <section className="rounded-3xl border border-slate-800 bg-slate-900 p-6">
      <div className="mb-5">
        <h2 className="text-2xl font-semibold">{title}</h2>
        <p className="mt-1 text-sm text-slate-400">{description}</p>
      </div>

      {result.error ? (
        <div className="rounded-2xl border border-amber-500/30 bg-amber-500/10 p-4 text-sm text-amber-100">
          {result.error}
        </div>
      ) : result.data && result.data.length > 0 ? (
        <div className="grid gap-3">{result.data.map(renderItem)}</div>
      ) : (
        <div className="rounded-2xl border border-slate-800 bg-slate-950 p-4 text-sm text-slate-400">
          {emptyMessage}
        </div>
      )}
    </section>
  );
}

export function ExplorerErrorState({ error }: { error: string }) {
  return (
    <div className="rounded-2xl border border-amber-500/30 bg-amber-500/10 p-4 text-sm text-amber-100">
      {error}
    </div>
  );
}

export function ExplorerEmptyState({ message }: { message: string }) {
  return (
    <div className="rounded-2xl border border-slate-800 bg-slate-950 p-4 text-sm text-slate-400">
      {message}
    </div>
  );
}

export function ExplorerNavLink({ href, children }: { href: string; children: ReactNode }) {
  return (
    <Link
      href={href}
      className="rounded-full border border-cyan-300 px-4 py-2 text-sm font-semibold text-cyan-100 transition hover:bg-cyan-300 hover:text-slate-950"
    >
      {children}
    </Link>
  );
}
