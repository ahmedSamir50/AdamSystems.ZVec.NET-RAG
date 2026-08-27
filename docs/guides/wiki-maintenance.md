# MkDocs & Wiki Maintenance

All architecture, math, dependencies, theory, scripts, and API changes must be documented in `docs/` (`mkdocs.yml`).

## Workflow

1. Modify or add documentation files under `docs/`.
2. Update `mkdocs.yml` navigation structure if adding new sections.
3. Validate locally:
   ```bash
   pip install mkdocs-material
   mkdocs serve
   OR python -m mkdocs serve
   ```
4. Verification gate: `zvec-code-reviewer-expert` audits documentation completeness before PR approval.

## Documentation Triggers (Recent Remediation Areas)

Update these pages when making related code changes:

| Change area | Primary docs |
|---|---|
| Filter visitor / `ContainAny` / error codes | `architecture/vectordata-connector.md`, `reference/api.md`, `architecture/native-aot-memory.md` |
| `OptimizeAndReopenAsync` lifecycle | `architecture/vectordata-connector.md` |
| Roslyn analyzers `ZVEC001` / `ZVEC002` | `architecture/native-aot-memory.md`, `reference/zvec-net-aot-recommendations.md`, `reference/api.md` |
| CI / pre-commit quality gate | `guides/testing-strategy.md`, `guides/code-standards.md`, `reference/zvec-net-aot-recommendations.md` |
| Agent harness / skills | `.agents/AGENTS.md`, `.agents/skills/*/SKILL.md` (cross-reference from `guides/code-standards.md`) |

## Architecture diagrams

- Use **Mermaid** (` ```mermaid ` fences) for layer stacks, pipelines, component maps, and DI trees — not ASCII box art (`+------`, `┌──`, `├──`).
- **Non-destructive rule:** converting a diagram must not remove prose, tables, or bullet lists; every label in the old diagram must appear in Mermaid node text and/or preserved bullets below.
- Mermaid syntax: no spaces in node IDs; quote labels with special characters; no `style`/`classDef` color overrides.
