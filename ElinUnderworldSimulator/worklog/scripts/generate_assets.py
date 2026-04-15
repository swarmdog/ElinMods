"""Generate Underworld Simulator sprite assets with Gemini Nano Banana.

Outputs raw, preview, and no-background variants into worklog/generated_assets/
for manual review before integration into the shipped Texture/ directory.
"""

from __future__ import annotations

import argparse
from collections import deque
from datetime import datetime, timezone
from math import gcd
import io
import json
import os
from pathlib import Path
import sys
import time

from dotenv import load_dotenv
from PIL import Image

from uw_asset_specs import CHARA_ASSETS, ITEM_ASSETS, get_asset_spec

if sys.stdout.encoding != "utf-8":
    sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8", errors="replace")
    sys.stderr = io.TextIOWrapper(sys.stderr.buffer, encoding="utf-8", errors="replace")

ROOT = Path(__file__).resolve().parent.parent.parent
ENV_PATH = ROOT.parent / ".env.local"
OUTPUT_DIR = ROOT / "worklog" / "generated_assets"
ITEMS_DIR = OUTPUT_DIR / "items"
CHARAS_DIR = OUTPUT_DIR / "charas"

DEFAULT_MODEL = "nano-banana-pro-preview"
DELAY_BETWEEN_CALLS = 8
MAX_RETRIES = 2
RETRY_BACKOFF_BASE = 30

ITEM_GENERATION_CONSTRAINTS = (
    " Requirements: create exactly one isolated game sprite. Fill the entire "
    "background outside the sprite with one uniform flat fuchsia or magenta screen "
    "color close to #ff00ff RGB(255,0,255). Keep the background flat, opaque, and "
    "free of gradients, scenery, floor, cast shadow, border, frame, card, text, logo, "
    "UI, or poster elements. The sprite itself must contain no fuchsia or magenta."
)

CHARA_GENERATION_CONSTRAINTS = (
    " Requirements: create exactly one isolated full-body game sprite with feet or "
    "lowest body contact clearly visible, a complete silhouette, and no cropping. "
    "Fill the entire background outside the sprite with one uniform flat fuchsia or "
    "magenta screen color close to #ff00ff RGB(255,0,255). Keep the background flat, "
    "opaque, and free of gradients, scenery, floor, cast shadow, border, frame, card, "
    "text, logo, or UI. The sprite itself must contain no fuchsia or magenta."
)

CHROMA_KEY_COLOR = (255, 0, 255)
KEY_COLOR_TOLERANCE = 140
STRICT_KEY_COLOR_TOLERANCE = 48
BORDER_KEY_MIN_RATIO = 0.90
CORNER_KEY_MIN_RATIO = 0.90
CORNER_SIZE_RATIO = 0.08
MAX_OPAQUE_CORNER_RATIO_AFTER_REMOVAL = 0.01
MAX_RESIDUAL_KEY_OPAQUE_RATIO = 0.001
CHARA_MIN_BBOX_HEIGHT_RATIO = 0.68
SUPPORTED_ASPECT_RATIOS = {
    "1:1",
    "2:3",
    "3:2",
    "3:4",
    "4:3",
    "4:5",
    "5:4",
    "9:16",
    "16:9",
    "21:9",
}


def is_key_color(pixel: tuple[int, int, int, int]) -> bool:
    r, g, b, _ = pixel
    key_r, key_g, key_b = CHROMA_KEY_COLOR
    return (
        abs(r - key_r) <= KEY_COLOR_TOLERANCE
        and abs(g - key_g) <= KEY_COLOR_TOLERANCE
        and abs(b - key_b) <= KEY_COLOR_TOLERANCE
    )


def is_strict_key_color(pixel: tuple[int, int, int, int]) -> bool:
    r, g, b, _ = pixel
    key_r, key_g, key_b = CHROMA_KEY_COLOR
    return (
        abs(r - key_r) <= STRICT_KEY_COLOR_TOLERANCE
        and abs(g - key_g) <= STRICT_KEY_COLOR_TOLERANCE
        and abs(b - key_b) <= STRICT_KEY_COLOR_TOLERANCE
    )


