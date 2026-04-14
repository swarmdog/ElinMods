"""Generate Skyreader Guild sprite assets using Google Gemini (Nano Banana).

Outputs raw and preview PNGs to worklog/generated_assets/ for manual review.
Loads GEMINI_API_KEY from ../.env.local automatically.

Usage:
    python generate_assets.py              # Generate all assets
    python generate_assets.py --only srg_arkyn srg_codex   # Regenerate specific IDs
    python generate_assets.py --test       # Quick API test with one image
"""

import argparse
from collections import deque
import io
import json
import os
import sys
import time

# Force UTF-8 output on Windows consoles
if sys.stdout.encoding != "utf-8":
    sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8", errors="replace")
    sys.stderr = io.TextIOWrapper(sys.stderr.buffer, encoding="utf-8", errors="replace")
from datetime import datetime, timezone
from math import gcd
from pathlib import Path

from dotenv import load_dotenv
from PIL import Image

from asset_specs import ASSET_SCALE_SPECS

# ---------------------------------------------------------------------------
# Configuration
# ---------------------------------------------------------------------------

ROOT = Path(__file__).resolve().parent.parent.parent
ENV_PATH = ROOT.parent / ".env.local"
OUTPUT_DIR = ROOT / "worklog" / "generated_assets"
ITEMS_DIR = OUTPUT_DIR / "items"
CHARAS_DIR = OUTPUT_DIR / "charas"

# Model to use – configurable via --model flag
# Confirmed available: nano-banana-pro-preview, gemini-2.5-flash-image,
#                      gemini-3.1-flash-image-preview, gemini-3-pro-image-preview
# Google currently recommends Gemini 3.1 Flash Image Preview as the default
# image generation model; 2.5 Flash Image remains available via --model.
DEFAULT_MODEL = "gemini-3.1-flash-image-preview"

# Rate limiting
DELAY_BETWEEN_CALLS = 8  # seconds between calls
MAX_RETRIES = 2
RETRY_BACKOFF_BASE = 30  # seconds (free tier has daily quotas)

# ---------------------------------------------------------------------------
# Prompt template helpers
# ---------------------------------------------------------------------------

ITEM_PROMPT_PREFIX = (
    "2D top-down RPG game {item_type} icon, "
)
ITEM_PROMPT_SUFFIX = (
    ", pixel art style, {palette}, uniform flat fuchsia/magenta screen background, "
    "centered, clean edges, no text, no frame, no card, no floor, game asset"
)

CHARA_PROMPT_PREFIX = "2D front-facing RPG {chara_type} sprite, "
CHARA_PROMPT_SUFFIX = (
    ", fantasy pixel art style, full body, feet visible, complete silhouette, "
    "not cropped, uniform flat fuchsia/magenta screen background, centered, clean edges, no text, "
    "no white background, no floor, no frame, no card, game {chara_type} sprite"
)

ITEM_GENERATION_CONSTRAINTS = (
    " Requirements: create exactly one isolated game sprite. Fill the entire "
    "background outside the sprite with one uniform flat fuchsia/magenta screen "
    "color close to #ff00ff RGB(255,0,255). The background is a removable color "
    "key, not part of the artwork: keep it flat, opaque, and free of gradients, "
    "scenery, floor, cast shadow, border, frame, card, text, logo, UI, or poster "
    "elements. The sprite itself must contain no fuchsia or magenta."
)

CHARA_GENERATION_CONSTRAINTS = (
    " Requirements: create exactly one isolated full-body game sprite with the "
    "feet or lowest body contact clearly visible, a complete silhouette, and no "
    "cropping. Fill the entire background outside the sprite with one uniform "
    "flat fuchsia/magenta screen color close to #ff00ff RGB(255,0,255). The "
    "background is a removable color key, not part of the artwork: keep it flat, "
    "opaque, and free of gradients, scenery, floor, cast shadow, border, frame, "
    "card, text, logo, UI, or poster elements. The sprite itself must contain no "
    "fuchsia or magenta. Preserve black outlines, near-black detail, void-dark "
    "features, and dark shadows inside the sprite."
)

CHROMA_KEY_COLOR = (255, 0, 255)
# Gemini often anti-aliases requested #ff00ff into fuchsia; edge flood-fill keeps
# this broad tolerance from touching disconnected sprite interiors.
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

# ---------------------------------------------------------------------------
# Asset manifest
# ---------------------------------------------------------------------------

