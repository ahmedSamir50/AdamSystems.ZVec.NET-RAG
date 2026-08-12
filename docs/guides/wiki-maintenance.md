# MkDocs & Wiki Maintenance

All architecture, math, dependencies, theory, scripts, and API changes must be documented in `docs/` (`mkdocs.yml`).

## Workflow

1. Modify or add documentation files under `docs/`.
2. Update `mkdocs.yml` navigation structure if adding new sections.
3. Validate locally:
   ```bash
   pip install mkdocs-material
   mkdocs serve
   ```
4. Verification gate: `zvec-code-reviewer-expert` audits documentation completeness before PR approval.
