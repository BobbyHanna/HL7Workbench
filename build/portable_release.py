"""Local candidate assembly/verification only; standard library, no runtime network calls."""
import argparse
import hashlib
import json
import pathlib
import re
import shutil
import subprocess
import xml.etree.ElementTree as ET
import zipfile

ROOT = pathlib.Path(__file__).resolve().parents[1]
VERSION = "0.1.0-beta"
ARCHIVE = f"MessageWorkbench-v{VERSION}-win-x64.zip"
ALLOWED = {".dll", ".exe", ".json", ".txt", ".md", ".rtf", ".dat", ".bin"}
FORBIDDEN_PARTS = {"bin", "obj", "testresults", "samples", "fixtures", ".git", ".vs", ".vscode", "scripts", "searches", "extractions"}
EXECUTION = {"Microsoft.CodeAnalysis.Scripting.dll", "Microsoft.CodeAnalysis.CSharp.Scripting.dll", "HL7Workbench.ScriptHost.dll"}
RECIPIENT_DOCUMENTS = {"docs/beta-readme.md": "README.md", "docs/beta-user-guide.md": "USER-GUIDE.md",
                       "docs/release-notes-template.md": "RELEASE-NOTES.md", "docs/PRIVATE-PREVIEW.txt": "PRIVATE-PREVIEW.txt"}
QUERY_LICENSES = {**{"SQLitePCLRaw." + name + "/3.0.5": ("expression", "Apache-2.0") for name in
                    ("bundle_e_sqlite3", "config.e_sqlite3", "core", "provider.e_sqlite3")},
                  "SQLite/3.53.4": ("file", "LICENSE.txt")}

def digest(path):
    with path.open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()

def write_json(path, value):
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2, sort_keys=True) + "\n", encoding="utf-8")

def checked_files(root):
    root = root.resolve()
    for path in sorted(root.rglob("*")):
        if path.is_symlink() or getattr(path, "is_junction", lambda: False)():
            raise ValueError("Links are not portable candidate inputs")
        if path.is_file():
            path.resolve().relative_to(root)
            yield path

def reject_private_build_paths(root):
    # Known build roots are never useful to recipients. Inspect bytes without
    # printing matched contents, including UTF-16 strings embedded in PE metadata.
    locations = {str(ROOT), str(pathlib.Path.home())}
    variants = {value.replace("\\", "/") for value in locations} | {value.replace("/", "\\") for value in locations}
    needles = [value.lower().encode(encoding) for value in variants for encoding in ("utf-8", "utf-16-le")]
    for path in checked_files(root):
        data = path.read_bytes().lower()
        if any(needle in data for needle in needles):
            raise ValueError("Private build path embedded in " + path.relative_to(root).as_posix())

def copy_publish(source, destination, desktop):
    for path in checked_files(source):
        relative = path.relative_to(source)
        # Symbols and the optional runtime dump utility are explicitly excluded.
        if path.suffix.lower() == ".pdb" or path.name.lower() == "createdump.exe":
            continue
        if path.suffix.lower() not in {".dll", ".exe", ".json", ".dat", ".bin"}:
            raise ValueError("Unexpected publish file: " + relative.as_posix())
        if desktop and (path.name in EXECUTION or path.name.startswith(("HL7Workbench.ScriptHost", "HL7Workbench.QueryHost", "SQLitePCLRaw")) or path.name == "e_sqlite3.dll"):
            raise ValueError("Worker content leaked into Desktop publish")
        if desktop and path.name == "HL7Workbench.Desktop.exe":
            relative = pathlib.Path("MessageWorkbench.exe")
        output = destination / relative
        output.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(path, output)

