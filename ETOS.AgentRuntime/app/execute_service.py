from __future__ import annotations

import json
import re
from typing import Any

from pydantic_ai import Agent, NativeOutput, StructuredDict, ToolOutput

from app.contracts import ExecuteRequest, ExecuteResponse
from app.model_router import (
    ModelCandidate,
    RetriableModelError,
    build_model_chain,
    create_pydantic_ai_model,
    format_model_used,
    should_use_deterministic_mock,
)

EXECUTION_STATUS_SUCCEEDED = "Succeeded"
EXECUTION_STATUS_FAILED = "Failed"

_STRING_MOCKS = {
    "sourceColumn": "partNumber",
    "canonicalObjectType": "part",
    "canonicalAttributeKey": "partNumber",
    "sourceValue": "released",
    "canonicalLifecycleKey": "released",
    "rationale": "Example mapping rationale.",
    "message": "Hi",
}


def _parse_json_object(raw: str | None, field_name: str) -> dict[str, Any]:
    if raw is None or not raw.strip():
        return {}
    try:
        parsed = json.loads(raw)
    except json.JSONDecodeError as exc:
        raise ValueError(f"{field_name} must be valid JSON.") from exc
    if not isinstance(parsed, dict):
        raise ValueError(f"{field_name} must be a JSON object.")
    return parsed


def _parse_json_array(raw: str | None, field_name: str) -> list[Any]:
    if raw is None or not raw.strip():
        return []
    try:
        parsed = json.loads(raw)
    except json.JSONDecodeError as exc:
        raise ValueError(f"{field_name} must be valid JSON.") from exc
    if not isinstance(parsed, list):
        raise ValueError(f"{field_name} must be a JSON array.")
    return parsed


def _parse_output_schema(raw: str) -> dict[str, Any]:
    schema = _parse_json_object(raw, "outputSchemaJson")
    if schema.get("type") not in (None, "object"):
        raise ValueError("outputSchemaJson root type must be 'object'.")
    return schema


def _render_prompt(request: ExecuteRequest) -> str:
    context = _parse_json_object(
        request.governed_context_summary_json,
        "governedContextSummaryJson",
    )
    structured_input = _parse_json_object(
        request.structured_input_json,
        "structuredInputJson",
    )
    sections = [
        request.prompt_template_body.strip(),
        "Governed context summary (pre-filtered by platform):",
        json.dumps(context, indent=2, sort_keys=True),
    ]
    if structured_input:
        sections.extend(
            [
                "Structured user input:",
                json.dumps(structured_input, indent=2, sort_keys=True),
            ]
        )
    if request.tool_output_summaries_json and request.tool_output_summaries_json.strip():
        tool_outputs = _parse_json_array(
            request.tool_output_summaries_json,
            "toolOutputSummariesJson",
        )
        sections.extend(
            [
                "Tool output summaries (deterministic hints):",
                json.dumps(tool_outputs, indent=2, sort_keys=True),
            ]
        )
    if request.preview:
        sections.append("Preview mode: produce structured output only; no side effects.")
    return "\n\n".join(section for section in sections if section)


def _mock_value_for_property(name: str, prop_schema: dict[str, Any]) -> Any:
    if "const" in prop_schema:
        return prop_schema["const"]
    if "enum" in prop_schema and prop_schema["enum"]:
        return prop_schema["enum"][0]

    prop_type = prop_schema.get("type")
    if isinstance(prop_type, list):
        prop_type = next((item for item in prop_type if item != "null"), prop_type[0])

    if prop_type == "string":
        return _STRING_MOCKS.get(name, f"mock-{name}")
    if prop_type == "integer":
        return 0
    if prop_type == "number":
        return 0.0
    if prop_type == "boolean":
        if name == "isIdentityField":
            return True
        if name == "isRequired":
            return True
        return False
    if prop_type == "array":
        item_schema = prop_schema.get("items", {"type": "string"})
        return [_mock_value_for_property(f"{name}Item", item_schema)]
    if prop_type == "object":
        nested = prop_schema.get("properties", {})
        required = prop_schema.get("required", list(nested.keys()))
        return {
            nested_name: _mock_value_for_property(nested_name, nested[nested_name])
            for nested_name in required
            if nested_name in nested
        }
    return f"mock-{name}"


