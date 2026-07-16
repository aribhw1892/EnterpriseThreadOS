"use client";

import Link from "next/link";
import { usePathname, useSearchParams } from "next/navigation";
import type { ReactNode } from "react";

export type TabItem = {
  id: string;
  label: string;
  href?: string;
};

/**
 * URL-param driven tabs (default `?tab=`). Prefer server-rendered panels
 * keyed off the same search param for RSC pages.
 */
export function Tabs({
  items,
  activeId,
  paramName = "tab",
  className = "",
}: {
  items: TabItem[];
  activeId: string;
  paramName?: string;
  className?: string;
}) {
  const pathname = usePathname();
  const searchParams = useSearchParams();

  return (
    <div
      role="tablist"
      aria-label="Sections"
      className={`flex flex-wrap gap-2 border-b border-etos-border pb-px ${className}`}
    >
      {items.map((item) => {
        const isActive = item.id === activeId;
        const href =
          item.href ??
          (() => {
            const params = new URLSearchParams(searchParams.toString());
            params.set(paramName, item.id);
            return `${pathname}?${params.toString()}`;
          })();

        return (
          <Link
            key={item.id}
            href={href}
            role="tab"
            aria-selected={isActive}
            className={
              isActive
                ? "rounded-t-xl border border-b-0 border-etos-border bg-etos-panel px-4 py-2 text-sm font-semibold text-etos-ink"
                : "rounded-t-xl border border-transparent px-4 py-2 text-sm font-medium text-etos-ink-muted hover:bg-etos-panel-muted hover:text-etos-ink"
            }
          >
            {item.label}
          </Link>
        );
      })}
    </div>
  );
}

export function TabPanel({
  id,
  activeId,
  children,
  className = "",
}: {
  id: string;
  activeId: string;
  children: ReactNode;
  className?: string;
}) {
  if (id !== activeId) {
    return null;
  }

  return (
    <div role="tabpanel" id={`tab-panel-${id}`} className={className}>
      {children}
    </div>
  );
}
