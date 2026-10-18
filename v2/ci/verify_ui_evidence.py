"""Validate real WebView2 screenshot dimensions and before/after evidence."""

from __future__ import annotations

import argparse
from pathlib import Path

from PIL import Image, ImageChops, ImageStat


LIGHT_CAPTURES = {
    "webview2-1016x689.png": (1016, 689),
    "webview2-1920x1080.png": (1920, 1080),
}
DARK_CAPTURE = {"webview2-dark-1016x689.png": (1016, 689)}


def open_checked(path: Path, expected_size: tuple[int, int]) -> Image.Image:
    if not path.is_file():
        raise FileNotFoundError(f"missing screenshot: {path}")
    with Image.open(path) as source:
        source.load()
        if source.format != "PNG":
            raise ValueError(f"expected PNG, got {source.format}: {path}")
        if source.size != expected_size:
            raise ValueError(
                f"wrong dimensions for {path.name}: {source.size}, expected {expected_size}"
            )
        image = source.convert("RGB")
    if sum(ImageStat.Stat(image).var) < 50:
        raise ValueError(f"screenshot appears blank or nearly uniform: {path}")
    return image


def changed_pixel_ratio(before: Image.Image, after: Image.Image) -> float:
    difference = ImageChops.difference(before, after)
    changed = 0
    for pixel in difference.getdata():
        if max(pixel) > 12:
            changed += 1
    return changed / (before.width * before.height)


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--before", type=Path, required=True)
    parser.add_argument("--after", type=Path, required=True)
    args = parser.parse_args()

    for filename, size in LIGHT_CAPTURES.items():
        before = open_checked(args.before / filename, size)
        after = open_checked(args.after / filename, size)
        ratio = changed_pixel_ratio(before, after)
        if ratio < 0.01:
            raise ValueError(
                f"before/after evidence is effectively identical for {filename}: {ratio:.2%}"
            )
        print(f"EVIDENCE PASS {filename}: {size[0]}x{size[1]}, changed {ratio:.2%}")

    for filename, size in DARK_CAPTURE.items():
        open_checked(args.after / filename, size)
        print(f"EVIDENCE PASS {filename}: {size[0]}x{size[1]}, dark capture present")


if __name__ == "__main__":
    main()