def _generate_deterministic_output(
    schema: dict[str, Any],
    structured_input: dict[str, Any],
) -> dict[str, Any]:
    properties = schema.get("properties", {})
    required = schema.get("required", list(properties.keys()))
    output: dict[str, Any] = {}

    for name in required:
        if name not in properties:
            output[name] = f"mock-{name}"
            continue
        if name in structured_input:
            output[name] = structured_input[name]
            continue
        output[name] = _mock_value_for_property(name, properties[name])

    for name, prop_schema in properties.items():
        if name in output:
            continue
        output[name] = _mock_value_for_property(name, prop_schema)

    return output


def _strip_markdown_fence(text: str) -> str:
    stripped = text.strip()
    if not stripped.startswith("```"):
        return stripped
    stripped = re.sub(r"^```(?:json)?\s*", "", stripped, flags=re.IGNORECASE)
    return re.sub(r"\s*```$", "", stripped).strip()


def _looks_like_json_schema_document(output: dict[str, Any]) -> bool:
    if "properties" not in output:
        return False
    return "type" in output or "required" in output


def _extract_json_object(text: str) -> dict[str, Any]:
    stripped = _strip_markdown_fence(text)
    if not stripped:
        raise ValueError("Model returned empty output.")

    try:
        parsed = json.loads(stripped)
        if isinstance(parsed, dict):
            return parsed
    except json.JSONDecodeError:
        pass

    match = re.search(r"\{[\s\S]*\}", stripped)
    if match:
        parsed = json.loads(match.group(0))
        if isinstance(parsed, dict):
            return parsed

    raise ValueError("Model output is not a JSON object.")


def _validate_required_fields(output: dict[str, Any], schema: dict[str, Any]) -> None:
    if _looks_like_json_schema_document(output):
        raise ValueError(
            "Model returned a JSON Schema document instead of task data. "
            "Return a data object with the required fields filled in, "
            "not a schema with properties/type/required at the root."
        )

    required = schema.get("required", [])
    missing = [name for name in required if name not in output]
    if missing:
        joined = ", ".join(missing)
        received = ", ".join(sorted(output.keys())) or "(empty object)"
        raise ValueError(
            f"Structured output missing required fields: {joined}. "
            f"Received top-level keys: {received}."
        )


def _validate_value_against_schema(value: Any, schema: dict[str, Any], path: str) -> None:
    schema_type = schema.get("type")
    if isinstance(schema_type, list):
        schema_type = next((item for item in schema_type if item != "null"), schema_type[0])

    if schema_type == "object":
        if not isinstance(value, dict):
            raise ValueError(f"{path} must be a JSON object.")
        properties = schema.get("properties", {})
        required = schema.get("required", [])
        missing = [name for name in required if name not in value]
        if missing:
            joined = ", ".join(missing)
            raise ValueError(f"{path} missing required fields: {joined}.")
        for name, prop_schema in properties.items():
            if name in value:
                _validate_value_against_schema(value[name], prop_schema, f"{path}.{name}")
        return

    if schema_type == "array":
        if not isinstance(value, list):
            raise ValueError(f"{path} must be a JSON array.")
        item_schema = schema.get("items")
        if item_schema:
            for index, item in enumerate(value):
                _validate_value_against_schema(item, item_schema, f"{path}[{index}]")
        return

    if schema_type == "string" and not isinstance(value, str):
        raise ValueError(f"{path} must be a string.")
    if schema_type in {"number", "integer"} and not isinstance(value, (int, float)):
        raise ValueError(f"{path} must be a number.")
    if schema_type == "boolean" and not isinstance(value, bool):
        raise ValueError(f"{path} must be a boolean.")


def _validate_schema_output(output: dict[str, Any], schema: dict[str, Any]) -> None:
    _validate_required_fields(output, schema)
    _validate_value_against_schema(output, schema, "output")


def _coerce_structured_output(raw_output: Any) -> dict[str, Any]:
    if isinstance(raw_output, dict):
        return dict(raw_output)
    return _extract_json_object(str(raw_output))


def _build_structured_output_type(schema: dict[str, Any], *, native: bool):
    structured = StructuredDict(schema, name="AgentStructuredOutput")
    return NativeOutput(structured) if native else ToolOutput(structured)