ITEM_ASSETS = [
    {
        "id": "srg_astral_extractor",
        "preview_size": (48, 48),
        "prompt": (
            "2D top-down RPG game tool icon, a golden sextant-like astronomical "
            "device with crystal lenses and delicate brass gears, glowing faintly "
            "with pale blue starlight, pixel art style, gold and deep blue color "
            "palette, uniform flat fuchsia/magenta screen background, centered, clean edges, no text, "
            "no frame, game asset"
        ),
    },
    {
        "id": "srg_meteorite_source",
        "preview_size": (48, 48),
        "prompt": (
            "2D top-down RPG game crafting material icon, an ethereal and mystical "
            "fragment of a star safely secured inside an ornate, man-made brass containment "
            "lantern or vial, giving off an otherworldly glow, not star-shaped, "
            "pixel art style, iridescent gold and white palette, uniform flat fuchsia/magenta screen background, "
            "centered, clean edges, no text, no frame, game asset"
        ),
    },
    {
        "id": "srg_meteor_core",
        "preview_size": (48, 48),
        "prompt": (
            "2D top-down RPG game item icon, a glowing, irregularly shaped meteorite core "
            "half-buried in cracked scorched earth, not a perfect sphere, cracks revealing "
            "molten cosmic energy inside, dark iron and glowing orange-red palette, "
            "pixel art style, uniform flat fuchsia/magenta screen background, centered, clean edges, no text, "
            "no frame, game asset"
        ),
    },
    {
        "id": "srg_debris",
        "preview_size": (48, 48),
        "prompt": (
            "2D top-down RPG game rubble icon, a blackened scorch mark on the ground "
            "with glowing embers and scattered ash radiating outward, completely flat, "
            "not a spherical pile, pixel art style, charcoal grey and faint orange palette, "
            "uniform flat fuchsia/magenta screen background, centered, clean edges, no text, no frame, game asset"
        ),
    },
    {
        "id": "srg_weave_stars",
        "preview_size": (48, 48),
        "prompt": (
            "2D top-down RPG game enchantment icon, a folded piece of shimmering "
            "star-fabric imbued with captured starlight, constellation patterns woven "
            "into gossamer cloth, pixel art style, deep indigo and bright star-white "
            "palette, uniform flat fuchsia/magenta screen background, centered, clean edges, no text, "
            "no frame, game asset"
        ),
    },
    {
        "id": "srg_starforge",
        "preview_size": (48, 48),
        "prompt": (
            "2D top-down RPG game enchantment icon, a bright shard of crystallized "
            "stellar fire shaped like a small forge spark, radiating intense "
            "golden-white energy, pixel art style, blazing gold and white palette, "
            "uniform flat fuchsia/magenta screen background, centered, clean edges, no text, no frame, "
            "game asset"
        ),
    },
    {
        "id": "srg_codex",
        "preview_size": (64, 64),
        "prompt": (
            "2D isometric RPG game furniture sprite, three-quarter top-down view "
            "angled from upper-right, a large ornate open book on a wooden lectern "
            "with pages covered in constellation maps and glowing astronomical "
            "diagrams, brass astrolabe beside it, showing top face and front-left "
            "face, pixel art style, dark leather brown and glowing gold palette, "
            "uniform flat fuchsia/magenta screen background, centered, clean edges, "
            "no text, no frame, game asset"
        ),
    },
    {
        "id": "srg_scroll_twilight",
        "preview_size": (48, 48),
        "prompt": (
            "2D top-down RPG game scroll icon, a rolled magical scroll with deep "
            "midnight blue paper, sealed with a silver wax seal shaped like a "
            "crescent moon, faint purple glow emanating from within, pixel art "
            "style, deep blue and silver palette, uniform flat fuchsia/magenta screen background, centered, "
            "clean edges, no text, no frame, game asset"
        ),
    },
    {
        "id": "srg_scroll_radiance",
        "preview_size": (48, 48),
        "prompt": (
            "2D top-down RPG game scroll icon, a rolled magical scroll with radiant "
            "golden-white paper, sealed with a sun-shaped golden wax seal, warm "
            "glowing light emanating from within, pixel art style, gold and warm "
            "white palette, uniform flat fuchsia/magenta screen background, centered, clean edges, no text, "
            "no frame, game asset"
        ),
    },
    {
        "id": "srg_scroll_abyss",
        "preview_size": (48, 48),
        "prompt": (
            "2D top-down RPG game scroll icon, a rolled magical scroll with dark "
            "void-black paper that seems to absorb light, sealed with an obsidian "
            "seal, tendrils of dark purple energy leaking out, pixel art style, "
            "void black and dark purple palette, uniform flat fuchsia/magenta screen background, centered, "
            "clean edges, no text, no frame, game asset"
        ),
    },
    {
        "id": "srg_scroll_nova",
        "preview_size": (48, 48),
        "prompt": (
            "2D top-down RPG game scroll icon, a rolled magical scroll with "
            "shimmering chromatic paper that shifts between colors, sealed with a "
            "starburst seal, prismatic sparks emanating from it, pixel art style, "
            "rainbow chromatic palette, uniform flat fuchsia/magenta screen background, centered, clean "
            "edges, no text, no frame, game asset"
        ),
    },
    {
        "id": "srg_scroll_convergence",
        "preview_size": (48, 48),
        "prompt": (
            "2D top-down RPG game scroll icon, a rolled magical scroll with pale "
            "silver paper covered in tiny glowing runes, sealed with a "
            "star-constellation wax seal, soft celestial glow, pixel art style, "
            "silver and soft blue palette, uniform flat fuchsia/magenta screen background, centered, "
            "clean edges, no text, no frame, game asset"
        ),
    },
    {
        "id": "srg_starchart",
        "preview_size": (48, 48),
        "prompt": (
            "2D isometric RPG game item sprite, three-quarter top-down view angled "
            "from upper-right, a small unfurled parchment map lying on a surface "
            "showing hand-drawn constellations and glowing star markers, compass "
            "rose in corner, pixel art style, parchment tan and glowing blue-gold "
            "palette, uniform flat fuchsia/magenta screen background, centered, "
            "clean edges, no text, no frame, game asset"
        ),
    },
    {
        "id": "srg_astral_portal",
        "preview_size": (128, 160),
        "prompt": (
            "2D front-view RPG game portal sprite, a tall swirling rift torn in "
            "reality revealing a cosmic starscape beyond, framed by floating stone "
            "fragments, spiraling blue-silver energy vortex, pixel art style, "
            "deep blue and bright silver palette, uniform flat fuchsia/magenta screen background, centered, "
            "clean edges, no text, no frame, game asset"
        ),
    },
    # ── Furniture assets (isometric ¾-view matching Elin's camera) ──────
    {
        "id": "srg_aurora_lamp",
        "preview_size": (48, 48),
        "prompt": (
            "2D isometric RPG game furniture sprite, three-quarter top-down view "
            "angled from upper-right, a small decorative glass lamp on a thin "
            "metal stand, glowing with soft aurora-like green-blue-purple shimmering "
            "light, meteorite fragment visible inside the glass globe, showing top "
            "and front-left faces, pixel art style, blue-green and purple palette, "
            "uniform flat fuchsia/magenta screen background, centered, clean edges, "
            "no text, no frame, game asset"
        ),
    },
    {
        "id": "srg_ladder_plaque",
        "preview_size": (64, 64),
        "prompt": (
            "2D isometric RPG game furniture sprite, three-quarter top-down view "
            "angled from upper-right, a compact freestanding guild notice board "
            "for a leaderboard, muted dark wood frame with worn brass trim, short "
            "wooden legs aligned to an isometric floor grid, angled board face with "
            "a tiny engraved star crest and subtle ladder motif, readable as an "
            "in-world object rather than a UI panel, no bright colored bars, no "
            "front-facing rectangle, pixel art style, dark teal parchment and muted "
            "gold-brown palette, uniform flat fuchsia/magenta screen background, "
            "centered with transparent-safe padding, clean edges, no text, no frame, "
            "game asset"
        ),
    },
    {
        "id": "srg_constellation_rug",
        "preview_size": (48, 48),
        "prompt": (
            "2D isometric RPG game furniture sprite, three-quarter top-down view "
            "angled from upper-right, a diamond-shaped woven rug lying flat on the "
            "floor in deep indigo and midnight blue with thin gold constellation "
            "line patterns connecting small star dots, isometric floor textile, "
            "pixel art style, indigo and gold palette, uniform flat fuchsia/magenta "
            "screen background, centered, clean edges, no text, no frame, game asset"
        ),
    },
    {
        "id": "srg_starfall_table",
        "preview_size": (48, 48),
        "prompt": (
            "2D isometric RPG game furniture sprite, three-quarter top-down view "
            "angled from upper-right, a sturdy wooden table with dark oak legs, "
            "polished tabletop inlaid with glowing meteorite fragments in a star "
            "pattern, showing top surface and front-left legs, pixel art style, "
            "dark wood and cosmic purple palette, uniform flat fuchsia/magenta "
            "screen background, centered, clean edges, no text, no frame, game asset"
        ),
    },
    {
        "id": "srg_lunar_armchair",
        "preview_size": (48, 48),
        "prompt": (
            "2D isometric RPG game furniture sprite, three-quarter top-down view "
            "angled from upper-right, a plush upholstered armchair with dark blue "
            "star-patterned fabric, silver crescent moon motif on the high backrest, "
            "wooden legs visible, showing seat cushion top and front-left armrest, "
            "pixel art style, dark blue and silver palette, uniform flat "
            "fuchsia/magenta screen background, centered, clean edges, no text, "
            "no frame, game asset"
        ),
    },
    {
        "id": "srg_celestial_globe",
        "preview_size": (48, 48),
        "prompt": (
            "2D isometric RPG game furniture sprite, three-quarter top-down view "
            "angled from upper-right, a translucent glass sphere on a polished "
            "brass tripod stand, glowing constellation lines visible inside the "
            "sphere, tiny stars dotted throughout, pixel art style, brass gold and "
            "glowing blue palette, uniform flat fuchsia/magenta screen background, "
            "centered, clean edges, no text, no frame, game asset"
        ),
    },
    {
        "id": "srg_zodiac_dresser",
        "preview_size": (48, 48),
        "prompt": (
            "2D isometric RPG game furniture sprite, three-quarter top-down view "
            "angled from upper-right, a wooden dresser with four drawers, each "
            "drawer front carved with zodiac constellation symbols, golden drawer "
            "handles, dark oak wood, showing top surface and front-left drawer "
            "face, pixel art style, dark wood and gold accent palette, uniform flat "
            "fuchsia/magenta screen background, centered, clean edges, no text, "
            "no frame, game asset"
        ),
    },
    {
        "id": "srg_cosmic_mirror",
        "preview_size": (48, 48),
        "prompt": (
            "2D isometric RPG game furniture sprite, three-quarter top-down view "
            "angled from upper-right, a tall standing mirror with an ornate silver "
            "frame shaped like crescent moons, the mirror surface shows a rippling "
            "starfield reflection, viewed at an angle showing frame depth and "
            "reflective face, pixel art style, silver and deep blue palette, "
            "uniform flat fuchsia/magenta screen background, centered, clean edges, "
            "no text, no frame, game asset"
        ),
    },
    {
        "id": "srg_planisphere_cabinet",
        "preview_size": (64, 64),
        "prompt": (
            "2D isometric RPG game furniture sprite, three-quarter top-down view "
            "angled from upper-right, a tall wooden bookshelf-cabinet with glass-paned "
            "doors, star charts and scrolls visible on shelves inside, brass "
            "planisphere mechanism mounted on top, showing top surface and front-left "
            "face with shelves, pixel art style, warm oak and brass palette, uniform "
            "flat fuchsia/magenta screen background, centered, clean edges, no text, "
            "no frame, game asset"
        ),
    },
    {
        "id": "srg_stardust_bed",
        "preview_size": (48, 48),
        "prompt": (
            "2D isometric RPG game furniture sprite, three-quarter top-down view "
            "angled from upper-right, an ornate bed with dark wooden frame, blue "
            "and silver sheets that shimmer with tiny star sparkles, showing the "
            "mattress top and headboard from an angle, pillow visible, pixel art "
            "style, dark wood and blue-silver palette, uniform flat fuchsia/magenta "
            "screen background, centered, clean edges, no text, no frame, game asset"
        ),
    },
    {
        "id": "srg_astral_chandelier",
        "preview_size": (48, 48),
        "prompt": (
            "2D isometric RPG game furniture sprite, three-quarter top-down view "
            "angled from upper-right, a floating chandelier made of interlocking "
            "crystal prisms and gold metalwork, each crystal emitting soft starlight "
            "glow, chains leading upward, viewed from slightly below at isometric "
            "angle, pixel art style, gold and glowing crystal white palette, uniform "
            "flat fuchsia/magenta screen background, centered, clean edges, no text, "
            "no frame, game asset"
        ),
    },
    {
        "id": "srg_meteorite_statue",
        "preview_size": (64, 96),
        "prompt": (
            "2D isometric RPG game tall furniture sprite, three-quarter top-down "
            "view angled from upper-right, a humanoid figure carved from dark "
            "meteorite stone on a stone pedestal, cosmic cracks running through "
            "the body glowing with blue-white captured starlight, dignified pose, "
            "showing front-left face and depth, pixel art style, dark grey stone "
            "and glowing blue-white palette, uniform flat fuchsia/magenta screen "
            "background, centered, clean edges, no text, no frame, game asset"
        ),
    },
    {
        "id": "srg_eclipse_hearth",
        "preview_size": (48, 48),
        "prompt": (
            "2D isometric RPG game furniture sprite, three-quarter top-down view "
            "angled from upper-right, a grand stone fireplace with a dark eclipse "
            "sun-and-moon motif carved above the mantle, glowing embers and "
            "flickering flame inside the firebox, heavy granite construction, "
            "showing mantle top and firebox opening from an angle, pixel art style, "
            "dark stone grey and warm ember orange palette, uniform flat "
            "fuchsia/magenta screen background, centered, clean edges, no text, "
            "no frame, game asset"
        ),
    },
]

