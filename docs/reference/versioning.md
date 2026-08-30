# Versioning

This repository publishes several NuGet packages. They share **one** product version. The native engine and third-party libraries do **not** share that number.

## 1. Repo product version (this repo)

**Single source of truth:** `Directory.Build.props` property `Version`.

**Current value:** `1.0.0-preview.1`

Every packable project under `src/` inherits that value. Do not set `<Version>` on individual `.csproj` files.

Packages on this line:

- `ZVec.Extensions.VectorData`
- `ZVec.Extensions.VectorData.Analyzers`
- `ZVec.Extensions.VectorData.SourceGenerator`
- `ZVec.Rag`
- `ZVec.Rag.Pdf`
- `ZVec.Rag.Testing`
- `ZVec.Rag.Template`

### How to bump our version

1. Edit `Directory.Build.props` `<Version>`.
2. Replace the same string in template content `PackageReference Version="..."` attributes (generated apps do not inherit this repo's Directory.Build.props).
3. Replace the same string in `samples/03-offline-phone-rag/MauiApp/ZVecRagApp.csproj` PackageReferences.
4. Replace the nupkg file name in `.github/workflows/quality-gate.yml` (`ZVec.Rag.Template.<Version>.nupkg`).
5. Replace quoted versions in `README.md`, `src/*/README.md`, and `docs/reference/dependencies.md`.

### SemVer for this line

- `1.0.0-preview.N` — prerelease of this repo. Increment `N` for any publish of these packages.
- `1.0.0` — this repo's first stable (independent of `ZVec.NET` leaving beta).
- Major bump (`2.0.0`) — breaking change to **our** public API.

## 2. Engine version (`ZVec.NET`)

**Source of truth:** `Directory.Packages.props` `PackageVersion Include="ZVec.NET"`.

**Current pin:** `1.0.0-beta.6`

This is the native embedded vector database and its .NET bindings. It is a **dependency**, not a package this repo authors.

When `ZVec.NET` ships `1.0.0` or a documented breaking change: update that pin, restore, run connector tests and AOT smoke. Do not silently copy the engine number onto `Directory.Build.props`.

## 3. Third-party versions

**Source of truth:** other `PackageVersion` rows in `Directory.Packages.props`.

Examples: `PdfPig` (PDF text extract), `Microsoft.Extensions.AI.Abstractions`, test SDKs.

These versions are unrelated to `1.0.0-preview.1`.

## 4. Record types vs collection names

`ZVecRagRecordV1` and `ZVecRagSectionSummaryV1` are the RAG pipeline's stored POCOs. They are not collection names.

Default collection names: `rag_chunks` (`ZVecRagOptions.CollectionName`) and `rag_section_summaries`. If you set a custom `CollectionName` and leave `SummaryCollectionName` unset, summaries use `{CollectionName}_summaries`. Set `SummaryCollectionName` to override. VectorData-only apps define their own `TRecord`; they do not use these types.
