# EnterpriseThreadOS Design System — Light & Dark Mode

Companion to `engineering-execution-ui-issues.md`. Extracts visual language from the mockup pack (light shell HTML for screens 02–37, e.g. `html/01-command-center-legacy-executive.html`) and defines dual-theme tokens for Tailwind 4 implementation.

**Theme rule:** every chrome surface and product screen — including sidebar and Mission Control (`/`) — follows the light/dark toggle. Dark mode is a deliberate second palette, not an inversion. Mockup PNGs that ship dark-only (e.g. screen 01 Mission Control) define the **dark** ops look; light mode uses the same layout with workspace-light ops tokens.

**Implementation status (2026-07-16):** Tokens + `next-themes` live in `ETOS.Frontend/src/app/globals.css`. Shared primitives live under `src/components/ui/` (**17** files: Badge, Button, Card, PageHeader, KpiCard, Empty/ErrorState, DataTable + TanStackDataTable, Tabs, Stepper, TraceTimeline, Timeline, ListItem, SidePanel/PillStack, GovernancePanel, Notice/Callout). Gallery: `/dev/ui-kit`. Gold pages (Phases 0–4) use these primitives; remaining slate dumps (`/tasks`, `/decisions`, `/context-packages`, `/admin/foundation`) still open.

---

## Design Intent

Most mockups use a **light enterprise workspace** with a **light sidebar** (white → soft slate gradient, dark ink). Content areas are bright, card-based, and audit-friendly. Status semantics (success, warning, danger, info) stay consistent across themes so operators recognize risk at a glance.

**Dark mode** uses a navy sidebar, deep canvas, elevated panels, and muted table rows. **Mission Control home (`/`)** and digital-thread screens use **ops-canvas** tokens that switch with the theme (light workspace panels in light mode; near-black ops panels in dark mode).

---

## Layout Constants

| Constant | Value | Notes |
| --- | --- | --- |
| Sidebar width | `280px` | Matches mockup grid `grid-template-columns: 280px 1fr` |
| Top bar height | `74px` | Breadcrumb + search + pills |
| Content padding | `26px 30px` | Mockup `.content` |
| Card radius | `18px` (`--radius`) | Cards, side panels |
| Button radius | `12px` | CTAs, inputs |
| Pill radius | `999px` | Badges, search, tabs |

---

## CSS Custom Properties

Implement in `ETOS.Frontend/src/app/globals.css`:

