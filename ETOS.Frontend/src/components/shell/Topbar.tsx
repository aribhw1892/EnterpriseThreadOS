"use client";

import { usePathname } from "next/navigation";
import { Menu, Search } from "lucide-react";
import { ThemeToggle } from "@/components/shell/ThemeToggle";

function breadcrumbSegments(pathname: string): string[] {
  if (pathname === "/") {
    return ["Mission Control"];
  }
  return pathname
    .split("/")
    .filter(Boolean)
    .map((segment) =>
      decodeURIComponent(segment)
        .replace(/-/g, " ")
        .replace(/\b\w/g, (char) => char.toUpperCase()),
    );
}

export function Topbar({
  tenantName,
  userInitials,
  onMenuClick,
}: {
  tenantName: string;
  userInitials: string;
  onMenuClick: () => void;
}) {
  const pathname = usePathname();
  const segments = breadcrumbSegments(pathname);

  return (
    <header
      className="sticky top-0 z-30 flex h-[74px] items-center gap-4 border-b border-etos-border px-5 backdrop-blur-[20px]"
      style={{ background: "var(--etos-topbar-bg)" }}
    >
      <button
        type="button"
        onClick={onMenuClick}
        aria-label="Open navigation"
        className="inline-flex h-9 w-9 items-center justify-center rounded-xl border border-etos-border text-etos-ink-muted lg:hidden"
      >
        <Menu className="h-4 w-4" />
      </button>

      <nav aria-label="Breadcrumb" className="min-w-0 flex-1">
        <ol className="flex items-center gap-1.5 truncate text-sm text-etos-ink-muted">
          <li className="font-semibold text-etos-ink">EnterpriseThreadOS</li>
          {segments.map((segment, index) => (
            <li key={`${segment}-${index}`} className="flex items-center gap-1.5 truncate">
              <span aria-hidden>/</span>
              <span
                className={
                  index === segments.length - 1 ? "truncate text-etos-ink" : "truncate"
                }
              >
                {segment}
              </span>
            </li>
          ))}
        </ol>
      </nav>

      <div className="hidden items-center md:flex">
        <div className="relative">
          <Search
            aria-hidden
            className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-etos-ink-subtle"
          />
          <input
            type="search"
            disabled
            title="Global search is coming soon — no unified search API yet"
            placeholder="Search artifacts, graph objects, traces, runs…"
            className="w-[280px] rounded-full border border-etos-border bg-etos-panel-muted py-2 pl-9 pr-4 text-sm text-etos-ink-muted placeholder:text-etos-ink-subtle disabled:cursor-not-allowed xl:w-[360px]"
          />
        </div>
      </div>

      <span
        className="hidden rounded-full border px-3 py-1 text-xs font-semibold sm:inline-flex"
        style={{
          background: "var(--etos-tenant-pill-bg)",
          color: "var(--etos-tenant-pill-fg)",
          borderColor: "var(--etos-tenant-pill-border)",
        }}
      >
        {tenantName}
      </span>

      <span className="hidden rounded-full border border-etos-warning-border bg-etos-warning-bg px-3 py-1 text-xs font-semibold text-etos-warning-fg sm:inline-flex">
        Read-only MVP
      </span>

      <ThemeToggle />

      <span
        aria-label={`Signed in as ${userInitials}`}
        className="flex h-9 w-9 items-center justify-center rounded-full bg-gradient-to-br from-[#2563eb] to-[#7c3aed] text-xs font-bold text-white"
      >
        {userInitials}
      </span>
    </header>
  );
}
