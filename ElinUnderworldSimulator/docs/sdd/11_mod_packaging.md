# 11 · Mod Packaging & Asset Pipeline

> Parent: [00_overview.md](./00_overview.md) · Data Model: [03_data_model.md](./03_data_model.md)

This document specifies the complete mod packaging structure, build pipeline, automated asset generation, SourceCard management, and deployment process.

---

## 11.1 Package Structure

```
ElinUnderworldSimulator/
├── UnderworldPlugin.cs              # BepInEx entry point
├── UnderworldStartupBootstrap.cs    # Scenario setup
├── Patches/
│   ├── PatchGamePrologue.cs         # Prologue getter
│   ├── PatchSetChara.cs             # Mode list injection
│   ├── PatchListModes.cs            # Mode selection mapping
│   └── PatchStartNewGame.cs         # Bootstrap trigger
├── Traits/
│   ├── TraitMixingTable.cs          # Basic crafting station
│   ├── TraitProcessingVat.cs        # Time-delayed processing
│   ├── TraitAdvancedLab.cs          # High-tier crafting
│   ├── TraitContrabandChest.cs      # Shipping container
│   └── TraitUnderworldFixer.cs      # Fixer NPC interaction
├── Network/
│   ├── UnderworldAuthManager.cs     # Token management
│   └── UnderworldNetworkClient.cs   # HTTP client
├── Economy/
│   ├── OrderManager.cs              # Local order tracking
│   ├── ReputationTracker.cs         # Rep + rank
│   ├── HeatTracker.cs               # Territory heat cache
│   └── NerveTracker.cs              # Shadow Nerve resource
├── UI/
│   ├── UnderworldUIManager.cs       # Panel coordinator
│   ├── LayerUnderworldNetwork.cs    # Main hub panel
│   ├── MarketPanel.cs               # Available orders
│   ├── ActiveOrdersPanel.cs         # Accepted orders
│   ├── ResultsPanel.cs              # Shipment results
│   ├── TerritoryPanel.cs            # Territory overview
│   └── ProfilePanel.cs              # Player stats
├── ElinUnderworldSimulator.csproj
├── package.xml
├── LangMod/
│   └── EN/
│       ├── SourceCard.xlsx          # Thing + Chara definitions
│       └── SourceGame.xlsx          # Zone definitions (if needed)
├── Texture/
│   ├── Item/
│   │   ├── uw_mixing_table.png
│   │   ├── uw_processing_vat.png
│   │   ├── uw_advanced_lab.png
│   │   ├── uw_contraband_chest.png
│   │   ├── uw_herb_whisper.png
│   │   ├── uw_herb_dream.png
│   │   ├── uw_herb_shadow.png
│   │   ├── uw_herb_crimson.png
│   │   ├── uw_mineral_crude.png
│   │   ├── uw_mineral_crystal.png
│   │   ├── uw_extract_whisper.png
│   │   ├── uw_extract_dream.png
│   │   ├── uw_extract_shadow.png
│   │   ├── uw_powder_moonite.png
│   │   ├── uw_crystal_void.png
│   │   ├── uw_tonic_whisper.png
│   │   ├── uw_powder_dream.png
│   │   ├── uw_elixir_shadow.png
│   │   ├── uw_salts_void.png
│   │   └── uw_elixir_crimson.png
│   └── Chara/
│       └── uw_fixer.png
└── worklog/
    └── scripts/
        ├── uw_asset_pipeline.py     # Unified asset management
        ├── uw_asset_specs.py        # Single-source-of-truth specs
        └── requirements.txt         # Python dependencies
```

---

## 11.2 package.xml

```xml
<?xml version="1.0" encoding="utf-8"?>
<Meta>
  <title>Elin Underworld Simulator</title>
  <id>mrmeagle_ElinUnderworldSimulator</id>
  <author>mrmeagle</author>
  <loadPriority>100</loadPriority>
  <description>A standalone starting scenario. Build a contraband empire in Ylva's underworld — craft illicit goods, fulfill black-market orders, manage heat, and compete for territory in an asynchronous multiplayer economy.</description>
  <tags>Gameplay</tags>
  <version>0.23.45</version>
</Meta>
```

---

## 11.3 Build Pipeline — `.csproj`

