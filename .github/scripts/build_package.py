# SPDX-License-Identifier: AGPL-3.0-only
# Copyright (c) 2026 CQMHV
"""Validate and build the VPM package without bundling repository tooling."""
import argparse
import hashlib
import json
import re
import zipfile
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
PACKAGE = "com.cqmhv.mobile-expression-converter"
FOLDERS = {"Runtime", "Editor", "docs"}
FILES = {"package.json", "README.md", "CHANGELOG.md", "LICENSE"}


def package_files(root):
    for path in sorted(root.rglob("*")):
        if not path.is_file():
            continue
        relative = path.relative_to(root)
        base = relative.name.removesuffix(".meta")
        if relative.parts[0] in FOLDERS or (
            len(relative.parts) == 1 and base in FILES | FOLDERS
        ):
            if path.is_symlink():
                raise ValueError(f"Symlink not allowed: {relative}")
            yield path


def validate(root):
    manifest = json.loads((root / "package.json").read_text(encoding="utf-8"))
    assert manifest["name"] == PACKAGE
    assert re.fullmatch(r"(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)", manifest["version"])
    assert manifest["license"] == "AGPL-3.0-only"
    assert manifest["author"]["name"] and manifest["author"]["email"]
    assert manifest["vpmDependencies"]["nadena.dev.ndmf"]
    assert manifest["vpmDependencies"]["com.vrchat.avatars"]
    assert "zipSHA256" not in manifest
    expected = f"https://github.com/CQMHV/vrc-mobile-expression-converter/releases/download/v{manifest['version']}/{PACKAGE}-{manifest['version']}.zip"
    assert manifest["url"] == expected
    license_text = (root / "LICENSE").read_text(encoding="utf-8")
    assert "GNU AFFERO GENERAL PUBLIC LICENSE" in license_text and "END OF TERMS AND CONDITIONS" in license_text
    guids = set()
    for path in package_files(root):
        if path.suffix != ".meta":
            assert Path(str(path) + ".meta").is_file(), f"Missing metadata: {path}"
        else:
            match = re.search(r"^guid: ([a-f0-9]{32})$", path.read_text(), re.M)
            assert match and match[1] not in guids, f"Invalid or duplicate GUID: {path}"
            guids.add(match[1])
    return manifest


def build(root, output):
    manifest = validate(root)
    output.mkdir(parents=True, exist_ok=True)
    archive = output / f"{PACKAGE}-{manifest['version']}.zip"
    with zipfile.ZipFile(archive, "w", compression=zipfile.ZIP_DEFLATED) as target:
        for path in package_files(root):
            info = zipfile.ZipInfo(path.relative_to(root).as_posix(), (2026, 1, 1, 0, 0, 0))
            info.external_attr = 0o100644 << 16
            info.compress_type = zipfile.ZIP_DEFLATED
            target.writestr(info, path.read_bytes())
    (output / "package.json").write_text(json.dumps(manifest, indent=4) + "\n", encoding="utf-8")
    checksum = hashlib.sha256(archive.read_bytes()).hexdigest()
    (output / "SHA256SUMS").write_text(f"{checksum}  {archive.name}\n", encoding="utf-8")
    print(f"Built {archive.name}: {checksum}")
    return archive


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--output", type=Path, default=ROOT / "dist")
    args = parser.parse_args()
    build(ROOT, args.output)