```css
:root {
  /* Surfaces */
  --etos-canvas: #f5f7fb;
  --etos-canvas-gradient-from: #f7f9fc;
  --etos-canvas-gradient-to: #edf3fb;
  --etos-panel: #ffffff;
  --etos-panel-muted: #f8fafc;
  --etos-panel-elevated: rgba(255, 255, 255, 0.92);

  /* Ink */
  --etos-ink: #0f172a;
  --etos-ink-muted: #64748b;
  --etos-ink-subtle: #94a3b8;

  /* Borders */
  --etos-border: #d8e0ea;
  --etos-border-soft: #e2e8f0;
  --etos-border-panel: rgba(203, 213, 225, 0.9);

  /* Sidebar — light theme */
  --etos-nav-from: #ffffff;
  --etos-nav-to: #f1f5f9;
  --etos-nav-ink: #334155;
  --etos-nav-muted: #64748b;
  --etos-nav-section: #94a3b8;
  --etos-nav-brand: #0f172a;
  --etos-nav-active-ink: #0f172a;
  --etos-nav-active-border: rgba(37, 99, 235, 0.28);
  --etos-nav-active-bg: linear-gradient(
    90deg,
    rgba(14, 165, 233, 0.14),
    rgba(124, 58, 237, 0.1)
  );
  --etos-nav-hover: rgba(15, 23, 42, 0.05);
  --etos-nav-divider: rgba(15, 23, 42, 0.08);
  --etos-nav-soon-border: rgba(15, 23, 42, 0.12);

  /* Accents */
  --etos-accent: #2563eb;
  --etos-accent-indigo: #4f46e5;
  --etos-accent-cyan: #0ea5e9;
  --etos-accent-purple: #7c3aed;

  /* Status — foreground / background pairs */
  --etos-success-fg: #166534;
  --etos-success-bg: #dcfce7;
  --etos-success-border: #bbf7d0;
  --etos-warning-fg: #92400e;
  --etos-warning-bg: #fef3c7;
  --etos-warning-border: #fde68a;
  --etos-danger-fg: #991b1b;
  --etos-danger-bg: #fee2e2;
  --etos-danger-border: #fecaca;
  --etos-info-fg: #1d4ed8;
  --etos-info-bg: #dbeafe;
  --etos-info-border: #bfdbfe;
  --etos-neutral-fg: #475569;
  --etos-neutral-bg: #f1f5f9;
  --etos-neutral-border: #e2e8f0;
  --etos-teal-fg: #0f766e;
  --etos-teal-bg: #ccfbf1;
  --etos-teal-border: #99f6e4;
  --etos-purple-fg: #5b21b6;
  --etos-purple-bg: #ede9fe;
  --etos-purple-border: #ddd6fe;

  /* Elevation */
  --etos-shadow: 0 18px 44px rgba(15, 23, 42, 0.1);

  /* Top bar */
  --etos-topbar-bg: rgba(255, 255, 255, 0.86);
  --etos-topbar-blur: 20px;
  --etos-tenant-pill-bg: #e0f2fe;
  --etos-tenant-pill-fg: #0369a1;
  --etos-tenant-pill-border: #bae6fd;

  /* Mission Control ops — light */
  --etos-ops-canvas: #f5f7fb;
  --etos-ops-panel: #ffffff;
  --etos-ops-panel-elevated: #ffffff;
  --etos-ops-border: #d8e0ea;
  --etos-ops-ink: #0f172a;
  --etos-ops-ink-muted: #64748b;
}

.dark {
  --etos-canvas: #0b1220;
  --etos-canvas-gradient-from: #0a101c;
  --etos-canvas-gradient-to: #0f172a;
  --etos-panel: #0f172a;
  --etos-panel-muted: #1e293b;
  --etos-panel-elevated: rgba(15, 23, 42, 0.92);

  --etos-ink: #e2e8f0;
  --etos-ink-muted: #94a3b8;
  --etos-ink-subtle: #64748b;

  --etos-border: #334155;
  --etos-border-soft: #1e293b;
  --etos-border-panel: rgba(51, 65, 85, 0.9);

  /* Sidebar — navy brand in dark */
  --etos-nav-from: #101a33;
  --etos-nav-to: #0b1224;
  --etos-nav-ink: #dbeafe;
  --etos-nav-muted: #93c5fd;
  --etos-nav-section: #7da0d5;
  --etos-nav-brand: #ffffff;
  --etos-nav-active-ink: #ffffff;
  --etos-nav-active-border: rgba(147, 197, 253, 0.22);
  --etos-nav-active-bg: linear-gradient(
    90deg,
    rgba(14, 165, 233, 0.22),
    rgba(124, 58, 237, 0.17)
  );
  --etos-nav-hover: rgba(255, 255, 255, 0.05);
  --etos-nav-divider: rgba(255, 255, 255, 0.1);
  --etos-nav-soon-border: rgba(255, 255, 255, 0.15);

  --etos-accent: #60a5fa;
  --etos-accent-indigo: #818cf8;
  --etos-accent-cyan: #38bdf8;
  --etos-accent-purple: #a78bfa;

  --etos-success-fg: #6ee7b7;
  --etos-success-bg: #052e2b;
  --etos-success-border: #115e59;
  --etos-warning-fg: #fcd34d;
  --etos-warning-bg: #422006;
  --etos-warning-border: #78350f;
  --etos-danger-fg: #fca5a5;
  --etos-danger-bg: #450a0a;
  --etos-danger-border: #7f1d1d;
  --etos-info-fg: #93c5fd;
  --etos-info-bg: #172554;
  --etos-info-border: #1e3a8a;
  --etos-neutral-fg: #cbd5e1;
  --etos-neutral-bg: #1e293b;
  --etos-neutral-border: #334155;
  --etos-teal-fg: #5eead4;
  --etos-teal-bg: #042f2e;
  --etos-teal-border: #0f766e;
  --etos-purple-fg: #c4b5fd;
  --etos-purple-bg: #2e1065;
  --etos-purple-border: #5b21b6;

  --etos-shadow: 0 18px 44px rgba(0, 0, 0, 0.35);

  --etos-topbar-bg: rgba(15, 23, 42, 0.86);
  --etos-tenant-pill-bg: #172554;
  --etos-tenant-pill-fg: #93c5fd;
  --etos-tenant-pill-border: #1e3a8a;

  /* Mission Control ops — dark (screen 01 PNG) */
  --etos-ops-canvas: #070b14;
  --etos-ops-panel: #0f172a;
  --etos-ops-panel-elevated: #152033;
  --etos-ops-border: #1e3a5f;
  --etos-ops-ink: #e2e8f0;
  --etos-ops-ink-muted: #7d93b5;
}
```

