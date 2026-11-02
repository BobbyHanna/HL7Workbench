"""Canonical containment and real Windows 8.3 aliases; synthetic fixtures only."""
import os
import pathlib
import tempfile
import unittest
from unittest.mock import patch

import portable_release as portable
import test_portable_release as historical
import test_portable_preparation as current


def windows_short_path(path):
    """Ask the filesystem for an existing alias; never fabricate a ~1 spelling."""
    import ctypes
    function = ctypes.WinDLL("kernel32", use_last_error=True).GetShortPathNameW
    function.argtypes = [ctypes.c_wchar_p, ctypes.c_wchar_p, ctypes.c_uint]
    function.restype = ctypes.c_uint
    size = function(str(path), None, 0)
    if not size:
        raise ctypes.WinError(ctypes.get_last_error())
    buffer = ctypes.create_unicode_buffer(size)
    if not function(str(path), buffer, size):
        raise ctypes.WinError(ctypes.get_last_error())
    return pathlib.Path(buffer.value)


class PortablePathTests(unittest.TestCase):
    def setUp(self):
        temporary = tempfile.TemporaryDirectory(prefix="synthetic-portable-paths-")
        self.addCleanup(temporary.cleanup)
        self.base = pathlib.Path(temporary.name).resolve()
        self.root = self.base / "owned"; self.root.mkdir()
        self.file = self.root / "nested/coreclr.dll"
        self.file.parent.mkdir(); self.file.write_bytes(b"synthetic runtime")

    def test_canonical_equivalent_roots_and_files_keep_relative_names(self):
        root = self.root / "nested/.."
        self.assertEqual(pathlib.Path("nested/coreclr.dll"), portable.relative_to_root(self.file, root))
        self.assertEqual(pathlib.Path("nested/coreclr.dll"), portable.relative_to_root(root / "nested/coreclr.dll", self.root))

    def test_genuinely_external_prefix_sibling_and_parent_traversal_are_rejected(self):
        outside = self.base / "owned-external"; outside.mkdir()
        external = outside / "coreclr.dll"; external.write_bytes(b"synthetic external")
        for path in (external, self.root / "../owned-external/coreclr.dll"):
            with self.subTest(path=path):
                with self.assertRaises(ValueError): portable.relative_to_root(path, self.root)

    def test_missing_files_do_not_become_authoritative_manifest_entries(self):
        with self.assertRaises(FileNotFoundError): portable.relative_to_root(self.root / "missing.dll", self.root)

    def test_traversal_cannot_yield_a_file_outside_the_root(self):
        outside = self.base / "external.dll"; outside.write_bytes(b"synthetic external")
        with patch.object(pathlib.Path, "rglob", return_value=iter([outside])):
            with self.assertRaises(ValueError): list(portable.checked_files(self.root))

    def test_link_and_junction_guards_run_before_canonical_containment(self):
        for attribute in ("is_symlink", "is_junction"):
            with self.subTest(attribute=attribute), patch.object(pathlib.Path, attribute, return_value=True, create=True):
                with self.assertRaisesRegex(ValueError, "Links"):
                    list(portable.checked_files(self.root))

    def test_manifest_paths_still_reject_lexical_traversal(self):
        for name in ("../coreclr.dll", "/coreclr.dll", "C:/coreclr.dll", "nested\\coreclr.dll", "nested/../coreclr.dll"):
            with self.subTest(name=name):
                with self.assertRaisesRegex(ValueError, "Invalid relative"): portable.safe_relative(name)

    @unittest.skipUnless(os.name == "nt", "Windows filesystem alias regression")
    def test_real_short_and_long_aliases_work_in_both_directions(self):
        directory = self.base / "synthetic long alias directory"; directory.mkdir()
        file = directory / "synthetic long runtime name.dll"; file.write_bytes(b"synthetic alias bytes")
        long_root = directory.resolve(); short_root = windows_short_path(long_root)
        long_file = file.resolve(); short_file = windows_short_path(long_file)
        if short_root == long_root:
            self.skipTest("Filesystem did not assign an 8.3 directory alias")
        # Reproduce the original lexical error, then exercise both representations.
        with self.assertRaises(ValueError): long_file.relative_to(short_root)
        for path, root in ((long_file, short_root), (short_file, long_root), (short_file, short_root)):
            with self.subTest(file=path, root=root):
                self.assertEqual(pathlib.Path(file.name), portable.relative_to_root(path, root))
                self.assertEqual(b"synthetic alias bytes", path.read_bytes())
        self.assertEqual([long_file], list(portable.checked_files(short_root)))

    @unittest.skipUnless(os.name == "nt" and hasattr(pathlib.Path, "is_junction"),
                         "Windows Path.is_junction API required for native link-guard assertion")
    def test_real_junction_cannot_escape_the_tree(self):
        import _winapi
        outside = self.base / "external"; outside.mkdir()
        (outside / "external.dll").write_bytes(b"synthetic external")
        junction = self.root / "junction"
        _winapi.CreateJunction(str(outside), str(junction))
        try:
            with self.assertRaisesRegex(ValueError, "Links"): list(portable.checked_files(self.root))
        finally:
            # Remove the junction itself, never recurse through its target.
            os.rmdir(junction)
        self.assertTrue((outside / "external.dll").exists())


class WindowsShortPathFixture:
    def setUp(self):
        temporary = tempfile.TemporaryDirectory(prefix="synthetic long portable alias parent ")
        self.addCleanup(temporary.cleanup)
        long_root = pathlib.Path(temporary.name).resolve()
        short_root = windows_short_path(long_root)
        if short_root == long_root:
            self.skipTest("Filesystem did not assign an 8.3 temporary-root alias")
        # Preserve the actual CI condition: TemporaryDirectory.name and fixture
        # roots use short paths, while checked_files enumerates resolved long paths.
        with patch.object(tempfile, "tempdir", str(short_root)):
            super().setUp()


@unittest.skipUnless(os.name == "nt", "Windows short-path fixture regression")
class WindowsShortPathReleaseTests(WindowsShortPathFixture, historical.PortableReleaseTests):
    """Run every retained legacy assertion, including its negative cases, via 8.3."""


@unittest.skipUnless(os.name == "nt", "Windows short-path fixture regression")
class WindowsShortPathPreparationTests(WindowsShortPathFixture, current.PortablePreparationTests):
    """Run every retained current assertion, including its negative cases, via 8.3."""


if __name__ == "__main__":
    unittest.main()
