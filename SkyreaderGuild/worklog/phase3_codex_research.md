# Phase 3: Astrological Codex & Research Tasks — Detailed Implementation Plan

## Overview

The Astrological Codex is a craftable workbench that enables research-style crafting (similar to vanilla crafters like Anvil, Loom). Players combine Meteorite Source with other materials to produce guild-specific items: Astral Extractors, Starforged equipment, boss summon scrolls, and more.

## Architecture

### TraitCrafter System

The game's crafting system uses `TraitCrafter` subclasses. Each crafter trait:
- Has a factory ID (`IdSource`) that maps to recipe rows in `SourceRecipe`
- Defines number of ingredient slots (`numIng`)
- Can filter valid ingredients (`IsCraftIngredient`)
- References a `SourceRecipe.Row` for each available recipe

**Key Reference Files:**
- `TraitCrafter.cs` — The base crafter trait (613 lines, handles UI, ingredient validation, crafting logic)
- `SourceRecipe.cs` — Recipe source data class
- `SourceCard_Recipe.md` — Recipe data format from game data

### Recipe Data Format

From `SourceCard_Recipe.md`, each recipe row has:
```
id | factory | ing | n | type | tag | detail_JP | detail
```

Where:
- `id`: Recipe product Thing ID
- `factory`: Crafter factory type (e.g., "Anvil", "Loom", "Alchemy")
- `ing`: Ingredient filter (category or specific item ID)
- `n`: Number of ingredients required
- `type`: Output type
- `tag`: Tags for filtering
- `detail` / `detail_JP`: Description text

## TraitAstrologicalCodex Implementation

### `TraitAstrologicalCodex.cs` (NEW)

```csharp
namespace SkyreaderGuild
{
    public class TraitAstrologicalCodex : TraitCrafter
    {
        // Factory ID mapped in recipe source data
        public override string IdSource => "SRG_Codex";
        
        // Title shown in crafting UI
        public override string CrafterTitle => "Astrological Codex";
        
        // Most recipes take 2 ingredients
        public override int numIng => 2;
        
        // Rank-gate recipes
        public override bool IsCraftable(RecipeSource r)
        {
            var quest = EClass.game.quests.Get<QuestSkyreader>();
            if (quest == null) return false;
            
            var rank = quest.GetCurrentRank();
            
            // Check rank requirements per recipe
            switch (r.id)
            {
                case "srg_astral_extractor":
                    return true; // Available at Wanderer
                case "srg_scroll_twilight":
                case "srg_scroll_radiance":
                case "srg_scroll_abyss":
                case "srg_scroll_nova":
                    return rank >= GuildRank.CosmosApplied;
                case "srg_scroll_convergence":
                    return rank >= GuildRank.Understander;
                default:
                    return true;
            }
        }
    }
}
```

## Recipe Data Registration

Add recipe rows at `OnStartCore`:

```csharp
private void AddCodexRecipes(SourceManager sources)
{
    AddRecipe(sources, "srg_astral_extractor", "SRG_Codex", 
        "srg_meteorite_source", 1, "", "",
        "天文エッセンスを作る", "Produce Astral Extractor");
    
    AddRecipe(sources, "srg_scroll_twilight", "SRG_Codex",
        "srg_meteorite_source", 5, "", "",
        "永遠の黄昏の巻物を作る", "Craft Scroll of Eternal Twilight");
    
    AddRecipe(sources, "srg_scroll_radiance", "SRG_Codex",
        "srg_meteorite_source", 5, "", "",
        "地獄の輝きの巻物を作る", "Craft Scroll of Infernal Radiance");
    
    AddRecipe(sources, "srg_scroll_abyss", "SRG_Codex",
        "srg_meteorite_source", 5, "", "",
        "深淵の巻物を作る", "Craft Scroll of the Abyss");
    
    AddRecipe(sources, "srg_scroll_nova", "SRG_Codex",
        "srg_meteorite_source", 5, "", "",
        "黒い新星の巻物を作る", "Craft Scroll of Black Nova");
    
    AddRecipe(sources, "srg_scroll_convergence", "SRG_Codex",
        "srg_meteorite_source", 2, "", "",
        "天文収束の巻物を作る", "Craft Scroll of Astral Convergence");
}

private void AddRecipe(SourceManager sources, string id, string factory,
    string ing, int n, string type, string tag,
    string detailJp, string detail)
{
    var recipe = sources.recipes.CreateRow();
    recipe.id = id;
    recipe.factory = factory;
    recipe.ing = new string[] { ing };
    recipe.n = new int[] { n };
    recipe.type = type;
    recipe.tag = new string[] { tag };
    recipe.detail_JP = detailJp;
    recipe.detail = detail;
    sources.recipes.rows.Add(recipe);
}
```

## Weave the Stars / Starforge (Enhancement Recipes)

These are special recipes that take an existing crafted item and apply a random enchantment using the vanilla enchantment system.

