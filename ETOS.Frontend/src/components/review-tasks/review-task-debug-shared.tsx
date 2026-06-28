"use client";

import type { ButtonHTMLAttributes, InputHTMLAttributes, ReactNode, SelectHTMLAttributes } from "react";

export function formatDebugJson(value: unknown): string {
  try {
    return JSON.stringify(value, null, 2);
  } catch {
    return String(value);
  }
}

export function DebugJsonBlock({ title, value }: { title: string; value: unknown }) {
  if (value === null || value === undefined) {
    return null;
  }

  return (
    <details className="rounded-xl border border-slate-800 bg-slate-950 p-4" open>
      <summary className="cursor-pointer text-sm font-semibold text-cyan-200">{title}</summary>
      <pre className="mt-3 max-h-96 overflow-auto whitespace-pre-wrap break-all text-xs text-slate-300">
        {formatDebugJson(value)}
      </pre>
    </details>
  );
}

export function DebugStatusPill({
  label,
  tone,
}: {
  label: string;
  tone: "ok" | "warn" | "error" | "neutral";
}) {
  const toneClass =
    tone === "ok"
      ? "border-emerald-500/40 bg-emerald-500/10 text-emerald-200"
      : tone === "warn"
        ? "border-amber-500/40 bg-amber-500/10 text-amber-200"
        : tone === "error"
          ? "border-rose-500/40 bg-rose-500/10 text-rose-200"
          : "border-slate-700 bg-slate-900 text-slate-300";

  return <span className={`rounded-full border px-3 py-1 text-xs font-medium ${toneClass}`}>{label}</span>;
}

export function DebugFieldLabel({ children }: { children: ReactNode }) {
  return <label className="text-xs font-semibold uppercase tracking-wide text-slate-500">{children}</label>;
}

export function DebugTextInput(props: InputHTMLAttributes<HTMLInputElement>) {
  return (
    <input
      {...props}
      className={`w-full rounded-xl border border-slate-700 bg-slate-950 px-3 py-2 text-sm text-slate-100 ${props.className ?? ""}`}
    />
  );
}

export function DebugSelect(props: SelectHTMLAttributes<HTMLSelectElement>) {
  return (
    <select
      {...props}
      className={`w-full rounded-xl border border-slate-700 bg-slate-950 px-3 py-2 text-sm text-slate-100 ${props.className ?? ""}`}
    />
  );
}

export function DebugButton({
  children,
  variant = "primary",
  ...props
}: ButtonHTMLAttributes<HTMLButtonElement> & { variant?: "primary" | "secondary" | "danger" }) {
  const variantClass =
    variant === "danger"
      ? "bg-rose-500 text-slate-950 hover:bg-rose-400"
      : variant === "secondary"
        ? "border border-slate-600 bg-slate-900 text-slate-100 hover:border-cyan-300/40"
        : "bg-cyan-500 text-slate-950 hover:bg-cyan-400";

  return (
    <button
      type="button"
      {...props}
      className={`rounded-full px-4 py-2 text-sm font-semibold disabled:cursor-not-allowed disabled:opacity-50 ${variantClass} ${props.className ?? ""}`}
    />
  );
}
