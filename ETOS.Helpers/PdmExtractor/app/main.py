from __future__ import annotations

import argparse
import logging
import sys
from pathlib import Path

from app.export_service import export_entities, export_relationships, write_manifest
from app.extract_service import (
    build_connection_string_from_env,
    create_db_connection,
    extract_entities,
    extract_relationships,
)
from app.xml_mapping import parse_xml

DEFAULT_XML_NAME = "mapping_definition.xml"
DEFAULT_OUTPUT_DIR = "pdm_export"
DEFAULT_LOG_FILE = "migration.log"


def setup_logging(log_file: str, verbose: bool) -> None:
    handlers: list[logging.Handler] = [logging.StreamHandler(sys.stdout)]
    if log_file:
        handlers.append(logging.FileHandler(log_file, mode="a", encoding="utf-8"))
    logging.basicConfig(
        level=logging.DEBUG if verbose else logging.INFO,
        format="%(asctime)s %(levelname)s %(message)s",
        handlers=handlers,
    )


def project_root() -> Path:
    return Path(__file__).resolve().parent.parent


def default_xml_path() -> Path:
    return project_root() / DEFAULT_XML_NAME


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Extract SolidWorks PDM data via XML mapping and export per-type CSV/JSON files."
    )
    parser.add_argument(
        "--xml",
        default=str(default_xml_path()),
        help=f"Path to mapping XML (default: project root / {DEFAULT_XML_NAME})",
    )
    parser.add_argument(
        "--output-dir",
        default=DEFAULT_OUTPUT_DIR,
        help=f"Output directory relative to project root unless absolute (default: {DEFAULT_OUTPUT_DIR})",
    )
    parser.add_argument(
        "--connection-string",
        default="",
        help="ODBC connection string. If omitted, uses PDM_DB_* environment variables.",
    )
    parser.add_argument("--csv", action="store_true", help="Export CSV files")
    parser.add_argument("--json", action="store_true", help="Export JSON files")
    parser.add_argument("--log-file", default=DEFAULT_LOG_FILE, help="Log file path")
    parser.add_argument("--verbose", action="store_true", help="Enable debug logging")
    return parser.parse_args()


def resolve_output_dir(output_dir: str) -> Path:
    path = Path(output_dir)
    if not path.is_absolute():
        return project_root() / path
    return path


def main() -> int:
    args = parse_args()
    setup_logging(args.log_file, args.verbose)

    if not args.csv and not args.json:
        args.csv = True
        args.json = True

    xml_path = Path(args.xml)
    if not xml_path.is_absolute():
        xml_path = project_root() / xml_path

    if not xml_path.exists():
        logging.critical(
            "Mapping XML not found at '%s'. Place %s in the project root and retry.",
            xml_path,
            DEFAULT_XML_NAME,
        )
        return 1

    output_dir = resolve_output_dir(args.output_dir)

    try:
        mapping = parse_xml(str(xml_path))
    except Exception as e:
        logging.critical("Failed to parse XML: %s", e)
        return 1

    connection_string = args.connection_string or build_connection_string_from_env()

    try:
        conn = create_db_connection(connection_string)
    except Exception as e:
        logging.critical("Database connection failed: %s", e)
        return 1

    try:
        entities = extract_entities(mapping, conn)
        relationships_by_type = extract_relationships(mapping, entities, conn)
    finally:
        conn.close()
        logging.info("Closed database connection.")

    export_entities(entities, output_dir, export_csv=args.csv, export_json=args.json)
    export_relationships(relationships_by_type, output_dir, export_csv=args.csv, export_json=args.json)
    write_manifest(output_dir, str(xml_path), entities, relationships_by_type)

    logging.info("Export completed. Output directory: %s", output_dir.resolve())
    return 0


def cli() -> None:
    raise SystemExit(main())


if __name__ == "__main__":
    cli()
