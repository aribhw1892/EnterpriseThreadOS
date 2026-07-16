# Governed document ingest (Issue 12.1)

Binary files (PDF, text, SolidWorks, generic attachments) upload through the **Document Memory** APIs — not CSV import staging. Structured `part` / `partVersion` rows still flow through governed CSV import (Issue 8).

## Four layers

| Layer | Role |
| ----- | ---- |
| Object storage (`IDocumentFileStorage`) | Original bytes — local disk by default, MinIO when configured |
| PostgreSQL | `DocumentArtifact`, `DocumentVersion`, links, extraction status, vector index records |
| Qdrant | Chunk embeddings — retrieval index only (disabled by default locally) |
| Neo4j | `part` ↔ `partVersion` ↔ `document` graph context via `DocumentObjectLink` |

## Retrieval order (governed query)

```text
Trusted graph → linked document SQL metadata → vector fallback (Qdrant) → LLM
```

Vector fallback applies only when `DocumentVectorIndexing:Enabled` is `true` and the intent strategy allows it (for example `document-evidence-context`).

## Extraction providers

Registered in `AddEnterpriseThreadDocumentMemory()`:

| Provider key | Handles |
| ------------ | ------- |
| `text-v1` | `.txt`, `.csv`, `.md`, `text/*` |
| `pdf-text-v1` | `.pdf` (text extraction via PdfPig) |
| `solidworks-metadata-v1` | `.sldprt`, `.sldasm`, `.slddrw` (metadata only — geometry parsing stays disabled) |
| `generic-binary-v1` | Fallback for unknown binaries |

`DocumentIngest:AutoExtractOnUpload` triggers extraction when a version upload leaves `ExtractionStatus` at `NotStarted`.

## Configuration (`appsettings.json`)

```json
"DocumentFileStorage": {
  "Provider": "Local",
  "RootPath": "document-memory",
  "Minio": {
    "Endpoint": "localhost:9000",
    "AccessKey": "etosminio",
    "SecretKey": "etos_minio_dev_password",
    "Bucket": "etos-documents",
    "UseSsl": false
  }
},
"DocumentIngest": {
  "AutoExtractOnUpload": true
},
"DocumentVectorIndexing": {
  "Enabled": false,
  "AutoIndexOnUpload": true,
  "Qdrant": {
    "Host": "localhost",
    "Port": 6334,
    "CollectionName": "etos-document-chunks",
    "UseGrpc": true
  },
  "Embedding": {
    "Provider": "deterministic-v1",
    "Model": "text-embedding-3-small",
    "Dimensions": 64,
    "MaxChunkCharacters": 1200
  }
}
```

### Embedding providers (local or cloud)

Same dual-mode principle as agent runtime (`openai` / `openai-compatible`):

| Provider | When to use | Credentials |
| -------- | ----------- | ----------- |
| `deterministic-v1` | CI / default / no secret | None (SHA256 stub) |
| `openai` / `openai-v1` | Cloud OpenAI embeddings | `ApiKey` or `OPENAI_API_KEY` |
| `openai-compatible` | LM Studio / local OpenAI-compatible server | `BaseUrl` or `OPENAI_BASE_URL` (API key optional; defaults to `lm-studio`) |

Examples:

```json
"Embedding": {
  "Provider": "openai",
  "Model": "text-embedding-3-small",
  "Dimensions": 1536
}
```

```json
"Embedding": {
  "Provider": "openai-compatible",
  "Model": "text-embedding-nomic-embed-text-v1.5",
  "Dimensions": 768,
  "BaseUrl": "http://localhost:1234/v1"
}
```

**Important:** Qdrant collection size follows `Dimensions` on first create. Changing provider/model/dimensions requires a new collection (or wipe) and re-index — do not mix deterministic vectors with real embeddings in one collection.

Missing cloud key or compatible base URL fails closed to `deterministic-v1` (same spirit as governed chat LLM fallback).

### Enable Qdrant locally

1. Start Docker Compose (Qdrant on port `6334` gRPC).
2. Set `DocumentVectorIndexing:Enabled` to `true` in `appsettings.Development.json` or user secrets.
3. Optionally set `Embedding:Provider` to `openai` or `openai-compatible` with matching `Dimensions`.
4. Restart the backend — `QdrantDocumentVectorIndexingService` creates the collection on first index.

### Switch to MinIO storage

Set `DocumentFileStorage:Provider` to `"Minio"` and ensure the MinIO bucket exists (the backend ensures the bucket on first store).

## API surface

- `POST /api/documents` — create document artifact
- `POST /api/documents/{id}/versions` — multipart upload
- `POST /api/documents/{id}/links` — link version to graph node (`part` / `partVersion`)
- `POST /api/documents/{id}/versions/{versionId}/vector-index` — manual re-index

## Manufacturing package

Ontology type `document` includes file-evidence attributes (`documentNumber`, `revision`, `filePath`, `sourceSystem`, `storageKey`, `contentType`, `sourceDocumentId`, `sourcePdmVersionKey`). Re-install the manufacturing reference package after ontology changes.

## Out of scope (MVP)

- Native SolidWorks geometry parsing
- OCR for images (`image-ocr-v1` remains roadmap)
- CSV staging for binary file bytes
- Identity resolution for file→parent links (use `DocumentObjectLink` instead)
