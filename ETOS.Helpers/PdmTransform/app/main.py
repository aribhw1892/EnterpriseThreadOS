from __future__ import annotations

import argparse
import logging
import sys
from pathlib import Path

from app.config import TransformConfig
from app.transform_service import transform_export

DEFAULT_CONFIG_NAME = "transform.config.json"


def setup_logging(verbose: bool) -> None:
    logging.basicConfig(
        level=logging.DEBUG if verbose else logging.INFO,
        format="%(asctime)s %(levelname)s %(message)s",
        handlers=[logging.StreamHandler(sys.stdout)],
    )


def project_root() -> Path:
    return Path(__file__).resolve().parent.parent


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Transform PdmExtractor exports into EnterpriseThreadOS import CSV batches."
    )
    parser.add_argument(
        "--input",
        required=True,
        help="Path to pdm_export directory (contains objects/ and relationships/)",
    )
    parser.add_argument(
        "--output",
        required=True,
        help="Output directory for ETOS import CSV files",
    )
    parser.add_argument(
        "--config",
        default=str(project_root() / DEFAULT_CONFIG_NAME),
        help=f"Transform config JSON (default: {DEFAULT_CONFIG_NAME})",
    )
    parser.add_argument("--verbose", action="store_true", help="Enable debug logging")
    return parser.parse_args()


def cli() -> None:
    args = parse_args()
    setup_logging(args.verbose)
    config = TransformConfig.load(Path(args.config))
    result = transform_export(Path(args.input), Path(args.output), config)
    logging.info("Wrote manifest to %s", result.manifest_path)
    for name, count in result.row_counts.items():
        logging.info("%s: %s rows", name, count)


if __name__ == "__main__":
    cli()