### 11.3.1 Project File

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <AssemblyName>ElinUnderworldSimulator</AssemblyName>
    <RootNamespace>ElinUnderworldSimulator</RootNamespace>
    <GenerateAssemblyInfo>false</GenerateAssemblyInfo>
    <!-- net48, LangVersion, etc. inherited from Directory.Build.props -->
  </PropertyGroup>

  <ItemGroup>
    <!-- Explicit compile list (EnableDefaultCompileItems is false in Directory.Build.props) -->
    <Compile Include="UnderworldPlugin.cs" />
    <Compile Include="UnderworldStartupBootstrap.cs" />
    <Compile Include="Patches\*.cs" />
    <Compile Include="Traits\*.cs" />
    <Compile Include="Network\*.cs" />
    <Compile Include="Economy\*.cs" />
    <Compile Include="UI\*.cs" />
  </ItemGroup>
</Project>
```

### 11.3.2 Directory.Build.props Registration

Add `ElinUnderworldSimulator` to the `IsElinModProject` condition in [Directory.Build.props](file:///c:/Users/mcounts/Documents/ElinMods/Directory.Build.props):

```xml
<IsElinModProject Condition="
    '$(MSBuildProjectName)' == 'FastStart' or 
    '$(MSBuildProjectName)' == 'PartyWage' or 
    '$(MSBuildProjectName)' == 'SkyreaderGuild' or
    '$(MSBuildProjectName)' == 'ElinUnderworldSimulator'
">true</IsElinModProject>
```

This gives the project automatic access to all Elin DLL references, Harmony, BepInEx, and the shared build targets from [Directory.Build.targets](file:///c:/Users/mcounts/Documents/ElinMods/Directory.Build.targets).

---

## 11.4 Automated Asset Pipeline

### 11.4.1 Design Philosophy

The SkyreaderGuild mod uses three separate scripts for asset management ([generate_assets.py](file:///c:/Users/mcounts/Documents/ElinMods/SkyreaderGuild/worklog/scripts/generate_assets.py), [integrate_assets.py](file:///c:/Users/mcounts/Documents/ElinMods/SkyreaderGuild/worklog/scripts/integrate_assets.py), [add_meteor_items.py](file:///c:/Users/mcounts/Documents/ElinMods/SkyreaderGuild/worklog/scripts/add_meteor_items.py)). Each has its own copy of asset definitions, leading to spec duplication.

The Underworld pipeline consolidates into:
- **One spec file** (`uw_asset_specs.py`) — single source of truth for all item/chara definitions
- **One pipeline script** (`uw_asset_pipeline.py`) — subcommands for each stage

### 11.4.2 `uw_asset_specs.py` — Single Source of Truth

```python
"""All Underworld Simulator asset specifications.

This is the SINGLE SOURCE OF TRUTH for:
1. Sprite generation prompts and target sizes
2. Deployment canvas/visible sizes and render data
3. SourceCard XLSX row data

No other file should duplicate item definitions.
"""

from dataclasses import dataclass, field
from typing import Optional


@dataclass(frozen=True)
class DeploySpec:
    """How the sprite is scaled and placed for Elin's renderer."""
    canvas_size: tuple[int, int]
    visible_size: tuple[int, int]
    anchor: str  # "center" or "bottom_center"
    render_data: str  # "@obj_S flat", "@obj", "@obj tall", etc.


@dataclass(frozen=True)
class SourceRowSpec:
    """Complete SourceCard.xlsx Thing row data."""
    name_JP: str = ""
    name: str = ""
    category: str = "_item"
    sort: str = ""
    sort_value: Optional[int] = None  # NUMERIC — never string
    _tileType: str = ""
    _idRenderData: str = "@obj_S flat"
    tiles: int = 530                   # NUMERIC — fallback tile
    recipeKey: str = ""
    factory: str = ""
    components: str = ""
    defMat: str = "glass"
    value: int = 100                   # NUMERIC
    LV: int = 1                        # NUMERIC
    chance: int = 0                    # NUMERIC
    quality: int = 0                   # NUMERIC
    HP: int = 0                        # NUMERIC
    weight: int = 100                  # NUMERIC
    trait: str = ""
    elements: str = ""
    tag: str = ""
    detail: str = ""


@dataclass(frozen=True)
class AssetSpec:
    """Complete specification for one mod asset."""
    id: str
    category: str  # "item" or "chara"
    prompt: str  # Gemini generation prompt
    preview_size: tuple[int, int]  # For human review
    deploy: DeploySpec
    source_row: SourceRowSpec


# ─── Item Asset Specifications ───────────────────────────────────────

SMALL_ITEM = DeploySpec(
    canvas_size=(32, 32), visible_size=(30, 30),
    anchor="center", render_data="@obj_S flat"
)

