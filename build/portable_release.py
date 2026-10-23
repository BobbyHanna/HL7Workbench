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
        if desktop and (path.name in EXECUTION or path.name.startswith("HL7Workbench.ScriptHost")):
            raise ValueError("Worker content leaked into Desktop publish")
        if desktop and path.name == "HL7Workbench.Desktop.exe":
            relative = pathlib.Path("MessageWorkbench.exe")
        output = destination / relative
        output.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(path, output)

def copy_notices(destination, cache, upstream_root, dependencies):
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
        if license_node.attrib["type"] == "file" and name != "Avalonia.Angle.Windows.Natives":
            raise ValueError("Unreviewed file license: " + key)
        if license_node.attrib["type"] == "expression" and license_id != "MIT":
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
        repository = metadata.find("{*}repository")
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

def dependency_inventory(desktop, worker):
    result = {}
    for directory, name, scope in ((desktop, "Desktop", "Desktop"), (worker, "ScriptHost", "ScriptHost")):
        data = json.loads((directory / f"HL7Workbench.{name}.deps.json").read_text(encoding="utf-8-sig"))
        for key, library in data["libraries"].items():
            if library["type"] not in {"package", "runtimepack"}:
                continue
            key = key.removeprefix("runtimepack.")
            result.setdefault(key, set()).add(scope)
    return result

def validate_tree(root, require_checksums=True):
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

def deterministic_zip(root, target):
    paths = validate_tree(root)
    with zipfile.ZipFile(target, "x", compression=zipfile.ZIP_DEFLATED, compresslevel=9) as archive:
        for name, path in sorted(paths.items()):
            info = zipfile.ZipInfo(name, (1980, 1, 1, 0, 0, 0)); info.compress_type = zipfile.ZIP_DEFLATED
            info._compresslevel = 9  # Python 3.11 has no public per-entry compression-level setter.
            info.create_system = 0; info.external_attr = 0o100644 << 16
            with path.open("rb") as source, archive.open(info, "w", force_zip64=True) as output:
                shutil.copyfileobj(source, output)

def verify_archive(root, archive_path):
    paths = validate_tree(root)
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

def assemble(desktop, worker, output, cache, provenance):
    output.mkdir(parents=True, exist_ok=False)
    tree = output / "MessageWorkbench"; tree.mkdir()
    copy_publish(desktop, tree, True); copy_publish(worker, tree / "ScriptHost", False)
    copy_notices(tree, cache, ROOT / "build/third-party", dependency_inventory(desktop, worker))
    for source, target in RECIPIENT_DOCUMENTS.items():
        shutil.copyfile(ROOT / source, tree / target)
    (tree / "CANDIDATE-NOT-FOR-DISTRIBUTION.txt").write_text("Local Stage 14 review candidate only. Human license/terms review and separate distribution authorization are required. Not signed. No updater.\n", encoding="utf-8")
    write_json(tree / "BUILD-PROVENANCE.json", provenance)
    paths = validate_tree(tree, require_checksums=False)
    (tree / "SHA256SUMS.txt").write_text("".join(f"{digest(path)}  {name}\n" for name, path in sorted(paths.items())), encoding="utf-8")
    archive = output / ARCHIVE; deterministic_zip(tree, archive)
    verify_archive(tree, archive)
    (output / (ARCHIVE + ".sha256")).write_text(digest(archive) + "  " + ARCHIVE + "\n", encoding="utf-8")
    return tree, archive

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--verify", type=pathlib.Path)
    parser.add_argument("--desktop", type=pathlib.Path); parser.add_argument("--worker", type=pathlib.Path)
    parser.add_argument("--output", type=pathlib.Path); parser.add_argument("--package-cache", type=pathlib.Path)
    parser.add_argument("--accepted-archive", type=pathlib.Path)
    parser.add_argument("--accepted-sha256")
    args = parser.parse_args()
    if args.verify:
        print(f"Verified {len(validate_tree(args.verify.resolve()))} candidate files; self-contained runtime, exclusions, notices and checksums."); return
    if any(value is None for value in (args.desktop, args.worker, args.output, args.package_cache)):
        parser.error("assembly requires desktop, worker, output and package-cache")
    if (args.accepted_archive is None) != (args.accepted_sha256 is None):
        parser.error("accepted-binary reuse requires both accepted-archive and accepted-sha256")
    output = args.output.resolve(); output.relative_to(ROOT / "artifacts")
    source_paths = subprocess.check_output(["git", "ls-files", "--cached", "--others", "--exclude-standard", "-z"], cwd=ROOT).decode().split("\0")
    inputs = {p: digest(ROOT / p) for p in sorted(set(source_paths)) if p and (p.startswith(("src/", "build/")) or p in {"Directory.Build.props", "Directory.Packages.props", "global.json", *RECIPIENT_DOCUMENTS})}
    provenance = {"product": "Message Workbench", "version": VERSION, "candidateOnly": True, "rid": "win-x64",
                  "sourceCommit": subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=ROOT).decode().strip(),
                  "dirty": bool(subprocess.check_output(["git", "status", "--porcelain"], cwd=ROOT)), "inputsSha256": inputs,
                  "sdk": subprocess.check_output(["dotnet", "--version"], cwd=ROOT).decode().strip(), "runtime": "10.0.12",
                  "assemblyIdentity": "HL7Workbench.Desktop", "entryPoint": "MessageWorkbench.exe",
                  "artworkRights": "Owner confirmed ownership or redistribution permission in Stage 14 conversation; artwork unchanged."}
    if args.accepted_archive:
        provenance.update(accepted_binary_provenance(args.desktop.resolve(), args.worker.resolve(), args.accepted_archive.resolve(), args.accepted_sha256, inputs))
    tree, archive = assemble(args.desktop.resolve(), args.worker.resolve(), output, args.package_cache.resolve(), provenance)
    print(f"Local candidate only: {archive.name}; {len(validate_tree(tree))} files; SHA-256 {digest(archive)}")

if __name__ == "__main__":
    main()
