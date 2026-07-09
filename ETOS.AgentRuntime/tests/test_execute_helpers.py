from __future__ import annotations

import pytest

from app.execute_service import (
    _extract_json_object,
    _generate_deterministic_output,
    _looks_like_json_schema_document,
    _validate_required_fields,
    _validate_schema_output,
)

MAPPING_SCHEMA = {
    "type": "object",
    "required": ["columnSuggestions", "lifecycleSuggestions"],
    "properties": {
        "columnSuggestions": {
            "type": "array",
            "items": {
                "type": "object",
                "required": [
                    "sourceColumn",
                    "canonicalObjectType",
                    "canonicalAttributeKey",
                    "isIdentityField",
                    "isRequired",
                    "confidence",
                    "rationale",
                ],
                "properties": {
                    "sourceColumn": {"type": "string"},
                    "canonicalObjectType": {"type": "string"},
                    "canonicalAttributeKey": {"type": "string"},
                    "isIdentityField": {"type": "boolean"},
                    "isRequired": {"type": "boolean"},
                    "confidence": {"type": "number"},
                    "rationale": {"type": "string"},
                },
            },
        },
        "lifecycleSuggestions": {
            "type": "array",
            "items": {
                "type": "object",
                "required": [
                    "sourceValue",
                    "canonicalLifecycleKey",
                    "confidence",
                    "rationale",
                ],
                "properties": {
                    "sourceValue": {"type": "string"},
                    "canonicalLifecycleKey": {"type": "string"},
                    "confidence": {"type": "number"},
                    "rationale": {"type": "string"},
                },
            },
        },
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


def test_mapping_mock_example_includes_canonical_attribute_key() -> None:
    output = _generate_deterministic_output(MAPPING_SCHEMA, {})
    column = output["columnSuggestions"][0]
    assert column["canonicalAttributeKey"] == "partNumber"
    assert column["isIdentityField"] is True


def test_validate_schema_output_rejects_missing_nested_field() -> None:
    output = {
        "columnSuggestions": [
            {
                "sourceColumn": "partNumber",
                "canonicalObjectType": "part",
                "isIdentityField": False,
                "isRequired": True,
                "confidence": 0.85,
                "rationale": "missing attribute key",
            }
        ],
        "lifecycleSuggestions": [],
    }

    with pytest.raises(ValueError, match="canonicalAttributeKey"):
        _validate_schema_output(output, MAPPING_SCHEMA)
