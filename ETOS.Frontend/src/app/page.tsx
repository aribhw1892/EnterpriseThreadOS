import {
  AccessGrant,
  Artifact,
  ArtifactDependency,
  ArtifactRelationship,
  ArtifactVersion,
  AuditRecord,
  ApiResult,
  ClassificationScheme,
  IdentityUser,
  PolicyImpact,
  PolicyVersion,
  RestrictedContextRule,
  SecurityEvent,
  Tenant,
  TenantMembership,
  TenantRole,
  adminUserId,
  apiBaseUrl,
  getArtifactRegistryLists,
  getClassificationPolicyLists,
  getGovernanceLists,
  getIdentityLists,
  getPlatformHealth,
  selectedTenantId,
} from "@/lib/etos-api";
import Link from "next/link";
import type { ReactNode } from "react";

export const dynamic = "force-dynamic";

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

function EmptyState({ message }: { message: string }) {
  return (
    <div className="rounded-2xl border border-slate-800 bg-slate-950 p-4 text-sm text-slate-400">
      {message}
    </div>
  );
}

function ErrorState({ error }: { error: string }) {
  return (
    <div className="rounded-2xl border border-amber-500/30 bg-amber-500/10 p-4 text-sm text-amber-100">
      {error}
    </div>
  );
}

function ListSection<T>({
  title,
  description,
  result,
  emptyMessage,
  renderItem,
}: {
  title: string;
  description: string;
  result: ApiResult<T[]>;
  emptyMessage: string;
  renderItem: (item: T) => ReactNode;
}) {
  return (
    <section className="rounded-3xl border border-slate-800 bg-slate-900 p-6">
      <div className="mb-5">
        <h2 className="text-2xl font-semibold">{title}</h2>
        <p className="mt-1 text-sm text-slate-400">{description}</p>
      </div>

      {result.error ? (
        <ErrorState error={result.error} />
      ) : result.data && result.data.length > 0 ? (
        <div className="grid gap-3">{result.data.map(renderItem)}</div>
      ) : (
        <EmptyState message={emptyMessage} />
      )}
    </section>
  );
}

function TenantCard(tenant: Tenant) {
  return (
    <article key={tenant.id} className="rounded-2xl border border-slate-800 bg-slate-950 p-4">
      <div className="flex items-center justify-between gap-3">
        <div>
          <h3 className="font-semibold">{tenant.name}</h3>
          <p className="mt-1 font-mono text-xs text-cyan-200">{tenant.identifier}</p>
        </div>
        <StatusBadge status={tenant.isActive ? "active" : "inactive"} />
      </div>
      <p className="mt-3 break-all text-xs text-slate-500">{tenant.id}</p>
    </article>
  );
}

function UserCard(user: IdentityUser) {
  return (
    <article key={user.id} className="rounded-2xl border border-slate-800 bg-slate-950 p-4">
      <h3 className="font-semibold">{user.displayName ?? user.userName}</h3>
      <p className="mt-1 text-sm text-slate-400">{user.email}</p>
      <p className="mt-3 break-all font-mono text-xs text-slate-500">{user.id}</p>
    </article>
  );
}

function RoleCard(role: TenantRole) {
  return (
    <article key={role.id} className="rounded-2xl border border-slate-800 bg-slate-950 p-4">
      <h3 className="font-semibold">{role.name}</h3>
      <p className="mt-1 text-sm text-slate-400">{role.description ?? "No description."}</p>
      <p className="mt-3 break-all font-mono text-xs text-slate-500">{role.id}</p>
    </article>
  );
}

function MembershipCard(membership: TenantMembership) {
  return (
    <article key={membership.id} className="rounded-2xl border border-slate-800 bg-slate-950 p-4">
      <div className="flex items-center justify-between gap-3">
        <div>
          <h3 className="font-semibold">{membership.userName}</h3>
          <p className="mt-1 text-sm text-slate-400">{membership.roleName}</p>
        </div>
        <StatusBadge status={membership.isActive ? "active" : "inactive"} />
      </div>
      <p className="mt-3 text-xs text-slate-500">
        Expires: {membership.expiresAt ? new Date(membership.expiresAt).toLocaleString() : "never"}
      </p>
    </article>
  );
}

