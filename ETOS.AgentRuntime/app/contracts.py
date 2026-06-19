from __future__ import annotations

from pydantic import BaseModel, ConfigDict, Field


class FallbackModelConfig(BaseModel):
    model_config = ConfigDict(populate_by_name=True)

    provider_key: str = Field(alias="providerKey")
    model_id: str = Field(alias="modelId")
    trigger_reason: str | None = Field(default=None, alias="triggerReason")


class ExecuteRequest(BaseModel):
    """Mirrors the HTTP payload sent by PydanticAiRuntimeAdapter (.NET)."""

    model_config = ConfigDict(populate_by_name=True)

    governed_context_summary_json: str = Field(alias="governedContextSummaryJson")
    prompt_template_body: str = Field(alias="promptTemplateBody")
    output_schema_json: str = Field(alias="outputSchemaJson")
    primary_model_provider_key: str = Field(alias="primaryModelProviderKey")
    primary_model_id: str = Field(alias="primaryModelId")
    fallback_models: list[FallbackModelConfig] = Field(
        default_factory=list,
        alias="fallbackModels",
    )
    structured_input_json: str | None = Field(default=None, alias="structuredInputJson")
    preview: bool = False


class ExecuteResponse(BaseModel):
    """Mirrors AgentRuntimeExecutionResult fields exposed over HTTP."""

    model_config = ConfigDict(populate_by_name=True)

    status: str
    structured_output_json: str | None = Field(default=None, alias="structuredOutputJson")
    trace_notes: list[str] = Field(default_factory=list, alias="traceNotes")
    model_used: str | None = Field(default=None, alias="modelUsed")
    fallback_applied: bool = Field(default=False, alias="fallbackApplied")
