from __future__ import annotations

import logging
import os
from typing import Any, Dict, List

import pyodbc

from app.xml_mapping import (
    Entity,
    MappingDefinition,
    RelationshipData,
)


def create_db_connection(connection_string: str) -> pyodbc.Connection:
    conn = pyodbc.connect(connection_string)
    logging.info("Database connection established.")
    return conn


def build_connection_string_from_env() -> str:
    driver = os.getenv("PDM_ODBC_DRIVER", "ODBC Driver 17 for SQL Server")
    server = os.environ["PDM_DB_SERVER"]
    database = os.environ["PDM_DB_NAME"]
    uid = os.environ["PDM_DB_USER"]
    pwd = os.environ["PDM_DB_PASSWORD"]
    return (
        f"DRIVER={{{driver}}};"
        f"SERVER={server};"
        f"DATABASE={database};"
        f"UID={uid};"
        f"PWD={pwd}"
    )


def extract_entities(mapping: MappingDefinition, conn: pyodbc.Connection) -> Dict[str, Dict[Any, Entity]]:
    entities: Dict[str, Dict[Any, Entity]] = {}
    cursor = conn.cursor()

    for obj_def in mapping.ObjectDefinitions:
        obj_type = obj_def.Type
        entities[obj_type] = {}
        logging.info("Extracting entities for type: %s", obj_type)

        try:
            cursor.execute(obj_def.Query)
        except pyodbc.Error as e:
            logging.error("Error executing master query for '%s': %s", obj_type, e)
            continue

        id_results = cursor.fetchall()
        columns = [column[0] for column in cursor.description]
        try:
            id_field_index = columns.index(obj_def.IdField)
        except ValueError:
            logging.error("IdField '%s' not found for '%s'", obj_def.IdField, obj_type)
            continue

        for row in id_results:
            obj_id = row[id_field_index]
            entities[obj_type][obj_id] = Entity(Type=obj_type, Id=obj_id)

        for attr_source in obj_def.AttributeSources:
            logging.info("Extracting attributes for type: %s", obj_type)
            if not attr_source.Query:
                continue
            try:
                cursor.execute(attr_source.Query)
            except pyodbc.Error as e:
                logging.error("Error executing attribute query for '%s': %s", obj_type, e)
                continue

            attr_results = cursor.fetchall()
            attr_field_names = [column[0] for column in cursor.description]
            try:
                id_field_idx = attr_field_names.index(obj_def.IdField)
            except ValueError:
                logging.error("IdField '%s' not found in attribute query for '%s'", obj_def.IdField, obj_type)
                continue

            for row in attr_results:
                obj_id = row[id_field_idx]
                entity = entities[obj_type].get(obj_id)
                if entity is None:
                    continue
                for attr in attr_source.Attributes:
                    if attr.Name in attr_field_names:
                        entity.Attributes[attr.Name] = row[attr_field_names.index(attr.Name)]

    cursor.close()
    logging.info("Entity extraction completed.")
    return entities


def extract_relationships(
    mapping: MappingDefinition,
    entities: Dict[str, Dict[Any, Entity]],
    conn: pyodbc.Connection,
) -> Dict[str, List[RelationshipData]]:
    relationships_by_type: Dict[str, List[RelationshipData]] = {}
    cursor = conn.cursor()

    for rel_def in mapping.RelationshipDefinitions:
        rel_type = rel_def.Type
        logging.info("Extracting relationships for type: %s", rel_type)

        try:
            cursor.execute(rel_def.Query)
        except pyodbc.Error as e:
            logging.error("Error executing relationship query for '%s': %s", rel_type, e)
            continue

        rel_results = cursor.fetchall()
        rel_field_names = [column[0] for column in cursor.description]

        try:
            id_field_idx = rel_field_names.index(rel_def.IdField)
            parent_id_idx = rel_field_names.index(rel_def.ParentIdField)
            child_id_idx = rel_field_names.index(rel_def.ChildIdField)
        except ValueError as e:
            logging.error("Missing relationship field for '%s': %s", rel_type, e)
            continue

        current_relationships: List[RelationshipData] = []

        for row in rel_results:
            rel_id = row[id_field_idx]
            parent_id = row[parent_id_idx]
            child_id = row[child_id_idx]

            parent_entity = entities.get(rel_def.ParentType, {}).get(parent_id)
            child_entity = entities.get(rel_def.ChildType, {}).get(child_id)

            if not parent_entity:
                logging.warning("Parent not found: Type=%s, Id=%s", rel_def.ParentType, parent_id)
                continue
            if not child_entity:
                logging.warning("Child not found: Type=%s, Id=%s", rel_def.ChildType, child_id)
                continue

            current_relationships.append(
                RelationshipData(
                    Type=rel_type,
                    Id=rel_id,
                    Parent=parent_entity,
                    Child=child_entity,
                )
            )

        relationships_by_type[rel_type] = current_relationships

        for attr_source in rel_def.AttributeSources:
            logging.info("Extracting relationship attributes for type: %s", rel_type)
            if not attr_source.Query:
                continue
            try:
                cursor.execute(attr_source.Query)
            except pyodbc.Error as e:
                logging.error("Error executing relationship attribute query for '%s': %s", rel_type, e)
                continue

            attr_results = cursor.fetchall()
            attr_field_names = [column[0] for column in cursor.description]
            try:
                attr_id_field_idx = attr_field_names.index(rel_def.IdField)
            except ValueError:
                logging.error(
                    "IdField '%s' not found in relationship attribute query for '%s'",
                    rel_def.IdField,
                    rel_type,
                )
                continue

            rel_index = {rel.Id: rel for rel in current_relationships}

            for row in attr_results:
                rel_id = row[attr_id_field_idx]
                rel = rel_index.get(rel_id)
                if rel is None:
                    continue
                for attr in attr_source.Attributes:
                    if attr.Name in attr_field_names:
                        rel.Attributes[attr.Name] = row[attr_field_names.index(attr.Name)]

    cursor.close()
    logging.info("Relationship extraction completed.")
    return relationships_by_type
