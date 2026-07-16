"use client";

import { useState, type ReactNode } from "react";
import { X } from "lucide-react";
import { Sidebar } from "@/components/shell/Sidebar";
import { Topbar } from "@/components/shell/Topbar";

export function AppShell({
  tenantName,
  userInitials,
  children,
}: {
  tenantName: string;
  userInitials: string;
  children: ReactNode;
}) {
  const [drawerOpen, setDrawerOpen] = useState(false);

  return (
    <div className="flex min-h-screen bg-etos-canvas">
      <a
        href="#etos-main"
        className="sr-only focus:not-sr-only focus:absolute focus:left-4 focus:top-4 focus:z-50 focus:rounded-xl focus:bg-etos-panel focus:px-4 focus:py-2 focus:text-sm focus:text-etos-ink"
      >
        Skip to main content
      </a>

      <aside className="fixed inset-y-0 left-0 z-40 hidden w-[280px] lg:block">
        <Sidebar />
      </aside>

      {drawerOpen ? (
        <div className="fixed inset-0 z-50 lg:hidden" role="dialog" aria-modal="true">
          <button
            type="button"
            aria-label="Close navigation"
            onClick={() => setDrawerOpen(false)}
            className="absolute inset-0 bg-black/50"
          />
          <div className="absolute inset-y-0 left-0 w-[280px] shadow-2xl">
            <Sidebar onNavigate={() => setDrawerOpen(false)} />
            <button
              type="button"
              aria-label="Close navigation"
              onClick={() => setDrawerOpen(false)}
              className="absolute right-3 top-4 inline-flex h-8 w-8 items-center justify-center rounded-full text-[var(--etos-nav-muted)] hover:bg-[var(--etos-nav-hover)] hover:text-[var(--etos-nav-brand)]"
            >
              <X className="h-4 w-4" />
            </button>
          </div>
        </div>
      ) : null}

      <div className="flex min-h-screen w-full flex-col lg:pl-[280px]">
        <Topbar
          tenantName={tenantName}
          userInitials={userInitials}
          onMenuClick={() => setDrawerOpen(true)}
        />
        <div id="etos-main" className="flex-1">
          {children}
        </div>
      </div>
    </div>
  );
}