def remove_chroma_key_background(img: Image.Image) -> Image.Image:
    img = img.convert("RGBA")
    pixels = img.load()
    w, h = img.size
    queue = deque()
    visited: set[tuple[int, int]] = set()

    def enqueue_if_key(x: int, y: int) -> None:
        point = (x, y)
        if point not in visited and is_key_color(pixels[x, y]):
            visited.add(point)
            queue.append(point)

    for x in range(w):
        enqueue_if_key(x, 0)
        enqueue_if_key(x, h - 1)
    for y in range(h):
        enqueue_if_key(0, y)
        enqueue_if_key(w - 1, y)

    while queue:
        x, y = queue.popleft()
        pixels[x, y] = (0, 0, 0, 0)
        for nx, ny in ((x - 1, y), (x + 1, y), (x, y - 1), (x, y + 1)):
            if 0 <= nx < w and 0 <= ny < h:
                enqueue_if_key(nx, ny)

    for y in range(h):
        for x in range(w):
            if pixels[x, y][3] > 0 and is_strict_key_color(pixels[x, y]):
                pixels[x, y] = (0, 0, 0, 0)

    return img


def border_pixels(img: Image.Image, border_width: int):
    w, h = img.size
    pixels = img.load()
    for y in range(h):
        for x in range(w):
            if x < border_width or x >= w - border_width or y < border_width or y >= h - border_width:
                yield pixels[x, y]


def corner_pixels(img: Image.Image):
    w, h = img.size
    corner_w = max(1, int(w * CORNER_SIZE_RATIO))
    corner_h = max(1, int(h * CORNER_SIZE_RATIO))
    pixels = img.load()
    boxes = (
        (0, 0, corner_w, corner_h),
        (w - corner_w, 0, w, corner_h),
        (0, h - corner_h, corner_w, h),
        (w - corner_w, h - corner_h, w, h),
    )
    for x0, y0, x1, y1 in boxes:
        yield [pixels[x, y] for y in range(y0, y1) for x in range(x0, x1)]


def corner_alpha_pixels(img: Image.Image):
    w, h = img.size
    corner_w = max(1, int(w * CORNER_SIZE_RATIO))
    corner_h = max(1, int(h * CORNER_SIZE_RATIO))
    alpha = img.getchannel("A")
    boxes = (
        (0, 0, corner_w, corner_h),
        (w - corner_w, 0, w, corner_h),
        (0, h - corner_h, corner_w, h),
        (w - corner_w, h - corner_h, w, h),
    )
    for box in boxes:
        yield from alpha.crop(box).getdata()


