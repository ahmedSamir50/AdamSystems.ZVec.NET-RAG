#!/usr/bin/env python3
"""
ZVec Gap Detection Pattern Scanner.

Reads a git diff and checks it against the defect patterns defined in
`.agents/gaps/patterns.md`. Outputs a structured YAML gap report to
`.agents/gaps/reports/YYYY-MM-DD-commitSHA.md` and `.agents/gaps/reports/latest.md`.

Exit codes:
  0 — No P1 gaps found (merge allowed by pattern scanner).
  1 — At least one P1 gap found (merge blocked).
  2 — Scanner error (could not read diff, etc.).

Usage:
  python3 .agents/gaps/scan_patterns.py <diff_path>
"""

import os
import re
import sys
from datetime import date
from pathlib import Path

# ---------------------------------------------------------------------------
# Pattern definitions
#
# Each entry: (pattern_id, regex, category, fix, severity, file_filter)
# `file_filter` is None (match anywhere) or a callable(path) -> bool.
# Patterns mirror `.agents/gaps/patterns.md`.
# ---------------------------------------------------------------------------

PATTERNS_P1 = [
    (
        "P1-01",
        r"Assert\.True\(true\)|Assert\.True\(false\)|Assert\.Pass\(\)",
        "dummy_test",
        "Replace with real assertion testing actual behavior",
        "P1",
        None,
    ),
    (
        "P1-02",
        r"catch\s*\{\s*\}|catch\s*\(\s*Exception\s*\)\s*\{\s*\}",
        "swallowed_exception",
        "Add logging or explicit justification comment",
        "P1",
        lambda p: "tests/" not in p,
    ),
    (
        "P1-03",
        r"\.Result\b|\.Wait\(\)|\.GetAwaiter\(\)\.GetResult\(\)",
        "sync_over_async",
        "Use await or configure with .ConfigureAwait(false)",
        "P1",
        lambda p: "src/" in p,
    ),
    (
        "P1-04",
        r"_nativeCollection\s*=\s*null",
        "null_before_recovery",
        "Prepare new handle first, then atomic swap",
        "P1",
        None,
    ),
]

PATTERNS_P2 = [
    (
        "P2-01",
        r"Type\.GetProperties|Activator\.CreateInstance|PropertyInfo\.SetValue",
        "unannotated_reflection",
        "Add [RequiresUnreferencedCode]/[RequiresDynamicCode] or use source generator",
        "P2",
        lambda p: "src/" in p,
    ),
    (
        "P2-02",
        r"new float\[",
        "array_allocation_vector_path",
        "Use ReadOnlyMemory<float> pin path",
        "P2",
        lambda p: any(tok in p for tok in ("Search", "Query", "Vector")),
    ),
    (
        "P2-03",
        r"\.ToArray\(\)",
        "defensive_copy",
        "Check if source is already an array; remove redundant copy",
        "P2",
        lambda p: "src/" in p,
    ),
    (
        "P2-04",
        r"Activator\.CreateInstance",
        "runtime_reflection",
        "Use source generator instead",
        "P2",
        lambda p: "src/" in p,
    ),
    (
        "P2-05",
        r"lock\s*\(",
        "lock_without_recovery",
        "Use try/catch inside lock to restore previous state on failure",
        "P2",
        lambda p: "src/" in p,
    ),
    (
        "P2-07",
        r"new\s+HttpClient\s*\(",
        "httpclient_no_factory",
        "Use IHttpClientFactory instead of new HttpClient() to avoid socket exhaustion",
        "P2",
        lambda p: "src/" in p,
    ),
]

ALL_PATTERNS = PATTERNS_P1 + PATTERNS_P2


# ---------------------------------------------------------------------------
# Diff parsing
# ---------------------------------------------------------------------------

DIFF_FILE_HEADER = re.compile(r"^\+\+\+\s+b?/(.*)$")
DIFF_HUNK_HEADER = re.compile(r"^@@\s+-\d+(?:,\d+)?\s+\+(\d+)(?:,\d+)?\s@@")


