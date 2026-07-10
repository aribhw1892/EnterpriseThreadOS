# Issue 29: PDM Extract, Transform, and Governed Import (Part / PartVersion Digital Thread)

Source PRD: `engineering-execution-prd.md`  
Related issues: Issue 8 (import staging), Issue 9 (identity resolution), Issue 11 (promotion), Issue 18.5 (manufacturing reference package), Issue 22 (connector contracts, future)  
Label: `needs-triage`

## Summary

Deliver a **three-layer** SolidWorks PDM ingestion path that stays architecture-honest for MVP:

1. **Extract** — XML-driven SQL export (`ETOS.Helpers/PdmExtractor`, **partially implemented**).
2. **Transform** — PDM-native CSV/JSON → canonical ETOS import CSVs (`ETOS.Helpers/PdmTransform`, **new**).
3. **Import** — existing `/imports` UI and staging graph flow, plus a **small backend generalization** so relationship CSVs are not BOM-only.

Target graph model:

```text
part(documentId) ──hasVersion──► partVersion(pdmVersionKey)
partVersion ──contains──► partVersion          (CAD BOM from PDM XRefs)
```

Raw PDM object names (Folder, File, Version, Configuration, Variables) remain in extract output only. Canonical ontology types are decided in transform config and the manufacturing reference package.

---

## Problem statement

Issue 8 import staging supports:

- **Flat CSV** → one canonical object type per batch → one node per row.
- **Structural CSV** (`parent` + `child` columns) → **only** the model package **default BOM** relationship from `bom-relationships.json` (today: `part` → `part` `contains`).

It does **not** support:

- Multiple canonical object types in one coordinated import story (e.g. `part` + `partVersion`).
- Arbitrary ontology relationships from `relationships.json` (e.g. `hasVersion`).
- Per-batch selection of which structural relationship definition applies.

PDM export produces **five** relationship types (`FolderToFile`, `FileToVersion`, `VersionConfiguration`, `VersionToVariable`, `VersionToVersion`). Only `VersionToVersion` accidentally fits today’s BOM structural path. `FileToVersion` needs `hasVersion` (`part` → `partVersion`) with the **same** parent/child CSV shape.

**This issue generalizes structural import into relationship import** — not a second engine.

---

## Architecture

```mermaid
flowchart LR
    subgraph extract [Layer 1 - Extract]
        XML[mapping_definition.xml]
        SQL[(PDM SQL Server)]
        XML --> PdmExtractor
        SQL --> PdmExtractor
        PdmExtractor --> pdm_export[pdm_export/ objects + relationships]
    end

    subgraph transform [Layer 2 - Transform]
        cfg[transform.config.json]
        pdm_export --> PdmTransform
        cfg --> PdmTransform
        PdmTransform --> etos_import[etos_import/ canonical CSVs]
    end

    subgraph import [Layer 3 - Import]
        etos_import --> UI[/imports UI]
        UI --> Staging[Staging graph]
        Staging --> Promote[Trusted graph]
    end
```

### Layer boundaries

| Layer | Location | Changes when |
| ----- | -------- | ------------ |
| Extract | `ETOS.Helpers/PdmExtractor` | New PDM vault / SQL only → edit XML |
| Transform | `ETOS.Helpers/PdmTransform` | Ontology / mapping decision → edit transform config |
| Ontology | `packages/manufacturing-reference/` | Canonical types, attrs, relationships |
| Import engine | `ETOS.Backend/Imports/` | Relationship generalization (this issue) |

Extract must **not** reference ETOS ontology. Transform must **not** connect to SQL.

---

## PDM → canonical mapping (target)

| PDM object / rel | Canonical | Import batch |
| ---------------- | ----------- | ------------ |
| **File** | `part` | Flat: `parts.csv` |
| **Version** | `partVersion` | Flat: `part-versions.csv` |
| **Folder** | metadata (`projectPath` on `part`) | Via transform join, not a node |
| **Variables** | attributes on `partVersion` | Pivot `VersionToVariable` in transform |
| **Configuration** | defer or `configurationName` attribute | Not a separate node in MVP |
| **DataCard** | skip | — |
| **FileToVersion** | `hasVersion` (`part` → `partVersion`) | Structural: `has-version.csv` |
| **VersionToVersion** | `contains` (`partVersion` → `partVersion`) | Structural: `version-bom.csv` |
| FolderToFile, VersionConfiguration, VersionToVariable | transform only | not separate graph rel imports |