FURNITURE = DeploySpec(
    canvas_size=(48, 48), visible_size=(46, 46),
    anchor="center", render_data="@obj"
)

FURNITURE_BIG = DeploySpec(
    canvas_size=(64, 64), visible_size=(62, 62),
    anchor="center", render_data="@obj"
)


ASSET_SPECS: list[AssetSpec] = [
    # ── Raw Ingredients ──────────────────────────────────────────────
    AssetSpec(
        id="uw_herb_whisper",
        category="item",
        prompt=(
            "2D top-down RPG game herb icon, a pale creeping vine "
            "with tiny luminescent leaves and thin tendrils, coiled loosely, "
            "pixel art style, pale green and soft white palette, "
            "uniform flat fuchsia/magenta screen background, centered, "
            "clean edges, no text, no frame, game asset"
        ),
        preview_size=(48, 48),
        deploy=SMALL_ITEM,
        source_row=SourceRowSpec(
            name_JP="ウィスパーヴァイン",
            name="whispervine",
            category="herb",
            sort="resource_herb",
            _idRenderData="@obj_S flat",
            tiles=530,
            defMat="leaf",
            value=30,
            LV=1,
            chance=20,
            weight=100,
            tag="contraband",
            detail="A pale, creeping vine that hums faintly when touched. "
                   "Herbalists prize it. Others do too.",
        ),
    ),
    
    # (Additional entries for all items defined in 03_data_model.md)
    # Each follows the same AssetSpec pattern with prompt, deploy, and source_row.
    # ...
    
    # ── Crafting Stations ────────────────────────────────────────────
    AssetSpec(
        id="uw_mixing_table",
        category="item",
        prompt=(
            "2D isometric RPG game furniture sprite, three-quarter top-down view "
            "angled from upper-right, a crude alchemist workstation with glass "
            "vials, copper tubing, a burner, and scattered herb residue on a "
            "stained wooden table, pixel art style, dark wood and copper palette, "
            "uniform flat fuchsia/magenta screen background, centered, clean edges, "
            "no text, no frame, game asset"
        ),
        preview_size=(64, 64),
        deploy=FURNITURE_BIG,
        source_row=SourceRowSpec(
            name_JP="錬金術師の悪徳台",
            name="alchemist's vice",
            category="crafter",
            _tileType="ObjBig",
            _idRenderData="@obj",
            tiles=1552,
            recipeKey="*",
            factory="workbench",
            components="log/4,ingot/4,glass/2",
            defMat="oak",
            value=3000,
            LV=1,
            HP=80,
            weight=15000,
            trait="MixingTable,crafting",
            detail="A clandestine workstation fitted with vials, burners, "
                   "and a crude distillation apparatus.",
        ),
    ),
    
    # ── NPCs ─────────────────────────────────────────────────────────
    AssetSpec(
        id="uw_fixer",
        category="chara",
        prompt=(
            "2D front-facing RPG character sprite, a cloaked rogueish figure "
            "with a shadowed face, wearing dark leather armor with a hood, "
            "one hand resting on a belt pouch, confident and mysterious pose, "
            "fantasy pixel art style, dark brown and deep purple palette, "
            "uniform flat fuchsia/magenta screen background, centered, "
            "full body visible, clean edges, no text, game character sprite"
        ),
        preview_size=(64, 96),
        deploy=DeploySpec(
            canvas_size=(128, 192), visible_size=(72, 108),
            anchor="bottom_center", render_data="@chara"
        ),
        source_row=None,  # Chara rows have different columns — handled separately
    ),
]
```

### 11.4.3 `uw_asset_pipeline.py` — Unified Pipeline

```python
"""Unified asset pipeline for Elin Underworld Simulator.

Usage:
    python uw_asset_pipeline.py generate [--only ID1 ID2]
    python uw_asset_pipeline.py integrate
    python uw_asset_pipeline.py sync-xlsx
    python uw_asset_pipeline.py validate
    python uw_asset_pipeline.py deploy

All specs come from uw_asset_specs.py — single source of truth.
"""
import argparse
import sys


def cmd_generate(args):
    """Generate sprites via Gemini API with chroma-key removal."""
    from uw_asset_specs import ASSET_SPECS
    # Reuses the exact generation logic from SkyreaderGuild's generate_assets.py:
    # - Gemini API call with response_modalities=["IMAGE"]
    # - Chroma-key background removal (edge flood-fill + strict cleanup)
    # - Save raw, nobg, and preview PNGs to worklog/generated_assets/
    # - Rate limiting: DELAY_BETWEEN_CALLS, MAX_RETRIES, RETRY_BACKOFF_BASE
    # 
    # Key difference from SkyreaderGuild: specs come from ASSET_SPECS
    # instead of inline ITEM_ASSETS/CHARA_ASSETS dicts.
    pass


