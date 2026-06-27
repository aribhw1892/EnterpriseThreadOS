from __future__ import annotations

import json

import pytest

from app.model_router import (
    provider_has_api_key,
    should_use_deterministic_mock,
)


@pytest.fixture(autouse=True)
def clear_openai_api_key(monkeypatch: pytest.MonkeyPatch) -> None:
    monkeypatch.delenv("OPENAI_API_KEY", raising=False)


def test_openai_compatible_available_with_base_url_without_api_key(
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    monkeypatch.setenv("OPENAI_BASE_URL", "http://localhost:1234/v1")
    monkeypatch.delenv("OPENAI_API_KEY", raising=False)

    assert provider_has_api_key("openai-compatible") is True
    assert should_use_deterministic_mock("openai-compatible") is False


def test_execute_includes_tool_output_summaries_in_prompt() -> None:
    from fastapi.testclient import TestClient

    from app.main import app

    client = TestClient(app)
    payload = {
        "governedContextSummaryJson": json.dumps({"entityCount": 1}),
        "promptTemplateBody": "Map import columns.",
        "outputSchemaJson": json.dumps(
            {
                "type": "object",
                "required": ["summary"],
                "properties": {"summary": {"type": "string"}},
            }
        ),
        "primaryModelProviderKey": "openai",
        "primaryModelId": "gpt-4o-mini",
        "fallbackModels": [],
        "toolOutputSummariesJson": json.dumps(
            [
                {
                    "toolRunId": "00000000-0000-0000-0000-000000000001",
                    "status": "Succeeded",
                    "outputSafeSummaryJson": json.dumps(
                        {"providerKey": "rule-based-v1", "columnSuggestions": []}
                    ),
                }
            ]
        ),
        "preview": True,
    }

    response = client.post("/v1/execute", json=payload)
    assert response.status_code == 200
    assert response.json()["status"] == "Succeeded"
