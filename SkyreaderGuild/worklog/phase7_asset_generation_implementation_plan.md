# Skyreader Guild Asset Generation & Integration

Generate custom sprite PNGs for all Skyreader Guild items, NPCs, and monsters using the Gemini API (Nano Banana), then integrate approved assets into the deployed mod.

## User Review Required

> [!IMPORTANT]
> **Elin texture sizes are NOT rigid grids.** Research into `D:\Steam\...\Package\_Elona\Texture\Item\` reveals assets range from 32×32 (`drawing_paper.png`) to 800×723 (`tree_cherry.png`). The engine scales based on `_idRenderData`, so we should generate at a **comfortable working resolution** (e.g. 256×256 for items, 256×384 for characters) rather than downscaling to 32×32 which destroys detail. The final images can be refined during review.

> [!WARNING]
> **Character sprites are single PNGs, not sprite sheets.** Elin's custom texture system (per the modding docs) expects a single `id.png` file per character — not an RPG Maker-style multi-frame sheet. The `variation.md` docs show gender skinsets via `id_skin0.png`/`id_skin1.png`, but the base case is one static front-facing image. The asset_listing's references to "sprite sheets" and "32×48 per frame" are incorrect for this mod's needs.

> [!IMPORTANT]
> **Prompt quality.** The existing prompts in asset_listing.md are generic descriptions. For Nano Banana to produce useful pixel-art-adjacent sprites, prompts need to specify: art style (top-down 2D RPG item icon), background (transparent/solid color we can chroma-key), color palette (cosmic purples, gold, silver), and avoid photorealism. See the improved prompts below.

---

## Phase A: Asset Generation Script

Generate all assets to `worklog/generated_assets/` for manual review before touching the mod.

### A.1 — Prerequisites

#### [NEW] [requirements.txt](file:///c:/Users/someuser/Documents/ElinMods/SkyreaderGuild/requirements.txt)
```
google-genai
Pillow
python-dotenv
```
- Install: `pip install -r requirements.txt`
- The `.env.local` at `c:\Users\someuser\Documents\ElinMods\.env.local` already contains `GEMINI_API_KEY`

---

### A.2 — Generation Script

#### [NEW] [generate_assets.py](file:///c:/Users/someuser/Documents/ElinMods/SkyreaderGuild/generate_assets.py)

**Architecture:**
1. Load `GEMINI_API_KEY` from `../.env.local` via `python-dotenv`
2. Define asset manifest with improved prompts, target sizes, and output subfolder
3. Call `client.models.generate_content()` with `response_modalities=["IMAGE"]` on model `gemini-2.0-flash`
4. Save raw generated images to `worklog/generated_assets/items/` and `worklog/generated_assets/charas/`
5. Also save a downscaled "preview" version at the target sprite size for quick visual comparison
6. Generate an `index.html` review gallery in `worklog/generated_assets/`

**Key design decisions:**
- Generate at **1024×1024** for items (square) and **1024×1536** for characters (2:3 aspect), giving Nano Banana enough canvas to work with
- Save both the raw high-res and a resized preview
- Use `response_modalities=["IMAGE"]` (not TEXT+IMAGE) to force pure image output
- Add retry logic (up to 3 attempts) with backoff for rate limiting
- Each asset gets a descriptive filename: `srg_astral_extractor_raw.png` + `srg_astral_extractor_preview.png`

### A.3 — Improved Prompts

The prompts below are designed for Nano Banana to produce sprite-appropriate results. Each prompt follows this template:

```
"2D top-down RPG game {item_type} icon, {description}, pixel art style,
{color_palette}, solid black background, centered, clean edges,
no text, no frame, game asset"
```

#### Items (square, 1024×1024 generation → preview at target size)

| Asset ID | Target | Improved Prompt |
|---|---|---|
| `srg_astral_extractor` | 48×48 | `"2D top-down RPG game tool icon, a golden sextant-like astronomical device with crystal lenses and delicate brass gears, glowing faintly with pale blue starlight, pixel art style, gold and deep blue color palette, solid black background, centered, clean edges, no text, no frame, game asset"` |
| `srg_meteorite_fragment` | 48×48 | `"2D top-down RPG game material icon, a rough cracked piece of meteorite stone with glowing purple-blue veins of cosmic energy visible in the fractures, smoldering edges, pixel art style, dark grey and glowing purple palette, solid black background, centered, clean edges, no text, no frame, game asset"` |
| `srg_meteorite_source` | 48×48 | `"2D top-down RPG game crafting material icon, a refined and polished crystalline star fragment, prismatic and luminous with inner golden-white light, faceted like a gemstone, pixel art style, iridescent gold and white palette, solid black background, centered, clean edges, no text, no frame, game asset"` |
| `srg_astral_thread` | 48×48 | `"2D top-down RPG game material icon, a small spool of shimmering ethereal silver thread that trails wisps of starlight, cosmic silk on a tiny wooden bobbin, pixel art style, silver and pale blue palette, solid black background, centered, clean edges, no text, no frame, game asset"` |
| `srg_codex` | 64×64 | `"2D top-down RPG game furniture icon, a large ornate open book on a wooden lectern with pages covered in moving constellation maps and glowing astronomical diagrams, brass astrolabe beside it, pixel art style, dark leather brown and glowing gold palette, solid black background, centered, clean edges, no text, no frame, game asset"` |
| `srg_scroll_twilight` | 48×48 | `"2D top-down RPG game scroll icon, a rolled magical scroll with deep midnight blue paper, sealed with a silver wax seal shaped like a crescent moon, faint purple glow emanating from within, pixel art style, deep blue and silver palette, solid black background, centered, clean edges, no text, no frame, game asset"` |
| `srg_scroll_radiance` | 48×48 | `"2D top-down RPG game scroll icon, a rolled magical scroll with radiant golden-white paper, sealed with a sun-shaped golden wax seal, warm glowing light emanating from within, pixel art style, gold and warm white palette, solid black background, centered, clean edges, no text, no frame, game asset"` |
| `srg_scroll_abyss` | 48×48 | `"2D top-down RPG game scroll icon, a rolled magical scroll with dark void-black paper that seems to absorb light, sealed with an obsidian seal, tendrils of dark purple energy leaking out, pixel art style, void black and dark purple palette, solid black background, centered, clean edges, no text, no frame, game asset"` |
| `srg_scroll_nova` | 48×48 | `"2D top-down RPG game scroll icon, a rolled magical scroll with shimmering chromatic paper that shifts between colors, sealed with a starburst seal, prismatic sparks emanating from it, pixel art style, rainbow chromatic palette, solid black background, centered, clean edges, no text, no frame, game asset"` |
| `srg_scroll_convergence` | 48×48 | `"2D top-down RPG game scroll icon, a rolled magical scroll with pale silver paper covered in tiny glowing runes, sealed with a star-constellation wax seal, soft celestial glow, pixel art style, silver and soft blue palette, solid black background, centered, clean edges, no text, no frame, game asset"` |
| `srg_starchart` | 96×96 | `"2D top-down RPG game map icon, a large unfurled treasure map made of aged parchment showing hand-drawn constellations and glowing impact zones marked with red circles, compass rose in corner, pixel art style, parchment tan and glowing blue-gold palette, solid black background, centered, clean edges, no text, no frame, game asset"` |
| `srg_astral_portal` | 128×160 | `"2D front-view RPG game portal sprite, a tall swirling rift torn in reality revealing a cosmic starscape beyond, framed by floating stone fragments, spiraling blue-silver energy vortex, pixel art style, deep blue and bright silver palette, solid black background, centered, clean edges, no text, no frame, game asset"` |
| `srg_meteor_core` | 48×48 | `"2D top-down RPG game item icon, a pulsating spherical meteorite core with cracks revealing molten cosmic energy inside, surrounded by a faint heat shimmer, pixel art style, dark iron and glowing orange-red palette, solid black background, centered, clean edges, no text, no frame, game asset"` |
| `srg_debris` | 48×48 | `"2D top-down RPG game rubble icon, scattered scorched rock fragments from a meteor impact with wisps of smoke and faint ember glow, pixel art style, charcoal grey and faint orange palette, solid black background, centered, clean edges, no text, no frame, game asset"` |
| `srg_weave_stars` | 48×48 | `"2D top-down RPG game enchantment icon, a folded piece of shimmering star-fabric imbued with captured starlight, constellation patterns woven into gossamer cloth, pixel art style, deep indigo and bright star-white palette, solid black background, centered, clean edges, no text, no frame, game asset"` |
| `srg_starforge` | 48×48 | `"2D top-down RPG game enchantment icon, a bright shard of crystallized stellar fire shaped like a small forge spark, radiating intense golden-white energy, pixel art style, blazing gold and white palette, solid black background, centered, clean edges, no text, no frame, game asset"` |

#### Characters (2:3 aspect, 1024×1536 generation → preview at target size)

| Asset ID | Target | Render Data | Improved Prompt |
|---|---|---|---|
| `srg_arkyn` | 128×192 | `chara` | `"2D front-facing RPG character sprite, an eccentric elven scholar with wild silver hair, wearing dark blue star-embroidered robes, holding a brass telescope, bright curious eyes, cosmic motifs on clothing, fantasy pixel art style, standing pose, solid black background, centered, clean edges, no text, game character sprite"` |
| `srg_archivist` | 128×192 | `chara` | `"2D front-facing RPG character sprite, a serene mystical elven sage wearing flowing celestial white-and-gold robes, holding an unfurled star map that glows softly, wise and calm expression, ethereal appearance, fantasy pixel art style, standing pose, solid black background, centered, clean edges, no text, game character sprite"` |
| `srg_growth` | 128×192 | `chara_L` | `"2D front-facing RPG monster sprite, an intermediate alien creature between hound and humanoid, semi-translucent cosmic flesh with visible constellation patterns beneath the skin, multiple vestigial limbs, unsettling and otherworldly, Lovecraftian horror style, fantasy pixel art, solid black background, centered, clean edges, no text, game monster sprite"` |
| `srg_yith_hound` | 128×192 | `chara` | `"2D front-facing RPG monster sprite, a small alien hound-like creature with dark iridescent skin, three glowing purple eyes, tentacle-like whiskers, low to the ground stalking pose, cosmic horror aesthetic, fantasy pixel art style, solid black background, centered, clean edges, no text, game monster sprite"` |
| `srg_yith_drone` | 128×192 | `chara` | `"2D front-facing RPG monster sprite, a hovering alien drone organism with a bulbous translucent head revealing a pulsing brain, spindly limbs, bioluminescent nerve-green markings, insectoid features, cosmic horror aesthetic, fantasy pixel art style, solid black background, centered, clean edges, no text, game monster sprite"` |
| `srg_yith_weaver` | 128×192 | `chara_L` | `"2D front-facing RPG monster sprite, a large arachnid-like cosmic horror with multiple segmented legs, weaving strands of chaotic energy between its claws, shifting iridescent carapace, multiple compound eyes glowing with chaos energy, fantasy pixel art style, solid black background, centered, clean edges, no text, game monster sprite"` |
| `srg_yith_ancient` | 128×192 | `chara_L` | `"2D front-facing RPG monster sprite, a massive ancient Lovecraftian entity with a towering hunched form, tattered cosmic robes revealing void-dark flesh, crown of broken stars floating above its head, hollow glowing eye sockets, radiating darkness, fantasy pixel art style, solid black background, centered, clean edges, no text, game monster sprite"` |
| `srg_yith_behemoth` | 128×192 | `chara_L` | `"2D front-facing RPG monster sprite, an enormous eldritch behemoth with a mountain-like body covered in eyes and mouths, writhing tentacles, chaotic energy crackling across its surface, reality distorting around it, ultimate cosmic horror, fantasy pixel art style, solid black background, centered, clean edges, no text, game monster sprite"` |
| `srg_umbryon` | 128×192 | `chara` | `"2D front-facing RPG boss monster sprite, an undead lich wreathed in dark necrotic energy, skeletal face with glowing green eye sockets, wearing tattered royal robes, surrounded by swirling darkness and decay, fantasy pixel art style, solid black background, centered, clean edges, no text, game boss sprite"` |
| `srg_solaris` | 128×192 | `chara` | `"2D front-facing RPG boss monster sprite, a blazing fire spirit entity made of living solar flame, humanoid form composed entirely of swirling fire and plasma, corona of solar flares around its head, molten golden eyes, fantasy pixel art style, solid black background, centered, clean edges, no text, game boss sprite"` |
| `srg_erevor` | 128×192 | `chara_L` | `"2D front-facing RPG boss monster sprite, a colossal dragon-like abyssal creature with void-black scales, massive jaws that warp gravity around them, floating debris orbiting its body, cosmic nebula patterns visible in its wingspan, fantasy pixel art style, solid black background, centered, clean edges, no text, game boss sprite"` |
| `srg_quasarix` | 128×192 | `chara` | `"2D front-facing RPG boss monster sprite, a divine cosmic entity that devours light, a tall elegant figure whose body is a living black hole silhouette outlined by brilliant accretion disc light, tendrils of consumed starlight trailing from its form, fantasy pixel art style, solid black background, centered, clean edges, no text, game boss sprite"` |

---

### A.4 — Script Behavior

```
worklog/generated_assets/
├── items/
│   ├── srg_astral_extractor_raw.png      (1024×1024, as generated)
│   ├── srg_astral_extractor_preview.png   (48×48, LANCZOS downscale)
│   ├── srg_meteorite_fragment_raw.png
│   ├── srg_meteorite_fragment_preview.png
│   └── ...
├── charas/
│   ├── srg_arkyn_raw.png                  (1024×1536, as generated)
│   ├── srg_arkyn_preview.png              (128×192, LANCZOS downscale)
│   └── ...
├── index.html                             (visual review gallery)
└── generation_log.json                    (prompts used, timestamps, success/failure)
```

- **`index.html`**: A simple HTML page showing each asset as a card with: preview image, raw image link, prompt used, asset ID, and target size. Makes batch review fast.
- **`generation_log.json`**: Machine-readable record of every generation attempt, including the exact prompt sent, model response status, and file paths. Useful for re-running failed assets.
- **Rate limiting**: 15 RPM is typical for Gemini free tier. Script will sleep 5s between calls and retry up to 3× on 429 errors with exponential backoff.
- **Background removal**: Since we request solid black background, the script will also produce a `_nobg.png` variant with the black background converted to transparency (alpha channel) using a simple threshold. This is what actually gets used in-game.

---

### A.5 — Manual Review (Human Step)

> [!NOTE]
> After generation, open `worklog/generated_assets/index.html` in a browser. For each asset:
> 1. Check if the style/subject matches the intended game object
> 2. If an asset is poor, note it — you can re-run individual assets by ID
> 3. Optionally hand-edit in an image editor (GIMP/Photoshop) for touch-ups
> 4. Mark assets as approved when satisfied

The script will support a `--only <id1> <id2>` flag to regenerate specific assets without re-running the entire batch.

---

## Phase B: Mod Integration

After manual review and approval of assets, integrate them into the mod.

### B.1 — Integration Script

#### [NEW] [integrate_assets.py](file:///c:/Users/someuser/Documents/ElinMods/SkyreaderGuild/integrate_assets.py)

This script:
1. Reads approved assets from `worklog/generated_assets/`
2. Uses the `_nobg.png` versions (transparent background)
3. Resizes to the final target sprite size
4. Copies to the correct mod directories:
   - Items → `Texture/Item/{id}.png` (within the mod package at `D:\Steam\...\Package\SkyreaderGuild\Texture\Item\`)
   - Characters → `Texture/Chara/{id}.png` (`...\Texture\Chara\`)
5. Optionally generates `.pref` files for sprites that need positioning tweaks

### B.2 — SourceCard.xlsx Updates

When switching from vanilla tile IDs to custom textures, the `tiles` column in the xlsx should be updated. Per Elin modding convention, when a custom `Texture/Item/{id}.png` exists, the engine loads it by matching the `id`. The `tiles` column can remain as the **fallback** tile ID.

> [!IMPORTANT]
> No changes to `tiles` column are strictly required — Elin's texture loading prioritizes custom PNGs by `id` match over the numeric tile reference. However, we should verify this works for each `_idRenderData` type in testing.

### B.3 — Deployment Structure

After integration, the mod package at `D:\Steam\steamapps\common\Elin\Package\SkyreaderGuild\` should look like:

```
SkyreaderGuild/
├── LangMod/
│   └── EN/
│       ├── SourceCard.xlsx
│       └── SourceLocalization.json
├── Texture/
│   ├── Item/
│   │   ├── srg_astral_extractor.png
│   │   ├── srg_meteorite_fragment.png
│   │   ├── srg_meteorite_source.png
│   │   ├── srg_astral_thread.png       (→ srg_weave_stars or srg_starforge)
│   │   ├── srg_codex.png
│   │   ├── srg_scroll_twilight.png
│   │   ├── srg_scroll_radiance.png
│   │   ├── srg_scroll_abyss.png
│   │   ├── srg_scroll_nova.png
│   │   ├── srg_scroll_convergence.png
│   │   ├── srg_starchart.png
│   │   ├── srg_astral_portal.png
│   │   ├── srg_meteor_core.png
│   │   ├── srg_debris.png
│   │   ├── srg_weave_stars.png
│   │   └── srg_starforge.png
│   └── Chara/
│       ├── srg_arkyn.png
│       ├── srg_archivist.png
│       ├── srg_growth.png
│       ├── srg_yith_hound.png
│       ├── srg_yith_drone.png
│       ├── srg_yith_weaver.png
│       ├── srg_yith_ancient.png
│       ├── srg_yith_behemoth.png
│       ├── srg_umbryon.png
│       ├── srg_solaris.png
│       ├── srg_erevor.png
│       └── srg_quasarix.png
├── package.xml
├── preview.jpg
└── SkyreaderGuild.dll
```

### B.4 — Deploy Script Additions

#### [MODIFY] [add_meteor_items.py](file:///c:/Users/someuser/Documents/ElinMods/SkyreaderGuild/add_meteor_items.py)

Add a new function at the end of the existing deploy step that copies `Texture/` directories to the deployed mod package. This keeps deployment automated.

---

## Open Questions

> [!IMPORTANT]
> **Asset ID mismatch.** The asset_listing.md references IDs that don't exist in `add_meteor_items.py`:
> - `srg_astral_thread` — the actual items are `srg_weave_stars` and `srg_starforge`
> - `srg_astrological_codex` — actual ID is `srg_codex`
> - `srg_archivist_scroll` / `srg_boss_scroll` — actual IDs are `srg_scroll_twilight`, `srg_scroll_radiance`, `srg_scroll_abyss`, `srg_scroll_nova`, `srg_scroll_convergence`
> - `srg_meteorite_fragment` — not present in `EXPECTED_THINGS` (only `srg_meteorite_source` and `srg_debris`)
>
> The plan above uses the **actual IDs from `add_meteor_items.py`** as the source of truth. Should we also generate assets for the orphaned IDs from the listing, or drop them?

> [!WARNING]
> **Character sprite size.** Elin base game character textures appear to be 128×192 based on `sam_id_render_data.md` examples showing 64×64 reference images. The docs mention PCC sprites at 128×192. I've set character preview targets to 128×192 — confirm this matches your expectation, or should we go larger?

> [!IMPORTANT]
> **Nano Banana model ID.** The asset_listing references `gemini-3.1-flash-image-preview` but web research shows `gemini-2.0-flash` supports `response_modalities=["IMAGE"]` currently. We should test with a quick single-image call first to confirm the working model ID before batch generation. The script will be written to accept the model name as a configurable parameter.

---

## Verification Plan

### Automated Tests
1. Run `generate_assets.py` and confirm all files are created in `worklog/generated_assets/`
2. Verify each generated PNG has RGBA mode and non-zero dimensions
3. Verify `_nobg.png` variants have transparent pixels
4. Open `index.html` in browser to visually inspect

### Manual Verification
1. Review each asset in the gallery — approve or flag for regeneration
2. After Phase B integration, launch Elin and visit the Skyreader Guild to verify:
   - Items show custom sprites instead of vanilla tiles
   - Characters render with custom textures
   - Portal animation still works with custom sprite
   - No missing texture / fallback to default errors in logs