def cmd_integrate(args):
    """Scale nobg sprites to deployment canvas and copy to Texture/."""
    from uw_asset_specs import ASSET_SPECS
    # Reuses the exact integration logic from SkyreaderGuild's integrate_assets.py:
    # - Load nobg PNG
    # - Auto-crop dead transparent space
    # - Fit to visible_size via LANCZOS downscale
    # - Paste onto canvas_size with anchor-based positioning
    # - Validate final image dimensions and alpha bbox
    # - Save to Texture/Item/ or Texture/Chara/
    pass


def cmd_sync_xlsx(args):
    """Ensure all expected rows exist in SourceCard.xlsx with correct values."""
    from uw_asset_specs import ASSET_SPECS
    # Steps:
    # 1. Open LangMod/EN/SourceCard.xlsx with openpyxl
    # 2. For each AssetSpec with source_row:
    #    a. Find existing row by id in column 1
    #    b. If missing, append new row
    #    c. If present, validate/update all columns
    #    d. Write NUMERIC columns as int, string columns as str
    # 3. Save workbook
    # 4. Run normalize_shared_strings() — critical for NPOI compliance
    #    (Ported from SkyreaderGuild add_meteor_items.py L49-L168)
    pass


def cmd_validate(args):
    """Validate all assets, textures, and XLSX rows against specs."""
    from uw_asset_specs import ASSET_SPECS
    errors = []
    
    # 1. Check every texture file exists in Texture/
    # 2. Check every expected XLSX row exists with correct values
    # 3. Check numeric columns are numeric (not strings)
    # 4. Unzip XLSX, check xl/sharedStrings.xml exists
    # 5. Grep worksheets for inlineStr — must be zero
    # 6. Cross-reference factory values against known station IDs
    # 7. Cross-reference components item IDs against known Thing IDs
    # 8. Check no unexpected .pref files exist
    
    if errors:
        for e in errors:
            print(f"ERROR: {e}", file=sys.stderr)
        sys.exit(1)
    else:
        print("All validations passed.")


def cmd_deploy(args):
    """Copy mod files to Elin's Package directory."""
    # Copy to: D:\Steam\steamapps\common\Elin\Package\ElinUnderworldSimulator\
    # Files: DLL, package.xml, LangMod/, Texture/
    pass


def main():
    parser = argparse.ArgumentParser(description="Underworld asset pipeline")
    sub = parser.add_subparsers(dest="command")
    
    gen = sub.add_parser("generate")
    gen.add_argument("--only", nargs="*", help="Regenerate specific IDs")
    gen.add_argument("--model", default="gemini-3.1-flash-image-preview")
    
    sub.add_parser("integrate")
    sub.add_parser("sync-xlsx")
    sub.add_parser("validate")
    sub.add_parser("deploy")
    
    args = parser.parse_args()
    
    commands = {
        "generate": cmd_generate,
        "integrate": cmd_integrate,
        "sync-xlsx": cmd_sync_xlsx,
        "validate": cmd_validate,
        "deploy": cmd_deploy,
    }
    
    if args.command in commands:
        commands[args.command](args)
    else:
        parser.print_help()


if __name__ == "__main__":
    main()
