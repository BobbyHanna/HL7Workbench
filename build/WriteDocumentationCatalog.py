"""Strict, deterministic offline help compiler. No runtime Markdown or network access."""
import argparse
import hashlib
import json
from pathlib import Path
import re
from urllib.parse import unquote, urlsplit

ROOT = Path(__file__).resolve().parent.parent
MANIFEST = ROOT / "docs/help-topics.json"
OUTPUT = ROOT / "src/HL7Workbench.Desktop/Documentation/catalog.json"


def slug(text):
    text = re.sub(r"\[([^\]]+)\]\([^)]*\)", r"\1", text)
    return re.sub(r"[^\w\- ]", "", text.replace("`", "").replace("\\", "").lower()).replace(" ", "-")


def headings(text):
    result, seen, fence = set(), {}, False
    for line in text.splitlines():
        if re.match(r"^\s*```", line):
            fence = not fence
        if fence:
            continue
        match = re.match(r"^#{1,6} (.+)$", line)
        if match:
            base = slug(match[1]); count = seen.get(base, 0); seen[base] = count + 1
            result.add(base + (f"-{count}" if count else ""))
    return result


def resolve_link(value, source, topic_paths, root):
    parsed = urlsplit(value)
    if parsed.scheme:
        if parsed.scheme != "https" or not parsed.netloc or parsed.username or parsed.password:
            raise ValueError("Unsupported link scheme")
        return {"kind": "external", "target": value, "anchor": ""}
    if parsed.netloc or parsed.query or value.startswith(("/", "\\")) or "\\" in value:
        raise ValueError("Unsupported local link")
    target = (source.parent / unquote(parsed.path)).resolve() if parsed.path else source.resolve()
    if not target.is_relative_to(root.resolve()) or not target.exists():
        raise ValueError(f"Missing/outside repository target: {value}")
    fragment = unquote(parsed.fragment)
    if fragment and (target.suffix != ".md" or fragment not in headings(target.read_text(encoding="utf-8-sig"))):
        raise ValueError(f"Missing heading: {value}")
    relative = target.relative_to(root.resolve()).as_posix()
    if relative in ("docs/script-api-reference.md", "docs/script-api-reference.json", "docs/script-api.md"):
        return {"kind": "api", "target": "", "anchor": ""}
    if relative in topic_paths:
        return {"kind": "topic", "target": topic_paths[relative], "anchor": fragment}
    return {"kind": "repository", "target": relative + ("#" + fragment if fragment else ""), "anchor": ""}


def inline(text, source, topic_paths, root):
    """Explicit supported inline grammar; fail rather than drop meaningful markup."""
    spans, plain, i = [], [], 0
    def flush():
        if plain:
            spans.append({"kind": "text", "text": "".join(plain)}); plain.clear()
    while i < len(text):
        c = text[i]
        if c == "\\":
            # Windows paths are ordinary literal text; only Markdown punctuation is escaped.
            if i + 1 < len(text) and text[i + 1] in r"\`*{}_[]()#+-.!|>":
                plain.append(text[i + 1]); i += 2
            else:
                plain.append(c); i += 1
        elif c == "`":
            flush(); run = len(text[i:]) - len(text[i:].lstrip("`")); marker = "`" * run
            end = text.find(marker, i + run)
            if end < 0: raise ValueError("Unclosed inline code")
            spans.append({"kind": "code", "text": text[i + run:end]}); i = end + run
        elif text.startswith("![", i):
            raise ValueError("Images are not supported in bundled Help")
        elif c == "[":
            flush(); end = text.find("](", i + 1)
            close = text.find(")", end + 2) if end >= 0 else -1
            if close < 0: raise ValueError("Unsupported/reference-style link")
            label = text[i + 1:end]; target = text[end + 2:close]
            if any(x in label for x in ("[", "`", "*")) or any(x.isspace() for x in target):
                raise ValueError("Unsupported nested/link-title markup")
            spans.append({"kind": "link", "text": label, "link": resolve_link(target, source, topic_paths, root)})
            i = close + 1
        elif c == "*" or c == "_" and (i == 0 or not text[i - 1].isalnum()):
            flush(); marker = c * (2 if text.startswith(c * 2, i) else 1)
            end = text.find(marker, i + len(marker))
            if end < 0: raise ValueError("Unclosed emphasis")
            value = text[i + len(marker):end]
            if not value or any(x in value for x in ("*", "`", "[")): raise ValueError("Unsupported nested emphasis")
            spans.append({"kind": "strong" if len(marker) == 2 else "emphasis", "text": value}); i = end + len(marker)
        elif c in "<>" or text.startswith("~~", i) or c == "&" and re.match(r"&(?:#\w+|\w+);", text[i:]):
            raise ValueError("HTML/entities/strikethrough require explicit renderer support")
        else:
            plain.append(c); i += 1
    flush()
    return spans


