export function ErrorState({ error }: { error: string }) {
  return (
    <div className="rounded-etos-card border border-etos-warning-border bg-etos-warning-bg p-4 text-sm text-etos-warning-fg">
      {error}
    </div>
  );
}