function GrantCard(grant: AccessGrant) {
  return (
    <article key={grant.id} className="rounded-2xl border border-slate-800 bg-slate-950 p-4">
      <div className="flex items-center justify-between gap-3">
        <div>
          <h3 className="font-semibold">{grant.permissionKey}</h3>
          <p className="mt-1 text-sm text-slate-400">{grant.userName}</p>
        </div>
        <StatusBadge status={grant.kind} />
      </div>
      <p className="mt-3 text-xs text-slate-500">
        Expires: {grant.expiresAt ? new Date(grant.expiresAt).toLocaleString() : "never"}
      </p>
    </article>
  );
}

function AuditRecordCard(record: AuditRecord) {
  return (
    <article key={record.id} className="rounded-2xl border border-slate-800 bg-slate-950 p-4">
      <div className="flex items-center justify-between gap-3">
        <div>
          <h3 className="font-semibold">{record.action}</h3>
          <p className="mt-1 text-sm text-slate-400">{record.safeSummary}</p>
        </div>
        <StatusBadge status={record.result} />
      </div>
      <div className="mt-3 grid gap-1 text-xs text-slate-500">
        <p>Reason: {record.reason ?? "none"}</p>
        <p>Retention: {record.retentionCategory}</p>
        <p>{new Date(record.createdAt).toLocaleString()}</p>
      </div>
    </article>
  );
}

function SecurityEventCard(event: SecurityEvent) {
  return (
    <article key={event.id} className="rounded-2xl border border-slate-800 bg-slate-950 p-4">
      <div className="flex items-center justify-between gap-3">
        <div>
          <h3 className="font-semibold">{event.eventType}</h3>
          <p className="mt-1 text-sm text-slate-400">{event.safeSummary}</p>
        </div>
        <StatusBadge status={event.severity} />
      </div>
      <div className="mt-3 grid gap-1 text-xs text-slate-500">
        <p>Action: {event.sourceAction}</p>
        <p>Reason: {event.reason ?? "none"}</p>
        <p>{new Date(event.createdAt).toLocaleString()}</p>
      </div>
    </article>
  );
}

function ArtifactCard(artifact: Artifact) {
  return (
    <article key={artifact.id} className="rounded-2xl border border-slate-800 bg-slate-950 p-4">
      <div className="flex items-center justify-between gap-3">
        <div>
          <h3 className="font-semibold">{artifact.name}</h3>
          <p className="mt-1 text-sm text-slate-400">{artifact.artifactType}</p>
        </div>
        <StatusBadge status={artifact.latestVersion?.readinessState ?? artifact.lifecycleState} />
      </div>
      <div className="mt-3 grid gap-1 text-xs text-slate-500">
        <p>{artifact.description ?? "No description."}</p>
        <p>Latest: {artifact.latestVersion?.versionLabel ?? "no versions"}</p>
        <p className="break-all font-mono">{artifact.id}</p>
      </div>
    </article>
  );
}

function ArtifactVersionCard(version: ArtifactVersion) {
  return (
    <article key={version.id} className="rounded-2xl border border-slate-800 bg-slate-950 p-4">
      <div className="flex items-center justify-between gap-3">
        <div>
          <h3 className="font-semibold">{version.versionLabel}</h3>
          <p className="mt-1 text-sm text-slate-400">{version.summary ?? "No summary."}</p>
        </div>
        <StatusBadge status={version.readinessState} />
      </div>
      <div className="mt-3 grid gap-1 text-xs text-slate-500">
        <p>Compatibility: {version.compatibilityStatus}</p>
        <p>Policy risk: {version.policyRiskStatus}</p>
        <p>{new Date(version.createdAt).toLocaleString()}</p>
      </div>
    </article>
  );
}

function ArtifactRelationshipCard(relationship: ArtifactRelationship) {
  return (
    <article key={relationship.id} className="rounded-2xl border border-slate-800 bg-slate-950 p-4">
      <div className="flex items-center justify-between gap-3">
        <div>
          <h3 className="font-semibold">{relationship.targetArtifactName}</h3>
          <p className="mt-1 text-sm text-slate-400">
            {relationship.description ?? "Generic artifact relationship."}
          </p>
        </div>
        <StatusBadge status={relationship.relationshipType} />
      </div>
    </article>
  );
}

