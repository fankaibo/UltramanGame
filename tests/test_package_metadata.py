import unittest

from scripts.repair_mediapipe_metadata import corrected, OLD_TAG, NEW_TAG


class PackageMetadataTests(unittest.TestCase):
    original = (b"Wheel-Version: 1.0\nGenerator: setuptools (75.8.0)\nRoot-Is-Purelib: false\n"
                b"Tag: cp312-cp312-macosx_14_0_x86_64\nGenerator: delocate 0.13.0\n\n")

    def test_known_upstream_tag_is_corrected_idempotently_with_same_minimum_os(self):
        updated = corrected(self.original)
        self.assertIn(NEW_TAG, updated)
        self.assertNotIn(OLD_TAG, updated)
        self.assertEqual(updated, corrected(updated))

    def test_other_wheel_builds_and_versions_are_never_relabelled(self):
        for wrong in (self.original.replace(b"cp312", b"cp311"),
                      self.original.replace(b"14_0", b"11_0"), b"arbitrary metadata"):
            with self.assertRaises(RuntimeError): corrected(wrong)
