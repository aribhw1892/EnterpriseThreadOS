# ETOS Odoo ERP Extractor

XML-driven Odoo ERP PostgreSQL extractor for EnterpriseThreadOS. Reads object and relationship definitions from `mapping_definition.xml`, queries the Odoo database, and writes **one CSV and/or JSON file per object type and per relationship type**.

Exports are intended for manual upload through the ETOS `/imports` UI. This project may later evolve into a governed read connector sidecar (`mock-erp-read`).

## Current status

No live Odoo database is wired in this repo yet. **Mock extract output** for the Steris/PDM demo dataset is committed under `odoo_export/mock/` and was derived from the current PDM transform fixtures. When Odoo PostgreSQL access is available, replace the placeholder SQL in `mapping_definition.xml` and run the same extractor CLI used by PDM.

## Prerequisites (live extract)

- Python 3.11+
- PostgreSQL network access to the Odoo database
- `uv sync --extra postgres` in this directory

## Mapping XML

Place your mapping file at the project root:

```text
ETOS.Helpers/OdooErpExtractor/mapping_definition.xml
```

The root element may be `<Mapping>` or `<MappingDefinition>`. Object types, relationship types, SQL queries, and attribute mappings are defined entirely in that XML file.

Shared XML parsing and export logic lives in [`../EtosExtractCommon/`](../EtosExtractCommon/).

## Setup

```powershell
cd ETOS.Helpers\OdooErpExtractor
uv sync --extra postgres
```

## Database credentials (live extract)

```powershell
$env:ODOO_DB_HOST = "localhost"
$env:ODOO_DB_PORT = "5432"
$env:ODOO_DB_NAME = "odoo"
$env:ODOO_DB_USER = "odoo"
$env:ODOO_DB_PASSWORD = "your-password"
```

Alternatively pass a full PostgreSQL connection string:

```powershell
uv run odoo-erp-extract --connection-string "host=... port=5432 dbname=... user=... password=..."
```

## Run

**Mock mode** (no database):

```powershell
uv run odoo-erp-extract --use-mock
```

Use `odoo_export/mock/` as transform input.

**Live extract** (when SQL is ready):

```powershell
uv run odoo-erp-extract --csv --json
```

## Output layout

```text
odoo_export/
  manifest.json
  objects/
    Product.csv
  relationships/
    BomLine.csv
```

Committed mock data:

```text
odoo_export/mock/
  manifest.json
  objects/Product.csv
  relationships/BomLine.csv
```

## Import into EnterpriseThreadOS

1. Run [`OdooErpTransform`](../OdooErpTransform/) to produce `etos_import/` CSV batches.
2. Create import batches in the UI with `SourceSystem=ODOO-ERP`.
3. Upload `parts.csv` and `ebom.csv` (see OdooErpTransform README).

## Project layout

```text
OdooErpExtractor/
  mapping_definition.xml   # placeholder SQL for future live extract
  odoo_export/mock/        # committed mock CSV export
  pyproject.toml
  app/
    main.py                # CLI entry (PostgreSQL + shared extract/export)
```

## Future direction

This helper may become an HTTP sidecar invoked by the `mock-erp-read` connector in the tool registry. The XML mapping profile would remain the configuration contract.
