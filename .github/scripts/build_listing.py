# SPDX-License-Identifier: AGPL-3.0-only
# Copyright (c) 2026 CQMHV
"""Build the listing from published release ZIPs, including previous versions."""
import argparse
import hashlib
import io
import json
import re
import shutil
import subprocess
import urllib.request
import zipfile
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
REPOSITORY = "CQMHV/vrc-mobile-expression-converter"
PACKAGE = "com.cqmhv.mobile-expression-converter"
SITE = "https://cqmhv.github.io/vrc-mobile-expression-converter/"


def read_version(data, url, version):
    with zipfile.ZipFile(io.BytesIO(data)) as archive:
        manifest = json.loads(archive.read("package.json"))
    if manifest["name"] != PACKAGE or manifest["version"] != version:
        raise ValueError("Release tag and package manifest do not match")
    if manifest["license"] != "AGPL-3.0-only" or manifest["url"] != url:
        raise ValueError("Unexpected package license or download URL")
    manifest["zipSHA256"] = hashlib.sha256(data).hexdigest()
    return manifest


def build(output):
    result = subprocess.run(
        ["gh", "api", f"repos/{REPOSITORY}/releases?per_page=100", "--paginate", "--slurp"],
        check=True, capture_output=True, text=True, encoding="utf-8"
    )
    versions = {}
    for batch in json.loads(result.stdout):
        for release in batch:
            tag = release["tag_name"]
            if release["draft"] or release["prerelease"] or not re.fullmatch(r"v\d+\.\d+\.\d+", tag):
                continue
            version = tag[1:]
            filename = f"{PACKAGE}-{version}.zip"
            assets = [a for a in release["assets"] if a["name"] == filename]
            if len(assets) != 1:
                raise ValueError(f"{tag} must have exactly one VPM ZIP")
            url = assets[0]["browser_download_url"]
            expected = f"https://github.com/{REPOSITORY}/releases/download/{tag}/{filename}"
            if url != expected:
                raise ValueError("Unexpected release asset URL")
            request = urllib.request.Request(url, headers={"User-Agent": "MEC-VPM-Listing"})
            with urllib.request.urlopen(request, timeout=60) as response:
                versions[version] = read_version(response.read(), url, version)
    versions = dict(sorted(versions.items(), key=lambda item: tuple(map(int, item[0].split("."))), reverse=True))
    output.mkdir(parents=True, exist_ok=True)
    shutil.copyfile(ROOT / "Website/index.html", output / "index.html")
    (output / ".nojekyll").write_text("")
    listing = {
        "name": "Mobile Expression Converter",
        "id": "com.cqmhv.mobile-expression-converter.repository",
        "url": SITE + "index.json",
        "author": "CQMHV",
        "packages": {PACKAGE: {"versions": versions}} if versions else {}
    }
    (output / "index.json").write_text(json.dumps(listing, indent=4) + "\n", encoding="utf-8")
    print(f"Listed {len(versions)} published package version(s)")


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--output", type=Path, default=ROOT / "site-dist")
    args = parser.parse_args()
    build(args.output)
