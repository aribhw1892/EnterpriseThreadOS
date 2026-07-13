from __future__ import annotations

import logging
import xml.etree.ElementTree as ET
from dataclasses import dataclass, field
from typing import Any, Dict, List


@dataclass
class Attribute:
    Type: str
    Name: str


@dataclass
class AttributeSource:
    Query: str
    Attributes: List[Attribute]


@dataclass
class ObjectDefinition:
    Type: str
    Query: str
    IdField: str
    AttributeSources: List[AttributeSource]


@dataclass
class RelationshipDefinition:
    Type: str
    Query: str
    IdField: str
    ParentIdField: str
    ChildIdField: str
    ParentType: str
    ChildType: str
    AttributeSources: List[AttributeSource]


@dataclass
class MappingDefinition:
    ObjectDefinitions: List[ObjectDefinition] = field(default_factory=list)
    RelationshipDefinitions: List[RelationshipDefinition] = field(default_factory=list)


@dataclass
class Entity:
    Type: str
    Id: Any
    Attributes: Dict[str, Any] = field(default_factory=dict)


@dataclass
class RelationshipData:
    Type: str
    Id: Any
    Parent: Entity
    Child: Entity
    Attributes: Dict[str, Any] = field(default_factory=dict)


def parse_attribute_sources(parent: ET.Element) -> List[AttributeSource]:
    attribute_sources: List[AttributeSource] = []
    attr_sources_el = parent.find("AttributeSources")
    if attr_sources_el is None:
        return attribute_sources

    for attr_source in attr_sources_el.findall("AttributeSource"):
        attr_query = attr_source.findtext("Query", default="").strip()
        attributes: List[Attribute] = []
        attrs_el = attr_source.find("Attributes")
        if attrs_el is not None:
            for attr in attrs_el.findall("Attribute"):
                attr_name = attr.get("Name", "")
                if attr_name:
                    attributes.append(Attribute(Type=attr.get("Type", ""), Name=attr_name))
        attribute_sources.append(AttributeSource(Query=attr_query, Attributes=attributes))

    return attribute_sources


def parse_xml(xml_file: str) -> MappingDefinition:
    try:
        tree = ET.parse(xml_file)
    except ET.ParseError as e:
        logging.critical("Error parsing XML file '%s': %s", xml_file, e)
        raise

    root = tree.getroot()
    mapping = MappingDefinition()

    object_defs = root.find("ObjectDefinitions")
    if object_defs is None:
        raise ValueError(
            f"XML missing <ObjectDefinitions>. Root element is <{root.tag}>."
        )

    for obj_def in object_defs.findall("ObjectDefinition"):
        obj_type = obj_def.get("Type")
        if not obj_type:
            raise ValueError("ObjectDefinition missing Type attribute")

        query = obj_def.findtext("Query", default="").strip()
        id_field = obj_def.findtext("IdField", default="").strip()
        if not query or not id_field:
            raise ValueError(f"ObjectDefinition '{obj_type}' missing Query or IdField")

        mapping.ObjectDefinitions.append(
            ObjectDefinition(
                Type=obj_type,
                Query=query,
                IdField=id_field,
                AttributeSources=parse_attribute_sources(obj_def),
            )
        )

    rel_defs = root.find("RelationshipDefinitions")
    if rel_defs is not None:
        for rel_def in rel_defs.findall("RelationshipDefinition"):
            rel_type = rel_def.get("Type")
            if not rel_type:
                raise ValueError("RelationshipDefinition missing Type attribute")

            query = rel_def.findtext("Query", default="").strip()
            id_field = rel_def.findtext("IdField", default="").strip()
            parent_id_field = rel_def.findtext("ParentIdField", default="").strip()
            child_id_field = rel_def.findtext("ChildIdField", default="").strip()
            parent_type = rel_def.findtext("ParentType", default="").strip()
            child_type = rel_def.findtext("ChildType", default="").strip()

            if not all([query, id_field, parent_id_field, child_id_field, parent_type, child_type]):
                raise ValueError(f"RelationshipDefinition '{rel_type}' has missing required fields")

            mapping.RelationshipDefinitions.append(
                RelationshipDefinition(
                    Type=rel_type,
                    Query=query,
                    IdField=id_field,
                    ParentIdField=parent_id_field,
                    ChildIdField=child_id_field,
                    ParentType=parent_type,
                    ChildType=child_type,
                    AttributeSources=parse_attribute_sources(rel_def),
                )
            )

    logging.info(
        "Parsed XML (root=<%s>): %d object types, %d relationship types.",
        root.tag,
        len(mapping.ObjectDefinitions),
        len(mapping.RelationshipDefinitions),
    )
    if not mapping.ObjectDefinitions and not mapping.RelationshipDefinitions:
        logging.warning(
            "No object or relationship definitions found. "
            "Ensure mapping_definition.xml is saved and contains <ObjectDefinition> / "
            "<RelationshipDefinition> entries under <ObjectDefinitions> and "
            "<RelationshipDefinitions>."
        )
    return mapping