def cells(line):
    # A pipe inside code or escaped text is data, not a column boundary.
    result, cell, code, i = [], [], "", 0
    value = line.strip()
    if not value.startswith("|") or not value.endswith("|"): raise ValueError("Tables require outer pipes")
    value = value[1:-1]
    while i < len(value):
        if value[i] == "\\" and i + 1 < len(value):
            cell.extend(value[i:i + 2]); i += 2; continue
        if value[i] == "`":
            n = len(value[i:]) - len(value[i:].lstrip("`")); marker = "`" * n
            code = "" if code == marker else marker if not code else code
            cell.append(marker); i += n; continue
        if value[i] == "|" and not code:
            result.append("".join(cell).strip()); cell = []
        else: cell.append(value[i])
        i += 1
    if code: raise ValueError("Unclosed code in table")
    result.append("".join(cell).strip()); return result


def parse(text, source, topic_paths, root):
    lines, blocks, seen, i = text.splitlines(), [], {}, 0
    def content(value): return inline(value, source, topic_paths, root)
    def block(kind, value="", **extra):
        spans = [] if kind == "code" else content(value)
        blocks.append({"kind": kind, "text": value if kind == "code" else "".join(s["text"] for s in spans), "spans": spans, **extra})
    while i < len(lines):
        line = lines[i]
        if not line.strip(): i += 1; continue
        stripped = line.lstrip(); indent = len(line) - len(stripped)
        if stripped.startswith("```"):
            if indent and (not blocks or blocks[-1]["kind"] != "list" or indent != len(blocks[-1]["marker"]) + 1):
                raise ValueError("Unsupported fence indentation")
            if not re.fullmatch(r"```[a-zA-Z0-9+-]*", stripped): raise ValueError("Unsupported fence")
            language, code = stripped[3:], []; i += 1
            while i < len(lines) and lines[i].lstrip() != "```":
                if len(lines[i]) - len(lines[i].lstrip()) < indent and lines[i].strip(): raise ValueError("Invalid code indentation")
                code.append(lines[i][indent:]); i += 1
            if i == len(lines): raise ValueError("Unclosed fenced code")
            block("code", "\n".join(code) + ("\n" if code else ""), language=language); i += 1; continue
        if line.endswith("  ") or line.endswith("\\"): raise ValueError("Hard line breaks require explicit renderer support")
        match = re.match(r"^(#{1,6}) (.+)$", line)
        if match:
            base = slug(match[2]); n = seen.get(base, 0); seen[base] = n + 1
            block("heading", match[2], level=len(match[1]), anchor=base + (f"-{n}" if n else "")); i += 1; continue
        if stripped.startswith("|"):
            header = cells(stripped); i += 1
            if i == len(lines) or not all(re.fullmatch(r"-{3,}", v) for v in cells(lines[i])):
                raise ValueError("Missing table separator")
            if len(cells(lines[i])) != len(header): raise ValueError("Table separator width")
            i += 1; rows = [header]
            while i < len(lines) and lines[i].lstrip().startswith("|"):
                row = cells(lines[i])
                if len(row) != len(header): raise ValueError("Table width mismatch")
                rows.append(row); i += 1
            if len(rows) < 2: raise ValueError("Table has no data")
            blocks.append({"kind": "table", "text": "\n".join(" | ".join(r) for r in rows), "spans": [], "rows": [[content(c) for c in row] for row in rows]}); continue
        marker = re.match(r"^(- |\d+\. )(.+)$", line)
        quote = line.startswith("> ")
        if indent or re.match(r"^(?:[-*_]{3,}|\+ |\* |~~~|\[.*\]:|<)", line): raise ValueError("Unsupported block/indentation")
        value = marker[2] if marker else line[2:] if quote else line
        i += 1
        while i < len(lines) and lines[i].strip():
            nxt = lines[i]
            if re.match(r"^(?:#{1,6} |[-*+] |[-*_]{3,}|\d+\. |\| |> |```|~~~|<)", nxt) or nxt.lstrip().startswith("```"): break
            if marker:
                if not nxt.startswith(" " * len(marker[1])): break
                nxt = nxt[len(marker[1]):]
                if re.match(r"^(?:[-*+] |\d+\. )", nxt): raise ValueError("Nested lists require explicit renderer support")
            elif quote:
                if not nxt.startswith("> "): break
                nxt = nxt[2:]
            elif nxt[:1].isspace(): raise ValueError("Unsupported indentation")
            if nxt.endswith("  ") or nxt.endswith("\\"): raise ValueError("Hard line breaks require explicit renderer support")
            value += " " + nxt; i += 1
        block("list" if marker else "quote" if quote else "paragraph", value, **({"marker": marker[1].strip()} if marker else {}))
    if not blocks or blocks[0]["kind"] != "heading" or blocks[0]["level"] != 1:
        raise ValueError("Topic needs a first-level title")
    if sum(b["kind"] == "heading" and b["level"] == 1 for b in blocks) != 1: raise ValueError("One title per topic")
    return blocks


