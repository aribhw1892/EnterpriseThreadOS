export function Notice({
  children,
  variant = "info",
  className = "",
}: {
  children: React.ReactNode;
  variant?: "info" | "warning" | "success" | "danger";
  className?: string;
}) {
  const styles: Record<typeof variant, string> = {
    info: "border-etos-info-border bg-etos-info-bg text-etos-info-fg",
    warning: "border-etos-warning-border bg-etos-warning-bg text-etos-warning-fg",
    success: "border-etos-success-border bg-etos-success-bg text-etos-success-fg",
    danger: "border-etos-danger-border bg-etos-danger-bg text-etos-danger-fg",
  };

  return (
    <div
      role="status"
      className={`rounded-etos-card border px-4 py-3 text-sm leading-6 ${styles[variant]} ${className}`}
    >
      {children}
    </div>
  );
}

export function Callout({
  title,
  children,
  variant = "info",
  className = "",
}: {
  title: string;
  children: React.ReactNode;
  variant?: "info" | "warning" | "success" | "danger";
  className?: string;
}) {
  return (
    <Notice variant={variant} className={className}>
      <p className="font-semibold">{title}</p>
      <div className="mt-1 opacity-90">{children}</div>
    </Notice>
  );
}