### Identity fields (recommended)

| Type | Identity | Example |
| ---- | -------- | ------- |
| `part` | `documentId` | `15` |
| `partVersion` | `pdmVersionKey` | `15-10` (= PDM `MigrationSourceId`) |

`pdmVersionKey` on versions and on BOM parent/child columns avoids Issue 8 structural import’s **single identity column** limitation for BOM rows.

`documentId` on `partVersion` is a **non-identity attribute** for queries until `hasVersion` edges exist.

---

## Sub-slices

### 29.0 PDM Extract helper (done)

**Path:** `ETOS.Helpers/PdmExtractor/`

- XML-driven object and relationship extraction to per-type CSV/JSON under `pdm_export/`.
- No ontology, no Neo4j, no ETOS backend coupling.
- Verified against Steris export columns (`DocumentID`, `MigrationSourceId`, `ParentId`/`ChildId`, `QTY`).

---

### 29.1 Manufacturing ontology extension (done)

**Path:** `packages/manufacturing-reference/ontology/`

Update reference package for PDM layered model:

**`object-types.json`**

- `part`: `versionIdentityFieldsJson` → `["documentId"]` (or keep `partNumber` for backward demo compatibility — document breaking change).
- `partVersion`: new type, `versionIdentityFieldsJson` → `["pdmVersionKey"]`.

**`attribute-schema.json`**

- `part`: `documentId`, `fileName`, `projectPath`, …
- `partVersion`: `pdmVersionKey`, `documentId`, `revision`, `fileName`, `status`, `workflow`, `vaultArchivePath`, variable columns, …

**`relationships.json`**

```json
{
  "relationshipType": "hasVersion",
  "fromObjectType": "part",
  "toObjectType": "partVersion",
  "description": "Part has a PDM file revision.",
  "isVersionRelationship": true
}
```

**`bom-relationships.json`**

- Add or replace default BOM with `partVersion` → `partVersion` `contains` (quantity from `quantity` / `QTY`).

**`semantic-layer-mappings.json`**

- Map `partVersion` → `PartVersion`, `hasVersion` → `HAS_VERSION`.

**`profiles/import-profile.json`**

- Synonyms: `ParentId`, `ChildId`, `QTY`, `parent`, `child`, `quantity`.

**`demo-imports/`** (optional fixtures)

- `pdm-parts.csv`, `pdm-part-versions.csv`, `pdm-version-bom.csv`, `pdm-has-version.csv` (small samples from real transform output).

Re-install reference package after publish.

---

### 29.2 PDM Transform helper (done)

**Path:** `ETOS.Helpers/PdmTransform/`

**CLI:**

```powershell
python -m app.main --input ../PdmExtractor/pdm_export --output etos_import --config transform.config.json
```

**Outputs:**

| File | Source | Rows (Steris sample) |
| ---- | ------ | -------------------- |
| `parts.csv` | `objects/File.csv` + `FolderToFile` | ~107 |
| `part-versions.csv` | `objects/Version.csv` + pivot `VersionToVariable` | ~479 |
| `version-bom.csv` | `relationships/VersionToVersion.csv` | ~719 |
| `has-version.csv` | `relationships/FileToVersion.csv` or derived from Version | ~479 |
| `transform-manifest.json` | counts + config version | 1 |

**Transform config** (`transform.config.json`): maps PDM types → canonical columns; lifecycle maps (`MFG` → `released`); pivot rules for variables; skip lists (DataCard).

No SQL, no XML parsing (reads extract CSVs only).

---

### 29.3 Generalized structural relationship import (backend, done)

**Problem:** `ImportService.StageBatchAsync` always calls `RequireDefaultBomRelationship()` when parent/child columns are detected. Semantic relationships in `relationships.json` are never used for CSV staging.