CHARA_ASSETS = [
    {
        "id": "srg_arkyn",
        "preview_size": (64, 96),
        "prompt": (
            "2D front-facing RPG character sprite, an eccentric elven scholar with "
            "wild silver hair, wearing dark blue star-embroidered robes, holding a "
            "brass telescope, bright curious eyes, cosmic motifs on clothing, "
            "fantasy pixel art style, standing pose, uniform flat fuchsia/magenta screen background, "
            "centered, clean edges, no text, game character sprite"
        ),
    },
    {
        "id": "srg_archivist",
        "preview_size": (64, 96),
        "prompt": (
            "2D front-facing RPG character sprite, a serene mystical elven sage "
            "wearing flowing celestial white-and-gold robes, holding an unfurled "
            "star map that glows softly, wise and calm expression, ethereal "
            "appearance, fantasy pixel art style, standing pose, uniform flat fuchsia/magenta screen "
            "background, centered, clean edges, no text, game character sprite"
        ),
    },
    {
        "id": "srg_growth",
        "preview_size": (64, 96),
        "prompt": (
            "2D front-facing RPG monster sprite, an intermediate alien creature "
            "between hound and humanoid, semi-translucent cosmic flesh with visible "
            "constellation patterns beneath the skin, multiple vestigial limbs, "
            "unsettling and otherworldly, Lovecraftian horror style, fantasy pixel "
            "art, uniform flat fuchsia/magenta screen background, centered, clean edges, no text, "
            "game monster sprite"
        ),
    },
    {
        "id": "srg_yith_hound",
        "preview_size": (48, 64),
        "prompt": (
            "2D front-facing RPG monster sprite, a small alien hound-like creature "
            "with dark iridescent skin, three glowing purple eyes, tentacle-like "
            "whiskers, low to the ground stalking pose, cosmic horror aesthetic, "
            "fantasy pixel art style, uniform flat fuchsia/magenta screen background, centered, clean edges, "
            "no text, game monster sprite"
        ),
    },
    {
        "id": "srg_yith_drone",
        "preview_size": (48, 64),
        "prompt": (
            "2D front-facing RPG monster sprite, a hovering alien drone organism "
            "with a bulbous translucent head revealing a pulsing brain, spindly "
            "limbs, bioluminescent nerve-green markings, insectoid features, "
            "cosmic horror aesthetic, fantasy pixel art style, uniform flat fuchsia/magenta screen "
            "background, centered, clean edges, no text, game monster sprite"
        ),
    },
    {
        "id": "srg_yith_weaver",
        "preview_size": (128, 192),
        "prompt": (
            "2D front-facing RPG monster sprite, a large arachnid-like cosmic "
            "horror with multiple segmented legs, weaving strands of chaotic energy "
            "between its claws, shifting iridescent carapace, multiple compound "
            "eyes glowing with chaos energy, fantasy pixel art style, uniform flat fuchsia/magenta screen "
            "background, centered, clean edges, no text, game monster sprite"
        ),
    },
    {
        "id": "srg_yith_ancient",
        "preview_size": (128, 192),
        "prompt": (
            "2D front-facing RPG monster sprite, a massive ancient Lovecraftian "
            "entity with a towering hunched form, tattered cosmic robes revealing "
            "void-dark flesh, crown of broken stars floating above its head, hollow "
            "glowing eye sockets, radiating darkness, fantasy pixel art style, "
            "uniform flat fuchsia/magenta screen background, centered, clean edges, no text, game monster sprite"
        ),
    },
    {
        "id": "srg_yith_behemoth",
        "preview_size": (128, 192),
        "prompt": (
            "2D front-facing RPG monster sprite, an enormous eldritch behemoth "
            "with a mountain-like body covered in eyes and mouths, writhing "
            "tentacles, chaotic energy crackling across its surface, reality "
            "distorting around it, ultimate cosmic horror, fantasy pixel art style, "
            "uniform flat fuchsia/magenta screen background, centered, clean edges, no text, game monster sprite"
        ),
    },
    {
        "id": "srg_umbryon",
        "preview_size": (128, 192),
        "prompt": (
            "2D front-facing RPG boss monster sprite, an undead lich wreathed in "
            "dark necrotic energy, skeletal face with glowing green eye sockets, "
            "wearing tattered royal robes, surrounded by swirling darkness and "
            "decay, fantasy pixel art style, uniform flat fuchsia/magenta screen background, centered, "
            "clean edges, no text, game boss sprite"
        ),
    },
    {
        "id": "srg_solaris",
        "preview_size": (128, 192),
        "prompt": (
            "2D front-facing RPG boss monster sprite, a blazing fire spirit entity "
            "made of living solar flame, humanoid form composed entirely of swirling "
            "fire and plasma, corona of solar flares around its head, molten golden "
            "eyes, fantasy pixel art style, uniform flat fuchsia/magenta screen background, centered, "
            "clean edges, no text, game boss sprite"
        ),
    },
    {
        "id": "srg_erevor",
        "preview_size": (128, 192),
        "prompt": (
            "2D front-facing RPG boss monster sprite, a colossal dragon-like "
            "abyssal creature with void-black scales, massive jaws that warp "
            "gravity around them, floating debris orbiting its body, cosmic nebula "
            "patterns visible in its wingspan, isolated full-body dragon boss sprite, "
            "complete silhouette, wings and lower body visible, not cropped, fantasy "
            "pixel art style, uniform flat fuchsia/magenta screen background only, no white backdrop, no scene, "
            "no floor, no logo, no words, no letters, no UI, no banner, no poster, "
            "centered, clean edges, no text, game boss sprite"
        ),
    },
    {
        "id": "srg_quasarix",
        "preview_size": (128, 192),
        "prompt": (
            "2D front-facing RPG boss monster sprite, a divine cosmic entity that "
            "devours light, a tall elegant figure whose body is a living black hole "
            "silhouette outlined by brilliant accretion disc light, tendrils of "
            "consumed starlight trailing from its form, fantasy pixel art style, "
            "uniform flat fuchsia/magenta screen background, centered, clean edges, no text, game boss sprite"
        ),
    },
]

