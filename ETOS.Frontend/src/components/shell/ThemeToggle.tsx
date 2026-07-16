"use client";

import { useEffect, useSyncExternalStore } from "react";
import { useTheme } from "next-themes";
import { Moon, Sun } from "lucide-react";

const emptySubscribe = () => () => {};

export function ThemeToggle() {
  const { theme, resolvedTheme, setTheme } = useTheme();
  // Hydration-safe mounted flag: false on server render, true on client.
  const mounted = useSyncExternalStore(
    emptySubscribe,
    () => true,
    () => false,
  );

  // Migrate legacy "system" storage value to an explicit light/dark theme.
  useEffect(() => {
    if (theme === "system") {
      setTheme(resolvedTheme === "dark" ? "dark" : "light");
    }
  }, [theme, resolvedTheme, setTheme]);

  const isDark = mounted && resolvedTheme === "dark";
  const nextTheme = isDark ? "light" : "dark";

  return (
    <button
      type="button"
      onClick={() => setTheme(nextTheme)}
      aria-label={isDark ? "Switch to light theme" : "Switch to dark theme"}
      title={isDark ? "Theme: dark — click for light" : "Theme: light — click for dark"}
      className="inline-flex h-9 w-9 items-center justify-center rounded-full border border-etos-border text-etos-ink-muted transition hover:bg-etos-panel-muted hover:text-etos-ink focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-etos-accent"
    >
      {!mounted ? (
        <Sun className="h-4 w-4" />
      ) : isDark ? (
        <Moon className="h-4 w-4" />
      ) : (
        <Sun className="h-4 w-4" />
      )}
    </button>
  );
}