function ArtifactDependencyCard(dependency: ArtifactDependency) {
  return (
    <article key={dependency.id} className="rounded-2xl border border-slate-800 bg-slate-950 p-4">
      <div className="flex items-center justify-between gap-3">
        <div>
          <h3 className="font-semibold">{dependency.requiredArtifactName}</h3>
          <p className="mt-1 text-sm text-slate-400">
            Requires version {dependency.requiredVersionLabel}
          </p>
        </div>
        <StatusBadge status={dependency.requiredReadinessState} />
      </div>
      <p className="mt-3 text-xs text-slate-500">Kind: {dependency.dependencyKind}</p>
    </article>
  );
}

function ClassificationSchemeCard(scheme: ClassificationScheme) {
  return (
    <article key={scheme.id} className="rounded-2xl border border-slate-800 bg-slate-950 p-4">
      <div className="flex items-center justify-between gap-3">
        <div>
          <h3 className="font-semibold">{scheme.name}</h3>
          <p className="mt-1 font-mono text-xs text-cyan-200">{scheme.key}</p>
        </div>
        <StatusBadge status={scheme.latestVersion?.state ?? "draftless"} />
      </div>
      <div className="mt-3 grid gap-1 text-xs text-slate-500">
        <p>{scheme.description ?? "No description."}</p>
        <p>Latest: {scheme.latestVersion?.versionLabel ?? "no versions"}</p>
      </div>
    </article>
  );
}

function PolicyVersionCard(policy: PolicyVersion) {
  return (
    <article key={policy.id} className="rounded-2xl border border-slate-800 bg-slate-950 p-4">
      <div className="flex items-center justify-between gap-3">
        <div>
          <h3 className="font-semibold">{policy.name}</h3>
          <p className="mt-1 font-mono text-xs text-cyan-200">
            {policy.policyKey} / {policy.versionLabel}
          </p>
        </div>
        <StatusBadge status={policy.state} />
      </div>
      <div className="mt-3 grid gap-1 text-xs text-slate-500">
        <p>{policy.summary ?? "No summary."}</p>
        <p>Scheme version: {policy.classificationSchemeVersionLabel}</p>
        <p>Restricted rules: {policy.restrictedRuleCount}</p>
      </div>
    </article>
  );
}

function RestrictedContextRuleCard(rule: RestrictedContextRule) {
  return (
    <article key={rule.id} className="rounded-2xl border border-slate-800 bg-slate-950 p-4">
      <div className="flex items-center justify-between gap-3">
        <div>
          <h3 className="font-semibold">{rule.classificationKey}</h3>
          <p className="mt-1 text-sm text-slate-400">{rule.safeSummary}</p>
        </div>
        <StatusBadge status={rule.effect} />
      </div>
      <div className="mt-3 grid gap-1 text-xs text-slate-500">
        <p>Policy: {rule.policyKey}</p>
        <p>Attribute: {rule.attributeKey ?? "any"}</p>
        <p>Permission: {rule.requiredPermissionKey ?? "none"}</p>
        <p>Grant required: {rule.requiresGrant ? "yes" : "no"}</p>
      </div>
    </article>
  );
}

function PolicyImpactCard({ impact }: { impact: ApiResult<PolicyImpact> }) {
  if (impact.error) {
    return <ErrorState error={impact.error} />;
  }

  if (!impact.data) {
    return <EmptyState message="No published policy is available for impact analysis." />;
  }

  return (
    <section className="rounded-3xl border border-slate-800 bg-slate-900 p-6">
      <div className="mb-5">
        <h2 className="text-2xl font-semibold">Policy impact</h2>
        <p className="mt-1 text-sm text-slate-400">
          First published policy matched to artifact versions for publish-risk review.
        </p>
      </div>
      <article className="rounded-2xl border border-slate-800 bg-slate-950 p-4">
        <div className="flex items-center justify-between gap-3">
          <div>
            <h3 className="font-semibold">{impact.data.policyKey}</h3>
            <p className="mt-1 text-sm text-slate-400">Version {impact.data.versionLabel}</p>
          </div>
          <StatusBadge status={`${impact.data.affectedArtifactCount} affected`} />
        </div>
        <div className="mt-3 grid gap-2 text-xs text-slate-500">
          <p>Restricted rules: {impact.data.restrictedRuleCount}</p>
          {impact.data.affectedArtifacts.slice(0, 5).map((artifact) => (
            <p key={artifact.artifactId}>
              {artifact.artifactName}: {artifact.policyRiskStatus}
            </p>
          ))}
        </div>
      </article>
    </section>
  );
}

