# SPDX-License-Identifier: AGPL-3.0-only
# Copyright (c) 2026 CQMHV
"""Verify an embedded UnityPackage against its published VPM ZIP."""
import argparse
import json
import re
import tarfile
import zipfile
from pathlib import Path


def validate(archive_path, unitypackage_path):
    with zipfile.ZipFile(archive_path) as archive, tarfile.open(unitypackage_path, "r:gz") as package:
        manifest = json.loads(archive.read("package.json"))
        prefix = f"Packages/{manifest['name']}/"
        entries = {}
        for member in package.getmembers():
            if member.isfile():
                if member.name in entries:
                    raise ValueError(f"Duplicate UnityPackage entry: {member.name}")
                entries[member.name] = package.extractfile(member).read()
        expected = set()
        for name in archive.namelist():
            if not name.endswith(".meta"):
                continue
            metadata = archive.read(name)
            match = re.search(rb"^guid: ([a-f0-9]{32})\r?$", metadata, re.M)
            if not match:
                raise ValueError(f"Invalid GUID: {name}")
            guid = match[1].decode("ascii")
            path = entries[f"{guid}/pathname"].decode("utf-8").rstrip("\r\n")
            relative = name.removesuffix(".meta")
            if path != prefix + relative or entries[f"{guid}/asset.meta"] != metadata:
                raise ValueError(f"Path or metadata mismatch: {name}")
            if relative in archive.namelist() and entries.get(f"{guid}/asset") != archive.read(relative):
                raise ValueError(f"Asset contents mismatch: {relative}")
            expected.add(guid)
        actual = {name.split("/")[0] for name in entries if name.endswith("/pathname")}
        if actual != expected:
            raise ValueError("UnityPackage contains missing or unexpected assets")
        print(f"Verified {len(expected)} asset GUIDs and all embedded package files")


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("archive", type=Path)
    parser.add_argument("unitypackage", type=Path)
    args = parser.parse_args()
    validate(args.archive, args.unitypackage)
