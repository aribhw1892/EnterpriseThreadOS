type ComponentHealth = {
  name: string;
  status: string;
  description?: string | null;
  durationMilliseconds: number;
};

type PlatformHealth = {
  status: string;
  environment: string;
  checkedAt: string;
  components: ComponentHealth[];
};

const apiBaseUrl = process.env.NEXT_PUBLIC_ETOS_API_BASE_URL ?? "http://localhost:5000";

export const dynamic = "force-dynamic";

async function getPlatformHealth(): Promise<PlatformHealth | null> {
  try {
    const response = await fetch(`${apiBaseUrl}/api/health`, {
      cache: "no-store",
      next: { revalidate: 0 },
    });

    if (!response.ok) {
      return null;
    }

    return (await response.json()) as PlatformHealth;
  } catch {
    return null;
  }
}

function StatusBadge({ status }: { status: string }) {
  const isHealthy = status.toLowerCase() === "healthy";

  return (
    <span
      className={`rounded-full px-3 py-1 text-xs font-semibold uppercase tracking-wide ${
        isHealthy
          ? "bg-emerald-100 text-emerald-800 dark:bg-emerald-950 dark:text-emerald-200"
          : "bg-amber-100 text-amber-800 dark:bg-amber-950 dark:text-amber-200"
      }`}
    >
      {status}
    </span>
  );
}

export default async function Home() {
  const health = await getPlatformHealth();
  const frontendEnvironment = process.env.NODE_ENV;

  return (
    <main className="min-h-screen bg-slate-950 px-6 py-10 text-slate-100">
      <div className="mx-auto flex max-w-5xl flex-col gap-8">
        <section className="rounded-3xl border border-slate-800 bg-slate-900/80 p-8 shadow-2xl">
          <p className="mb-3 text-sm font-semibold uppercase tracking-[0.3em] text-cyan-300">
            EnterpriseThreadOS
          </p>
          <div className="flex flex-col gap-6 md:flex-row md:items-end md:justify-between">
            <div>
              <h1 className="text-4xl font-semibold tracking-tight">
                Local platform foundation
              </h1>
              <p className="mt-4 max-w-2xl text-base leading-7 text-slate-300">
                This shell proves the frontend can reach the ASP.NET Core backend
                and display safe infrastructure health for the Issue 1 bootstrap.
              </p>
            </div>
            <StatusBadge status={health?.status ?? "unavailable"} />
          </div>
        </section>

        <section className="grid gap-4 md:grid-cols-3">
          <div className="rounded-2xl border border-slate-800 bg-slate-900 p-5">
            <p className="text-sm text-slate-400">Frontend environment</p>
            <p className="mt-2 text-2xl font-semibold">{frontendEnvironment}</p>
          </div>
          <div className="rounded-2xl border border-slate-800 bg-slate-900 p-5">
            <p className="text-sm text-slate-400">Backend environment</p>
            <p className="mt-2 text-2xl font-semibold">
              {health?.environment ?? "unavailable"}
            </p>
          </div>
          <div className="rounded-2xl border border-slate-800 bg-slate-900 p-5">
            <p className="text-sm text-slate-400">Backend API base URL</p>
            <p className="mt-2 break-all font-mono text-sm text-cyan-200">
              {apiBaseUrl}
            </p>
          </div>
        </section>

        <section className="rounded-3xl border border-slate-800 bg-slate-900 p-6">
          <div className="mb-5 flex items-center justify-between gap-4">
            <div>
              <h2 className="text-2xl font-semibold">Infrastructure health</h2>
              <p className="mt-1 text-sm text-slate-400">
                PostgreSQL, Memgraph, Qdrant, MinIO, Redis, and RabbitMQ.
              </p>
            </div>
            {health?.checkedAt ? (
              <p className="text-right text-xs text-slate-500">
                Checked {new Date(health.checkedAt).toLocaleString()}
              </p>
            ) : null}
          </div>

          {health ? (
            <div className="grid gap-3 md:grid-cols-2">
              {health.components.map((component) => (
                <article
                  key={component.name}
                  className="rounded-2xl border border-slate-800 bg-slate-950 p-4"
                >
                  <div className="flex items-center justify-between gap-3">
                    <h3 className="font-semibold">{component.name}</h3>
                    <StatusBadge status={component.status} />
                  </div>
                  <p className="mt-3 text-sm text-slate-400">
                    {component.description ?? "No additional details."}
                  </p>
                  <p className="mt-3 text-xs text-slate-500">
                    Probe duration: {component.durationMilliseconds}ms
                  </p>
                </article>
              ))}
            </div>
          ) : (
            <div className="rounded-2xl border border-amber-500/30 bg-amber-500/10 p-5 text-amber-100">
              Backend health is unavailable. Start the backend at{" "}
              <code className="rounded bg-slate-950 px-2 py-1 font-mono text-sm">
                {apiBaseUrl}
              </code>{" "}
              and refresh this page.
            </div>
          )}
        </section>
      </div>
    </main>
  );
}
