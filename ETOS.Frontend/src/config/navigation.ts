export type NavGroup = "operate" | "govern" | "model" | "build" | "admin";

export type NavItem = {
  href: string;
  label: string;
  group: NavGroup;
  implemented: boolean;
  blockerIssue?: string;
};

export const navGroupLabels: Record<NavGroup, string> = {
  operate: "Operate",
  govern: "Govern",
  model: "Model",
  build: "Build",
  admin: "Admin",
};

export const navGroupOrder: NavGroup[] = [
  "operate",
  "govern",
  "model",
  "build",
  "admin",
];

/**
 * Single navigation source for sidebar + placeholders (UI-0.4 contract).
 * `implemented: false` still navigates — to an honest placeholder page.
 */
export const navItems: NavItem[] = [
  // Operate
  { href: "/", label: "Mission Control", group: "operate", implemented: true },
  { href: "/imports", label: "Imports", group: "operate", implemented: true },
  { href: "/documents", label: "Documents", group: "operate", implemented: true },
  { href: "/graph", label: "Graph", group: "operate", implemented: true },
  { href: "/explorers/graph", label: "Graph canvas", group: "operate", implemented: true },
  { href: "/explorers", label: "Explorers", group: "operate", implemented: true },
  { href: "/artifacts", label: "Artifacts", group: "operate", implemented: true },
  { href: "/chat", label: "Governed Chat", group: "operate", implemented: true },
  { href: "/dashboards", label: "Dashboards", group: "operate", implemented: true },
  { href: "/reports", label: "Reports", group: "operate", implemented: true },
  {
    href: "/recommendations",
    label: "Recommendations",
    group: "operate",
    implemented: true,
  },
  {
    href: "/context-packages",
    label: "Context Packages",
    group: "operate",
    implemented: true,
  },

  // Govern
  { href: "/ai-traces", label: "AI Traces", group: "govern", implemented: true },
  { href: "/governance", label: "Governance", group: "govern", implemented: true },
  { href: "/decisions", label: "Decisions", group: "govern", implemented: true },
  {
    href: "/learning-signals",
    label: "Learning Signals",
    group: "govern",
    implemented: true,
  },
  { href: "/tasks", label: "Review Tasks", group: "govern", implemented: true },

  // Model
  {
    href: "/model-artifacts",
    label: "Model Packages",
    group: "model",
    implemented: true,
  },
  { href: "/capabilities", label: "Capabilities", group: "model", implemented: true },
  {
    href: "/business-policies",
    label: "Business Policies",
    group: "model",
    implemented: true,
  },
  {
    href: "/optimization-models",
    label: "Optimization Models",
    group: "model",
    implemented: true,
  },
  {
    href: "/agent-templates",
    label: "Agent Templates",
    group: "model",
    implemented: true,
  },

  // Build
  { href: "/tools", label: "Tools & Connectors", group: "build", implemented: true },
  { href: "/agents", label: "Agents", group: "build", implemented: true },
  { href: "/agent-runs", label: "Agent Runs", group: "build", implemented: true },
  { href: "/workflows", label: "Workflows", group: "build", implemented: true },
  {
    href: "/agent-teams",
    label: "Agent Teams",
    group: "build",
    implemented: false,
    blockerIssue: "Issue 25",
  },
  {
    href: "/digital-thread/timeline",
    label: "Digital Thread",
    group: "build",
    implemented: true,
  },

  // Admin
  {
    href: "/admin/foundation",
    label: "Foundation",
    group: "admin",
    implemented: true,
  },
  {
    href: "/admin/identity",
    label: "Identity",
    group: "admin",
    implemented: true,
  },
  {
    href: "/admin/settings",
    label: "Settings",
    group: "admin",
    implemented: false,
  },
];