export default async function Home() {
  const [health, identity, governance, artifactRegistry, classificationPolicy] = await Promise.all([
    getPlatformHealth(),
    getIdentityLists(),
    getGovernanceLists(),
    getArtifactRegistryLists(),
    getClassificationPolicyLists(),
  ]);
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
                EnterpriseThreadOS admin foundation
              </h1>
              <p className="mt-4 max-w-2xl text-base leading-7 text-slate-300">
                This shell lists the tenant identity, governance audit, and artifact
                registry foundations resolved through the backend API, including
                classification and policy enforcement records.
              </p>
            </div>
            <div className="flex flex-wrap items-center gap-3">
              <Link
                href="/explorers"
                className="rounded-full border border-cyan-300 px-4 py-2 text-sm font-semibold text-cyan-100 transition hover:bg-cyan-300 hover:text-slate-950"
              >
                Explorers
              </Link>
              <Link
                href="/artifacts"
                className="rounded-full border border-cyan-300 px-4 py-2 text-sm font-semibold text-cyan-100 transition hover:bg-cyan-300 hover:text-slate-950"
              >
                Artifacts
              </Link>
              <Link
                href="/model-artifacts"
                className="rounded-full bg-cyan-300 px-4 py-2 text-sm font-semibold text-slate-950 transition hover:bg-cyan-200"
              >
                Model artifacts
              </Link>
              <Link
                href="/imports"
                className="rounded-full border border-cyan-300 px-4 py-2 text-sm font-semibold text-cyan-100 transition hover:bg-cyan-300 hover:text-slate-950"
              >
                Imports
              </Link>
              <Link
                href="/documents"
                className="rounded-full border border-cyan-300 px-4 py-2 text-sm font-semibold text-cyan-100 transition hover:bg-cyan-300 hover:text-slate-950"
              >
                Documents
              </Link>
              <Link
                href="/ai-traces"
                className="rounded-full border border-cyan-300 px-4 py-2 text-sm font-semibold text-cyan-100 transition hover:bg-cyan-300 hover:text-slate-950"
              >
                AI Traces
              </Link>
              <Link
                href="/chat"
                className="rounded-full border border-cyan-300 px-4 py-2 text-sm font-semibold text-cyan-100 transition hover:bg-cyan-300 hover:text-slate-950"
              >
                Governed Chat
              </Link>
              <Link
                href="/dashboards"
                className="rounded-full border border-cyan-300 px-4 py-2 text-sm font-semibold text-cyan-100 transition hover:bg-cyan-300 hover:text-slate-950"
              >
                Dashboards
              </Link>
              <Link
                href="/reports"
                className="rounded-full border border-cyan-300 px-4 py-2 text-sm font-semibold text-cyan-100 transition hover:bg-cyan-300 hover:text-slate-950"
              >
                Reports
              </Link>
              <Link
                href="/recommendations"
                className="rounded-full border border-cyan-300 px-4 py-2 text-sm font-semibold text-cyan-100 transition hover:bg-cyan-300 hover:text-slate-950"
              >
                Recommendations
              </Link>
              <StatusBadge status={health?.status ?? "unavailable"} />
            </div>
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
          <div className="rounded-2xl border border-slate-800 bg-slate-900 p-5">
            <p className="text-sm text-slate-400">Selected tenant</p>
            <p className="mt-2 break-all font-mono text-sm text-cyan-200">
              {identity.activeTenantId ?? selectedTenantId ?? "not selected"}
            </p>
          </div>
          <div className="rounded-2xl border border-slate-800 bg-slate-900 p-5">
            <p className="text-sm text-slate-400">Admin user header</p>
            <p className="mt-2 break-all font-mono text-sm text-cyan-200">
              {identity.activeUserId ?? adminUserId ?? "not selected"}
            </p>
          </div>
        </section>

        <section className="grid gap-4 lg:grid-cols-2">
          <ListSection
            title="Tenants"
            description="Platform tenants available to Finbuckle tenant resolution."
            result={identity.tenants}
            emptyMessage="No tenants have been created yet."
            renderItem={TenantCard}
          />
          <ListSection
            title="Users"
            description="ASP.NET Identity users available for tenant memberships."
            result={identity.users}
            emptyMessage="No users have been created yet."
            renderItem={UserCard}
          />
          <ListSection
            title="Tenant roles"
            description="Roles in the selected tenant context."
            result={identity.roles}
            emptyMessage="No tenant roles are available for the selected tenant."
            renderItem={RoleCard}
          />
          <ListSection
            title="Memberships"
            description="User-to-role assignments in the selected tenant."
            result={identity.memberships}
            emptyMessage="No memberships are available for the selected tenant."
            renderItem={MembershipCard}
          />
          <ListSection
            title="Access grants"
            description="Temporary and permanent permission grants in the selected tenant."
            result={identity.grants}
            emptyMessage="No grants are available for the selected tenant."
            renderItem={GrantCard}
          />
        </section>

        <section className="grid gap-4 lg:grid-cols-2">
          <ListSection
            title="Artifacts"
            description="Governed BaseArtifact records in the selected tenant. Open the artifact explorer for full lists and 360° views."
            result={artifactRegistry.artifacts}
            emptyMessage="No artifacts are available for the selected tenant."
            renderItem={ArtifactCard}
          />
          <ListSection
            title="Artifact versions"
            description="Immutable version history for the first artifact in the explorer list."
            result={artifactRegistry.versions}
            emptyMessage="No versions are available for the first artifact."
            renderItem={ArtifactVersionCard}
          />
          <ListSection
            title="Artifact relationships"
            description="Generic relationships from the first artifact in the explorer list."
            result={artifactRegistry.relationships}
            emptyMessage="No relationships are available for the first artifact."
            renderItem={ArtifactRelationshipCard}
          />
          <ListSection
            title="Artifact dependencies"
            description="Dependency edges for the latest version of the first artifact."
            result={artifactRegistry.dependencies}
            emptyMessage="No dependencies are available for the first artifact version."
            renderItem={ArtifactDependencyCard}
          />
        </section>

        <section className="grid gap-4 lg:grid-cols-2">
          <ListSection
            title="Classification schemes"
            description="Versioned classification schemes available in the selected tenant."
            result={classificationPolicy.schemes}
            emptyMessage="No classification schemes are available for the selected tenant."
            renderItem={ClassificationSchemeCard}
          />
          <ListSection
            title="Policy versions"
            description="Tenant policy versions that govern restricted context access."
            result={classificationPolicy.policies}
            emptyMessage="No policy versions are available for the selected tenant."
            renderItem={PolicyVersionCard}
          />
          <ListSection
            title="Restricted context rules"
            description="Rules that split allowed context from denied summaries and sensitive references."
            result={classificationPolicy.rules}
            emptyMessage="No restricted context rules are available for the selected tenant."
            renderItem={RestrictedContextRuleCard}
          />
          <PolicyImpactCard impact={classificationPolicy.impact} />
        </section>

        <section className="grid gap-4 lg:grid-cols-2">
          <ListSection
            title="Audit records"
            description="Tenant-scoped actions, denials, and governance-relevant runtime summaries."
            result={governance.auditRecords}
            emptyMessage="No audit records are available for the selected tenant."
            renderItem={AuditRecordCard}
          />
          <ListSection
            title="Security events"
            description="Security-relevant denials and policy violation placeholders ready for later review tasks."
            result={governance.securityEvents}
            emptyMessage="No security events are available for the selected tenant."
            renderItem={SecurityEventCard}
          />
        </section>

        <section className="rounded-3xl border border-slate-800 bg-slate-900 p-6">
          <div className="mb-5 flex items-center justify-between gap-4">
            <div>
              <h2 className="text-2xl font-semibold">Infrastructure health</h2>
              <p className="mt-1 text-sm text-slate-400">
                PostgreSQL, Neo4j, Qdrant, MinIO, Redis, and RabbitMQ.
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
