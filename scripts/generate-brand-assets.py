#!/usr/bin/env python3
"""Generate ZVec.Rag brand PNGs (Pillow-only; no Cairo required on Windows)."""

from __future__ import annotations

import sys
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[1]
ASSETS = ROOT / "docs" / "assets"
ENGINE_ICON = Path(r"D:\A_S\AdamSystems.ZVec.NET\src\Core\ZVec.NET\package-icon.png")

DOTNET_PURPLE = (81, 43, 212)
DOTNET_PURPLE_LIGHT = (124, 77, 255)
TEXT_LIGHT = "#1B1B1B"
TEXT_DARK = "#FFFFFF"
WORDMARK = "Zvec.Rag"


def load_font(size: int) -> ImageFont.FreeTypeFont | ImageFont.ImageFont:
    for path in (
        Path(r"C:\Windows\Fonts\segoeuib.ttf"),
        Path(r"C:\Windows\Fonts\segoeui.ttf"),
        Path(r"C:\Windows\Fonts\bahnschrift.ttf"),
    ):
        if path.exists():
            return ImageFont.truetype(str(path), size=size)
    return ImageFont.load_default()


def is_background(r: int, g: int, b: int) -> bool:
    return r + g + b < 30


def is_wordmark_white(r: int, g: int, b: int) -> bool:
    return r > 210 and g > 210 and b > 210


def extract_z_glyph(source: Path) -> Image.Image:
    img = Image.open(source).convert("RGBA")
    w, h = img.size
    pixels = img.load()
    min_x, min_y, max_x, max_y = w, h, 0, 0
    for y in range(h):
        for x in range(w):
            r, g, b, a = pixels[x, y]
            if is_background(r, g, b) or is_wordmark_white(r, g, b):
                continue
            if x > w * 0.32:
                continue
            min_x = min(min_x, x)
            min_y = min(min_y, y)
            max_x = max(max_x, x)
            max_y = max(max_y, y)
    z = img.crop((min_x - 4, min_y - 4, max_x + 5, max_y + 5))
    return recolor_to_dotnet_purple(z)


def recolor_to_dotnet_purple(img: Image.Image) -> Image.Image:
    out = Image.new("RGBA", img.size, (0, 0, 0, 0))
    src = img.load()
    dst = out.load()
    for y in range(img.height):
        for x in range(img.width):
            r, g, b, a = src[x, y]
            if is_background(r, g, b):
                continue
            if is_wordmark_white(r, g, b):
                continue
            luminance = (r + g + b) / 3
            if b >= r and b >= g:
                target = DOTNET_PURPLE_LIGHT if luminance > 120 else DOTNET_PURPLE
            else:
                target = DOTNET_PURPLE
            alpha = max(a, 255) if luminance > 20 else 0
            dst[x, y] = (*target, alpha)
    return out


def trim_transparent(img: Image.Image, padding: int = 4) -> Image.Image:
    bbox = img.getbbox()
    if not bbox:
        return img
    left, top, right, bottom = bbox
    return img.crop(
        (
            max(0, left - padding),
            max(0, top - padding),
            min(img.width, right + padding),
            min(img.height, bottom + padding),
        )
    )


def fit_height(img: Image.Image, target_h: int) -> Image.Image:
    scale = target_h / img.height
    return img.resize((max(1, int(img.width * scale)), target_h), Image.Resampling.LANCZOS)


def make_icon(z_img: Image.Image, out_path: Path) -> None:
    z = fit_height(trim_transparent(z_img), 96)
    canvas = Image.new("RGBA", (128, 128), (0, 0, 0, 0))
    canvas.paste(z, ((128 - z.width) // 2, (128 - z.height) // 2), z)
    canvas.save(out_path, format="PNG", optimize=True)
    print(f"wrote {out_path} ({canvas.size[0]}x{canvas.size[1]})")


def make_wordmark(z_img: Image.Image, text_color: str, out_path: Path, display_width: int = 360) -> None:
    z = fit_height(trim_transparent(z_img), 72)
    font = load_font(44)
    dummy = Image.new("RGBA", (1, 1))
    draw = ImageDraw.Draw(dummy)
    bbox = draw.textbbox((0, 0), WORDMARK, font=font)
    text_w = bbox[2] - bbox[0]
    text_h = bbox[3] - bbox[1]
    gap = 18
    canvas_w = z.width + gap + text_w + 8
    canvas_h = max(z.height, text_h) + 8
    canvas = Image.new("RGBA", (canvas_w, canvas_h), (0, 0, 0, 0))
    canvas.paste(z, (0, (canvas_h - z.height) // 2), z)
    text_draw = ImageDraw.Draw(canvas)
    text_draw.text(
        (z.width + gap, (canvas_h - text_h) // 2 - bbox[1]),
        WORDMARK,
        fill=text_color,
        font=font,
    )
    scale = display_width / canvas_w
    final = canvas.resize(
        (display_width, max(1, int(canvas_h * scale))),
        Image.Resampling.LANCZOS,
    )
    final.save(out_path, format="PNG", optimize=True)
    print(f"wrote {out_path} ({final.size[0]}x{final.size[1]})")


def main() -> int:
    if not ENGINE_ICON.exists():
        print(f"Missing engine icon: {ENGINE_ICON}", file=sys.stderr)
        return 1
    ASSETS.mkdir(parents=True, exist_ok=True)
    z = extract_z_glyph(ENGINE_ICON)
    make_icon(z, ASSETS / "zvec-icon.png")
    make_wordmark(z, TEXT_LIGHT, ASSETS / "zvec-rag-logo-light.png")
    make_wordmark(z, TEXT_DARK, ASSETS / "zvec-rag-logo-dark.png")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