def copy_notices(destination, cache, upstream_root, dependencies, scope="legacy"):
    upstream = json.loads((upstream_root / "upstream-licenses.json").read_text(encoding="utf-8-sig"))
    records = []
    for key, scopes in sorted(dependencies.items()):
        name, version = key.split("/", 1)
        package = cache / name.lower() / version
        metadata = ET.parse(package / (name.lower() + ".nuspec")).getroot().find("{*}metadata")
        license_node = metadata.find("{*}license")
        if license_node is None or license_node.attrib.get("type") not in {"expression", "file"}:
            raise ValueError("Package license requires review: " + key)
        license_id = license_node.text or ""
        if scope == "current" and key in QUERY_LICENSES and (license_node.attrib["type"], license_id) != QUERY_LICENSES[key]:
            raise ValueError("Unreviewed query-package license declaration: " + key)
        sqlite = scope == "current" and key == "SQLite/3.53.4" and license_id == "LICENSE.txt"
        apache = scope == "current" and key in {
            "SQLitePCLRaw.bundle_e_sqlite3/3.0.5", "SQLitePCLRaw.config.e_sqlite3/3.0.5",
            "SQLitePCLRaw.core/3.0.5", "SQLitePCLRaw.provider.e_sqlite3/3.0.5"} and license_id == "Apache-2.0"
        if license_node.attrib["type"] == "file" and name != "Avalonia.Angle.Windows.Natives" and not sqlite:
            raise ValueError("Unreviewed file license: " + key)
        if license_node.attrib["type"] == "expression" and license_id != "MIT" and not apache:
            raise ValueError("Unreviewed license expression: " + key)
        directory = destination / "ThirdPartyNotices" / name / version
        directory.mkdir(parents=True)
        notices = []
        upstream_records = []
        for path in checked_files(package):
            if re.search(r"licen[cs]e|notice|copying", path.name, re.I):
                relative = path.relative_to(package)
                target = directory / "package" / relative
                target.parent.mkdir(parents=True, exist_ok=True); shutil.copyfile(path, target)
                notices.append(target.relative_to(destination).as_posix())
        for entry in upstream:
            if key not in entry["packages"]:
                continue
            source = upstream_root / entry["file"]
            if digest(source) != entry["sha256"]:
                raise ValueError("Pinned upstream notice hash mismatch")
            target = directory / "upstream" / entry["file"]
            target.parent.mkdir(parents=True, exist_ok=True); shutil.copyfile(source, target)
            notices.append(target.relative_to(destination).as_posix())
            upstream_records.append({"path": notices[-1], **{key: entry[key] for key in
                                    ("source", "revision", "component", "sha256", "sourceSha256", "sourceSelection") if key in entry}})
        if not notices:
            raise ValueError("Missing original license text: " + key)
        if apache and len(upstream_records) < 2:
            raise ValueError("SQLitePCLRaw requires pinned Apache license and NOTICE")
        if sqlite and (package / "LICENSE.txt").read_bytes() != b"\nSQLite is Public Domain\n\nhttps://sqlite.org/copyright.html\n\n":
            raise ValueError("Unreviewed SQLite license text")
        repository = metadata.find("{*}repository")
        if scope == "current" and repository is not None:
            from urllib.parse import urlsplit
            url = repository.attrib.get("url", "")
            parsed = urlsplit(url)
            # These exact pinned Mono package declarations use the same public
            # Microsoft repository redirect. Do not admit arbitrary query URLs.
            reviewed_redirect = url == "https://go.microsoft.com/fwlink/?linkid=868515" and key in {
                "HarfBuzzSharp/8.3.1.3", "HarfBuzzSharp.NativeAssets.Win32/8.3.1.3",
                "SkiaSharp/3.119.4", "SkiaSharp.NativeAssets.Win32/3.119.4"}
            if url and not reviewed_redirect and (parsed.scheme != "https" or parsed.hostname not in {"github.com", "sqlite.org", "www.sqlite.org"} or
                        parsed.username or parsed.password or parsed.query or parsed.fragment):
                raise ValueError("Unreviewed repository metadata")
        records.append({"package": name, "version": version, "license": license_id,
                        "licenseType": license_node.attrib["type"], "copyright": metadata.findtext("{*}copyright", ""),
                        "repository": {} if repository is None else repository.attrib,
                        "scopes": sorted(scopes), "notices": notices,
                        "obligations": "Retain original copyright, license, disclaimer and bundled notices. Native/third-party terms also apply; no endorsement or legal-clearance claim."})
        if upstream_records:
            records[-1]["upstreamNotices"] = upstream_records
    write_json(destination / "dependency-licenses.json", records)
    lines = ["# Third-party dependency inventory", "", "Retain every referenced notice. This is an inventory, not legal clearance.", "",
             "| Package | Version | License declaration | Scope |", "| --- | --- | --- | --- |"]
    lines += [f"| {r['package']} | {r['version']} | {r['license']} ({r['licenseType']}) | {', '.join(r['scopes'])} |" for r in records]
    (destination / "THIRD-PARTY-NOTICES.md").write_text("\n".join(lines) + "\n\nSee ThirdPartyNotices/ for complete texts. Package notices retain their bytes; upstream text normalizes line endings/trailing whitespace only. ANGLE includes BSD-style conditions; native and .NET bundles contain additional component notices. Human review is required before distribution.\n", encoding="utf-8")
    return records

def dependency_inventory(desktop, worker, query=None):
    result = {}
    processes = [(desktop, "Desktop", "Desktop"), (worker, "ScriptHost", "ScriptHost")]
    if query is not None:
        processes.append((query, "QueryHost", "QueryHost"))
    for directory, name, scope in processes:
        data = json.loads((directory / f"HL7Workbench.{name}.deps.json").read_text(encoding="utf-8-sig"))
        for key, library in data["libraries"].items():
            if library["type"] not in {"package", "runtimepack"}:
                continue
            key = key.removeprefix("runtimepack.")
            result.setdefault(key, set()).add(scope)
    return result