```

### 11.4.4 NPOI Shared-String Normalization

The `normalize_shared_strings()` function is the most critical part of XLSX management. It must be called after every openpyxl save. The complete implementation is ported from [add_meteor_items.py L49-L168](file:///c:/Users/mcounts/Documents/ElinMods/SkyreaderGuild/worklog/scripts/add_meteor_items.py#L49-L168).

Summary of what it does:
1. Opens the `.xlsx` as a ZIP
2. Parses each worksheet XML
3. Converts `inlineStr` and `str` type cells to `s` (shared string reference)
4. Builds a new `xl/sharedStrings.xml` with all unique string values
5. Updates `[Content_Types].xml` to register the shared strings part type
6. Updates `xl/_rels/workbook.xml.rels` to link the shared strings
7. Replaces the original file with the corrected version

**Critical:** Without this step, Elin's NPOI importer reads all string values as blank, causing `ThingGen.Create()` to fail silently and produce fallback junk items.

### 11.4.5 Python Dependencies

```
# worklog/scripts/requirements.txt
google-genai
Pillow
python-dotenv
openpyxl
```

---

## 11.5 Deployment Process

### 11.5.1 Build & Deploy Checklist

```
1. dotnet build ElinUnderworldSimulator.csproj -c Release
2. python worklog/scripts/uw_asset_pipeline.py sync-xlsx
3. python worklog/scripts/uw_asset_pipeline.py validate
4. python worklog/scripts/uw_asset_pipeline.py deploy
5. Launch Elin → Mod Viewer → verify [Local] ElinUnderworldSimulator
6. Check BepInEx/LogOutput.log for errors
```

### 11.5.2 Deploy Script Detail

```python
def cmd_deploy(args):
    """Copy mod files to Elin's Package directory."""
    import shutil
    from pathlib import Path
    
    ELIN_DIR = Path(os.environ.get("ELIN_DIR", r"D:\Steam\steamapps\common\Elin"))
    MOD_ROOT = Path(__file__).resolve().parent.parent.parent
    DEPLOY_DIR = ELIN_DIR / "Package" / "ElinUnderworldSimulator"
    
    # Ensure target exists
    DEPLOY_DIR.mkdir(parents=True, exist_ok=True)
    
    # Copy files
    shutil.copy2(MOD_ROOT / "bin" / "ElinUnderworldSimulator.dll", DEPLOY_DIR)
    shutil.copy2(MOD_ROOT / "package.xml", DEPLOY_DIR)
    
    # Copy LangMod
    lang_dest = DEPLOY_DIR / "LangMod"
    if lang_dest.exists():
        shutil.rmtree(lang_dest)
    shutil.copytree(MOD_ROOT / "LangMod", lang_dest)
    
    # Copy Texture
    tex_dest = DEPLOY_DIR / "Texture"
    if tex_dest.exists():
        shutil.rmtree(tex_dest)
    shutil.copytree(MOD_ROOT / "Texture", tex_dest)
    
    print(f"Deployed to {DEPLOY_DIR}")
```

---

## 11.6 Workshop Publishing

For Steam Workshop publication:

| Field | Value |
|-------|-------|
| Title | Elin Underworld Simulator |
| Tags | Gameplay |
| Preview | `preview.jpg` — 640×360 banner image |
| Description | Markdown-formatted feature list and usage instructions |
| Visibility | Public |

The preview image should be generated via the Gemini API using a prompt like:
```
"Steam Workshop preview banner for 'Elin Underworld Simulator', 
dark fantasy RPG pixel art scene showing a cloaked figure at a 
crude alchemist's workstation surrounded by glowing vials and herbs, 
moody candlelight, 640x360 resolution"
```

---

## 11.7 Testing & Verification

### Build Tests

| Test | Command | Expected |
|------|---------|----------|
| Clean build | `dotnet build -c Release` | 0 errors, 0 warnings |
| Reference validation | Build triggers `ValidateElinReferences` | All DLLs found |
| DLL size | Check output DLL | Reasonable size (<500KB) |

### Asset Pipeline Tests

| Test | Command | Expected |
|------|---------|----------|
| Validate passes | `python uw_asset_pipeline.py validate` | "All validations passed" |
| XLSX shared strings | Unzip → check for `xl/sharedStrings.xml` | File exists |
| No inlineStr | Grep sheets for `inlineStr` | Zero matches |
| Numeric columns | Open XLSX in Excel → check `tiles`, `value` cells | Numbers, not text |
| All textures present | Check `Texture/Item/*.png` count | Matches asset spec count |
| Texture dimensions | Check each PNG canvas size | Matches deploy spec |

### Deployment Tests

| Test | Expected |
|------|----------|
| Deploy to Package/ | All files present in `Package/ElinUnderworldSimulator/` |
| Elin loads mod | Mod Viewer shows `[Local] Elin Underworld Simulator` |
| No log errors | `LogOutput.log` has no ERROR lines from this mod |
| Items load | `ThingGen.Create("uw_mixing_table")` → valid item, not fallback |
| Textures render | Place mixing table in-game → custom sprite displayed |
| Chara texture | Interact with Fixer → custom sprite displayed |

### Regression Test: Rubber Duck Prevention

The most critical test — verifying that NPOI shared-string normalization works:
1. Run `sync-xlsx`
2. Deploy to Elin
3. Start new game
4. Create every `uw_*` item via debug console
5. **Every item must have its correct name and properties — NO rubber ducks or fallback items**
