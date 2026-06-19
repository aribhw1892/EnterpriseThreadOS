from __future__ import annotations

import json
import os

import pytest
from fastapi.testclient import TestClient

from app.main import app

client = TestClient(app)

OUTPUT_SCHEMA = {
    "type": "object",
    "required": ["summary", "confidence"],
    "properties": {
        "summary": {"type": "string"},
        "confidence": {"type": "number"},
        "findings": {
            "type": "array",
            "items": {"type": "string"},
        },
    },
}


@pytest.fixture(autouse=True)
def clear_openai_api_key(monkeypatch: pytest.MonkeyPatch) -> None:
    monkeypatch.delenv("OPENAI_API_KEY", raising=False)


def test_health() -> None:
    response = client.get("/health")
    assert response.status_code == 200
    assert response.json() == {"status": "healthy"}


def test_execute_returns_deterministic_structured_output_without_api_key() -> None:
    payload = {
        "governedContextSummaryJson": json.dumps({"entityCount": 2}),
        "promptTemplateBody": "Analyze the governed context and summarize findings.",
        "outputSchemaJson": json.dumps(OUTPUT_SCHEMA),
        "primaryModelProviderKey": "openai",
        "primaryModelId": "gpt-4o-mini",
        "fallbackModels": [],
        "structuredInputJson": json.dumps({"focus": "bom-drift"}),
        "preview": True,
    }

    response = client.post("/v1/execute", json=payload)
    assert response.status_code == 200
    body = response.json()

    assert body["status"] == "Succeeded"
    assert body["fallbackApplied"] is False
    assert body["modelUsed"] == "openai:gpt-4o-mini"

    structured = json.loads(body["structuredOutputJson"])
    assert structured["summary"] == "mock-summary"
    assert structured["confidence"] == 0.0
    assert structured["findings"] == ["mock-findingsItem"]
    assert any("Deterministic mock output" in note for note in body["traceNotes"])


def test_execute_uses_structured_input_values_when_present() -> None:
    schema = {
        "type": "object",
        "required": ["summary"],
        "properties": {"summary": {"type": "string"}},
    }
    payload = {
        "governedContextSummaryJson": "{}",
        "promptTemplateBody": "Summarize.",
        "outputSchemaJson": json.dumps(schema),
        "primaryModelProviderKey": "openai",
        "primaryModelId": "gpt-4o-mini",
        "fallbackModels": [],
        "structuredInputJson": json.dumps({"summary": "provided-summary"}),
        "preview": False,
    }

    response = client.post("/v1/execute", json=payload)
    assert response.status_code == 200
    structured = json.loads(response.json()["structuredOutputJson"])
    assert structured["summary"] == "provided-summary"


def test_execute_rejects_invalid_output_schema() -> None:
    payload = {
        "governedContextSummaryJson": "{}",
        "promptTemplateBody": "Summarize.",
        "outputSchemaJson": "not-json",
        "primaryModelProviderKey": "openai",
        "primaryModelId": "gpt-4o-mini",
        "fallbackModels": [],
        "preview": True,
    }

    response = client.post("/v1/execute", json=payload)
    assert response.status_code == 422
    assert response.json()["status"] == "Failed"


def test_execute_fallback_chain_without_api_keys() -> None:
    payload = {
        "governedContextSummaryJson": "{}",
        "promptTemplateBody": "Summarize.",
        "outputSchemaJson": json.dumps(
            {
                "type": "object",
                "required": ["summary"],
                "properties": {"summary": {"type": "string"}},
            }
        ),
        "primaryModelProviderKey": "unsupported-primary",
        "primaryModelId": "model-a",
        "fallbackModels": [
            {
                "providerKey": "openai",
                "modelId": "gpt-4o-mini",
                "triggerReason": "primary unavailable",
            }
        ],
        "preview": True,
    }

    response = client.post("/v1/execute", json=payload)
    assert response.status_code == 200
    body = response.json()
    assert body["status"] == "Succeeded"
    assert body["fallbackApplied"] is True
    assert body["modelUsed"] == "openai:gpt-4o-mini"
    assert any("Fallback model applied" in note for note in body["traceNotes"])


@pytest.mark.skipif(
    not os.environ.get("OPENAI_API_KEY"),
    reason="Live OpenAI execution requires OPENAI_API_KEY.",
)
def test_execute_live_openai_when_api_key_configured() -> None:
    payload = {
        "governedContextSummaryJson": json.dumps({"parts": ["PN-100"]}),
        "promptTemplateBody": "Return a short JSON summary for the part list.",
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
        "preview": True,
    }

    response = client.post("/v1/execute", json=payload)
    assert response.status_code == 200
    body = response.json()
    assert body["status"] == "Succeeded"
    assert json.loads(body["structuredOutputJson"])["summary"]