def validate_legacy_tree(root, require_checksums=True):
    paths = {p.relative_to(root).as_posix(): p for p in checked_files(root)}
    required = {"MessageWorkbench.exe", "HL7Workbench.Desktop.dll", "HL7Workbench.Desktop.deps.json",
                "HL7Workbench.Desktop.runtimeconfig.json", "coreclr.dll", "hostfxr.dll", "hostpolicy.dll",
                "ScriptHost/HL7Workbench.ScriptHost.exe", "ScriptHost/HL7Workbench.ScriptHost.dll",
                "ScriptHost/HL7Workbench.ScriptHost.runtimeconfig.json", "ScriptHost/coreclr.dll",
                "ScriptHost/hostfxr.dll", "ScriptHost/hostpolicy.dll", "CANDIDATE-NOT-FOR-DISTRIBUTION.txt",
                "BUILD-PROVENANCE.json", "dependency-licenses.json", "THIRD-PARTY-NOTICES.md", *RECIPIENT_DOCUMENTS.values()}
    required.update("ScriptHost/" + name for name in EXECUTION)
    if require_checksums:
        required.add("SHA256SUMS.txt")
    if required - paths.keys():
        raise ValueError("Incomplete portable output: " + ", ".join(sorted(required - paths.keys())))
    reject_private_build_paths(root)
    for relative, path in paths.items():
        if relative.startswith("ThirdPartyNotices/"):
            continue  # Preserve original license filenames, including extensionless COPYING/LICENSE.
        if path.suffix.lower() not in ALLOWED or any(part.lower() in FORBIDDEN_PARTS for part in path.relative_to(root).parts):
            raise ValueError("Excluded file in candidate: " + relative)
        if path.suffix.lower() == ".json" and not (path.name.endswith((".deps.json", ".runtimeconfig.json")) or relative in {"BUILD-PROVENANCE.json", "dependency-licenses.json"}):
            raise ValueError("Unexpected configuration or data file")
        if path.name.lower() in {"createdump.exe", "hl7workbench.desktop.exe"} or relative in EXECUTION or relative.startswith("HL7Workbench.ScriptHost"):
            raise ValueError("Unexpected executable/worker/diagnostic placement")
    for relative in ("HL7Workbench.Desktop.runtimeconfig.json", "ScriptHost/HL7Workbench.ScriptHost.runtimeconfig.json"):
        runtime = json.loads(paths[relative].read_text(encoding="utf-8-sig"))["runtimeOptions"]
        if "framework" in runtime or "frameworks" in runtime or runtime.get("includedFrameworks") != [{"name": "Microsoft.NETCore.App", "version": "10.0.12"}]:
            raise ValueError("Not the pinned self-contained runtime")
    for relative, binding in (("MessageWorkbench.exe", b"HL7Workbench.Desktop.dll"), ("ScriptHost/HL7Workbench.ScriptHost.exe", b"HL7Workbench.ScriptHost.dll")):
        data = paths[relative].read_bytes()
        offset = int.from_bytes(data[60:64], "little")
        if data[:2] != b"MZ" or data[offset:offset+6] != b"PE\0\0\x64\x86" or binding not in data:
            raise ValueError("Invalid x64 apphost or changed assembly binding")
    inventory = json.loads(paths["dependency-licenses.json"].read_text(encoding="utf-8"))
    if not inventory:
        raise ValueError("Missing license inventory")
    for record in inventory:
        if not record["notices"] or any(name not in paths for name in record["notices"]):
            raise ValueError("Missing required third-party notice")
        for notice in record.get("upstreamNotices", []):
            if notice["path"] not in record["notices"] or digest(paths[notice["path"]]) != notice["sha256"]:
                raise ValueError("Pinned packaged notice hash mismatch")
    provenance = json.loads(paths["BUILD-PROVENANCE.json"].read_text(encoding="utf-8"))
    if provenance.get("version") != VERSION or provenance.get("candidateOnly") is not True:
        raise ValueError("Invalid candidate provenance")
    if "SHA256SUMS.txt" in paths:
        expected = {}
        for line in paths["SHA256SUMS.txt"].read_text(encoding="utf-8").splitlines():
            value, name = line.split("  ", 1)
            if name in expected or name not in paths or name == "SHA256SUMS.txt":
                raise ValueError("Invalid checksum manifest")
            expected[name] = value
        if set(expected) != paths.keys() - {"SHA256SUMS.txt"} or any(digest(paths[name]) != value for name, value in expected.items()):
            raise ValueError("Candidate integrity mismatch")
    return paths

