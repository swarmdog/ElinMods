"""Integrate reviewed Underworld generated assets into the mod Texture folder."""

from __future__ import annotations

import json
from pathlib import Path

from PIL import Image

from uw_asset_specs import ROOT, TEXTURE_DIR, get_asset_spec

WORKLOG_DIR = ROOT / "worklog" / "generated_assets"
LOG_FILE = WORKLOG_DIR / "generation_log.json"


def alpha_bbox(img: Image.Image):
    return img.getchannel("A").getbbox()


def paste_position(spec, resized_size: tuple[int, int]) -> tuple[int, int]:
    canvas_w, canvas_h = spec.canvas_size
    resized_w, resized_h = resized_size
    paste_x = (canvas_w - resized_w) // 2

    if spec.anchor == "bottom_center":
        paste_y = canvas_h - resized_h - spec.ground_lift_px
    elif spec.anchor == "center":
        paste_y = (canvas_h - resized_h) // 2
    else:
        raise ValueError(f"Unsupported anchor {spec.anchor!r}")

    if paste_y < 0:
        raise ValueError(
            f"ground_lift_px {spec.ground_lift_px} places sprite outside canvas {spec.canvas_size}"
        )
    return paste_x, paste_y


def fit_to_visible_size(img: Image.Image, spec) -> Image.Image:
    visible_w, visible_h = spec.visible_size
    scale = min(visible_w / img.width, visible_h / img.height)
    new_w = max(1, int(img.width * scale))
    new_h = max(1, int(img.height * scale))
    return img.resize((new_w, new_h), Image.Resampling.LANCZOS)


def validate_integrated_image(asset_id: str, category: str, spec, img: Image.Image) -> None:
    if img.size != spec.canvas_size:
        raise ValueError(f"{asset_id} saved as {img.size}, expected {spec.canvas_size}")

    bbox = alpha_bbox(img)
    if not bbox:
        raise ValueError(f"{asset_id} has no opaque pixels after integration")

    bbox_w = bbox[2] - bbox[0]
    bbox_h = bbox[3] - bbox[1]
    if bbox_w > spec.visible_size[0] or bbox_h > spec.visible_size[1]:
        raise ValueError(
            f"{asset_id} alpha bbox {bbox_w}x{bbox_h} exceeds visible_size {spec.visible_size}"
        )

    expected_bottom = img.height - spec.ground_lift_px
    if category == "chara" and bbox[3] != expected_bottom:
        raise ValueError(
            f"{asset_id} chara alpha bbox bottom is {bbox[3]}, expected {expected_bottom}"
        )


def integrate_assets(only_ids: list[str] | None = None) -> None:
    if not LOG_FILE.exists():
        raise FileNotFoundError(f"Could not find generation log at {LOG_FILE}")

    TEXTURE_DIR.mkdir(parents=True, exist_ok=True)
    entries = json.loads(LOG_FILE.read_text(encoding="utf-8"))
    failures: list[tuple[str, str]] = []

    allowed_ids = set(only_ids) if only_ids else None
    for entry in entries:
        if not entry.get("success"):
            continue

        asset_id = entry["id"]
        if allowed_ids is not None and asset_id not in allowed_ids:
            continue

        try:
            category = entry["category"]
            spec = get_asset_spec(asset_id)
            nobg_path = Path(entry["files"]["nobg"])
            if not nobg_path.exists():
                raise FileNotFoundError(f"Missing no-background variant for {asset_id}: {nobg_path}")

            img = Image.open(nobg_path).convert("RGBA")
            bbox = alpha_bbox(img)
            if not bbox:
                raise ValueError(f"{asset_id} has no opaque pixels after background removal")

            cropped = img.crop(bbox)
            resized = fit_to_visible_size(cropped, spec)
            final_img = Image.new("RGBA", spec.canvas_size, (0, 0, 0, 0))
            final_img.paste(resized, paste_position(spec, resized.size))
            validate_integrated_image(asset_id, category, spec, final_img)

            dest_path = TEXTURE_DIR / f"{asset_id}.png"
            final_img.save(dest_path)
            print(f"Integrated {asset_id} -> {dest_path.relative_to(ROOT)}")
        except Exception as exc:
            failures.append((asset_id, str(exc)))
            print(f"Failed to integrate {asset_id}: {exc}")

    if failures:
        failed_ids = ", ".join(asset_id for asset_id, _ in failures)
        raise RuntimeError(f"Integration failed for {len(failures)} asset(s): {failed_ids}")


if __name__ == "__main__":
    integrate_assets()
