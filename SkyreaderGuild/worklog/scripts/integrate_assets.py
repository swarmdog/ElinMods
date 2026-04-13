"""Integrate generated Skyreader Guild assets into the mod structure.

Reads assets from worklog/generated_assets/, loads the nobg versions,
fits their visible alpha bbox into an explicit deployment canvas, and copies
them into Texture/.

Elin uses custom PNG dimensions for gameplay scale. The generated preview size
is therefore review-only; deployment scale comes from asset_specs.py.
"""
import json
from pathlib import Path
from PIL import Image

from asset_specs import ANCHOR_BOTTOM_CENTER, ANCHOR_CENTER, get_asset_spec

ROOT = Path(__file__).resolve().parent.parent.parent
WORKLOG_DIR = ROOT / "worklog" / "generated_assets"
LOG_FILE = WORKLOG_DIR / "generation_log.json"
MOD_TEXTURE_DIR = ROOT / "Texture"


def alpha_bbox(img: Image.Image):
    return img.getchannel("A").getbbox()


def resolved_spec_for(entry):
    asset_id = entry["id"]
    spec = get_asset_spec(asset_id)

    if spec.category != entry["category"]:
        raise ValueError(
            f"{asset_id} log category {entry['category']!r} does not match "
            f"deployment spec category {spec.category!r}"
        )

    for field in ("canvas_size", "visible_size", "anchor", "render_data", "ground_lift_px"):
        if field not in entry or entry[field] in (None, ""):
            continue
        expected = getattr(spec, field)
        actual = tuple(entry[field]) if field.endswith("_size") else entry[field]
        if actual != expected:
            raise ValueError(
                f"{asset_id} log {field}={actual!r} conflicts with "
                f"deployment spec {expected!r}"
            )

    return spec


def paste_position(spec, resized_size):
    canvas_w, canvas_h = spec.canvas_size
    resized_w, resized_h = resized_size
    paste_x = (canvas_w - resized_w) // 2

    if spec.anchor == ANCHOR_BOTTOM_CENTER:
        paste_y = canvas_h - resized_h - spec.ground_lift_px
    elif spec.anchor == ANCHOR_CENTER:
        paste_y = (canvas_h - resized_h) // 2
    else:
        raise ValueError(f"Unsupported anchor {spec.anchor!r}")

    if paste_y < 0:
        raise ValueError(
            f"ground_lift_px {spec.ground_lift_px} places sprite outside "
            f"canvas {spec.canvas_size}"
        )

    return paste_x, paste_y


def fit_to_visible_size(img: Image.Image, spec):
    visible_w, visible_h = spec.visible_size
    if visible_w > spec.canvas_size[0] or visible_h > spec.canvas_size[1]:
        raise ValueError(
            f"visible_size {spec.visible_size} exceeds canvas_size {spec.canvas_size}"
        )

    scale = min(visible_w / img.width, visible_h / img.height)
    new_w = max(1, int(img.width * scale))
    new_h = max(1, int(img.height * scale))
    return img.resize((new_w, new_h), Image.Resampling.LANCZOS)


def validate_integrated_image(asset_id, category, spec, img):
    if img.size != spec.canvas_size:
        raise ValueError(f"{asset_id} saved as {img.size}, expected {spec.canvas_size}")

    bbox = alpha_bbox(img)
    if not bbox:
        raise ValueError(f"{asset_id} has no opaque pixels after integration")

    _, _, bbox_right, bbox_bottom = bbox
    bbox_w = bbox_right - bbox[0]
    bbox_h = bbox_bottom - bbox[1]
    if bbox_w > spec.visible_size[0] or bbox_h > spec.visible_size[1]:
        raise ValueError(
            f"{asset_id} alpha bbox {bbox_w}x{bbox_h} exceeds visible_size {spec.visible_size}"
        )

    expected_bottom = img.height - spec.ground_lift_px
    if category == "chara" and bbox_bottom != expected_bottom:
        raise ValueError(
            f"{asset_id} chara alpha bbox bottom is {bbox_bottom}, expected {expected_bottom}"
        )


def validate_no_unexpected_pref_files():
    pref_files = sorted(MOD_TEXTURE_DIR.glob("srg_*.pref"))
    if pref_files:
        rel = ", ".join(str(p.relative_to(ROOT)) for p in pref_files)
        raise ValueError(f"Unexpected custom .pref files present: {rel}")

def integrate_assets():
    if not LOG_FILE.exists():
        print(f"Error: Could not find generation log at {LOG_FILE}")
        return

    # Ensure target directory exists
    MOD_TEXTURE_DIR.mkdir(parents=True, exist_ok=True)

    with open(LOG_FILE, "r", encoding="utf-8") as f:
        log_entries = json.load(f)

    success_count = 0
    failures = []
    for entry in log_entries:
        if not entry.get("success"):
            print(f"Skipping {entry['id']}: generation was not successful.")
            continue

        asset_id = entry["id"]

        # Open, resize, and save to correct directory
        try:
            category = entry["category"]
            files = entry["files"]
            spec = resolved_spec_for(entry)

            # Validate that the nobg file exists
            nobg_path = Path(files["nobg"])
            if not nobg_path.exists():
                raise FileNotFoundError(f"Missing file for {asset_id}: {nobg_path}")

            img = Image.open(nobg_path).convert("RGBA")
            
            # Auto-crop dead transparent space
            bbox = alpha_bbox(img)
            if not bbox:
                raise ValueError(f"{asset_id} has no opaque pixels after background removal")
            img = img.crop(bbox)

            resized_img = fit_to_visible_size(img, spec)
            new_w, new_h = resized_img.size
            
            # Create a blank target canvas
            final_img = Image.new("RGBA", spec.canvas_size, (0, 0, 0, 0))
            
            # Calculate anchor points
            paste_x, paste_y = paste_position(spec, resized_img.size)
            final_img.paste(resized_img, (paste_x, paste_y))
            validate_integrated_image(asset_id, category, spec, final_img)
            
            dest_path = MOD_TEXTURE_DIR / f"{asset_id}.png"
                
            final_img.save(dest_path)
            print(
                f"Integrated {asset_id} -> {dest_path.relative_to(ROOT)} "
                f"(resized {img.size} to {new_w}x{new_h} inside {spec.canvas_size}, "
                f"anchor={spec.anchor}, ground_lift_px={spec.ground_lift_px}, "
                f"render_data={spec.render_data})"
            )
            success_count += 1
            
        except Exception as e:
            print(f"Failed to integrate {asset_id}: {e}")
            failures.append((asset_id, e))

    validate_no_unexpected_pref_files()
    if failures:
        failed_ids = ", ".join(asset_id for asset_id, _ in failures)
        raise RuntimeError(f"Integration failed for {len(failures)} asset(s): {failed_ids}")
    print(f"\nIntegration complete. {success_count} assets processed and validated.")

if __name__ == "__main__":
    integrate_assets()
