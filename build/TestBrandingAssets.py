"""Permanent image-processing regressions; no app build/runtime dependency on Python."""
import io
import unittest
from hashlib import sha256
from PIL import Image
import numpy as np
from CreateBrandingAssets import APPROVED_SHA256, ROOT, SOURCE, SIZES, prepare, small_variant, ico_bytes


class BrandingAssetsTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.master, cls.crop, cls.bounds = prepare()
        cls.frames = {s: small_variant(cls.master, s) if s <= 32 else cls.master.resize((s, s), Image.Resampling.LANCZOS) for s in SIZES}

    def test_source_and_master_are_unchanged(self):
        self.assertEqual(APPROVED_SHA256, sha256(SOURCE.read_bytes()).hexdigest().upper())
        expected = io.BytesIO()
        self.master.save(expected, optimize=True, compress_level=9, format="PNG")
        self.assertEqual(expected.getvalue(), (ROOT / "src/HL7Workbench.Desktop/Assets/workbench-icon.png").read_bytes())

    def test_optimized_small_frames_enlarge_the_colored_silhouette(self):
        def foreground(image):
            p = np.array(image)
            # Ignore background/border: only interior cyan/orange visual content.
            p = p[1:-1, 1:-1]
            return np.count_nonzero(((p[:, :, 1] > 90) & (p[:, :, 2] > 140)) | ((p[:, :, 0] > 180) & (p[:, :, 1] > 80)))
        for size in (16, 24, 32):
            with self.subTest(size=size):
                old = self.master.resize((size, size), Image.Resampling.LANCZOS)
                new = self.frames[size]
                self.assertNotEqual(old.tobytes(), new.tobytes())
                self.assertGreater(foreground(new), foreground(old) * 1.15)
                self.assertEqual(0, new.getpixel((0, 0))[3])
                self.assertEqual(255, new.getpixel((size // 2, size // 2))[3])

    def test_large_frames_retain_original_artwork(self):
        for size in (48, 64, 128, 256):
            self.assertEqual(self.master.resize((size, size), Image.Resampling.LANCZOS).tobytes(), self.frames[size].tobytes())

    def test_generation_is_reproducible_and_ico_contains_optimized_frames(self):
        expected = ico_bytes(self.frames)
        self.assertEqual(expected, (ROOT / "src/HL7Workbench.Desktop/Assets/workbench.ico").read_bytes())
        self.assertEqual(expected, ico_bytes(self.frames))


if __name__ == "__main__":
    unittest.main()
