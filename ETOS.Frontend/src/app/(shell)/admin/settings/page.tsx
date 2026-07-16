import { PlaceholderPage } from "@/components/placeholders/PlaceholderPage";

export default function AdminSettingsPage() {
  return (
    <PlaceholderPage
      title="Settings"
      description="Tenant branding, notification preferences, and platform configuration. No settings API exists in the current backend scope — this page is a static placeholder."
      primaryAction={{
        label: "Save settings",
        reason: "No settings API in current backend scope",
      }}
    />
  );
}
