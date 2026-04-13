"""Deployment scale specs for generated Skyreader Guild assets.

The generated preview size is for human review only. Elin renders custom
sprites from their deployed PNG dimensions, so gameplay scale belongs here.
"""

from __future__ import annotations

from dataclasses import dataclass


CHARA_CANVAS_SIZE = (128, 192)
SMALL_ITEM_CANVAS_SIZE = (32, 32)
SMALL_ITEM_VISIBLE_SIZE = (30, 30)

ANCHOR_CENTER = "center"
ANCHOR_BOTTOM_CENTER = "bottom_center"


@dataclass(frozen=True)
class AssetScaleSpec:
    category: str
    canvas_size: tuple[int, int]
    visible_size: tuple[int, int]
    anchor: str
    render_data: str
    ground_lift_px: int = 0

    def to_log_fields(self) -> dict[str, object]:
        return {
            "canvas_size": list(self.canvas_size),
            "visible_size": list(self.visible_size),
            "anchor": self.anchor,
            "render_data": self.render_data,
            "ground_lift_px": self.ground_lift_px,
        }


def small_item(render_data: str = "@obj_S flat") -> AssetScaleSpec:
    return AssetScaleSpec(
        category="item",
        canvas_size=SMALL_ITEM_CANVAS_SIZE,
        visible_size=SMALL_ITEM_VISIBLE_SIZE,
        anchor=ANCHOR_CENTER,
        render_data=render_data,
    )


def chara(
    visible_size: tuple[int, int],
    render_data: str = "@chara",
    ground_lift_px: int = 0,
) -> AssetScaleSpec:
    return AssetScaleSpec(
        category="chara",
        canvas_size=CHARA_CANVAS_SIZE,
        visible_size=visible_size,
        anchor=ANCHOR_BOTTOM_CENTER,
        render_data=render_data,
        ground_lift_px=ground_lift_px,
    )


ASSET_SCALE_SPECS: dict[str, AssetScaleSpec] = {
    # Small items: Elin uses native sprite size in world and inventory.
    "srg_astral_extractor": small_item("@obj_S flat"),
    "srg_meteorite_source": small_item("@obj_S flat"),
    "srg_meteor_core": small_item("@obj_S"),
    "srg_debris": small_item("@obj_S"),
    "srg_weave_stars": small_item("@obj_S flat"),
    "srg_starforge": small_item("@obj_S flat"),
    "srg_scroll_twilight": small_item("@obj_S flat"),
    "srg_scroll_radiance": small_item("@obj_S flat"),
    "srg_scroll_abyss": small_item("@obj_S flat"),
    "srg_scroll_nova": small_item("@obj_S flat"),
    "srg_scroll_convergence": small_item("@obj_S flat"),
    "srg_starchart": AssetScaleSpec("item", (64, 64), (60, 60), ANCHOR_CENTER, "@obj_S flat"),
    "srg_codex": AssetScaleSpec("item", (64, 64), (60, 60), ANCHOR_CENTER, "@obj"),
    "srg_astral_portal": AssetScaleSpec("item", (128, 160), (128, 160), ANCHOR_CENTER, "@obj tall"),

    # Characters: stable 128x192 canvas, explicit visible silhouette scale.
    "srg_arkyn": chara((72, 108), "@chara", ground_lift_px=16),
    "srg_archivist": chara((72, 108), "@chara", ground_lift_px=16),
    "srg_growth": chara((80, 120), "@chara_L"),
    "srg_yith_hound": chara((64, 80), "@chara", ground_lift_px=14),
    "srg_yith_drone": chara((64, 80), "@chara", ground_lift_px=14),
    "srg_yith_weaver": chara((128, 192), "@chara_L"),
    "srg_yith_ancient": chara((128, 154), "@chara_L"),
    "srg_yith_behemoth": chara((128, 192), "@chara_L"),
    "srg_umbryon": chara((128, 192), "@chara"),
    "srg_solaris": chara((128, 192), "@chara"),
    "srg_erevor": chara((128, 192), "@chara_L"),
    "srg_quasarix": chara((128, 192), "@chara"),
}


def get_asset_spec(asset_id: str) -> AssetScaleSpec:
    try:
        return ASSET_SCALE_SPECS[asset_id]
    except KeyError as exc:
        raise KeyError(f"No deployment scale spec defined for {asset_id}") from exc