**Solution:** Generalize to **relationship import** driven by the **approved import mapping**.

#### 29.3.1 Data model

Extend `ImportMappingVersion` (or approved mapping request DTO) with:

- `StructuralRelationshipType` (nullable string) — e.g. `contains`, `hasVersion`.
- When null and structural headers detected: preserve today’s behavior (default BOM from import profile).
- When set: resolve relationship definition from ontology:

  1. `bom-relationships.json` match on `relationshipType`, else
  2. `relationships.json` match on `relationshipType`.

Store normalized key on mapping version; validate at approve time against active model package.

#### 29.3.2 Resolver

Add to `ResolvedModelPackageContext` (or import helper):

```csharp
RelationshipImportDefinition ResolveStructuralRelationship(string relationshipType);
// Returns: relationshipType, parentObjectType, childObjectType,
//          quantity/unit/usage attribute keys (BOM only), graph rel type mapping
```

#### 29.3.3 Staging

Refactor `ImportService` structural branch:

- Replace `RequireDefaultBomRelationship()` with resolver using mapping’s `StructuralRelationshipType` or default BOM.
- Reuse existing `BuildIdentityAttributes`, `BuildRelationshipAttributes`, `CreateNodeAsync`, `CreateRelationshipAsync`.
- Parent/child object types come from resolved definition (`part`/`partVersion` or `partVersion`/`partVersion`).

#### 29.3.4 API / UI

- `CreateImportMappingVersionRequest`: optional `structuralRelationshipType`.
- Import mapping UI: when preview detects parent/child columns, show relationship type selector populated from package `bom-relationships` + `relationships`.
- Mapping assistant suggestions: optional hint from filename (`version-bom` → `contains`, `has-version` → `hasVersion`).

#### 29.3.5 Validation

- Approve mapping: structural relationship type must exist in ontology.
- Parent/child object types must exist in `object-types.json`.
- Identity mappings on structural batches: validate first identity field populated per row (unchanged).

#### 29.3.6 Tests

- Structural `contains` on `partVersion` → `partVersion` (regression for BOM).
- Structural `hasVersion` on `part` → `partVersion`.
- Unknown relationship type → approve blocked.
- Flat import unchanged (no structural headers).

---

### 29.4 Governed import runbook (documentation + smoke, done)

Document end-to-end operator flow in `ETOS.Helpers/README.md` and `docs/local-development.md`:

1. Run PdmExtractor → `pdm_export/`.
2. Run PdmTransform → `etos_import/`.
3. ETOS: four import batches, `SourceSystem` = `SOLIDWORKS-PDM`:

   | Order | CSV | Mapping mode | `structuralRelationshipType` |
   | ----- | --- | ------------ | ---------------------------- |
   | 1 | `parts.csv` | Flat | — |
   | 2 | `part-versions.csv` | Flat | — |
   | 3 | `version-bom.csv` | Structural | `contains` |
   | 4 | `has-version.csv` | Structural | `hasVersion` |

4. Each batch: preview → approve mapping → validate → stage → promote.
5. Verify graph: version-level BOM edges; part → version edges; governed query / explorer smoke.

**Backend integration test:** seed manufacturing package with PDM ontology extensions, stage all four batch types, assert node/relationship counts and relationship types in staging.

---

### 29.5 Future: connector (out of scope for 29)

- `solidworks-pdm-read` connector artifact metadata.
- HTTP sidecar wrapping extract + transform.
- `IToolGateway` execute → auto-create import batches.

Track under Issue 22 / post-29 follow-up. Do not implement live connector in this issue.

---

## Acceptance criteria

### Extract (29.0)

- [ ] `PdmExtractor` runs against PDM SQL with tenant-provided XML; writes per-type files under `pdm_export/`.
- [ ] No ontology or Neo4j code in extractor.

### Transform (29.2)

- [ ] `PdmTransform` reads `pdm_export/` only; writes `etos_import/` CSVs listed above.
- [ ] Transform driven by `transform.config.json`; no hardcoded PDM type names in Python beyond config.
- [ ] `transform-manifest.json` reports row counts per output file.

