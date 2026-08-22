#!/usr/bin/env python3
"""Regenerate docs/form-spec.md from the live HPAC Typeform.

The Typeform renderer embeds the complete form definition as JSON inside the
page HTML, so the whole question set is readable without an API token, an
account, or browser automation.

Usage:
    curl -sL -A "$UA" https://pq3ivecn4rb.typeform.com/to/ZzIBaNLP \
      | python3 tools/extract-typeform.py > docs/form-spec.md

    python3 tools/extract-typeform.py --json < page.html > form.json

This file is the source of truth for the domain model (issue #5) and for the
public report form (issue #11). Regenerate it rather than hand-editing
docs/form-spec.md; CI diffs the two.
"""
from __future__ import annotations

import argparse
import json
import re
import sys

FORM_URL = "https://pq3ivecn4rb.typeform.com/to/ZzIBaNLP"


def scan_balanced(text: str, start: int) -> str:
    """Return the balanced JSON value beginning at `start`.

    A plain bracket count is not enough: field titles contain brackets and
    escaped quotes, so quote state and backslash escapes are tracked too.
    """
    depth = 0
    in_string = False
    escaped = False
    for i in range(start, len(text)):
        char = text[i]
        if escaped:
            escaped = False
        elif char == "\\":
            escaped = True
        elif char == '"':
            in_string = not in_string
        elif not in_string:
            if char in "[{":
                depth += 1
            elif char in "]}":
                depth -= 1
                if depth == 0:
                    return text[start : i + 1]
    raise ValueError("unbalanced JSON starting at offset %d" % start)


def extract_fields(html: str) -> list[dict]:
    """Pull the top-level `fields` array out of the embedded form definition.

    The page contains several `"fields":[` occurrences — the top-level question
    list plus the nested lists belonging to group and contact_info fields. The
    longest one is the top-level list.
    """
    best: tuple[int, list] | None = None
    for match in re.finditer(r'"fields":\[', html):
        start = match.end() - 1
        try:
            blob = scan_balanced(html, start)
            parsed = json.loads(blob)
        except (ValueError, json.JSONDecodeError):
            continue
        if best is None or len(blob) > best[0]:
            best = (len(blob), parsed)
    if best is None:
        raise SystemExit(
            "no form definition found in input — Typeform may have changed its "
            "page format; see docs/form-spec.md for the last known-good copy"
        )
    return best[1]


def choices_of(field: dict) -> list[str]:
    return [c.get("label", "") for c in field.get("properties", {}).get("choices", [])]


def render_field(field: dict, out: list[str], depth: int = 0) -> None:
    props = field.get("properties", {})
    indent = "  " * depth
    required = " **(required)**" if field.get("validations", {}).get("required") else ""
    out.append(f"{indent}- **{field.get('title', '')}** — `{field.get('type')}`{required}")

    description = props.get("description")
    if description:
        out.append(f"{indent}  - {description.strip()}")

    choices = choices_of(field)
    if choices:
        multi = " (multi-select)" if props.get("allow_multiple_selection") else ""
        out.append(f"{indent}  - Choices{multi}: " + ", ".join(f"`{c}`" for c in choices))

    for sub in props.get("fields", []):
        render_field(sub, out, depth + 1)


def render_markdown(fields: list[dict]) -> str:
    out = [
        "# Occurrence report — form specification",
        "",
        "> **Generated file — do not edit by hand.**",
        "> Regenerate with `tools/extract-typeform.py`; CI diffs this against the live form.",
        "",
        f"Source: <{FORM_URL}>",
        "",
        "This is the question set HPAC has been collecting through Typeform. The new",
        "form reproduces it so that historical and future reports stay comparable, and",
        "so reporters see wording they already recognise.",
        "",
        "See `docs/anonymization-policy.md` for which of these fields survive into a",
        "published summary — several of them never do.",
        "",
        "## Fields",
        "",
    ]
    for field in fields:
        render_field(field, out)
    out.append("")
    return "\n".join(out)


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--json",
        action="store_true",
        help="emit the raw field JSON instead of markdown",
    )
    args = parser.parse_args()

    html = sys.stdin.read()
    if not html.strip():
        raise SystemExit("no input on stdin — pipe the fetched Typeform page in")

    fields = extract_fields(html)
    if args.json:
        json.dump(fields, sys.stdout, indent=1, ensure_ascii=False)
        sys.stdout.write("\n")
    else:
        sys.stdout.write(render_markdown(fields))
    return 0


if __name__ == "__main__":
    sys.exit(main())