### Implementation Approach

Rather than being standard recipes (which produce a new item), these work as **modification recipes** — the player puts in an existing piece of equipment + Meteorite Source, and the equipment comes back with an added enchantment.

**Reference:** `TraitGrindstone` and `TraitMod` show how to modify items in-place.

```csharp
// In TraitAstrologicalCodex, override Craft() for special recipes
public override Thing Craft(RecipeSource r, List<Thing> ings)
{
    if (r.id == "srg_weave_stars" || r.id == "srg_starforge")
    {
        // Find the equipment ingredient (not Meteorite Source)
        Thing equipment = ings.FirstOrDefault(t => t.id != "srg_meteorite_source");
        if (equipment == null) return null;
        
        // Add a random enchantment from the element system
        // Reference: ElementContainer.ModBase(), ELEMENT class
        int[] enchantPool = GetEnchantPool(r.id);
        int enchantId = enchantPool[EClass.rnd(enchantPool.Length)];
        int enchantValue = 5 + EClass.rnd(11); // +5 to +15
        
        equipment.elements.ModBase(enchantId, enchantValue);
        
        Msg.SayRaw($"Starlight infuses the {equipment.Name}!");
        
        return equipment;
    }
    
    return base.Craft(r, ings);
}

private int[] GetEnchantPool(string recipeId)
{
    if (recipeId == "srg_weave_stars")
    {
        // Defensive enchants for cloth/armor
        return new int[] {
            60, 61, 62, 63, 64, 65, // Elemental resistances
            76, 77, 78,              // DV, PV, Speed
        };
    }
    else // srg_starforge
    {
        // Offensive enchants for weapons/jewelry
        return new int[] {
            70, 71, 72, 73, 74, 75, // Attack elements
            66, 67, 68, 69,          // Stat bonuses
        };
    }
}
```

## SourceCard.xlsx Additions

### Thing Sheet — Codex Item

| id | name | trait | category | LV | value | weight | _tileType | note |
|----|------|-------|----------|----|-------|--------|-----------|------|
| `srg_codex` | Astrological Codex | `SkyreaderGuild.TraitAstrologicalCodex` | furniture | 1 | 5000 | 30 | ObjBig | Crafting station |
| `srg_astral_extractor` | Astral Extractor | `SkyreaderGuild.TraitAstralExtractor` | potion | 1 | 300 | 2 | — | Consumable |
| `srg_scroll_twilight` | Scroll of Eternal Twilight | `SkyreaderGuild.TraitBossScroll` | scroll | 1 | 2000 | 1 | — | Boss summon |
| `srg_scroll_radiance` | Scroll of Infernal Radiance | `SkyreaderGuild.TraitBossScroll` | scroll | 1 | 2000 | 1 | — | Boss summon |
| `srg_scroll_abyss` | Scroll of the Abyss | `SkyreaderGuild.TraitBossScroll` | scroll | 1 | 2000 | 1 | — | Boss summon |
| `srg_scroll_nova` | Scroll of Black Nova | `SkyreaderGuild.TraitBossScroll` | scroll | 1 | 2000 | 1 | — | Boss summon |
| `srg_scroll_convergence` | Scroll of Astral Convergence | `SkyreaderGuild.TraitArchivistScroll` | scroll | 1 | 5000 | 1 | — | Archivist summon |

### Codex Crafting Recipe

The Codex itself is crafted at a standard workbench:
- 4 Spellbooks + 12 Gems + 4 Logs + 9 Ingots
- Requires Literacy skill 10

This needs a recipe row with `factory = "Workbench"` (or similar).

## Sprite IDs

Use existing sprites:
- `srg_codex`: Reuse bookshelf or altar sprite
- `srg_astral_extractor`: Reuse potion sprite
- `srg_scroll_*`: Reuse scroll sprite

## Verification

1. Craft an Astrological Codex at a workbench (or spawn via debug)
2. Place the Codex — verify it opens a crafting UI when interacted with
3. Verify recipe list shows appropriate recipes for current guild rank
4. Craft an Astral Extractor — verify 1 Meteorite Source consumed, Extractor produced
5. Attempt a rank-locked recipe at low rank — verify it's grayed out / unavailable
6. Rank up and re-check — verify recipe becomes available
7. Test Weave the Stars with cloth armor — verify enchantment applied
8. Test Starforge with a weapon — verify enchantment applied

## Dependencies

- Phase 1 (guild membership check)
- Phase 2 (Meteorite Source production)

## File Changes Summary

| File | Change Type | Description |
|------|-------------|-------------|
| `TraitAstrologicalCodex.cs` | NEW | Crafter trait for the Codex |
| `SkyreaderGuild.cs` | MODIFY | Add recipe registration in `OnStartCore` |
| `Assets/SourceCard.xlsx` | MODIFY | Add Thing rows for Codex + products |
