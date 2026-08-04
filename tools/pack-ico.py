#!/usr/bin/env python3
"""Packs the already-rendered hanki-logo-*.png sizes into hanki-logo.ico.

Run after tools/render-logo-assets.mjs has produced the PNG set:
    python tools/pack-ico.py

Only paths relative to the repo root are used.
"""
from pathlib import Path
from PIL import Image

ICO_SIZES = [16, 20, 24, 32, 40, 48, 64, 128, 256]

def main():
    branding_dir = Path(__file__).resolve().parent.parent / "assets" / "branding"
    frames = []
    for size in ICO_SIZES:
        frame_path = branding_dir / f"hanki-logo-{size}.png"
        if not frame_path.exists():
            raise SystemExit(f"missing {frame_path}, run render-logo-assets.mjs first")
        frames.append(Image.open(frame_path).convert("RGBA"))

    # Pillow's ICO writer treats the *first* image as the upper size bound and
    # silently skips any requested size larger than it -- so the base image
    # passed to save() must be the largest frame, not the smallest.
    ico_path = branding_dir / "hanki-logo.ico"
    frames[-1].save(
        ico_path,
        format="ICO",
        sizes=[(s, s) for s in ICO_SIZES],
        append_images=frames[:-1],
    )
    print(f"wrote {ico_path} sizes={ICO_SIZES}")


if __name__ == "__main__":
    main()
