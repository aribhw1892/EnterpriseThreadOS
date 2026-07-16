import type { ReactNode } from "react";

export type StepperStep = {
  id: string;
  label: string;
  description?: string;
};

export type StepperStatus = "complete" | "current" | "upcoming" | "blocked";

/**
 * Mockup horizontal stepper: numbered circles + connector lines
 * (Source → Mapping → Validate → Identity → Commit).
 */
export function Stepper({
  steps,
  currentStepId,
  statusForStep,
  className = "",
}: {
  steps: StepperStep[];
  currentStepId: string;
  statusForStep?: (stepId: string) => StepperStatus;
  className?: string;
}) {
  const currentIndex = Math.max(
    0,
    steps.findIndex((step) => step.id === currentStepId),
  );

  return (
    <ol
      className={`mb-4 mt-2 flex flex-wrap items-center gap-2 ${className}`}
      aria-label="Progress"
    >
      {steps.map((step, index) => {
        const status =
          statusForStep?.(step.id) ??
          (index < currentIndex
            ? "complete"
            : index === currentIndex
              ? "current"
              : "upcoming");

        const numClass =
          status === "complete"
            ? "bg-etos-success-fg text-white"
            : status === "current"
              ? "bg-etos-accent text-white"
              : status === "blocked"
                ? "bg-etos-danger-fg text-white"
                : "bg-etos-border-soft text-etos-ink-muted";

        return (
          <li key={step.id} className="flex min-w-0 flex-1 items-center gap-2">
            <div
              className="flex items-center gap-2"
              aria-current={status === "current" ? "step" : undefined}
            >
              <span
                className={`grid h-7 w-7 place-items-center rounded-full text-xs font-black ${numClass}`}
              >
                {status === "complete" ? "✓" : index + 1}
              </span>
              <span className="text-xs font-extrabold text-etos-ink-muted">
                {step.label}
              </span>
            </div>
            {index < steps.length - 1 ? (
              <span
                aria-hidden
                className={`h-0.5 min-w-[30px] flex-1 ${
                  status === "complete"
                    ? "bg-etos-success-border"
                    : "bg-etos-border-soft"
                }`}
              />
            ) : null}
          </li>
        );
      })}
    </ol>
  );
}

export function StepperLinkRow({ children }: { children: ReactNode }) {
  return <div className="mt-4 flex flex-wrap gap-3">{children}</div>;
}