def _build_structured_user_prompt(
    prompt: str,
    schema: dict[str, Any],
    structured_input: dict[str, Any],
) -> str:
    example_output = _generate_deterministic_output(schema, structured_input)
    example_json = json.dumps(example_output, indent=2, sort_keys=True)
    sections = [
        prompt,
        "Return one structured JSON object that satisfies the output schema.",
    ]
    if "columnSuggestions" in schema.get("properties", {}):
        sections.extend(
            [
                "Every column suggestion must include canonicalAttributeKey.",
                "Set isIdentityField=true when the source column is the primary identifier.",
            ]
        )
    sections.extend(
        [
            "Example complete response shape:",
            example_json,
        ]
    )
    return "\n\n".join(sections)


async def _run_structured_agent(
    model,
    output_type,
    user_prompt: str,
) -> dict[str, Any]:
    agent = Agent(
        model,
        output_type=output_type,
        system_prompt=(
            "You are a governed EnterpriseThreadOS agent. "
            "Respond with structured task DATA only. "
            "Never return a JSON Schema document. "
            "Do not execute tools or access databases."
        ),
    )
    result = await agent.run(user_prompt)
    return _coerce_structured_output(result.output)


async def _run_with_model(
    candidate: ModelCandidate,
    prompt: str,
    schema: dict[str, Any],
    structured_input: dict[str, Any],
) -> tuple[dict[str, Any], list[str]]:
    trace_notes: list[str] = []

    if should_use_deterministic_mock(candidate.provider_key):
        trace_notes.append(
            "Deterministic mock output generated because no API key is configured "
            f"for provider '{candidate.provider_key}'."
        )
        output = _generate_deterministic_output(schema, structured_input)
        _validate_schema_output(output, schema)
        return output, trace_notes

    model = create_pydantic_ai_model(candidate)
    user_prompt = _build_structured_user_prompt(prompt, schema, structured_input)
    last_error: Exception | None = None

    for native in (True, False):
        mode = "native" if native else "tool"
        try:
            output = await _run_structured_agent(
                model,
                _build_structured_output_type(schema, native=native),
                user_prompt,
            )
            _validate_schema_output(output, schema)
            trace_notes.append(f"Structured output produced by PydanticAI {mode} output mode.")
            return output, trace_notes
        except Exception as exc:
            last_error = exc
            trace_notes.append(f"PydanticAI {mode} structured output failed: {exc}")

    raise last_error or ValueError("Structured output generation failed.")


async def execute_request(request: ExecuteRequest) -> ExecuteResponse:
    trace_notes: list[str] = []
    try:
        schema = _parse_output_schema(request.output_schema_json)
        structured_input = _parse_json_object(
            request.structured_input_json,
            "structuredInputJson",
        )
        prompt = _render_prompt(request)
        chain = build_model_chain(request)

        last_error: Exception | None = None
        for candidate in chain:
            try:
                output, candidate_notes = await _run_with_model(
                    candidate,
                    prompt,
                    schema,
                    structured_input,
                )
                trace_notes.extend(candidate_notes)
                if candidate.is_fallback:
                    reason = candidate.trigger_reason or "primary model unavailable"
                    trace_notes.append(f"Fallback model applied ({reason}).")
                return ExecuteResponse(
                    status=EXECUTION_STATUS_SUCCEEDED,
                    structured_output_json=json.dumps(output, sort_keys=True),
                    trace_notes=trace_notes,
                    model_used=format_model_used(candidate),
                    fallback_applied=candidate.is_fallback,
                )
            except RetriableModelError as exc:
                last_error = exc
                trace_notes.append(str(exc))
                continue
            except Exception as exc:
                if candidate is chain[-1]:
                    raise
                last_error = exc
                trace_notes.append(
                    f"Model '{format_model_used(candidate)}' failed: {exc}"
                )
                continue

        message = str(last_error) if last_error else "No models were available."
        return ExecuteResponse(
            status=EXECUTION_STATUS_FAILED,
            structured_output_json=None,
            trace_notes=[*trace_notes, message],
            model_used=None,
            fallback_applied=False,
        )
    except Exception as exc:
        trace_notes.append(str(exc))
        return ExecuteResponse(
            status=EXECUTION_STATUS_FAILED,
            structured_output_json=None,
            trace_notes=trace_notes,
            model_used=None,
            fallback_applied=False,
        )