### Tailwind 4 `@theme inline` mapping

Map tokens to utilities, e.g.:

- `--color-etos-panel` → `bg-etos-panel`
- `--color-etos-ink` → `text-etos-ink`
- `--color-etos-border` → `border-etos-border`

### Mission Control / digital-thread ops canvas

Use on `/` (screen 01) and `/digital-thread/*` (38–40). Tokens **switch with theme**:

| Token | Light | Dark | Usage |
| --- | --- | --- | --- |
| `--etos-ops-canvas` | `#f5f7fb` | `#070b14` | Page background |
| `--etos-ops-panel` | `#ffffff` | `#0f172a` | Cards, rails |
| `--etos-ops-panel-elevated` | `#ffffff` | `#152033` | KPI cards, stream |
| `--etos-ops-border` | `#d8e0ea` | `#1e3a5f` | Panel borders |
| `--etos-ops-ink` / muted | slate ink | slate light | Labels, body |
| Heatmap (`--etos-ops-heat-0…4`) | soft blues → teal → amber | deep blues → teal → amber | Activity intensity |

Keep semantic status badges aligned with the main token set so Mission Control and light-shell routes share meaning.

---

## Component Recipes

### Badge variants

| Variant | Light | Dark | Use |
| --- | --- | --- | --- |
| `success` | green bg/fg | teal-green bg/fg | Published, healthy, trusted |
| `warning` | amber | amber | Staged, review needed |
| `danger` | red | red | Blocked, high risk, conflict |
| `info` | blue | blue | Trace, schema, intent |
| `purple` | purple | purple | Agent, workflow |
| `teal` | teal | teal | Graph, connector ok |
| `neutral` | gray | gray | Draft, unknown |

Always include text label; do not use color alone.

### Primary button

- Light: `linear-gradient(135deg, #2563eb, #4f46e5)`, white text, no border.
- Dark: same gradient with slightly lighter stops or solid `bg-etos-accent` if gradient low contrast.

### Card

- Background: `--etos-panel-elevated`
- Border: `--etos-border-panel`
- Shadow: `--etos-shadow`
- Radius: `18px`

### KPI card

- Label: uppercase, 12px, `--etos-ink-muted`
- Value: 28px, weight 900, `--etos-ink`
- Trend: `.up` success, `.warn` warning, `.bad` danger

### Sidebar nav item

- Default: `--etos-nav-ink` at 14px
- Active: `--etos-nav-active-bg`, `--etos-nav-active-ink` text, cyan glow dot
- Brand title: `--etos-nav-brand`
- Section label: uppercase 11px, `--etos-nav-section`
- Light: light panel sidebar; dark: navy brand sidebar

### Top bar search

- Width: `360px` desktop, full width mobile
- Placeholder: "Search artifacts, graph objects, traces, runs…"
- Style: pill input, muted background

---

## Digital Thread Timeline — Special Canvas Theme

Screens 38–40 use ops-canvas tokens (same light/dark switch as Mission Control). Optional scoped class `.digital-thread-canvas` may deepen the dark palette further when immersive zoom ships; light mode still follows workspace-light ops tokens.

---

## Theme Toggle UX

- Location: top bar, right cluster before avatar.
- Control: icon button cycle or dropdown: Light | Dark | System.
- No flash: `next-themes` with `attribute="class"` on `<html>`, `suppressHydrationWarning` on html tag.
- Respect `prefers-color-scheme` when System selected.

---

## Migration from Current Frontend

Current pages use `bg-slate-950 text-slate-100` patterns. Migration steps:

1. Land tokens + ThemeProvider (UI-0.1).
2. Introduce shell (UI-0.2)—shell owns canvas background.
3. Replace page-level backgrounds with transparent or `bg-etos-canvas`.
4. Map `StatusBadge` colors to badge variants.
5. Remove `@media (prefers-color-scheme: dark)` block from old `:root` once `next-themes` owns switching.

---

## Verification Checklist

- [ ] Sidebar flips with theme (light panel / dark navy)
- [ ] Mission Control `/` readable in light and dark (ops tokens)
- [ ] Table rows readable in both modes
- [ ] Primary CTA contrast ≥ 4.5:1
- [ ] Warning/danger badges distinguishable for deuteranopia
- [ ] Focus rings visible on nav, buttons, inputs in both modes
- [ ] Screenshot diff: screen 01 light, screen 01 dark, screen 38 timeline

---

*Reference mockups: `References/etos_ui_mockup_pack_with_digital_thread_timeline/etos_ui_mockups/images/`.*