def parse_diff(diff_text):
    """Yield (file_path, line_number, line_text) for each added line in the diff.

    `line_number` is the new-file line number for added lines, or None when
    it cannot be determined (e.g., context/deleted lines are skipped).
    """
    current_file = None
    new_line = 0
    for raw in diff_text.splitlines():
        if raw.startswith("+++ "):
            m = DIFF_FILE_HEADER.match(raw)
            current_file = m.group(1) if m else None
            continue
        if raw.startswith("@@"):
            m = DIFF_HUNK_HEADER.match(raw)
            new_line = int(m.group(1)) if m else 0
            continue
        if not raw or raw[0] not in ("+", " ", "-"):
            continue
        if raw[0] == "+":
            yield (current_file or "", new_line, raw[1:])
            new_line += 1
        elif raw[0] in (" ", "-"):
            if raw[0] == " ":
                new_line += 1


# ---------------------------------------------------------------------------
# Scanning
# ---------------------------------------------------------------------------

def scan_diff(diff_path):
    """Scan the diff at `diff_path` and return a list of gap dicts."""
    try:
        with open(diff_path, encoding="utf-8", errors="replace") as f:
            diff_text = f.read()
    except OSError as exc:
        print(f"[scan_patterns] ERROR reading diff: {exc}", file=sys.stderr)
        sys.exit(2)

    gaps = []
    for pid, pattern, category, fix, severity, file_filter in ALL_PATTERNS:
        regex = re.compile(pattern)
        for file_path, line_no, line_text in parse_diff(diff_text):
            if file_filter is not None and not file_filter(file_path):
                continue
            if not regex.search(line_text):
                continue
            gaps.append(
                {
                    "id": pid,
                    "severity": severity,
                    "category": category,
                    "file": file_path or "<unknown>",
                    "line": line_no or 0,
                    "matched": regex.search(line_text).group(),
                    "fix": fix,
                }
            )
    return gaps


# ---------------------------------------------------------------------------
# Report writing
# ---------------------------------------------------------------------------

def write_report(gaps, commit_sha):
    """Write the structured YAML report. Returns the number of P1 gaps."""
    report_dir = Path(".agents/gaps/reports")
    report_dir.mkdir(parents=True, exist_ok=True)

    p1_gaps = [g for g in gaps if g["severity"] == "P1"]
    p2_gaps = [g for g in gaps if g["severity"] == "P2"]

    lines = []
    lines.append("# Auto-generated by .agents/gaps/scan_patterns.py — do not edit manually.")
    lines.append(f"commit: {commit_sha}")
    lines.append(f"date: {date.today()}")
    lines.append("gates:")
    lines.append(f"  merge_allowed: {'false' if p1_gaps else 'true'}")
    lines.append(
        "  blocking_gaps: [" + ", ".join(g["id"] for g in p1_gaps) + "]"
    )
    lines.append(
        "  warning_gaps: [" + ", ".join(g["id"] for g in p2_gaps) + "]"
    )
    lines.append("")
    lines.append("gaps_found:")
    if gaps:
        for g in gaps:
            lines.append(f"  - id: {g['id']}")
            lines.append(f"    severity: {g['severity']}")
            lines.append(f"    category: {g['category']}")
            lines.append(f"    file: {g['file']}")
            lines.append(f"    line: {g['line']}")
            lines.append(f"    matched: {g['matched']!r}")
            lines.append(f"    fix: {g['fix']}")
    else:
        lines.append("  []")
    lines.append("")
    lines.append("gaps_updated: []")
    lines.append("gaps_closed: []")
    lines.append("gaps_new: [" + ", ".join(sorted({g['id'] for g in gaps})) + "]")

    body = "\n".join(lines) + "\n"

    filename = f"{date.today()}-{commit_sha[:7]}.md"
    (report_dir / filename).write_text(body, encoding="utf-8")
    (report_dir / "latest.md").write_text(body, encoding="utf-8")

    return len(p1_gaps)


# ---------------------------------------------------------------------------
# Entry point
# ---------------------------------------------------------------------------

def main():
    if len(sys.argv) < 2:
        print("Usage: scan_patterns.py <diff_path>", file=sys.stderr)
        sys.exit(2)

    diff_path = sys.argv[1]
    commit_sha = (
        os.popen("git rev-parse HEAD").read().strip() or "unknown"
    )

    gaps = scan_diff(diff_path)
    p1_count = write_report(gaps, commit_sha)

    print(f"Found {len(gaps)} gaps ({p1_count} P1 blocking)")
    if gaps:
        report_path = Path(".agents/gaps/reports/latest.md")
        print(f"Report written to {report_path}")
    sys.exit(1 if p1_count > 0 else 0)


if __name__ == "__main__":
    main()