# ---------------------------------------------------------------------------
# Image processing
# ---------------------------------------------------------------------------

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
    """Remove only edge-connected chroma-key background pixels."""
    img = img.convert("RGBA")
    pixels = img.load()
    w, h = img.size

    queue = deque()
    visited = set()

    def enqueue_if_key(x: int, y: int):
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

    # After the edge flood-fill, remove exact-ish magenta islands left behind by
    # antialiasing or disconnected background speckles. The sprite contract bans
    # magenta, and this does not touch black outlines or dark sprite detail.
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


def validate_raw_generation(img: Image.Image, asset_id: str):
    """Reject generations that violate the chroma-key source-background contract."""
    img = img.convert("RGBA")
    border_width = max(1, min(img.size) // 32)
    samples = list(border_pixels(img, border_width))
    if not samples:
        raise ValueError(f"{asset_id} produced an empty image")

    key_count = sum(1 for pixel in samples if is_key_color(pixel))
    key_ratio = key_count / len(samples)
    if key_ratio < BORDER_KEY_MIN_RATIO:
        raise ValueError(
            f"{asset_id} raw border is only {key_ratio:.1%} chroma-key colored; "
            "expected isolated sprite on pure magenta #ff00ff background"
        )

    for index, samples in enumerate(corner_pixels(img), 1):
        key_count = sum(1 for pixel in samples if is_key_color(pixel))
        key_ratio = key_count / len(samples)
        if key_ratio < CORNER_KEY_MIN_RATIO:
            raise ValueError(
                f"{asset_id} raw corner {index} is only {key_ratio:.1%} chroma-key colored; "
                "expected mostly pure magenta #ff00ff corners"
            )


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


def validate_nobg_generation(img: Image.Image, asset_id: str, category: str):
    """Reject removed-background images that still look like a boxed rectangle."""
    img = img.convert("RGBA")
    bbox = img.getchannel("A").getbbox()
    if not bbox:
        raise ValueError(f"{asset_id} has no opaque pixels after chroma-key removal")
    if bbox == (0, 0, img.width, img.height):
        raise ValueError(f"{asset_id} alpha bbox covers the full image after chroma-key removal")

    corner_alpha = list(corner_alpha_pixels(img))
    opaque_corner_count = sum(1 for alpha in corner_alpha if alpha > 0)
    opaque_corner_ratio = opaque_corner_count / len(corner_alpha)
    if opaque_corner_ratio > MAX_OPAQUE_CORNER_RATIO_AFTER_REMOVAL:
        raise ValueError(
            f"{asset_id} still has {opaque_corner_ratio:.1%} opaque corner pixels "
            "after chroma-key removal"
        )

    opaque_pixels = [pixel for pixel in img.getdata() if pixel[3] > 0]
    residual_key_count = sum(1 for pixel in opaque_pixels if is_strict_key_color(pixel))
    residual_key_ratio = residual_key_count / len(opaque_pixels)
    if residual_key_ratio > MAX_RESIDUAL_KEY_OPAQUE_RATIO:
        raise ValueError(
            f"{asset_id} still has {residual_key_ratio:.2%} opaque chroma-key pixels "
            "after edge-connected chroma-key removal"
        )

    if category == "chara":
        bbox_w = bbox[2] - bbox[0]
        bbox_h = bbox[3] - bbox[1]
        height_ratio = bbox_h / img.height
        width_ratio = bbox_w / img.width
        if width_ratio > 0.90 and height_ratio < CHARA_MIN_BBOX_HEIGHT_RATIO:
            raise ValueError(
                f"{asset_id} chara crop is banner-like after chroma-key removal "
                f"({bbox_w}x{bbox_h} in {img.width}x{img.height}); expected full-body sprite"
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


def save_variants(raw_bytes: bytes, asset_id: str, output_dir: Path,
                  preview_size: tuple[int, int], category: str) -> dict:
    """Save raw, preview, and no-background variants. Returns file paths."""
    img = Image.open(io.BytesIO(raw_bytes)).convert("RGBA")
    validate_raw_generation(img, asset_id)

    nobg = remove_chroma_key_background(img)
    validate_nobg_generation(nobg, asset_id, category)

    raw_path = output_dir / f"{asset_id}_raw.png"
    img.save(raw_path)

    preview = img.resize(preview_size, Image.Resampling.LANCZOS)
    preview_path = output_dir / f"{asset_id}_preview.png"
    preview.save(preview_path)

    nobg_path = output_dir / f"{asset_id}_nobg.png"
    nobg.save(nobg_path)

    return {
        "raw": str(raw_path),
        "preview": str(preview_path),
        "nobg": str(nobg_path),
        "raw_size": img.size,
        "preview_size": preview_size,
    }


# ---------------------------------------------------------------------------
# HTML gallery generator
# ---------------------------------------------------------------------------

def generate_gallery(log_entries: list[dict], output_dir: Path):
    """Write an index.html review gallery."""
    html_parts = [
        "<!DOCTYPE html>",
        "<html lang='en'><head><meta charset='utf-8'>",
        "<title>Skyreader Guild Asset Review</title>",
        "<style>",
        "  body { background: #1a1a2e; color: #eee; font-family: 'Segoe UI', sans-serif; padding: 20px; }",
        "  h1 { color: #e0c097; text-align: center; }",
        "  .grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(320px, 1fr)); gap: 20px; }",
        "  .card { background: #16213e; border-radius: 12px; padding: 16px; border: 1px solid #0f3460; }",
        "  .card h3 { color: #e0c097; margin: 0 0 8px 0; font-size: 14px; }",
        "  .card .type { color: #888; font-size: 11px; margin-bottom: 8px; }",
        "  .images { display: flex; gap: 12px; align-items: flex-end; margin: 12px 0; }",
        "  .images img { border: 1px solid #333; border-radius: 4px; background: #000; }",
        "  .preview-img { width: 96px; height: 96px; object-fit: contain; image-rendering: pixelated; }",
        "  .raw-link { font-size: 11px; color: #53a8b6; }",
        "  .prompt { font-size: 11px; color: #999; margin-top: 8px; word-break: break-word; }",
        "  .status-ok { color: #4caf50; } .status-fail { color: #f44336; }",
        "  h2 { color: #c9a96e; border-bottom: 1px solid #333; padding-bottom: 8px; margin-top: 40px; }",
        "</style></head><body>",
        "<h1>🌟 Skyreader Guild — Asset Review Gallery</h1>",
        f"<p style='text-align:center;color:#888;'>Generated {datetime.now().strftime('%Y-%m-%d %H:%M')}</p>",
    ]

    # Group by category
    items = [e for e in log_entries if e.get("category") == "item"]
    charas = [e for e in log_entries if e.get("category") == "chara"]

    for section_name, entries in [("Items & Objects", items), ("Characters & Monsters", charas)]:
        html_parts.append(f"<h2>{section_name}</h2>")
        html_parts.append("<div class='grid'>")
        for e in entries:
            status_cls = "status-ok" if e.get("success") else "status-fail"
            status_txt = "✓ Generated" if e.get("success") else f"✗ Failed: {e.get('error', 'unknown')}"
            preview_src = Path(e["files"]["preview"]).name if e.get("success") and e.get("files") else ""
            raw_src = Path(e["files"]["raw"]).name if e.get("success") and e.get("files") else ""
            subfolder = "items" if e["category"] == "item" else "charas"

            html_parts.append(f"""<div class='card'>
  <h3>{e['id']}</h3>
  <div class='type'>Preview: {e.get('preview_size', '?')} | Deploy: {e.get('canvas_size', '?')} / visible {e.get('visible_size', '?')}</div>
  <span class='{status_cls}'>{status_txt}</span>
  {"<div class='images'>" + f"<img class='preview-img' src='{subfolder}/{preview_src}' alt='preview'>" + f" <a class='raw-link' href='{subfolder}/{raw_src}' target='_blank'>View Raw</a>" + "</div>" if e.get('success') else ""}
  <div class='prompt'><strong>Prompt:</strong> {e.get('prompt', '?')[:200]}...</div>
</div>""")
        html_parts.append("</div>")

    html_parts.append("</body></html>")

    gallery_path = output_dir / "index.html"
    gallery_path.write_text("\n".join(html_parts), encoding="utf-8")
    print(f"\n📄 Gallery written to {gallery_path}")


# ---------------------------------------------------------------------------
# Generation logic
# ---------------------------------------------------------------------------

def generate_one(client, model: str, asset: dict, output_dir: Path,
                 category: str) -> dict:
    """Generate a single asset. Returns a log entry dict."""
    from google.genai import types

    asset_id = asset["id"]
    prompt = constrained_prompt(asset["prompt"], category)
    preview_size = asset["preview_size"]
    scale_spec = ASSET_SCALE_SPECS.get(asset_id)

    entry = {
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
    if scale_spec:
        if scale_spec.category != category:
            raise ValueError(
                f"{asset_id} manifest category {category!r} does not match "
                f"deployment spec category {scale_spec.category!r}"
            )
        entry.update(scale_spec.to_log_fields())
        entry["aspect_ratio"] = aspect_ratio_for_size(scale_spec.canvas_size)
    elif not asset_id.startswith("test_"):
        raise ValueError(f"No deployment scale spec defined for {asset_id}")
    else:
        entry["aspect_ratio"] = aspect_ratio_for_size(preview_size)

    for attempt in range(1, MAX_RETRIES + 1):
        try:
            print(f"  [{attempt}/{MAX_RETRIES}] Generating {asset_id}...")
            config = types.GenerateContentConfig(
                response_modalities=["IMAGE"],
                image_config=types.ImageConfig(
                    aspect_ratio=entry["aspect_ratio"],
                ),
            )
            response = client.models.generate_content(
                model=model,
                contents=prompt,
                config=config,
            )

            # Extract image from response
            if not response.candidates:
                raise RuntimeError("No candidates in response")

            image_data = None
            for part in response.candidates[0].content.parts:
                if part.inline_data is not None:
                    image_data = part.inline_data.data
                    break

            if image_data is None:
                # Check if there's text explaining a refusal
                texts = [p.text for p in response.candidates[0].content.parts if p.text]
                if texts:
                    raise RuntimeError(f"Model returned text instead of image: {texts[0][:200]}")
                raise RuntimeError("No inline_data found in response parts")

            files = save_variants(image_data, asset_id, output_dir, preview_size, category)
            entry["success"] = True
            entry["error"] = None
            entry["files"] = files
            print(f"  ✓ {asset_id} — raw {files['raw_size']}, preview {preview_size}")
            return entry

        except Exception as exc:
            err_str = str(exc)
            print(f"  ✗ Attempt {attempt} failed: {err_str[:150]}")
            entry["error"] = err_str

            if attempt < MAX_RETRIES:
                # Check for rate limiting
                if "429" in err_str or "rate" in err_str.lower() or "quota" in err_str.lower():
                    wait = RETRY_BACKOFF_BASE * (2 ** (attempt - 1))
                    print(f"    Rate limited. Waiting {wait}s...")
                    time.sleep(wait)
                else:
                    time.sleep(DELAY_BETWEEN_CALLS)

    return entry


def run_generation(only_ids: list[str] | None = None, model: str = DEFAULT_MODEL,
                   test_mode: bool = False):
    """Main generation loop."""
    # Load API key
    load_dotenv(ENV_PATH)
    api_key = os.environ.get("GEMINI_API_KEY")
    if not api_key:
        print(f"ERROR: GEMINI_API_KEY not found. Checked {ENV_PATH}")
        sys.exit(1)
    print(f"✓ API key loaded from {ENV_PATH}")

    # Import and init client
    from google import genai
    client = genai.Client(api_key=api_key)
    print(f"✓ Gemini client initialized, model={model}")

    # Create output dirs
    ITEMS_DIR.mkdir(parents=True, exist_ok=True)
    CHARAS_DIR.mkdir(parents=True, exist_ok=True)

    # If test mode, just do one quick generation
    if test_mode:
        print("\n🧪 TEST MODE — generating one test image...")
        test_asset = {
            "id": "test_api",
            "preview_size": (48, 48),
            "prompt": "2D top-down RPG game item icon, a glowing blue crystal, pixel art style, uniform flat fuchsia/magenta screen background, centered, clean edges, no text, game asset",
        }
        result = generate_one(client, model, test_asset, ITEMS_DIR, "item")
        if result["success"]:
            print("✓ API test PASSED. You're good to run the full generation.")
        else:
            print(f"✗ API test FAILED: {result['error']}")
        return [result]

    # Build work list
    all_items = ITEM_ASSETS
    all_charas = CHARA_ASSETS

    if only_ids:
        id_set = set(only_ids)
        all_items = [a for a in all_items if a["id"] in id_set]
        all_charas = [a for a in all_charas if a["id"] in id_set]
        found = {a["id"] for a in all_items + all_charas}
        missing = id_set - found
        if missing:
            print(f"WARNING: IDs not found in manifest: {missing}")

    total = len(all_items) + len(all_charas)
    print(f"\n{'='*60}")
    print(f"Generating {total} assets ({len(all_items)} items, {len(all_charas)} charas)")
    print(f"{'='*60}\n")

    log_entries = []

    # Load existing log to preserve prior results when doing partial runs
    log_path = OUTPUT_DIR / "generation_log.json"
    existing_log = {}
    if log_path.exists() and only_ids:
        try:
            prior = json.loads(log_path.read_text(encoding="utf-8"))
            for e in prior:
                existing_log[e["id"]] = e
        except Exception:
            pass

    # Generate items
    print("━━━ Items & Objects ━━━")
    for i, asset in enumerate(all_items, 1):
        print(f"\n[{i}/{len(all_items)}] {asset['id']}")
        result = generate_one(client, model, asset, ITEMS_DIR, "item")
        log_entries.append(result)
        existing_log[result["id"]] = result
        if i < len(all_items):
            time.sleep(DELAY_BETWEEN_CALLS)

    # Generate characters
    print("\n━━━ Characters & Monsters ━━━")
    for i, asset in enumerate(all_charas, 1):
        print(f"\n[{i}/{len(all_charas)}] {asset['id']}")
        result = generate_one(client, model, asset, CHARAS_DIR, "chara")
        log_entries.append(result)
        existing_log[result["id"]] = result
        if i < len(all_charas):
            time.sleep(DELAY_BETWEEN_CALLS)

    # Write log
    all_entries = list(existing_log.values())
    log_path.write_text(
        json.dumps(all_entries, indent=2, ensure_ascii=False),
        encoding="utf-8",
    )
    print(f"\n📝 Log written to {log_path}")

    # Generate gallery
    generate_gallery(all_entries, OUTPUT_DIR)

    # Summary
    succeeded = sum(1 for e in log_entries if e["success"])
    failed = sum(1 for e in log_entries if not e["success"])
    print(f"\n{'='*60}")
    print(f"DONE: {succeeded} succeeded, {failed} failed out of {total}")
    if failed:
        print("Failed assets:")
        for e in log_entries:
            if not e["success"]:
                print(f"  - {e['id']}: {e['error'][:100]}")
    print(f"{'='*60}")
    print(f"\nReview gallery: {OUTPUT_DIR / 'index.html'}")

    return log_entries


# ---------------------------------------------------------------------------
# CLI
# ---------------------------------------------------------------------------

def main():
    parser = argparse.ArgumentParser(description="Generate Skyreader Guild assets")
    parser.add_argument("--only", nargs="+", help="Only generate these asset IDs")
    parser.add_argument("--model", default=DEFAULT_MODEL, help=f"Gemini model (default: {DEFAULT_MODEL})")
    parser.add_argument("--test", action="store_true", help="Quick API connectivity test")
    args = parser.parse_args()

    run_generation(only_ids=args.only, model=args.model, test_mode=args.test)


if __name__ == "__main__":
    main()
