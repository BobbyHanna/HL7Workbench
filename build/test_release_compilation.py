"""Real compiler regressions: source-root privacy, warm publish reuse and reproducibility.

Uses the installed SDK and actual production sources in owned temporary checkouts, without
clinical fixtures, Git mutations, network dependencies or changes to repository bin/obj.
Run alongside test_portable_release.py after installing the repository SDK/restoring dependencies.
"""
import os
import pathlib
import shutil
import struct
import subprocess
import tempfile
import unittest

import portable_release as portable


def codeview_paths(data):
    """Read PE debug records, rather than assuming absence of a separate .pdb is safe."""
    pe = struct.unpack_from("<I", data, 60)[0]
    sections = struct.unpack_from("<H", data, pe + 6)[0]
    optional = pe + 24
    optional_size = struct.unpack_from("<H", data, pe + 20)[0]
    directories = optional + (112 if struct.unpack_from("<H", data, optional)[0] == 0x20b else 96)
    debug_rva, debug_size = struct.unpack_from("<II", data, directories + 6 * 8)

    def file_offset(rva):
        for index in range(sections):
            section = optional + optional_size + index * 40
            virtual_size, start, raw_size, raw = struct.unpack_from("<IIII", data, section + 8)
            if start <= rva < start + max(virtual_size, raw_size):
                return raw + rva - start
        return rva

    start = file_offset(debug_rva)
    result = []
    for entry in range(start, start + debug_size, 28):
        kind, size, _, pointer = struct.unpack_from("<IIII", data, entry + 12)
        if kind == 2 and data[pointer:pointer + 4] == b"RSDS":
            result.append(data[pointer + 24:pointer + size].split(b"\0")[0].decode("utf-8"))
    return result