def validate_raw_generation(img: Image.Image, asset_id: str) -> None:
    img = img.convert("RGBA")
    border_width = max(1, min(img.size) // 32)
    samples = list(border_pixels(img, border_width))
    if not samples:
        raise ValueError(f"{asset_id} produced an empty image")

    key_ratio = sum(1 for pixel in samples if is_key_color(pixel)) / len(samples)
    if key_ratio < BORDER_KEY_MIN_RATIO:
        raise ValueError(
            f"{asset_id} raw border is only {key_ratio:.1%} chroma-key colored; "
            "expected isolated sprite on a pure magenta background"
        )

    for index, samples in enumerate(corner_pixels(img), 1):
        key_ratio = sum(1 for pixel in samples if is_key_color(pixel)) / len(samples)
        if key_ratio < CORNER_KEY_MIN_RATIO:
            raise ValueError(
                f"{asset_id} raw corner {index} is only {key_ratio:.1%} chroma-key colored; "
                "expected mostly pure magenta corners"
            )


def validate_nobg_generation(img: Image.Image, asset_id: str, category: str) -> None:
    img = img.convert("RGBA")
    bbox = img.getchannel("A").getbbox()
    if not bbox:
        raise ValueError(f"{asset_id} has no opaque pixels after chroma-key removal")
    if bbox == (0, 0, img.width, img.height):
        raise ValueError(f"{asset_id} alpha bbox covers the full image after chroma-key removal")

    corner_alpha = list(corner_alpha_pixels(img))
    opaque_corner_ratio = sum(1 for alpha in corner_alpha if alpha > 0) / len(corner_alpha)
    if opaque_corner_ratio > MAX_OPAQUE_CORNER_RATIO_AFTER_REMOVAL:
        raise ValueError(
            f"{asset_id} still has {opaque_corner_ratio:.1%} opaque corner pixels after chroma-key removal"
        )

    opaque_pixels = [pixel for pixel in img.getdata() if pixel[3] > 0]
    residual_key_ratio = sum(1 for pixel in opaque_pixels if is_strict_key_color(pixel)) / len(opaque_pixels)
    if residual_key_ratio > MAX_RESIDUAL_KEY_OPAQUE_RATIO:
        raise ValueError(
            f"{asset_id} still has {residual_key_ratio:.2%} opaque chroma-key pixels after cleanup"
        )

    if category == "chara":
        bbox_w = bbox[2] - bbox[0]
        bbox_h = bbox[3] - bbox[1]
        width_ratio = bbox_w / img.width
        height_ratio = bbox_h / img.height
        if width_ratio > 0.90 and height_ratio < CHARA_MIN_BBOX_HEIGHT_RATIO:
            raise ValueError(
                f"{asset_id} chara crop is banner-like after chroma-key removal "
                f"({bbox_w}x{bbox_h} in {img.width}x{img.height})"
            )


def constrained_prompt(prompt: str, category: str) -> str:
    if category == "chara":
        return f"Generate an image of one isolated RPG sprite: {prompt}.{CHARA_GENERATION_CONSTRAINTS}"
    if category == "item":
        return f"Generate an image of one isolated RPG asset: {prompt}.{ITEM_GENERATION_CONSTRAINTS}"
    raise ValueError(f"Unknown asset category {category!r}")


def aspect_ratio_for_size(size: tuple[int, int]) -> str:
    width, height = size
    divisor = gcd(width, height)
    ratio = f"{width // divisor}:{height // divisor}"
    if ratio not in SUPPORTED_ASPECT_RATIOS:
        raise ValueError(f"Unsupported Gemini image aspect ratio {ratio} for size {size}")
    return ratio


def save_variants(raw_bytes: bytes, asset_id: str, output_dir: Path, preview_size: tuple[int, int], category: str) -> dict[str, object]:
    img = Image.open(io.BytesIO(raw_bytes)).convert("RGBA")
    validate_raw_generation(img, asset_id)

    nobg = remove_chroma_key_background(img)
    validate_nobg_generation(nobg, asset_id, category)

    raw_path = output_dir / f"{asset_id}_raw.png"
    preview_path = output_dir / f"{asset_id}_preview.png"
    nobg_path = output_dir / f"{asset_id}_nobg.png"

    img.save(raw_path)
    img.resize(preview_size, Image.Resampling.LANCZOS).save(preview_path)
    nobg.save(nobg_path)

    return {
        "raw": str(raw_path),
        "preview": str(preview_path),
        "nobg": str(nobg_path),
        "raw_size": img.size,
        "preview_size": list(preview_size),
    }


def generate_gallery(log_entries: list[dict], output_dir: Path) -> None:
    html_parts = [
        "<!DOCTYPE html>",
        "<html lang='en'><head><meta charset='utf-8'>",
        "<title>Underworld Asset Review</title>",
        "<style>",
        "body { background: #10131a; color: #ece7dc; font-family: 'Segoe UI', sans-serif; padding: 20px; }",
        "h1 { color: #8fd1bb; text-align: center; }",
        ".grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(320px, 1fr)); gap: 20px; }",
        ".card { background: #171d28; border-radius: 12px; padding: 16px; border: 1px solid #2b3647; }",
        ".card h3 { margin: 0 0 8px 0; font-size: 14px; color: #f0e0c4; }",
        ".type { color: #93a1b5; font-size: 11px; margin-bottom: 8px; }",
        ".images { display: flex; gap: 12px; align-items: flex-end; margin: 12px 0; }",
        ".images img { border: 1px solid #364155; border-radius: 4px; background: #000; }",
        ".preview-img { width: 96px; height: 96px; object-fit: contain; image-rendering: pixelated; }",
        ".raw-link { font-size: 11px; color: #7dc8ff; }",
        ".prompt { font-size: 11px; color: #b5b0a7; margin-top: 8px; word-break: break-word; }",
        ".status-ok { color: #7ecb7e; } .status-fail { color: #ff8d8d; }",
        "h2 { color: #d8bf7b; border-bottom: 1px solid #2b3647; padding-bottom: 8px; margin-top: 40px; }",
        "</style></head><body>",
        "<h1>Elin Underworld Simulator Asset Review</h1>",
        f"<p style='text-align:center;color:#7f8b9e;'>Generated {datetime.now().strftime('%Y-%m-%d %H:%M')}</p>",
    ]

    grouped = [("Items & Objects", "item"), ("Characters", "chara")]
    for section_name, category in grouped:
        entries = [entry for entry in log_entries if entry.get("category") == category]
        html_parts.append(f"<h2>{section_name}</h2>")
        html_parts.append("<div class='grid'>")
        for entry in entries:
            status_cls = "status-ok" if entry.get("success") else "status-fail"
            status_txt = "Generated" if entry.get("success") else f"Failed: {entry.get('error', 'unknown')}"
            if entry.get("success") and entry.get("files"):
                subfolder = "items" if category == "item" else "charas"
                preview_src = Path(entry["files"]["preview"]).name
                raw_src = Path(entry["files"]["raw"]).name
                images = (
                    "<div class='images'>"
                    f"<img class='preview-img' src='{subfolder}/{preview_src}' alt='preview'>"
                    f"<a class='raw-link' href='{subfolder}/{raw_src}' target='_blank'>View Raw</a>"
                    "</div>"
                )
            else:
                images = ""

            html_parts.append(
                f"<div class='card'>"
                f"<h3>{entry['id']}</h3>"
                f"<div class='type'>Preview: {entry.get('preview_size', '?')} | Deploy: {entry.get('canvas_size', '?')} / visible {entry.get('visible_size', '?')}</div>"
                f"<span class='{status_cls}'>{status_txt}</span>"
                f"{images}"
                f"<div class='prompt'><strong>Prompt:</strong> {entry.get('prompt', '')[:260]}...</div>"
                f"</div>"
            )
        html_parts.append("</div>")

    html_parts.append("</body></html>")
    (output_dir / "index.html").write_text("\n".join(html_parts), encoding="utf-8")


def generate_one(client, model: str, asset: dict[str, object], output_dir: Path, category: str) -> dict[str, object]:
    from google.genai import types

    asset_id = asset["id"]
    preview_size = tuple(asset["preview_size"])
    prompt = constrained_prompt(asset["prompt"], category)
    scale_spec = get_asset_spec(asset_id)

    entry: dict[str, object] = {
        "id": asset_id,
        "category": category,
        "prompt": prompt,
        "preview_size": list(preview_size),
        "model": model,
        "timestamp": datetime.now(timezone.utc).isoformat(),
        "success": False,
        "error": None,
        "files": None,
    }
    entry.update(scale_spec.to_log_fields())
    entry["aspect_ratio"] = aspect_ratio_for_size(scale_spec.canvas_size)

    for attempt in range(1, MAX_RETRIES + 1):
        try:
            print(f"  [{attempt}/{MAX_RETRIES}] Generating {asset_id}...")
            config = types.GenerateContentConfig(
                response_modalities=["IMAGE"],
                image_config=types.ImageConfig(aspect_ratio=entry["aspect_ratio"]),
            )
            response = client.models.generate_content(
                model=model,
                contents=prompt,
                config=config,
            )

            if not response.candidates:
                raise RuntimeError("No candidates in response")

            image_data = None
            for part in response.candidates[0].content.parts:
                if part.inline_data is not None:
                    image_data = part.inline_data.data
                    break

            if image_data is None:
                texts = [part.text for part in response.candidates[0].content.parts if part.text]
                if texts:
                    raise RuntimeError(f"Model returned text instead of image: {texts[0][:200]}")
                raise RuntimeError("No inline image data found in Gemini response")

            entry["files"] = save_variants(image_data, asset_id, output_dir, preview_size, category)
            entry["success"] = True
            entry["error"] = None
            print(f"  OK {asset_id}")
            return entry
        except Exception as exc:
            entry["error"] = str(exc)
            print(f"  FAIL {asset_id}: {entry['error'][:160]}")
            if attempt < MAX_RETRIES:
                if "429" in entry["error"] or "rate" in entry["error"].lower() or "quota" in entry["error"].lower():
                    wait = RETRY_BACKOFF_BASE * (2 ** (attempt - 1))
                else:
                    wait = DELAY_BETWEEN_CALLS
                print(f"    Waiting {wait}s before retry...")
                time.sleep(wait)

    return entry


def run_generation(only_ids: list[str] | None = None, model: str = DEFAULT_MODEL, test_mode: bool = False) -> list[dict[str, object]]:
    load_dotenv(ENV_PATH)
    api_key = os.environ.get("GEMINI_API_KEY")
    if not api_key:
        raise RuntimeError(f"GEMINI_API_KEY not found. Checked {ENV_PATH}")

    from google import genai

    client = genai.Client(api_key=api_key)
    ITEMS_DIR.mkdir(parents=True, exist_ok=True)
    CHARAS_DIR.mkdir(parents=True, exist_ok=True)

    if test_mode:
        test_asset = {
            "id": "uw_antidote_vial",
            "preview_size": (48, 48),
            "prompt": "2D top-down RPG game item icon, a sealed emerald potion bottle, pixel art style, uniform flat fuchsia/magenta screen background, centered, clean edges, no text, no frame, game asset",
        }
        return [generate_one(client, model, test_asset, ITEMS_DIR, "item")]

    items = ITEM_ASSETS
    charas = CHARA_ASSETS
    if only_ids:
        wanted = set(only_ids)
        items = [asset for asset in items if asset["id"] in wanted]
        charas = [asset for asset in charas if asset["id"] in wanted]

    total = len(items) + len(charas)
    print(f"Generating {total} Underworld assets with model {model}")

    log_path = OUTPUT_DIR / "generation_log.json"
    existing_log: dict[str, dict[str, object]] = {}
    if log_path.exists() and only_ids:
        try:
            prior_entries = json.loads(log_path.read_text(encoding="utf-8"))
            for entry in prior_entries:
                existing_log[entry["id"]] = entry
        except Exception:
            existing_log = {}

    results: list[dict[str, object]] = []

    for index, asset in enumerate(items, 1):
        print(f"[item {index}/{len(items)}] {asset['id']}")
        result = generate_one(client, model, asset, ITEMS_DIR, "item")
        results.append(result)
        existing_log[result["id"]] = result
        if index < len(items) or charas:
            time.sleep(DELAY_BETWEEN_CALLS)

    for index, asset in enumerate(charas, 1):
        print(f"[chara {index}/{len(charas)}] {asset['id']}")
        result = generate_one(client, model, asset, CHARAS_DIR, "chara")
        results.append(result)
        existing_log[result["id"]] = result
        if index < len(charas):
            time.sleep(DELAY_BETWEEN_CALLS)

    all_entries = list(existing_log.values()) if only_ids else results
    if not only_ids:
        all_entries = results

    OUTPUT_DIR.mkdir(parents=True, exist_ok=True)
    log_path.write_text(json.dumps(all_entries, indent=2, ensure_ascii=False), encoding="utf-8")
    generate_gallery(all_entries, OUTPUT_DIR)
    return all_entries


def main() -> None:
    parser = argparse.ArgumentParser(description="Generate Underworld Simulator assets")
    parser.add_argument("--only", nargs="+", help="Only generate these asset ids")
    parser.add_argument("--model", default=DEFAULT_MODEL, help=f"Gemini image model (default: {DEFAULT_MODEL})")
    parser.add_argument("--test", action="store_true", help="Run a single API connectivity test image")
    args = parser.parse_args()

    run_generation(only_ids=args.only, model=args.model, test_mode=args.test)


if __name__ == "__main__":
    main()
