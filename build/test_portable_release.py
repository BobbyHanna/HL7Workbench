import json
import pathlib
import tempfile
import unittest
from unittest.mock import patch
import portable_release as portable


class PortableReleaseTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(prefix="synthetic-portable-tests-")
        self.root = pathlib.Path(self.temp.name) / "candidate"
        self.root.mkdir()
        names = ["MessageWorkbench.exe", "HL7Workbench.Desktop.dll", "HL7Workbench.Desktop.deps.json", "HL7Workbench.Desktop.runtimeconfig.json",
                 "coreclr.dll", "hostfxr.dll", "hostpolicy.dll", "ScriptHost/HL7Workbench.ScriptHost.exe", "ScriptHost/HL7Workbench.ScriptHost.dll",
                 "ScriptHost/HL7Workbench.ScriptHost.runtimeconfig.json", "ScriptHost/coreclr.dll", "ScriptHost/hostfxr.dll", "ScriptHost/hostpolicy.dll",
                 "ScriptHost/Microsoft.CodeAnalysis.Scripting.dll", "ScriptHost/Microsoft.CodeAnalysis.CSharp.Scripting.dll",
                 "CANDIDATE-NOT-FOR-DISTRIBUTION.txt", "BUILD-PROVENANCE.json", "dependency-licenses.json", "THIRD-PARTY-NOTICES.md", "README.md", "USER-GUIDE.md", "RELEASE-NOTES.md", "ThirdPartyNotices/synthetic/LICENSE"]
        for name in names:
            path = self.root / name; path.parent.mkdir(parents=True, exist_ok=True); path.write_bytes(b"synthetic fixture")
        for name in ("HL7Workbench.Desktop", "ScriptHost/HL7Workbench.ScriptHost"):
            data = bytearray(256); data[:2] = b"MZ"; data[60:64] = (64).to_bytes(4, "little"); data[64:70] = b"PE\0\0\x64\x86"
            data[128:128+len(pathlib.Path(name).name)+4] = (pathlib.Path(name).name + ".dll").encode()
            (self.root / ("MessageWorkbench.exe" if name == "HL7Workbench.Desktop" else name + ".exe")).write_bytes(data)
            portable.write_json(self.root / (name + ".runtimeconfig.json"), {"runtimeOptions": {"includedFrameworks": [{"name": "Microsoft.NETCore.App", "version": "10.0.12"}]}})
        portable.write_json(self.root / "BUILD-PROVENANCE.json", {"candidateOnly": True, "version": portable.VERSION})
        portable.write_json(self.root / "dependency-licenses.json", [{"notices": ["ThirdPartyNotices/synthetic/LICENSE"]}])
        self.checksums()

    def tearDown(self):
        self.temp.cleanup()

    def checksums(self):
        files = {p.relative_to(self.root).as_posix(): p for p in self.root.rglob("*") if p.is_file() and p.name != "SHA256SUMS.txt"}
        (self.root / "SHA256SUMS.txt").write_text("".join(f"{portable.digest(p)}  {name}\n" for name, p in sorted(files.items())), encoding="utf-8")

    def test_archive_is_byte_identical_with_fixed_order_and_timestamps(self):
        a = pathlib.Path(self.temp.name) / "one.zip"; b = pathlib.Path(self.temp.name) / "two.zip"
        portable.deterministic_zip(self.root, a); portable.deterministic_zip(self.root, b)
        self.assertEqual(portable.digest(a), portable.digest(b))
        with portable.zipfile.ZipFile(a) as archive:
            self.assertEqual(sorted(archive.namelist()), archive.namelist())
            self.assertTrue(all(i.date_time == (1980, 1, 1, 0, 0, 0) for i in archive.infolist()))

    def test_excluded_payloads_symbols_sources_configs_and_diagnostics_fail_closed(self):
        for name in ("private.hl7", "private.csv", "source.cs", "secret.env", "debug.pdb", "createdump.exe", "fixtures/data.json", "appsettings.Local.json"):
            with self.subTest(name=name):
                path = self.root / name; path.parent.mkdir(exist_ok=True); path.write_bytes(b"synthetic")
                # Even a caller updating checksums cannot legitimize excluded artifacts.
                self.checksums()
                with self.assertRaises(ValueError):
                    portable.validate_tree(self.root)
                path.unlink()
        self.checksums()

    def test_missing_or_changed_files_and_absent_checksums_are_rejected(self):
        # Refreshing checksums cannot excuse a missing recipient document.
        for name in ("README.md", "USER-GUIDE.md", "RELEASE-NOTES.md"):
            with self.subTest(name=name):
                path = self.root / name; content = path.read_bytes(); path.unlink(); self.checksums()
                with self.assertRaisesRegex(ValueError, "Incomplete"):
                    portable.validate_tree(self.root)
                path.write_bytes(content)
        self.checksums()
        (self.root / "coreclr.dll").write_bytes(b"synthetic tamper")
        with self.assertRaisesRegex(ValueError, "integrity"):
            portable.validate_tree(self.root)
        self.checksums(); (self.root / "SHA256SUMS.txt").unlink()
        with self.assertRaisesRegex(ValueError, "Incomplete"):
            portable.validate_tree(self.root)

    def test_worker_leak_and_framework_dependent_runtime_cannot_pass(self):
        (self.root / "Microsoft.CodeAnalysis.Scripting.dll").write_bytes(b"synthetic")
        with self.assertRaises(ValueError): portable.validate_tree(self.root)
        (self.root / "Microsoft.CodeAnalysis.Scripting.dll").unlink()
        portable.write_json(self.root / "HL7Workbench.Desktop.runtimeconfig.json", {"runtimeOptions": {"framework": {"name": "Microsoft.NETCore.App", "version": "10.0.12"}}})
        self.checksums()
        with self.assertRaisesRegex(ValueError, "self-contained"): portable.validate_tree(self.root)

    def test_notices_and_original_x64_assembly_binding_are_required(self):
        (self.root / "ThirdPartyNotices/synthetic/LICENSE").unlink(); self.checksums()
        with self.assertRaisesRegex(ValueError, "notice"): portable.validate_tree(self.root)
        (self.root / "ThirdPartyNotices/synthetic/LICENSE").write_bytes(b"synthetic notice")
        (self.root / "MessageWorkbench.exe").write_bytes(b"synthetic renamed assembly"); self.checksums()
        with self.assertRaisesRegex(ValueError, "binding"): portable.validate_tree(self.root)

    def test_links_are_rejected_and_existing_output_is_never_overwritten(self):
        with patch.object(pathlib.Path, "is_symlink", return_value=True):
            with self.assertRaises(ValueError): list(portable.checked_files(self.root))
        with self.assertRaises(FileExistsError):
            portable.assemble(self.root, self.root, self.root, self.root, {})

    def test_unknown_licenses_and_missing_copyright_text_fail_closed(self):
        cache = pathlib.Path(self.temp.name) / "cache"; package = cache / "synthetic" / "1.0"; package.mkdir(parents=True)
        upstream = pathlib.Path(self.temp.name) / "upstream"; upstream.mkdir(); portable.write_json(upstream / "upstream-licenses.json", [])
        nuspec = package / "synthetic.nuspec"
        nuspec.write_text('<package><metadata><license type="expression">UNREVIEWED</license></metadata></package>', encoding="utf-8")
        with self.assertRaisesRegex(ValueError, "Unreviewed"):
            portable.copy_notices(pathlib.Path(self.temp.name) / "unknown", cache, upstream, {"Synthetic/1.0": {"Desktop"}})
        nuspec.write_text('<package><metadata><license type="expression">MIT</license></metadata></package>', encoding="utf-8")
        with self.assertRaisesRegex(ValueError, "Missing original license"):
            portable.copy_notices(pathlib.Path(self.temp.name) / "missing", cache, upstream, {"Synthetic/1.0": {"Desktop"}})

    def test_modified_pinned_upstream_notice_is_rejected(self):
        cache = pathlib.Path(self.temp.name) / "cache"; package = cache / "synthetic" / "1.0"; package.mkdir(parents=True)
        (package / "synthetic.nuspec").write_text('<package><metadata><license type="expression">MIT</license></metadata></package>', encoding="utf-8")
        upstream = pathlib.Path(self.temp.name) / "upstream"; upstream.mkdir(); (upstream / "LICENSE").write_text("synthetic notice", encoding="utf-8")
        portable.write_json(upstream / "upstream-licenses.json", [{"file": "LICENSE", "sha256": "invalid", "packages": ["Synthetic/1.0"]}])
        with self.assertRaisesRegex(ValueError, "hash mismatch"):
            portable.copy_notices(pathlib.Path(self.temp.name) / "modified", cache, upstream, {"Synthetic/1.0": {"Desktop"}})

    def test_archive_roundtrip_rejects_extra_entries_or_changed_content(self):
        archive = pathlib.Path(self.temp.name) / "verified.zip"
        portable.deterministic_zip(self.root, archive); portable.verify_archive(self.root, archive)
        with portable.zipfile.ZipFile(archive, "a") as changed:
            changed.writestr("unexpected.txt", "synthetic")
        with self.assertRaisesRegex(ValueError, "inventory"):
            portable.verify_archive(self.root, archive)
        changed = pathlib.Path(self.temp.name) / "changed.zip"
        with portable.zipfile.ZipFile(changed, "w") as output:
            for path in self.root.rglob("*"):
                if path.is_file():
                    output.writestr(path.relative_to(self.root).as_posix(), b"synthetic tamper" if path.name == "USER-GUIDE.md" else path.read_bytes())
        with self.assertRaisesRegex(ValueError, "content"):
            portable.verify_archive(self.root, changed)

    def test_private_build_roots_in_utf8_or_utf16_are_not_packaged(self):
        for encoding in ("utf-8", "utf-16-le"):
            with self.subTest(encoding=encoding):
                (self.root / "USER-GUIDE.md").write_bytes(str(portable.ROOT).encode(encoding)); self.checksums()
                with self.assertRaisesRegex(ValueError, "Private build path"):
                    portable.validate_tree(self.root)


if __name__ == "__main__":
    unittest.main()
