# ETOS PDM Extractor

XML-driven SolidWorks PDM SQL extractor for EnterpriseThreadOS. Reads object and relationship definitions from `mapping_definition.xml`, queries the PDM vault database, and writes **one CSV and/or JSON file per object type and per relationship type**.

Exports are intended for manual upload through the ETOS `/imports` UI. This project may later evolve into a governed read connector sidecar.

## Prerequisites

- Python 3.11+
- [ODBC Driver for SQL Server](https://learn.microsoft.com/en-us/sql/connect/odbc/download-odbc-driver-for-sql-server) (default: `ODBC Driver 17 for SQL Server`)
- Network access to the SolidWorks PDM SQL Server database

## Mapping XML

Place your mapping file at the project root:

```text
ETOS.Helpers/PdmExtractor/mapping_definition.xml
```

**Save the file to disk before running.** An unsaved editor buffer is not read by the extractor.

The root element may be `<Mapping>` or `<MappingDefinition>`. Object types, relationship types, SQL queries, and attribute mappings are defined entirely in that XML file. The Python code does not hardcode PDM object or relationship names.

See your vault-specific mapping document or a future sample under `samples/` when added.

Shared XML parsing and export logic lives in [`../EtosExtractCommon/`](../EtosExtractCommon/).

## Setup

From this directory (`ETOS.Helpers/PdmExtractor`):

```powershell
python -m venv .venv
.\.venv\Scripts\Activate.ps1
pip install -e .
```

## Database credentials

Set environment variables (do not commit secrets).

**PowerShell** (prompt ends with nothing, or `PS D:\...>`):

```powershell
$env:PDM_DB_SERVER = "XOR-DESKTOP2\SQLDEV2019"
$env:PDM_DB_NAME = "PDMOdooTest"
$env:PDM_DB_USER = "sa"
$env:PDM_DB_PASSWORD = "sa123$"
# Optional override:
# $env:PDM_ODBC_DRIVER = "ODBC Driver 17 for SQL Server"
```

**Command Prompt (cmd.exe)** — if you see `D:\...>` and `(base)`, use `set`, not `$env:`:

```bat
set PDM_DB_SERVER=XOR-DESKTOP2\SQLDEV2019
set PDM_DB_NAME=PDMOdooTest
set PDM_DB_USER=sa
set PDM_DB_PASSWORD=sa123$
```

Use a **single** backslash before the instance name (`\SQLDEV2019`). Double backslashes are not needed in cmd or PowerShell strings.

Alternatively pass a full ODBC string:

```powershell
python -m app.main --connection-string "DRIVER={ODBC Driver 17 for SQL Server};SERVER=...;DATABASE=...;UID=...;PWD=..."
```

## Run

Default: reads `mapping_definition.xml` from this project root, writes to `./pdm_export/`, exports CSV and JSON.

```powershell
python -m app.main
```

Explicit options:

```powershell
python -m app.main --xml mapping_definition.xml --output-dir pdm_export --csv --json --verbose
```

After install, you can also use:

```powershell
pdm-extract
```

## Output layout

```text
pdm_export/
  manifest.json
  objects/
    <ObjectTypeFromXml>.csv
    <ObjectTypeFromXml>.json
  relationships/
    <RelationshipTypeFromXml>.csv
    <RelationshipTypeFromXml>.json
```

`manifest.json` lists row counts per type.

## Import into EnterpriseThreadOS

1. Create an import batch in the UI with `SourceSystem` set to your PDM vault name (for example `SOLIDWORKS-PDM`).
2. Upload each exported CSV (one object or relationship dataset per batch, or as your mapping workflow requires).
3. Preview and approve column mapping, validate, stage, and promote as usual.

Relationship CSVs include `ParentId`, `ChildId`, and related columns. Map them to `parent` / `child` (or synonyms in the active model package import profile) during import mapping.

## Logs

By default, logs append to `migration.log` in the current working directory and print to the console. Override with `--log-file`.

## Project layout

```text
PdmExtractor/
  mapping_definition.xml   # you provide this at project root
  pyproject.toml
  app/
    main.py                # CLI entry (ODBC + shared extract/export)
  pdm_export/              # generated (gitignored)
```

## Future direction

This helper may become an HTTP sidecar (`ETOS.PdmExtractor` service) invoked by a `solidworks-pdm-read` connector in the tool registry. The XML mapping profile would remain the configuration contract.