def deterministic_zip(root, target, scope="current"):
    paths = validate_tree(root, scope=scope)
    with zipfile.ZipFile(target, "x", compression=zipfile.ZIP_DEFLATED, compresslevel=9) as archive:
        for name, path in sorted(paths.items()):
            info = zipfile.ZipInfo(name, (1980, 1, 1, 0, 0, 0)); info.compress_type = zipfile.ZIP_DEFLATED
            info._compresslevel = 9  # Python 3.11 has no public per-entry compression-level setter.
            info.create_system = 0; info.external_attr = 0o100644 << 16
            with path.open("rb") as source, archive.open(info, "w", force_zip64=True) as output:
                shutil.copyfileobj(source, output)

def verify_archive(root, archive_path, scope="current"):
    paths = validate_tree(root, scope=scope)
    with zipfile.ZipFile(archive_path) as archive:
        if len(archive.namelist()) != len(paths) or set(archive.namelist()) != paths.keys():
            raise ValueError("Archive inventory differs from verified directory")
        for name, path in paths.items():
            with archive.open(name) as stream:
                if hashlib.file_digest(stream, "sha256").hexdigest() != digest(path):
                    raise ValueError("Archive content differs from verified directory")

def accepted_binary_provenance(desktop, worker, archive_path, expected_sha256, inputs):
    """Reuse accepted executables only when their bytes and compilation inputs match.

    Packaging commits change the SDK's assembly revision stamp on recompilation.
    Keep that accepted stamp and identify the original binary commit separately.
    The explicit archive checksum is the owner's selected acceptance boundary.
    """
    if not re.fullmatch(r"[0-9a-fA-F]{64}", expected_sha256) or digest(archive_path) != expected_sha256.lower():
        raise ValueError("Accepted archive checksum mismatch")
    with zipfile.ZipFile(archive_path) as archive:
        origin = json.loads(archive.read("BUILD-PROVENANCE.json"))
        if origin.get("dirty") is not False or origin.get("version") != VERSION or not re.fullmatch(r"[0-9a-f]{40}", origin.get("sourceCommit", "")):
            raise ValueError("Accepted archive must identify a clean binary source commit")
        def compilation_input(name):
            return name.startswith("src/") or name in {"Directory.Build.props", "Directory.Packages.props", "global.json", "build/ScriptHost.targets"}
        original_inputs = {name: value for name, value in origin["inputsSha256"].items() if compilation_input(name)}
        current_inputs = {name: value for name, value in inputs.items() if compilation_input(name)}
        if not original_inputs or original_inputs != current_inputs:
            raise ValueError("Accepted binary compilation inputs changed")
        expected = {name: archive.read(name) for name in archive.namelist()
                    if pathlib.PurePosixPath(name).suffix.lower() in {".dll", ".exe", ".dat", ".bin"}
                    or name.endswith((".deps.json", ".runtimeconfig.json"))}
        actual = {}
        for source, prefix in ((desktop, ""), (worker, "ScriptHost/")):
            for path in checked_files(source):
                if path.suffix.lower() == ".pdb" or path.name.lower() == "createdump.exe":
                    continue
                name = path.relative_to(source).as_posix()
                if not prefix and name == "HL7Workbench.Desktop.exe":
                    name = "MessageWorkbench.exe"
                actual[prefix + name] = path
        if actual.keys() != expected.keys() or any(digest(actual[name]) != hashlib.sha256(data).hexdigest() for name, data in expected.items()):
            raise ValueError("Published binary/runtime inputs differ from accepted archive")
    return {"assemblyMethod": "accepted-binary-repackage", "binarySourceCommit": origin["sourceCommit"],
            "binarySourceArchive": {"file": archive_path.name, "sha256": expected_sha256.lower()},
            "acceptedBinaryFiles": len(expected)}

def assemble_legacy(desktop, worker, output, cache, provenance):
    output.mkdir(parents=True, exist_ok=False)
    tree = output / "MessageWorkbench"; tree.mkdir()
    copy_publish(desktop, tree, True); copy_publish(worker, tree / "ScriptHost", False)
    copy_notices(tree, cache, ROOT / "build/third-party", dependency_inventory(desktop, worker))
    for source, target in RECIPIENT_DOCUMENTS.items():
        shutil.copyfile(ROOT / source, tree / target)
    (tree / "CANDIDATE-NOT-FOR-DISTRIBUTION.txt").write_text("Local Stage 14 review candidate only. Human license/terms review and separate distribution authorization are required. Not signed. No updater.\n", encoding="utf-8")
    write_json(tree / "BUILD-PROVENANCE.json", provenance)
    paths = validate_legacy_tree(tree, require_checksums=False)
    (tree / "SHA256SUMS.txt").write_text("".join(f"{digest(path)}  {name}\n" for name, path in sorted(paths.items())), encoding="utf-8")
    archive = output / ARCHIVE; deterministic_zip(tree, archive, scope="legacy")
    verify_archive(tree, archive, scope="legacy")
    (output / (ARCHIVE + ".sha256")).write_text(digest(archive) + "  " + ARCHIVE + "\n", encoding="utf-8")
    return tree, archive

