"""Current three-process preparation; fake binaries only, never a release archive."""
import json
import pathlib
import shutil
import subprocess
import sys
import unittest

import portable_release as portable
import test_portable_release as historical


class PortablePreparationTests(unittest.TestCase):
    def setUp(self):
        self.old = historical.PortableReleaseTests(); self.old.setUp(); self.addCleanup(self.old.tearDown)
        self.base = pathlib.Path(self.old.temp.name)
        resource = (portable.ROOT / "src/HL7Workbench.Core/ValidationCatalog/v251-model.json").read_bytes()
        for prefix in ("", "ScriptHost/"):
            (self.old.root / (prefix + "HL7Workbench.Core.dll")).write_bytes(b"synthetic PE resource fixture\0" + resource)
        self.desktop, self.worker, self.cache = self.old.published_inputs()
        self.query = self.base / "publish/QueryHost"; self.query.mkdir()
        for leaf in ("HL7Workbench.QueryHost.dll", "HL7Workbench.Querying.dll", "e_sqlite3.dll", "SQLitePCLRaw.core.dll",
                     "SQLitePCLRaw.provider.e_sqlite3.dll", "SQLitePCLRaw.batteries_v2.dll", "coreclr.dll", "hostfxr.dll", "hostpolicy.dll"):
            (self.query / leaf).write_bytes(b"synthetic dependency fixture")
        apphost = (self.worker / "HL7Workbench.ScriptHost.exe").read_bytes().replace(b"HL7Workbench.ScriptHost.dll", b"HL7Workbench.QueryHost.dll\0")
        (self.query / "HL7Workbench.QueryHost.exe").write_bytes(apphost)
        portable.write_json(self.query / "HL7Workbench.QueryHost.runtimeconfig.json",
                            {"runtimeOptions": {"includedFrameworks": [{"name": "Microsoft.NETCore.App", "version": "10.0.12"}]}})
        self.query_keys = ["SQLitePCLRaw." + name + "/3.0.5" for name in ("bundle_e_sqlite3", "config.e_sqlite3", "core", "provider.e_sqlite3")] + ["SQLite/3.53.4"]
        for key in self.query_keys:
            name, version = key.split("/"); package = self.cache / name.lower() / version; package.mkdir(parents=True)
            license = '<license type="file">LICENSE.txt</license>' if name == "SQLite" else '<license type="expression">Apache-2.0</license>'
            (package / (name.lower() + ".nuspec")).write_text('<package><metadata>' + license + '</metadata></package>', encoding="utf-8")
            if name == "SQLite":
                (package / "LICENSE.txt").write_bytes(b"\nSQLite is Public Domain\n\nhttps://sqlite.org/copyright.html\n\n")
        for directory, process in ((self.desktop, "Desktop"), (self.worker, "ScriptHost"), (self.query, "QueryHost")):
            assets = {p.name: {} for p in directory.glob("*.dll")}
            targets = {"Synthetic/1.0": {"runtime": assets}}
            libraries = {"Synthetic/1.0": {"type": "package"}}
            if process == "QueryHost":
                targets.update({key: {} for key in self.query_keys}); libraries.update({key: {"type": "package"} for key in self.query_keys})
            target = ".NETCoreApp,Version=v10.0/win-x64"
            portable.write_json(directory / ("HL7Workbench." + process + ".deps.json"),
                                {"runtimeTarget": {"name": target}, "targets": {target: targets}, "libraries": libraries})
        self.provenance = {"product": "Message Workbench", "version": portable.VERSION, "sourceCommit": "e" * 40, "buildId": "eeeeeeee", "dirty": True,
                           "assemblyIdentity": "HL7Workbench.Desktop", "entryPoint": "MessageWorkbench.exe",
                           "sdk": "10.0.401", "runtime": "10.0.12", "rid": "win-x64", "configuration": "Release",
                           "inputsSha256": {p: portable.digest(portable.ROOT / p) for p in portable.COVERED_FILES}}
        self.root = portable.stage_current(self.desktop, self.worker, self.query, self.base / "current", self.cache, self.provenance)

    def checksums(self):
        paths = {p.relative_to(self.root).as_posix(): p for p in self.root.rglob("*") if p.is_file() and p.name != "SHA256SUMS.txt"}
        (self.root / "SHA256SUMS.txt").write_text("".join(f"{portable.digest(p)}  {name}\n" for name, p in sorted(paths.items())), encoding="utf-8")

    def test_current_stages_three_complete_processes_and_no_archive(self):
        paths = portable.validate_tree(self.root)
        self.assertIn("QueryHost/e_sqlite3.dll", paths); self.assertIn(portable.CURRENT_MARKER, paths)
        self.assertFalse(list((self.base / "current").rglob("*.zip")))
        self.assertFalse((self.root / "CANDIDATE-NOT-FOR-DISTRIBUTION.txt").exists())
        origin = json.loads((self.root / "BUILD-PROVENANCE.json").read_text(encoding="utf-8"))
        self.assertIsNone(origin["artifactChecksums"]["archive"]); self.assertTrue(origin["verificationOnly"])

    def test_each_process_executable_config_runtime_deps_and_native_sqlite_are_required(self):
        names = ["MessageWorkbench.exe", "HL7Workbench.Desktop.dll", "HL7Workbench.Desktop.runtimeconfig.json", "HL7Workbench.Desktop.deps.json", "coreclr.dll"]
        for process in ("ScriptHost", "QueryHost"):
            names += [process + "/HL7Workbench." + process + suffix for suffix in (".exe", ".dll", ".runtimeconfig.json", ".deps.json")]
            names += [process + "/" + leaf for leaf in ("coreclr.dll", "hostfxr.dll", "hostpolicy.dll")]
        names.append("QueryHost/e_sqlite3.dll")
        for name in names:
            with self.subTest(name=name):
                path = self.root / name; content = path.read_bytes(); path.unlink(); self.checksums()
                with self.assertRaisesRegex(ValueError, "Incomplete"): portable.validate_tree(self.root)
                path.write_bytes(content)
        self.checksums(); portable.validate_tree(self.root)

    def test_missing_selected_transitive_asset_fails_even_after_checksum_refresh(self):
        (self.root / "QueryHost/SQLitePCLRaw.core.dll").unlink(); self.checksums()
        with self.assertRaisesRegex(ValueError, "Incomplete published"): portable.validate_tree(self.root)

    def test_declared_optional_dump_and_native_symbols_are_deliberately_excluded(self):
        path = self.root / "HL7Workbench.Desktop.deps.json"; data = json.loads(path.read_text(encoding="utf-8"))
        data["targets"][data["runtimeTarget"]["name"]]["Synthetic/1.0"]["native"] = {"createdump.exe": {}, "libSkiaSharp.pdb": {}}
        portable.write_json(path, data); self.checksums(); portable.validate_tree(self.root)
        (self.root / "createdump.exe").write_bytes(b"not allowed to ship"); self.checksums()
        with self.assertRaisesRegex(ValueError, "Unauthorized"): portable.validate_tree(self.root)

    def test_all_three_runtime_configs_must_be_pinned_self_contained(self):
        for prefix, process in portable.PROCESSES:
            with self.subTest(process=process):
                path = self.root / (prefix + "HL7Workbench." + process + ".runtimeconfig.json"); original = path.read_bytes()
                portable.write_json(path, {"runtimeOptions": {"framework": {"name": "Microsoft.NETCore.App", "version": "10.0.12"}}}); self.checksums()
                with self.assertRaisesRegex(ValueError, "self-contained"): portable.validate_tree(self.root)
                path.write_bytes(original)

    def test_every_apphost_must_keep_its_x64_binding(self):
        for name in ("MessageWorkbench.exe", "ScriptHost/HL7Workbench.ScriptHost.exe", "QueryHost/HL7Workbench.QueryHost.exe"):
            with self.subTest(name=name):
                path = self.root / name; original = path.read_bytes(); path.write_bytes(b"not a native x64 apphost"); self.checksums()
                with self.assertRaisesRegex(ValueError, "apphost"): portable.validate_tree(self.root)
                path.write_bytes(original)

    def test_dependency_inventory_cannot_omit_or_relabel_query_scope(self):
        path = self.root / "dependency-licenses.json"; records = json.loads(path.read_text(encoding="utf-8"))
        record = next(r for r in records if r["package"] == "SQLitePCLRaw.core"); record["scopes"] = ["Desktop"]
        portable.write_json(path, records); self.checksums()
        with self.assertRaisesRegex(ValueError, "dependency inventory"): portable.validate_tree(self.root)

    def test_missing_or_changed_notice_fails_even_after_checksum_refresh(self):
        records = json.loads((self.root / "dependency-licenses.json").read_text(encoding="utf-8"))
        record = next(r for r in records if r["package"] == "SQLitePCLRaw.core")
        for name in record["notices"]:
            with self.subTest(name=name):
                path = self.root / name; original = path.read_bytes(); path.unlink(); self.checksums()
                with self.assertRaisesRegex(ValueError, "notice"): portable.validate_tree(self.root)
                path.write_bytes(b"changed legal text"); self.checksums()
                with self.assertRaisesRegex(ValueError, "notice"): portable.validate_tree(self.root)
                path.write_bytes(original)

    def test_apache_support_is_exact_package_and_version_not_a_broad_allowlist(self):
        for key in ("Synthetic.Apache/1.0", "SQLitePCLRaw.core/99.0"):
            name, version = key.split("/"); package = self.cache / name.lower() / version; package.mkdir(parents=True)
            (package / (name.lower() + ".nuspec")).write_text('<package><metadata><license type="expression">Apache-2.0</license></metadata></package>', encoding="utf-8")
            with self.assertRaisesRegex(ValueError, "Unreviewed"):
                portable.copy_notices(self.base / name, self.cache, portable.ROOT / "build/third-party", {key: {"QueryHost"}}, scope="current")

    def test_exact_apache_and_sqlite_notices_are_included_with_query_provenance(self):
        records = json.loads((self.root / "dependency-licenses.json").read_text(encoding="utf-8"))
        for name in ("SQLitePCLRaw.core", "SQLitePCLRaw.bundle_e_sqlite3", "SQLitePCLRaw.config.e_sqlite3", "SQLitePCLRaw.provider.e_sqlite3"):
            record = next(r for r in records if r["package"] == name)
            self.assertEqual("Apache-2.0", record["license"]); self.assertEqual(["QueryHost"], record["scopes"])
            self.assertEqual(2, len(record["upstreamNotices"]))
            self.assertTrue(all(n["revision"] == "ed046114d5a30534e13294d94d78eb73de896ad4" for n in record["upstreamNotices"]))

    def test_changed_sqlite_file_license_is_not_accepted_as_an_arbitrary_file_license(self):
        (self.cache / "sqlite/3.53.4/LICENSE.txt").write_bytes(b"different terms")
        with self.assertRaisesRegex(ValueError, "Unreviewed SQLite"):
            portable.copy_notices(self.base / "changed-license", self.cache, portable.ROOT / "build/third-party", {"SQLite/3.53.4": {"QueryHost"}}, scope="current")

    def test_reviewed_query_package_cannot_silently_change_its_declaration_to_mit(self):
        spec = self.cache / "sqlitepclraw.core/3.0.5/sqlitepclraw.core.nuspec"
        spec.write_text('<package><metadata><license type="expression">MIT</license></metadata></package>', encoding="utf-8")
        with self.assertRaisesRegex(ValueError, "query-package license"):
            portable.copy_notices(self.base / "wrong-declaration", self.cache, portable.ROOT / "build/third-party", {"SQLitePCLRaw.core/3.0.5": {"QueryHost"}}, scope="current")

    def test_covered_source_notice_license_and_hashes_are_required(self):
        for name in portable.COVERED_FILES.values():
            with self.subTest(name=name):
                path = self.root / name; original = path.read_bytes(); path.unlink(); self.checksums()
                with self.assertRaisesRegex(ValueError, "Incomplete"): portable.validate_tree(self.root)
                path.write_bytes(original + b"changed"); self.checksums()
                with self.assertRaisesRegex(ValueError, "Covered-source hash"): portable.validate_tree(self.root)
                path.write_bytes(original)

    def test_forging_source_hashes_cannot_replace_the_embedded_adapted_resource(self):
        name = "covered-source/v251-model.json"; path = self.root / name
        data = json.loads(path.read_bytes()); data["synthetic-test-change"] = True; portable.write_json(path, data)
        provenance_path = self.root / "BUILD-PROVENANCE.json"; provenance = json.loads(provenance_path.read_text(encoding="utf-8"))
        provenance["coveredSource"]["files"][name] = portable.digest(path)
        provenance["inputsSha256"]["src/HL7Workbench.Core/ValidationCatalog/v251-model.json"] = portable.digest(path)
        portable.write_json(provenance_path, provenance); self.checksums()
        with self.assertRaisesRegex(ValueError, "embedded resource"): portable.validate_tree(self.root)

    def test_unclaimed_data_resources_test_fixtures_and_dlls_are_rejected(self):
        for name in ("patient.hl7", "table.csv", "appsettings.json", "debug.pdb", "unclaimed.dll", "covered-source/unapproved.json", "fixtures/input.txt"):
            with self.subTest(name=name):
                path = self.root / name; path.parent.mkdir(exist_ok=True); path.write_bytes(b"synthetic"); self.checksums()
                with self.assertRaisesRegex(ValueError, "Unauthorized"): portable.validate_tree(self.root)
                path.unlink()

    def test_legacy_scope_remains_explicit_and_cannot_be_inferred_from_missing_current_metadata(self):
        self.old.checksums()
        portable.validate_tree(self.old.root, scope="legacy")
        with self.assertRaisesRegex(ValueError, "Incomplete current"): portable.validate_tree(self.old.root)
        path = self.root / "BUILD-PROVENANCE.json"; data = json.loads(path.read_text(encoding="utf-8")); del data["schemaVersion"]
        portable.write_json(path, data); self.checksums()
        with self.assertRaisesRegex(ValueError, "current provenance"): portable.validate_tree(self.root)

    def test_absent_source_control_metadata_is_honestly_unavailable(self):
        path = self.root / "BUILD-PROVENANCE.json"; data = json.loads(path.read_text(encoding="utf-8"))
        data.update(sourceCommit=None, buildId="unavailable"); portable.write_json(path, data); self.checksums(); portable.validate_tree(self.root)

    def test_private_or_invalid_provenance_values_fail_closed(self):
        path = self.root / "BUILD-PROVENANCE.json"; original = path.read_bytes()
        mutations = [{"sourceCommit": "https://private.invalid/token"}, {"buildId": "invented"}, {"sdk": str(portable.ROOT)},
                     {"inputsSha256": {"../private": "a" * 64}}, {"dependencyInventorySha256": "0" * 64},
                     {"privateRepositoryUrl": "https://token@private.invalid"}, {"dirty": "not boolean"}]
        for mutation in mutations:
            with self.subTest(fields=list(mutation)):
                data = json.loads(original); data.update(mutation); portable.write_json(path, data); self.checksums()
                with self.assertRaises(ValueError): portable.validate_tree(self.root)
                path.write_bytes(original)

    def test_failed_staging_removes_only_its_new_tree_and_preserves_inputs_and_existing_outputs(self):
        output = self.base / "failed-stage"; missing = self.query / "HL7Workbench.QueryHost.exe"; missing.unlink()
        with self.assertRaisesRegex(ValueError, "Incomplete"):
            portable.stage_current(self.desktop, self.worker, self.query, output, self.cache, self.provenance)
        self.assertFalse(output.exists()); self.assertTrue((self.query / "e_sqlite3.dll").exists())
        existing = self.base / "existing"; existing.mkdir(); (existing / "sentinel").write_bytes(b"owned by caller")
        with self.assertRaises(FileExistsError): portable.stage_current(self.desktop, self.worker, self.query, existing, self.cache, self.provenance)
        self.assertEqual(b"owned by caller", (existing / "sentinel").read_bytes())

    def test_current_cli_without_query_or_verification_flag_never_creates_output_or_archive(self):
        output = portable.ROOT / "artifacts/this-path-is-not-created-by-preparation-tests"
        result = subprocess.run([sys.executable, "-B", str(portable.ROOT / "build/portable_release.py"),
                                 "--desktop", str(self.desktop), "--worker", str(self.worker), "--output", str(output),
                                 "--package-cache", str(self.cache)], capture_output=True, text=True)
        self.assertEqual(2, result.returncode); self.assertIn("--verification-only", result.stderr); self.assertFalse(output.exists())

    def test_legacy_cli_requires_accepted_binary_reuse_instead_of_current_sources(self):
        output = portable.ROOT / "artifacts/this-legacy-path-is-not-created-by-preparation-tests"
        result = subprocess.run([sys.executable, "-B", str(portable.ROOT / "build/portable_release.py"), "--scope", "legacy",
                                 "--desktop", str(self.desktop), "--worker", str(self.worker), "--output", str(output),
                                 "--package-cache", str(self.cache)], capture_output=True, text=True)
        self.assertEqual(2, result.returncode); self.assertIn("accepted-archive", result.stderr); self.assertFalse(output.exists())

    def test_arbitrary_repository_credentials_or_queries_cannot_enter_notice_provenance(self):
        package = self.cache / "synthetic/1.0"; spec = package / "synthetic.nuspec"
        for url in ("https://token@github.com/private/repo", "https://github.com/public/repo?secret=token", "file:///private/root"):
            spec.write_text('<package><metadata><license type="expression">MIT</license><repository url="' + url + '" /></metadata></package>', encoding="utf-8")
            with self.assertRaisesRegex(ValueError, "repository metadata"):
                portable.copy_notices(self.base / ("repo-" + str(len(url))), self.cache, portable.ROOT / "build/third-party", {"Synthetic/1.0": {"Desktop"}}, scope="current")

    def test_rid_locks_cover_all_eight_projects_without_package_version_drift(self):
        for project in (portable.ROOT / "src").glob("*/*.csproj"):
            with self.subTest(project=project.parent.name):
                locked = json.loads((project.parent / "packages.win-x64.lock.json").read_text(encoding="utf-8-sig"))["dependencies"]
                normal = json.loads((project.parent / "packages.lock.json").read_text(encoding="utf-8-sig"))["dependencies"]["net10.0"]
                self.assertIn("net10.0/win-x64", locked)
                pinned = lambda graph: {k: (v["resolved"], v["contentHash"]) for k, v in graph.items() if v["type"] != "Project"}
                self.assertEqual(pinned(normal), pinned(locked["net10.0"]))

    def test_onboarding_and_feedback_require_no_repository_fixtures_or_automatic_collection(self):
        guide = (portable.ROOT / "docs/user/getting-started.md").read_text(encoding="utf-8")
        for phrase in ("Generate Messages", "Undo", "Redo", "Search & Replace", "Bulk Result.hl7", "Save As", "Help", "Build"):
            self.assertIn(phrase, guide)
        self.assertNotIn("../../samples", guide); self.assertNotIn("do not edit values directly", guide)
        feedback = (self.root / "FEEDBACK.md").read_text(encoding="utf-8")
        for phrase in ("build ID", "synthetic", "PHI", "credentials", "endpoint secrets", "screenshots", "crash", "temporary storage"):
            self.assertIn(phrase, feedback)


if __name__ == "__main__":
    unittest.main()