### Ontology (29.1)

- [ ] Reference package defines `part`, `partVersion`, `hasVersion`, and `partVersion`→`partVersion` `contains`.
- [ ] Attribute schema covers PDM version fields and pivoted variables.
- [ ] Import profile includes parent/child/qty synonyms for PDM CSV headers.

### Import engine (29.3)

- [ ] Approved mapping can specify `structuralRelationshipType` for parent/child CSVs.
- [ ] Staging creates correct graph relationship types for both `contains` and `hasVersion`.
- [ ] Default BOM behavior preserved when structural type omitted (backward compatible).
- [ ] Tests cover both relationship types and validation failures.

### End-to-end (29.4)

- [ ] Documented four-batch import runbook from real PDM extract sample.
- [ ] Integration test or script proves staging graph with parts, versions, BOM, and hasVersion edges.
- [ ] No public API exposes raw PDM SQL; no fake live PDM connector enabled.

---

## Out of scope

- Live PDM connector execution (Issue 22 / future).
- `Configuration` as first-class graph nodes.
- `DataCard` import.
- Automatic merge of part/partVersion without explicit `hasVersion` edges.
- ERP import (same transform/import patterns apply later with separate transform config).
- Trusted promotion rule changes beyond existing Issue 11 flow.
- Frontend redesign of `/imports` (minimal mapping relationship selector only).

---

## Dependency graph

```text
Issue 7 (ontology) ──► Issue 8 (import staging) ──► Issue 29.3 (rel generalization)
Issue 18.5 (mfg package) ──► Issue 29.1 (ontology extension)
Issue 29.0 (extract, done) ──► Issue 29.2 (transform)
Issue 29.1 + 29.2 + 29.3 ──► Issue 29.4 (E2E runbook)
Issue 11 (promotion) ──► promote staged PDM graph
Issue 9 (identity resolution) ──► optional PDM ↔ ERP later
```

**Blocked by:** Issue 8, Issue 18.5 (published manufacturing package).  
**Blocks:** PDM MVP demo data on governed graph; future ERP transform using same relationship import.

---

## Implementation order (recommended)

1. **29.1** Ontology package extension + re-install.
2. **29.3** Backend relationship import generalization + tests (unblocks both structural CSVs).
3. **29.2** PdmTransform helper + `transform.config.json`.
4. **29.4** Runbook, demo fixtures, integration smoke with Steris sample export.

---

## Key files (implementation reference)

| Area | Files |
| ---- | ----- |
| Extract | `ETOS.Helpers/PdmExtractor/**` |
| Transform | `ETOS.Helpers/PdmTransform/**` (new) |
| Ontology | `packages/manufacturing-reference/ontology/*.json`, `profiles/import-profile.json` |
| Import staging | `ETOS.Backend/Imports/ImportService.cs`, `ImportStructuralImportHelper.cs`, `ImportModels.cs`, `ImportContracts.cs` |
| Resolver | `ETOS.Backend/Ontology/ModelPackageContextResolver.cs` |
| UI | `ETOS.Frontend` import mapping form (relationship type when structural) |
| Tests | `ETOS.Backend.Tests/*Import*`, new `PdmTransform` tests optional in helper |
| Docs | `docs/local-development.md`, `ETOS.Helpers/README.md` |

---

## Open decisions (resolve in 29.1)

1. **`part` identity:** `documentId` only vs keep `partNumber` for existing demo CSVs (may require dual identity or demo file update).
2. **All versions vs latest only:** transform filter `IsLatest=TRUE` for smaller graph.
3. **Configuration:** attribute-only vs deferred.
4. **Variable pivot:** fixed column list vs dynamic headers in attribute schema.

---

## Review questions

1. Should `hasVersion` live in `relationships.json` only, or also mirrored in `bom-relationships.json` for backward compatibility?
2. Is four import batches acceptable for MVP, or should backend support multi-file batch orchestration later?
3. Should transform manifest be uploaded as import evidence alongside canonical CSVs?