def compile_catalog(root=ROOT, manifest=None):
    root = root.resolve(); entries = manifest if manifest is not None else json.loads((root / "docs/help-topics.json").read_text(encoding="utf-8"))
    if not 1 <= len(entries) <= 64: raise ValueError("Topic limit")
    paths, ids, topics = {}, set(), []
    for e in entries:
        if set(e) != {"id", "source", "keywords"} or not re.fullmatch(r"[a-z][a-z0-9-]{0,63}", e["id"]): raise ValueError("Invalid topic metadata")
        if e["id"] in ids or e["source"] in paths: raise ValueError("Duplicate topic")
        if not (e["source"].startswith("docs/user/") or e["source"] in ("docs/reference/limits.md", "docs/reference/storage-and-formats.md")):
            raise ValueError("Only approved user/shared content may be bundled")
        if e["source"] != Path(e["source"]).as_posix() or ".." in Path(e["source"]).parts or Path(e["source"]).suffix != ".md":
            raise ValueError("Non-canonical topic source")
        source = root / e["source"]
        if not source.resolve().is_relative_to(root) or not source.is_file() or source.is_symlink(): raise ValueError("Invalid topic source")
        ids.add(e["id"]); paths[e["source"]] = e["id"]
    for e in entries:
        source = root / e["source"]; text = source.read_text(encoding="utf-8-sig")
        if len(text) > 100_000 or not isinstance(e["keywords"], str) or len(e["keywords"]) > 512: raise ValueError("Topic budget")
        blocks = parse(text, source, paths, root)
        if len(blocks) > 512: raise ValueError("Block budget")
        for block in blocks:
            rows = block.get("rows", [])
            if len(rows) > 256 or any(len(row) > 16 for row in rows): raise ValueError("Table budget")
            for spans in [block["spans"], *(cell for row in rows for cell in row)]:
                if len(spans) > 2048 or any(len(span["text"]) > 32768 for span in spans): raise ValueError("Inline budget")
        topics.append({"id": e["id"], "source": e["source"], "sourceHash": hashlib.sha256(text.encode("utf-8")).hexdigest(), "title": blocks[0]["text"], "keywords": e["keywords"], "blocks": blocks})
    return {"version": 1, "topics": topics}


def render(catalog):
    data = (json.dumps(catalog, ensure_ascii=False, indent=2) + "\n").encode("utf-8")
    if len(data) > 2 * 1024 * 1024: raise ValueError("Catalog byte budget")
    return data


def main():
    parser = argparse.ArgumentParser(description=__doc__); parser.add_argument("--check", action="store_true"); args = parser.parse_args()
    data = render(compile_catalog())
    if args.check:
        if not OUTPUT.exists() or OUTPUT.read_bytes() != data: raise SystemExit("Offline documentation catalog is stale; run build/WriteDocumentationCatalog.py")
        print("Offline documentation catalog matches curated Markdown.")
    else:
        OUTPUT.parent.mkdir(parents=True, exist_ok=True); OUTPUT.write_bytes(data); print("Rendered offline documentation catalog.")


if __name__ == "__main__": main()
