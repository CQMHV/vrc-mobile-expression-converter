# SPDX-License-Identifier: AGPL-3.0-only
# Copyright (c) 2026 CQMHV
import hashlib
import io
import json
import tempfile
import unittest
from unittest.mock import patch
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


class DependencyListingTests(unittest.TestCase):
    def setUp(self):
        self.name = "nadena.dev.ndmf"
        self.manifest = {
            "name": self.name,
            "version": "1.14.8",
            "url": "https://packages.vpm.nadena.dev/ndmf.zip",
            "zipSHA256": "a" * 64,
            "repo": "https://vpm.nadena.dev/vpm.json",
            "dependencies": {"com.unity.collections": "2.1.4"}
        }
        self.listing = {"packages": {self.name: {"versions": {"1.14.8": self.manifest}}}}

    def test_upstream_metadata_and_older_versions_are_preserved(self):
        versions = self.listing["packages"][self.name]["versions"]
        versions["1.14.7"] = dict(self.manifest, version="1.14.7")
        result = build_listing.read_dependency_package(self.listing, self.name)
        self.assertEqual(result, {"versions": versions})
        self.assertEqual(result["versions"]["1.14.8"], self.manifest)

    def test_missing_or_invalid_dependency_is_rejected(self):
        with self.assertRaises(KeyError):
            build_listing.read_dependency_package({"packages": {}}, self.name)
        for field, value in [("name", "other.package"), ("version", "1.0.0"),
                             ("url", "http://example.com/ndmf.zip"), ("zipSHA256", "bad")]:
            with self.subTest(field=field):
                invalid = {"packages": {self.name: {"versions": {
                    "1.14.8": dict(self.manifest, **{field: value})}}}}
                with self.assertRaises(ValueError):
                    build_listing.read_dependency_package(invalid, self.name)

    def test_fetch_uses_configured_source_and_propagates_failures(self):
        source = {"dependencyRepositories": [{"url": "https://vpm.nadena.dev/vpm.json",
                                               "packages": [self.name]}]}
        with patch.object(build_listing, "SOURCE", source):
            with patch("urllib.request.urlopen", return_value=io.BytesIO(json.dumps(self.listing).encode())) as fetch:
                self.assertEqual(build_listing.fetch_dependency_packages(), self.listing["packages"])
                self.assertEqual(fetch.call_args.args[0].full_url, source["dependencyRepositories"][0]["url"])
            with patch("urllib.request.urlopen", side_effect=OSError("Upstream unavailable")):
                with self.assertRaises(OSError):
                    build_listing.fetch_dependency_packages()


if __name__ == "__main__":
    unittest.main()