CURRENT_DOCUMENTS = {"docs/preview-readme.md": "README.md", "docs/user/getting-started.md": "USER-GUIDE.md",
                     "docs/private-preview-feedback.md": "FEEDBACK.md", "docs/redistribution-review-checklist.md": "REDISTRIBUTION-REVIEW.md",
                     "docs/PRIVATE-PREVIEW.txt": "PRIVATE-PREVIEW.txt"}
COVERED_FILES = {"src/HL7Workbench.Core/ValidationCatalog/v251-model.json": "covered-source/v251-model.json",
                 "src/HL7Workbench.Core/ValidationCatalog/NOTICE.md": "covered-source/NOTICE.md",
                 "build/third-party/nHapi-MPL-2.0-LICENSE.txt": "covered-source/MPL-2.0.txt"}
PROCESSES = (("", "Desktop"), ("ScriptHost/", "ScriptHost"), ("QueryHost/", "QueryHost"))
CURRENT_MARKER = "NON-DISTRIBUTABLE-VERIFICATION.txt"


def canonical_hash(value):
    return hashlib.sha256(json.dumps(value, sort_keys=True, separators=(",", ":")).encode("utf-8")).hexdigest()


def safe_relative(name):
    path = pathlib.PurePosixPath(name)
    if not name or path.is_absolute() or ".." in path.parts or "\\" in name or ":" in name or str(path) != name:
        raise ValueError("Invalid relative manifest path")
    return name


def process_assets(directory, name):
    """Inspect the selected published RID, not every platform in a NuGet cache.

    Publish flattens runtime/native assets; localized resources retain their locale.
    Refuse ambiguous/non-published graphs rather than guessing missing files.
    """
    deps = json.loads((directory / f"HL7Workbench.{name}.deps.json").read_text(encoding="utf-8-sig"))
    target = deps.get("runtimeTarget", {}).get("name", "")
    if not target.endswith("/win-x64") or target not in deps.get("targets", {}):
        raise ValueError("Expected a published win-x64 dependency graph")
    assets = set()
    for library in deps["targets"][target].values():
        if library.get("runtimeTargets"):
            raise ValueError("Unselected runtime targets in portable publish")
        for kind in ("runtime", "native", "resources"):
            for asset, metadata in library.get(kind, {}).items():
                safe_relative(asset)
                if pathlib.PurePosixPath(asset).name == "_._":
                    continue
                leaf = pathlib.PurePosixPath(asset).name
                # The accepted privacy policy deliberately omits symbols and the
                # optional runtime dump utility, even when deps lists native PDBs.
                if leaf.lower().endswith(".pdb") or leaf.lower() == "createdump.exe":
                    continue
                path = metadata.get("locale", "") + "/" + leaf if kind == "resources" else leaf
                assets.add(safe_relative(path))
    missing = sorted(asset for asset in assets if not (directory / asset).is_file())
    if missing:
        raise ValueError("Incomplete published dependency assets for " + name + ": " + ", ".join(missing))
    return assets


