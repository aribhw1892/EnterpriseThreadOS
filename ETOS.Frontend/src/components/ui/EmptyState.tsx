export function EmptyState({ message }: { message: string }) {
  return (
    <div className="rounded-etos-card border border-etos-border bg-etos-panel-muted p-4 text-sm text-etos-ink-muted">
      {message}
    </div>
  );
}
