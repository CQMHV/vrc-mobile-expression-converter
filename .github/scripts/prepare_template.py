# SPDX-License-Identifier: AGPL-3.0-only
# Copyright (c) 2026 CQMHV
"""Apply the same small official-template adjustments used by VRCLearn."""
import argparse
import shutil
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]


def replace_exact(path, old, new, count):
    text = path.read_text(encoding="utf-8")
    if text.count(old) != count:
        raise ValueError(f"Unexpected official template contents in {path.name}")
    path.write_text(text.replace(old, new), encoding="utf-8")


def prepare(template):
    shutil.copyfile(ROOT / ".github/source.json", template / "source.json")
    replace_exact(template / "Website/index.html", ">Add to VCC<", ">Add to VCC/ALCOMD<", 2)
    replace_exact(
        template / "Website/index.html",
        'grid-template-columns="1fr 100px 220px"',
        'grid-template-columns="1fr 100px 280px"', 1
    )
    replace_exact(
        template / "Website/app.js",
        "{{ if package.Description; package.Description; end; }}",
        "{{ if package.Description; package.Description | string.escape; end; }}", 1
    )


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("template", type=Path)
    prepare(parser.parse_args().template)
