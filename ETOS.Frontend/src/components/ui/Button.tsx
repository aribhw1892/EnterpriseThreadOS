import type { ButtonHTMLAttributes, ReactNode } from "react";

export type ButtonVariant = "primary" | "ghost" | "danger" | "good";

const variantClasses: Record<ButtonVariant, string> = {
  primary:
    "bg-gradient-to-br from-[#2563eb] to-[#4f46e5] text-white border-transparent hover:opacity-90 focus-visible:outline-etos-accent",
  ghost:
    "bg-etos-panel-muted text-etos-ink border-etos-border hover:bg-etos-panel focus-visible:outline-etos-accent",
  danger:
    "bg-etos-danger-bg text-etos-danger-fg border-etos-danger-border hover:opacity-85 focus-visible:outline-etos-danger-fg",
  good:
    "bg-etos-success-bg text-etos-success-fg border-etos-success-border hover:opacity-85 focus-visible:outline-etos-success-fg",
};

export function Button({
  variant = "primary",
  className = "",
  children,
  ...props
}: ButtonHTMLAttributes<HTMLButtonElement> & {
  variant?: ButtonVariant;
  children: ReactNode;
}) {
  return (
    <button
      className={`inline-flex items-center gap-2 rounded-etos-button border px-3.5 py-2.5 text-[13px] font-extrabold transition focus-visible:outline-2 focus-visible:outline-offset-2 disabled:cursor-not-allowed disabled:opacity-50 ${variantClasses[variant]} ${className}`}
      {...props}
    >
      {children}
    </button>
  );
}