class ReleaseCompilationTests(unittest.TestCase):
    def run_sdk(self, root, *arguments):
        environment = dict(os.environ, DOTNET_CLI_TELEMETRY_OPTOUT="1", DOTNET_NOLOGO="1")
        completed = subprocess.run(["dotnet", *arguments], cwd=root, env=environment,
                                   capture_output=True, text=True, timeout=120)
        self.assertEqual(0, completed.returncode, completed.stdout + completed.stderr)
        return completed.stdout

    def copy_core(self, root):
        source = portable.ROOT / "src/HL7Workbench.Core"
        target = root / "src/HL7Workbench.Core"
        shutil.copytree(source, target, ignore=shutil.ignore_patterns("bin", "obj"))
        for name in ("Directory.Build.props", "Directory.Packages.props", "global.json"):
            shutil.copyfile(portable.ROOT / name, root / name)
        return target.relative_to(root) / "HL7Workbench.Core.csproj"

    def assert_private_paths_absent(self, assembly, root, require_codeview=True):
        data = assembly.read_bytes()
        for location in (root, portable.ROOT, pathlib.Path.home()):
            for spelling in (str(location), str(location).replace("\\", "/")):
                for encoding in ("utf-8", "utf-16-le"):
                    self.assertNotIn(spelling.lower().encode(encoding), data.lower())
        paths = codeview_paths(data)
        if require_codeview:
            self.assertTrue(paths, "Normal Release must exercise a real CodeView/PDB path")
        self.assertTrue(all(path.startswith("/_/") for path in paths), paths)

    def test_real_core_release_is_private_and_identical_across_roots_and_warm_portable_build(self):
        with tempfile.TemporaryDirectory(prefix="synthetic-release-compiler-") as temporary:
            hashes = []
            for checkout in ("developer-one/workbench", "different user/workbench"):
                root = pathlib.Path(temporary) / checkout
                root.mkdir(parents=True)
                project = self.copy_core(root)
                self.run_sdk(root, "restore", str(project), "--locked-mode")
                arguments = ("build", str(project), "-c", "Release", "--no-restore", "-warnaserror",
                             "-p:IncludeSourceRevisionInInformationalVersion=false")
                self.run_sdk(root, *arguments)
                assembly = root / "src/HL7Workbench.Core/bin/Release/net10.0/HL7Workbench.Core.dll"
                self.assert_private_paths_absent(assembly, root)
                hashes.append(portable.digest(assembly))
                # This is the actual failing sequence: a normal Release compile,
                # then a CI/no-symbol build sharing the same project-reference cache.
                self.run_sdk(root, *arguments, "-p:ContinuousIntegrationBuild=true",
                             "-p:DebugType=None", "-p:DebugSymbols=false")
                self.assert_private_paths_absent(assembly, root, require_codeview=False)
                portable.reject_private_build_paths(assembly.parent)
            self.assertEqual(hashes[0], hashes[1], "Checkout location changed real Core assembly bytes")

    def test_desktop_pe_has_no_physical_pdb_reference_after_fresh_xaml_compile_and_warm_reuse(self):
        with tempfile.TemporaryDirectory(prefix="synthetic-desktop-compiler-") as temporary:
            root = pathlib.Path(temporary)
            for project in ("Core", "Transport", "Scripting", "ScriptHost", "Querying", "QueryHost", "Workspace", "Desktop"):
                shutil.copytree(portable.ROOT / f"src/HL7Workbench.{project}",
                                root / f"src/HL7Workbench.{project}",
                                ignore=shutil.ignore_patterns("bin", "obj"))
            for name in ("Directory.Build.props", "Directory.Build.targets", "Directory.Packages.props", "global.json"):
                if (portable.ROOT / name).exists():
                    shutil.copyfile(portable.ROOT / name, root / name)
            (root / "build").mkdir(); (root / "docs").mkdir()
            shutil.copyfile(portable.ROOT / "build/ScriptHost.targets", root / "build/ScriptHost.targets")
            shutil.copyfile(portable.ROOT / "build/QueryHost.targets", root / "build/QueryHost.targets")
            shutil.copyfile(portable.ROOT / "docs/script-api-reference.json", root / "docs/script-api-reference.json")
            # Compare Csc's PE with Avalonia's rewritten PE. Merely checking source
            # document mappings misses the physical PDB reference the rewriter adds.
            trace = root / "trace.targets"
            trace.write_text('''<Project>
              <Target Name="CaptureDesktopCompilerPE" BeforeTargets="CompileAvaloniaXaml"
                      Condition="'$(MSBuildProjectName)' == 'HL7Workbench.Desktop'">
                <Copy SourceFiles="@(IntermediateAssembly)" DestinationFiles="$(MSBuildProjectDirectory)/before-xaml.dll" />
              </Target>
            </Project>''', encoding="utf-8")
            project = "src/HL7Workbench.Desktop/HL7Workbench.Desktop.csproj"
            self.run_sdk(root, "restore", project, "--locked-mode")
            arguments = ("build", project, "-c", "Release", "--no-restore", "-warnaserror",
                         f"-p:DirectoryBuildTargetsPath={trace}", "-v:diag")
            fresh = self.run_sdk(root, *arguments)
            desktop = root / "src/HL7Workbench.Desktop"
            self.assertIn('Done executing task "Csc"', fresh)
            self.assertIn('Done executing task "CompileAvaloniaXamlTask"', fresh)
            for assembly in (desktop / "before-xaml.dll", desktop / "bin/Release/net10.0/HL7Workbench.Desktop.dll"):
                self.assertEqual([], codeview_paths(assembly.read_bytes()))
                self.assert_private_paths_absent(assembly, root, require_codeview=False)
            output = desktop / "bin/Release/net10.0/HL7Workbench.Desktop.dll"
            expected = portable.digest(output)
            warm = self.run_sdk(root, *arguments, "-p:BuildProjectReferences=false",
                                "-p:ContinuousIntegrationBuild=true", "-p:DebugType=None", "-p:DebugSymbols=false")
            self.assertIn('Skipping target "CoreCompile"', warm)
            self.assertEqual(expected, portable.digest(output))
            self.assertEqual([], codeview_paths(output.read_bytes()))
            self.assert_private_paths_absent(output, root, require_codeview=False)


if __name__ == "__main__":
    unittest.main()
