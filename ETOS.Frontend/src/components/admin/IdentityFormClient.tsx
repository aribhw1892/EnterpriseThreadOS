"use client";

import { useActionState, type ReactNode } from "react";
import { ErrorState } from "@/components/ui/ErrorState";
import { Notice } from "@/components/ui/Notice";

export type IdentityFormState = { error?: string; success?: string } | null;

export function IdentityFormClient({
  action,
  children,
}: {
  action: (
    prev: IdentityFormState,
    formData: FormData,
  ) => Promise<IdentityFormState>;
  children: ReactNode;
}) {
  const [state, formAction, pending] = useActionState(action, null);

  return (
    <form action={formAction} className="flex flex-col gap-3">
      {state?.error ? <ErrorState error={state.error} /> : null}
      {state?.success ? <Notice variant="success">{state.success}</Notice> : null}
      <fieldset disabled={pending} className="flex flex-col gap-3 border-0 p-0">
        {children}
      </fieldset>
    </form>
  );
}
