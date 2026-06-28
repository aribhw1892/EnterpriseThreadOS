from __future__ import annotations

import pytest

from app.execute_service import (
    _extract_json_object,
    _looks_like_json_schema_document,
    _validate_required_fields,
)

MAPPING_SCHEMA = {
    "type": "object",
    "required": ["columnSuggestions", "lifecycleSuggestions"],
    "properties": {
        "columnSuggestions": {"type": "array"},
        "lifecycleSuggestions": {"type": "array"},
    },
}


def test_extract_json_object_strips_markdown_fence() -> None:
    parsed = _extract_json_object(
        '```json\n{"columnSuggestions": [], "lifecycleSuggestions": []}\n```'
    )
    assert parsed["columnSuggestions"] == []


def test_looks_like_json_schema_document() -> None:
    assert _looks_like_json_schema_document(
        {
            "properties": {"columnSuggestions": {"type": "array"}},
            "required": ["columnSuggestions"],
            "type": "object",
        }
    )
    assert not _looks_like_json_schema_document(
        {"columnSuggestions": [], "lifecycleSuggestions": []}
    )


def test_validate_rejects_json_schema_echo() -> None:
    schema_echo = {
        "properties": {
            "columnSuggestions": {"type": "array"},
            "lifecycleSuggestions": {"type": "array"},
        },
        "required": ["columnSuggestions", "lifecycleSuggestions"],
        "type": "object",
    }

    with pytest.raises(ValueError, match="JSON Schema document"):
        _validate_required_fields(schema_echo, MAPPING_SCHEMA)


def test_validate_accepts_mapping_data_object() -> None:
    _validate_required_fields(
        {"columnSuggestions": [], "lifecycleSuggestions": []},
        MAPPING_SCHEMA,
    )
