from __future__ import annotations

import json
from datetime import datetime
from typing import Any, Dict

from app.xml_mapping import Entity, RelationshipData


class DateTimeEncoder(json.JSONEncoder):
    def default(self, obj: Any) -> Any:
        if isinstance(obj, datetime):
            return obj.isoformat()
        return super().default(obj)


def preprocess_value(value: Any) -> Any:
    if isinstance(value, datetime):
        return value.isoformat()
    if isinstance(value, bytes):
        try:
            return value.decode("utf-8")
        except UnicodeDecodeError:
            return str(value)
    if isinstance(value, float):
        return round(value, 6)
    if value is None or isinstance(value, (bool, int, str)):
        return value
    return str(value)


def preprocess_attributes(attributes: Dict[str, Any]) -> Dict[str, Any]:
    return {key: preprocess_value(val) for key, val in attributes.items()}


def safe_filename(name: str) -> str:
    return "".join(c if c.isalnum() or c in ("-", "_") else "_" for c in name)


def entity_to_row(entity: Entity) -> Dict[str, Any]:
    row: Dict[str, Any] = {
        "Type": entity.Type,
        "Id": preprocess_value(entity.Id),
    }
    for key, value in preprocess_attributes(entity.Attributes).items():
        row[key] = value
    return row


def relationship_to_row(rel: RelationshipData) -> Dict[str, Any]:
    row: Dict[str, Any] = {
        "Type": rel.Type,
        "Id": preprocess_value(rel.Id),
        "ParentType": rel.Parent.Type,
        "ParentId": preprocess_value(rel.Parent.Id),
        "ChildType": rel.Child.Type,
        "ChildId": preprocess_value(rel.Child.Id),
    }
    for key, value in preprocess_attributes(rel.Attributes).items():
        row[key] = value
    return row
