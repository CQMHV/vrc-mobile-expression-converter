# SPDX-License-Identifier: AGPL-3.0-only
# Copyright (c) 2026 CQMHV
import hashlib
import io
import json
import tempfile
import unittest
import zipfile
from pathlib import Path

import build_listing
import build_package


class DistributionTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.directory = tempfile.TemporaryDirectory()
        cls.archive = build_package.build(build_package.ROOT, Path(cls.directory.name))
        cls.data = cls.archive.read_bytes()
        cls.manifest = json.loads((build_package.ROOT / "package.json").read_text())

    @classmethod
    def tearDownClass(cls):
        cls.directory.cleanup()

    def test_root_manifest_and_no_repository_tooling(self):
        with zipfile.ZipFile(io.BytesIO(self.data)) as archive:
            names = archive.namelist()
            self.assertIn("package.json", names)
            self.assertIn("Runtime/MobileExpressionSettings.cs", names)
            self.assertIn("LICENSE", names)
            self.assertFalse(any(name.startswith((".git", "Website", "Assets", "dist")) for name in names))

    def test_listing_checksum_matches_downloaded_zip(self):
        result = build_listing.read_version(self.data, self.manifest["url"], self.manifest["version"])
        self.assertEqual(result["zipSHA256"], hashlib.sha256(self.data).hexdigest())

    def test_mismatched_tag_is_rejected(self):
        with self.assertRaises(ValueError):
            build_listing.read_version(self.data, self.manifest["url"], "99.99.99")

    def test_wrong_license_and_url_are_rejected(self):
        for field, value in [("license", "AGPL-3.0-or-later"), ("url", "https://example.com/package.zip")]:
            manifest = dict(self.manifest, **{field: value})
            stream = io.BytesIO()
            with zipfile.ZipFile(stream, "w") as archive:
                archive.writestr("package.json", json.dumps(manifest))
            with self.assertRaises(ValueError):
                build_listing.read_version(stream.getvalue(), self.manifest["url"], self.manifest["version"])


if __name__ == "__main__":
    unittest.main()