def validate_tree(root, require_checksums=True, scope="current"):
    if scope == "legacy":
        return validate_legacy_tree(root, require_checksums)
    if scope != "current":
        raise ValueError("Unknown release scope")
    paths = {p.relative_to(root).as_posix(): p for p in checked_files(root)}
    required = {CURRENT_MARKER, "BUILD-PROVENANCE.json", "dependency-licenses.json", "THIRD-PARTY-NOTICES.md",
                *CURRENT_DOCUMENTS.values(), *COVERED_FILES.values()}
    allowed = set(required)
    if require_checksums:
        required.add("SHA256SUMS.txt")
    allowed.add("SHA256SUMS.txt")
    for prefix, process in PROCESSES:
        stem = "HL7Workbench." + process
        executable = "MessageWorkbench.exe" if process == "Desktop" else stem + ".exe"
        required.update(prefix + leaf for leaf in (executable, stem + ".dll", stem + ".deps.json", stem + ".runtimeconfig.json",
                                                  "coreclr.dll", "hostfxr.dll", "hostpolicy.dll"))
    required.update({"QueryHost/e_sqlite3.dll", "ScriptHost/HL7Workbench.Core.dll", "HL7Workbench.Core.dll"})
    if required - paths.keys():
        raise ValueError("Incomplete current three-process output: " + ", ".join(sorted(required - paths.keys())))
    reject_private_build_paths(root)
    for prefix, process in PROCESSES:
        stem = "HL7Workbench." + process
        allowed.update(prefix + p for p in process_assets(root / prefix, process))
        executable = "MessageWorkbench.exe" if process == "Desktop" else stem + ".exe"
        allowed.update(prefix + leaf for leaf in (executable, stem + ".deps.json", stem + ".runtimeconfig.json"))
        runtime = json.loads(paths[prefix + stem + ".runtimeconfig.json"].read_text(encoding="utf-8-sig"))["runtimeOptions"]
        if "framework" in runtime or "frameworks" in runtime or runtime.get("includedFrameworks") != [{"name": "Microsoft.NETCore.App", "version": "10.0.12"}]:
            raise ValueError("Not the pinned self-contained runtime for " + process)
        data = paths[prefix + executable].read_bytes(); offset = int.from_bytes(data[60:64], "little")
        if data[:2] != b"MZ" or data[offset:offset + 6] != b"PE\0\0\x64\x86" or (stem + ".dll").encode() not in data:
            raise ValueError("Invalid x64 apphost binding for " + process)
    inventory = json.loads(paths["dependency-licenses.json"].read_text(encoding="utf-8"))
    dependencies = dependency_inventory(root, root / "ScriptHost", root / "QueryHost")
    query_packages = {"SQLite/3.53.4", *("SQLitePCLRaw." + name + "/3.0.5" for name in
                                       ("bundle_e_sqlite3", "config.e_sqlite3", "core", "provider.e_sqlite3"))}
    if any("QueryHost" not in dependencies.get(key, set()) for key in query_packages):
        raise ValueError("Incomplete QueryHost package inventory")
    actual_inventory = {}
    for record in inventory:
        key = record["package"] + "/" + record["version"]
        if key in QUERY_LICENSES and (record.get("licenseType"), record.get("license")) != QUERY_LICENSES[key]:
            raise ValueError("Unreviewed query-package license declaration")
        if key in actual_inventory:
            raise ValueError("Duplicate dependency inventory")
        actual_inventory[key] = set(record["scopes"])
        if not record["notices"] or set(record["notices"]) != set(record.get("noticeSha256", {})):
            raise ValueError("Missing required notice hashes")
        for name in record["notices"]:
            safe_relative(name)
            if not name.startswith("ThirdPartyNotices/") or name not in paths or digest(paths[name]) != record["noticeSha256"][name]:
                raise ValueError("Missing or changed required notice")
            allowed.add(name)
        for notice in record.get("upstreamNotices", []):
            if notice["path"] not in record["notices"] or digest(paths[notice["path"]]) != notice["sha256"]:
                raise ValueError("Pinned packaged notice hash mismatch")
    if actual_inventory != dependencies:
        raise ValueError("Incomplete three-process dependency inventory")
    provenance = json.loads(paths["BUILD-PROVENANCE.json"].read_text(encoding="utf-8"))
    permitted_fields = {"schemaVersion", "deploymentScope", "verificationOnly", "product", "version", "rid", "configuration",
                        "runtime", "sdk", "sourceCommit", "buildId", "dirty", "inputsSha256", "assemblyIdentity", "entryPoint",
                        "dependencyInventorySha256", "artifactChecksums", "coveredSource"}
    if (provenance.get("schemaVersion") != 2 or provenance.get("deploymentScope") != "current-three-process" or
            provenance.get("verificationOnly") is not True or provenance.get("version") != VERSION or
            provenance.get("rid") != "win-x64" or provenance.get("configuration") != "Release" or
            provenance.get("runtime") != "10.0.12" or provenance.get("dependencyInventorySha256") != digest(paths["dependency-licenses.json"]) or
            provenance.keys() - permitted_fields or provenance.get("product") != "Message Workbench" or
            provenance.get("assemblyIdentity") != "HL7Workbench.Desktop" or provenance.get("entryPoint") != "MessageWorkbench.exe" or
            not isinstance(provenance.get("dirty"), bool) or not provenance.get("inputsSha256")):
        raise ValueError("Invalid current provenance")
    commit = provenance.get("sourceCommit")
    if commit is not None and not re.fullmatch(r"[0-9a-f]{40}|[0-9a-f]{64}", commit):
        raise ValueError("Invalid source commit")
    if provenance.get("buildId") != (commit[:8] if commit else "unavailable"):
        raise ValueError("Invalid build identifier")
    for path, value in provenance.get("inputsSha256", {}).items():
        safe_relative(path)
        if not re.fullmatch(r"[0-9a-f]{64}", value):
            raise ValueError("Invalid input hash")
    if not re.fullmatch(r"\d+\.\d+\.\d+(?:[-+][A-Za-z0-9.-]+)?", provenance.get("sdk", "")):
        raise ValueError("Invalid SDK identity")
    if provenance.get("artifactChecksums") != {"algorithm": "SHA-256", "files": "SHA256SUMS.txt", "archive": None}:
        raise ValueError("Invalid verification checksum scope")
    covered = provenance.get("coveredSource", {})
    if covered.get("license") != "MPL-2.0" or covered.get("upstreamRevision") != "9027797b1fd1faebcffd743ac11324499145f29d":
        raise ValueError("Invalid covered-source provenance")
    if set(covered.get("files", {})) != set(COVERED_FILES.values()):
        raise ValueError("Incomplete covered-source inventory")
    for source, destination in COVERED_FILES.items():
        value = digest(paths[destination])
        if covered["files"][destination] != value or provenance.get("inputsSha256", {}).get(source) != value:
            raise ValueError("Covered-source hash mismatch")
    resource = paths["covered-source/v251-model.json"].read_bytes()
    if json.loads(resource)["source"] != "https://github.com/nHapiNET/nHapi/tree/" + covered["upstreamRevision"]:
        raise ValueError("Unexpected adapted resource origin")
    # EmbeddedResource is an uncompressed PE payload. Require the exact delivered
    # source bytes in both assemblies that carry Core, not just a claimed JSON hash.
    if any(resource not in paths[prefix + "HL7Workbench.Core.dll"].read_bytes() for prefix in ("", "ScriptHost/")):
        raise ValueError("Covered source differs from embedded resource")
    if paths.keys() - allowed:
        raise ValueError("Unauthorized files in current output: " + ", ".join(sorted(paths.keys() - allowed)))
    if "SHA256SUMS.txt" in paths:
        expected = {}
        for line in paths["SHA256SUMS.txt"].read_text(encoding="utf-8").splitlines():
            value, name = line.split("  ", 1); safe_relative(name)
            if name in expected or not re.fullmatch(r"[0-9a-f]{64}", value):
                raise ValueError("Invalid checksum manifest")
            expected[name] = value
        if set(expected) != paths.keys() - {"SHA256SUMS.txt"} or any(digest(paths[name]) != value for name, value in expected.items()):
            raise ValueError("Current output integrity mismatch")
    return paths


