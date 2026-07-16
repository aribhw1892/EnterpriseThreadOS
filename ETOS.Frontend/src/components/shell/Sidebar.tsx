"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { navGroupLabels, navGroupOrder, navItems } from "@/config/navigation";

function isActive(pathname: string, href: string): boolean {
  if (href === "/") {
    return pathname === "/";
  }
  return pathname === href || pathname.startsWith(`${href}/`);
}

export function Sidebar({ onNavigate }: { onNavigate?: () => void }) {
  const pathname = usePathname();

  return (
    <div
      className="flex h-full flex-col overflow-y-auto border-r border-[var(--etos-nav-divider)] text-[var(--etos-nav-ink)]"
      style={{
        background:
          "linear-gradient(180deg, var(--etos-nav-from), var(--etos-nav-to))",
      }}
    >
      <div className="flex items-center gap-3 px-6 py-5">
        <span
          aria-hidden
          className="flex h-9 w-9 items-center justify-center rounded-xl bg-gradient-to-br from-[#0ea5e9] to-[#7c3aed] text-sm font-black text-white"
        >
          E
        </span>
        <div>
          <p className="text-sm font-bold leading-tight text-[var(--etos-nav-brand)]">
            EnterpriseThreadOS
          </p>
          <p className="text-[11px] text-[var(--etos-nav-muted)]">
            Digital Thread Platform
          </p>
        </div>
      </div>

      <nav aria-label="Primary" className="flex-1 px-3 pb-4">
        {navGroupOrder.map((group) => (
          <div key={group} className="mt-4">
            <p className="px-3 pb-1 text-[11px] font-semibold uppercase tracking-[0.18em] text-[var(--etos-nav-section)]">
              {navGroupLabels[group]}
            </p>
            <ul className="grid gap-0.5">
              {navItems
                .filter((item) => item.group === group)
                .map((item) => {
                  const active = isActive(pathname, item.href);
                  return (
                    <li key={item.href}>
                      <Link
                        href={item.href}
                        onClick={onNavigate}
                        aria-current={active ? "page" : undefined}
                        className={`flex items-center gap-2 rounded-xl px-3 py-2 text-sm transition focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-etos-accent ${
                          active
                            ? "border border-[var(--etos-nav-active-border)] font-semibold text-[var(--etos-nav-active-ink)]"
                            : "border border-transparent text-[var(--etos-nav-ink)] hover:bg-[var(--etos-nav-hover)]"
                        }`}
                        style={
                          active
                            ? { background: "var(--etos-nav-active-bg)" }
                            : undefined
                        }
                      >
                        {active ? (
                          <span
                            aria-hidden
                            className="h-1.5 w-1.5 rounded-full bg-[var(--etos-accent-cyan)] shadow-[0_0_8px_var(--etos-accent-cyan)]"
                          />
                        ) : null}
                        <span className="truncate">{item.label}</span>
                        {!item.implemented ? (
                          <span className="ml-auto rounded-full border border-[var(--etos-nav-soon-border)] px-1.5 py-0.5 text-[9px] uppercase tracking-wide text-[var(--etos-nav-muted)]">
                            Soon
                          </span>
                        ) : null}
                      </Link>
                    </li>
                  );
                })}
            </ul>
          </div>
        ))}
      </nav>

      <div className="border-t border-[var(--etos-nav-divider)] px-6 py-4">
        <p className="text-[11px] font-semibold uppercase tracking-wide text-[var(--etos-nav-section)]">
          MVP safety boundary
        </p>
        <p className="mt-1 text-xs leading-5 text-[var(--etos-nav-muted)]">
          Read-only for source systems. Writes stay disabled until governed
          action framework ships.
        </p>
      </div>
    </div>
  );
}
