"""Permanent offline compiler regressions; uses temporary synthetic Markdown only."""
import copy
import hashlib
import importlib.util
import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch
import sys

sys.dont_write_bytecode = True

SPEC = importlib.util.spec_from_file_location("help_compiler", Path(__file__).with_name("WriteDocumentationCatalog.py"))
compiler = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(compiler)


class DocumentationCompilerTests(unittest.TestCase):
    def setUp(self):
        owned = compiler.ROOT / "artifacts/documentation-tests"
        owned.mkdir(parents=True, exist_ok=True)
        self.temporary = tempfile.TemporaryDirectory(prefix="synthetic-help-", dir=owned)
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name)
        (self.root / "docs/user").mkdir(parents=True)
        self.source = self.root / "docs/user/start.md"
        self.source.write_text("# Synthetic help\n\n## Safety\n\n**Review required.** Never transmit automatically.\n", encoding="utf-8")
        self.entries = [{"id": "start", "source": "docs/user/start.md", "keywords": "synthetic"}]

    def compile(self, text=None):
        if text is not None: self.source.write_text(text, encoding="utf-8")
        return compiler.compile_catalog(self.root, self.entries)

    def test_real_catalog_is_deterministic_current_and_complete(self):
        first = compiler.render(compiler.compile_catalog())
        self.assertEqual(first, compiler.render(compiler.compile_catalog()))
        self.assertEqual(first, compiler.OUTPUT.read_bytes())
        data = json.loads(first)
        expected = {p.relative_to(compiler.ROOT).as_posix() for p in (compiler.ROOT / "docs/user").glob("*.md")}
        expected |= {"docs/reference/limits.md", "docs/reference/storage-and-formats.md"}
        self.assertEqual(expected, {t["source"] for t in data["topics"]})
        self.assertEqual(20, len(data["topics"]))
        for topic in data["topics"]:
            text = (compiler.ROOT / topic["source"]).read_text(encoding="utf-8-sig")
            self.assertEqual(hashlib.sha256(text.encode("utf-8")).hexdigest(), topic["sourceHash"])

    def test_line_endings_have_canonical_hash_and_bytes(self):
        text = "# Synthetic help\n\nReview required.\n"
        first = compiler.render(self.compile(text))
        self.source.write_bytes(text.replace("\n", "\r\n").encode())
        self.assertEqual(first, compiler.render(self.compile()))

    def test_main_writes_bom_free_utf8_lf_bytes(self):
        catalog = self.compile("# Synthetic help\n\nCaf\u00e9: review required.\n")
        expected = compiler.render(catalog)
        output = self.root / "generated/catalog.json"
        with patch.object(compiler, "OUTPUT", output), patch.object(compiler, "compile_catalog", return_value=catalog), patch("builtins.print"):
            with patch.object(sys, "argv", ["WriteDocumentationCatalog.py"]):
                compiler.main()
            actual = output.read_bytes()
            self.assertEqual(expected, actual)
            self.assertIn("Caf\u00e9".encode("utf-8"), actual)
            self.assertNotIn(b"\r", actual)
            self.assertFalse(actual.startswith(b"\xef\xbb\xbf"))
            self.assertTrue(actual.endswith(b"\n"))
            with patch.object(sys, "argv", ["WriteDocumentationCatalog.py", "--check"]):
                compiler.main()

    def test_check_rejects_crlf_catalog_without_rewriting_it(self):
        catalog = self.compile()
        output = self.root / "catalog.json"
        crlf = compiler.render(catalog).replace(b"\n", b"\r\n")
        output.write_bytes(crlf)
        with patch.object(compiler, "OUTPUT", output), patch.object(compiler, "compile_catalog", return_value=catalog), patch.object(sys, "argv", ["WriteDocumentationCatalog.py", "--check"]):
            with self.assertRaisesRegex(SystemExit, "catalog is stale"):
                compiler.main()
        self.assertEqual(crlf, output.read_bytes())

    def test_tables_code_escaping_warning_and_list_markers_are_preserved(self):
        result = self.compile("# Synthetic help\n\n> **Review required.**\n\n1. Use `A|B` and \\*literal\\*.\n\n   ```sql\n   SELECT '<literal>', 'A|B';\n   ```\n\n| Name | Value |\n| --- | --- |\n| `A|B` | escaped \\| and **warning** |\n")
        blocks = result["topics"][0]["blocks"]
        self.assertEqual("Review required.", blocks[1]["spans"][0]["text"])
        self.assertEqual("strong", blocks[1]["spans"][0]["kind"])
        self.assertEqual("1.", blocks[2]["marker"])
        self.assertIn("*literal*", blocks[2]["text"])
        self.assertEqual("SELECT '<literal>', 'A|B';\n", blocks[3]["text"])
        self.assertEqual("A|B", blocks[4]["rows"][1][0][0]["text"])
        self.assertIn("escaped | and ", blocks[4]["rows"][1][1][0]["text"])

    def test_topic_links_anchors_api_and_repository_references_are_classified(self):
        (self.root / "docs/script-api-reference.md").write_text("# API\n", encoding="utf-8")
        (self.root / "docs/detail.md").write_text("# Detail\n", encoding="utf-8")
        result = self.compile("# Synthetic help\n\n## Same\n\n## Same\n\n[local](#same-1) [API](../script-api-reference.md) [detail](../detail.md)\n")
        spans = result["topics"][0]["blocks"][-1]["spans"]
        links = [s["link"] for s in spans if s["kind"] == "link"]
        self.assertEqual(["topic", "api", "repository"], [l["kind"] for l in links])
        self.assertEqual("same-1", links[0]["anchor"])

    def test_missing_or_outside_links_and_bad_anchors_are_rejected(self):
        for value in ("missing.md", "#absent", "../../../outside.md", "file:///private", "javascript:alert", "/absolute"):
            with self.subTest(value=value), self.assertRaises(ValueError): self.compile(f"# Synthetic help\n\n[bad]({value})\n")

    def test_unsupported_constructs_are_rejected(self):
        for value in ("![image](x.png)", "<script>bad</script>", "~~removed~~", "[ref][id]", "    indented code", "- outer\n  - nested", "**unclosed", "`unclosed", "~~~text\ncode\n~~~", "+ item", "a  \nb", "a\\\nb", "| A |\n| :--- |\n| B |", "text &amp; entity"):
            with self.subTest(value=value), self.assertRaises(ValueError): self.compile("# Synthetic help\n\n" + value + "\n")

    def test_table_width_fences_and_title_are_validated(self):
        for value in ("# Title\n\n| A | B |\n| --- | --- |\n| one |", "# Title\n\n```csharp\nmissing close", "No title", "# One\n# Two"):
            with self.subTest(value=value), self.assertRaises(ValueError): self.compile(value)

    def test_manifest_ids_and_scope_are_validated(self):
        for changes in (("id", "Bad ID"), ("source", "docs/developer/internal.md"), ("source", "docs/user/../stage19-private.md"), ("keywords", "x" * 513)):
            entries = copy.deepcopy(self.entries); entries[0][changes[0]] = changes[1]
            with self.subTest(changes=changes), self.assertRaises(ValueError): compiler.compile_catalog(self.root, entries)
        with self.assertRaises(ValueError): compiler.compile_catalog(self.root, self.entries * 2)

    def test_corrupt_budget_and_empty_topic_are_rejected(self):
        with self.assertRaises(ValueError): self.compile("# Title\n" + "a" * 100001)
        with self.assertRaises(ValueError): self.compile("")
        with self.assertRaises(ValueError): compiler.compile_catalog(self.root, [])

    def test_runtime_span_and_table_budgets_are_rejected_at_generation(self):
        for value in ("x" * 40000, "`x` " * 1100, "| A |\n| --- |\n" + "| B |\n" * 256):
            with self.subTest(length=len(value)), self.assertRaises(ValueError): self.compile("# Title\n\n" + value)


if __name__ == "__main__": unittest.main()