def stage_current(desktop, worker, query, output, cache, provenance):
    """Create only an owned, non-distributable verification tree; never an archive.

    Failure removes only the new tree created by this call. Incoming publishes and
    diagnostics belong to the caller and are retained until its reviewed cleanup.
    """
    if query is None:
        raise ValueError("Current deployment requires QueryHost")
    owned = output.resolve()
    if any(owned.is_relative_to(source.resolve()) for source in (desktop, worker, query)):
        raise ValueError("Output must not be inside publish inputs")
    output.mkdir(parents=True, exist_ok=False)
    try:
        tree = output / "MessageWorkbench"; tree.mkdir()
        copy_publish(desktop, tree, True); copy_publish(worker, tree / "ScriptHost", False); copy_publish(query, tree / "QueryHost", False)
        dependencies = dependency_inventory(desktop, worker, query)
        records = copy_notices(tree, cache, ROOT / "build/third-party", dependencies, scope="current")
        for record in records:
            record["noticeSha256"] = {p: digest(tree / p) for p in record["notices"]}
        write_json(tree / "dependency-licenses.json", records)
        for source, destination in CURRENT_DOCUMENTS.items():
            shutil.copyfile(ROOT / source, tree / destination)
        for source, destination in COVERED_FILES.items():
            path = tree / destination; path.parent.mkdir(exist_ok=True); shutil.copyfile(ROOT / source, path)
        (tree / CURRENT_MARKER).write_text("NON-DISTRIBUTABLE TEST OUTPUT. Stage 21B.1 verification only. No legal clearance, release, ZIP or distribution authorization.\n", encoding="utf-8")
        provenance = {**provenance, "schemaVersion": 2, "deploymentScope": "current-three-process", "verificationOnly": True,
                      "dependencyInventorySha256": digest(tree / "dependency-licenses.json"),
                      "artifactChecksums": {"algorithm": "SHA-256", "files": "SHA256SUMS.txt", "archive": None},
                      "coveredSource": {"license": "MPL-2.0", "upstreamRevision": "9027797b1fd1faebcffd743ac11324499145f29d",
                                        "files": {p: digest(tree / p) for p in COVERED_FILES.values()}}}
        write_json(tree / "BUILD-PROVENANCE.json", provenance)
        paths = validate_tree(tree, require_checksums=False)
        (tree / "SHA256SUMS.txt").write_text("".join(f"{digest(p)}  {name}\n" for name, p in sorted(paths.items())), encoding="utf-8")
        validate_tree(tree)
        return tree
    except BaseException:
        if output.resolve() == owned and not output.is_symlink():
            shutil.rmtree(output)
        raise


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--verify", type=pathlib.Path)
    parser.add_argument("--desktop", type=pathlib.Path); parser.add_argument("--worker", type=pathlib.Path)
    parser.add_argument("--output", type=pathlib.Path); parser.add_argument("--package-cache", type=pathlib.Path)
    parser.add_argument("--accepted-archive", type=pathlib.Path)
    parser.add_argument("--accepted-sha256")
    parser.add_argument("--scope", choices=("current", "legacy"), default="current")
    parser.add_argument("--query", type=pathlib.Path)
    parser.add_argument("--verification-only", action="store_true")
    parser.add_argument("--binary-version")
    args = parser.parse_args()
    if args.verify:
        print(f"Verified {len(validate_tree(args.verify.resolve(), scope=args.scope))} {args.scope} files; runtime, exclusions, notices and checksums."); return
    if any(value is None for value in (args.desktop, args.worker, args.output, args.package_cache)):
        parser.error("assembly requires desktop, worker, output and package-cache")
    if (args.accepted_archive is None) != (args.accepted_sha256 is None):
        parser.error("accepted-binary reuse requires both accepted-archive and accepted-sha256")
    output = args.output.resolve(); output.relative_to(ROOT / "artifacts")
    if output.is_relative_to(ROOT / "artifacts/private-preview"):
        parser.error("Protected private-preview paths are never output targets")
    if args.scope == "current" and (not args.verification_only or args.query is None or args.accepted_archive):
        parser.error("Current preparation requires --verification-only and --query; no archive creation/reuse")
    if args.scope == "legacy" and args.verification_only:
        parser.error("Historical archive assembly requires explicit legacy scope; it is not current verification")
    if args.scope == "legacy" and args.accepted_archive is None:
        parser.error("Legacy CLI assembly requires explicit accepted-archive and accepted-sha256; current binaries cannot use the old scope")
    source_paths = subprocess.check_output(["git", "ls-files", "--cached", "--others", "--exclude-standard", "-z"], cwd=ROOT).decode().split("\0")
    inputs = {p: digest(ROOT / p) for p in sorted(set(source_paths)) if p and (p.startswith(("src/", "build/")) or p in {"Directory.Build.props", "Directory.Packages.props", "global.json", *RECIPIENT_DOCUMENTS, *CURRENT_DOCUMENTS})}
    provenance = {"product": "Message Workbench", "version": VERSION, "candidateOnly": True, "rid": "win-x64",
                  "sourceCommit": subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=ROOT).decode().strip(),
                  "dirty": bool(subprocess.check_output(["git", "status", "--porcelain"], cwd=ROOT)), "inputsSha256": inputs,
                  "sdk": subprocess.check_output(["dotnet", "--version"], cwd=ROOT).decode().strip(), "runtime": "10.0.12",
                  "assemblyIdentity": "HL7Workbench.Desktop", "entryPoint": "MessageWorkbench.exe",
                  "artworkRights": "Owner confirmed ownership or redistribution permission in Stage 14 conversation; artwork unchanged."}
    if args.scope == "current":
        if args.binary_version != VERSION + "+" + provenance["sourceCommit"]:
            parser.error("Published version/source metadata differs from the checkout; no build identity is invented")
        provenance.pop("candidateOnly"); provenance.pop("artworkRights")
        provenance.update(configuration="Release", buildId=provenance["sourceCommit"][:8])
        tree = stage_current(args.desktop.resolve(), args.worker.resolve(), args.query.resolve(), output, args.package_cache.resolve(), provenance)
        print(f"NON-DISTRIBUTABLE verification tree only: {len(validate_tree(tree))} files; no archive created.")
        return
    if args.accepted_archive:
        provenance.update(accepted_binary_provenance(args.desktop.resolve(), args.worker.resolve(), args.accepted_archive.resolve(), args.accepted_sha256, inputs))
    tree, archive = assemble_legacy(args.desktop.resolve(), args.worker.resolve(), output, args.package_cache.resolve(), provenance)
    print(f"Legacy candidate only: {archive.name}; {len(validate_legacy_tree(tree))} files; SHA-256 {digest(archive)}")

if __name__ == "__main__":
    main()
