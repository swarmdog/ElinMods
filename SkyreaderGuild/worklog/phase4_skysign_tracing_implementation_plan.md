# Skysign Tracing — Implementation Plan

## Overview

Skysign Tracing adds a discovery mechanic to town/overworld exploration. When guild members visit civilized zones, 1–2 NPCs can become "Meteor Touched." Players hold **Astral Extractors** and right-click Touched NPCs (brush-tool pattern) to earn GP, Meteorite Source, and trigger one of five randomized Skysign effects — including spawning an astral rift Nefia with a convenience portal in town.

---

## Phase 3 Code Audit

| File | Status | Issue |
|------|--------|-------|
| [TraitAstrologicalCodex.cs](file:///c:/Users/mcounts/Documents/ElinMods/SkyreaderGuild/TraitAstrologicalCodex.cs) | ✅ OK | Extends `TraitWorkbench`, rank-gates recipes correctly |
| [TraitStarImbuement.cs](file:///c:/Users/mcounts/Documents/ElinMods/SkyreaderGuild/TraitStarImbuement.cs) | ✅ OK | `InvOwnerStarImbuement` applies enchantments via `AddEnchant` |
| [TraitMeteorCore.cs](file:///c:/Users/mcounts/Documents/ElinMods/SkyreaderGuild/TraitMeteorCore.cs) | ✅ OK | Post-event rolls work correctly |
| [MeteorManager.cs](file:///c:/Users/mcounts/Documents/ElinMods/SkyreaderGuild/MeteorManager.cs) | ⚠️ Needs cap | No active meteor cap — needs `CountSites()` guard |
| [SkyreaderGuild.cs](file:///c:/Users/mcounts/Documents/ElinMods/SkyreaderGuild/SkyreaderGuild.cs) | ⚠️ Needs rift reg | `OnStartCore` only registers meteor zone; needs rift zone + portal item |
| SourceCard.xlsx | ⚠️ **Trait fix** | `srg_astral_extractor` trait = `Item`, must be `AstralExtractor` |
| [SkyreaderGuild.csproj](file:///c:/Users/mcounts/Documents/ElinMods/SkyreaderGuild/SkyreaderGuild.csproj) | ⚠️ Needs new files | Missing `TraitAstralExtractor.cs`, `TraitAstralPortal.cs` |

> [!IMPORTANT]
> **`srg_astral_extractor` trait is `Item` not `AstralExtractor`** in SourceCard.xlsx. The ensure script (renamed from `add_meteor_items.py`) will validate and fix this along with any other item data issues.

---

## Design Decisions

1. **Brush-tool UX** — same as vanilla brush/shears: hold extractor → see hint icons → right-click to interact.
2. **Astral rift is a full Nefia** — uses `type = "Zone_RandomDungeon"` for multi-floor dungeon gen, boss, fog, ore, shrines, traps. Post-gen patch adds astral theming.
3. **Convenience portal in town** — when the Dimensional Gateway Skysign triggers, the rift spawns on the overworld AND a portal object spawns near the Touched NPC. Players can step through the portal to teleport directly to the rift, or walk there on the world map.
4. **Separate caps** — max 2 active meteor sites, max **1** active astral rift.
5. **Max 3 Touched NPCs per zone**.
6. **Reading skill buff** — element ID `285` via `ConBuffStats`.

---

## Proposed Changes

### Component 1: Meteor Touched NPC Tagging

#### [MODIFY] [SkyreaderGuild.cs](file:///c:/Users/mcounts/Documents/ElinMods/SkyreaderGuild/SkyreaderGuild.cs)

Harmony postfix on `Zone.OnVisit()` to tag 1–2 NPCs in civilized zones.

```csharp
/// <summary>
/// Tags 1–2 non-hostile, non-player NPCs in civilized zones as "Meteor Touched" 
/// when a guild member visits. The Touched flag is a persistent mapInt key (9001)
/// cleared via Astral Extractor interaction (see TraitAstralExtractor).
/// Max 3 Touched NPCs per zone at any time.
/// </summary>
[HarmonyPatch(typeof(Zone), "OnVisit")]
public static class TagMeteorTouchedOnTownVisit
{
    public const int MeteorTouchedKey = 9001;
    public const int MaxTouchedPerZone = 3;

    public static void Postfix(Zone __instance)
    {
        if (!(__instance is Zone_Civilized)) return;
        if (!EClass.game.quests.IsStarted<QuestSkyreader>()) return;
        if (EClass.rnd(100) >= 30) return; // 30% chance per visit

        int alreadyTouched = 0;
        var eligible = new System.Collections.Generic.List<Chara>();
        foreach (var c in EClass._map.charas)
        {
            if (c.GetInt(MeteorTouchedKey) > 0)
            {
                alreadyTouched++;
                continue;
            }
            if (!c.IsHostile() && !c.IsPCFaction)
            {
                eligible.Add(c);
            }
        }

        int remaining = MaxTouchedPerZone - alreadyTouched;
        if (remaining <= 0 || eligible.Count == 0) return;

        int count = System.Math.Min(1 + EClass.rnd(2), remaining);
        for (int i = 0; i < count && eligible.Count > 0; i++)
        {
            var npc = eligible.RandomItem();
            npc.SetInt(MeteorTouchedKey, 1);
            eligible.Remove(npc);
            SkyreaderGuild.Log($"Tagged {npc.Name} as Meteor Touched in {__instance.Name}");
        }
    }
}
```

---

### Component 2: Astral Rift Zone — Full Nefia

#### Research Summary

From [SourceGame_Zone.md](file:///c:/Users/mcounts/Documents/ElinMods/elin_readable_game_data/SourceGame_Zone.md), the vanilla `dungeon` row is:
```
id=dungeon | type=Zone_RandomDungeon | idProfile=Lesimas | idBiome=Cave | idPlaylist=Dungeon | tag=random
```

[Zone_RandomDungeon](file:///c:/Users/mcounts/Documents/ElinMods/Elin-Decompiled-main/Elin/Zone_RandomDungeon.cs) provides:
- `IsNefia => true` — marks it as a proper Nefia for conquest tracking
- Multi-floor: `StartLV => -1`, levels descend (neg lv = underground)
- Boss floor: `LvBoss` seeded from `uid`; boss spawns at deepest level via `SpawnMob(null, SpawnSetting.Boss(...))`
- Map gen: `MapGenDungen` creates rooms, corridors, doors, entrance/exit stairs, traps
- `OnGenerateMap()` calls parent `Zone_Dungeon.OnGenerateMap()` which runs `TryGenerateOre()`, `TryGenerateBigDaddy()`, `TryGenerateEvolved()`, `TryGenerateShrine()`

**Our approach**: Register `srg_astral_rift` with `type = "Zone_RandomDungeon"`. Full Nefia pipeline runs automatically. A Harmony postfix on `Zone_RandomDungeon.OnGenerateMap` adds astral-themed loot/enemies when `id == "srg_astral_rift"`.

#### [MODIFY] [SkyreaderGuild.cs](file:///c:/Users/mcounts/Documents/ElinMods/SkyreaderGuild/SkyreaderGuild.cs) — `OnStartCore`

```csharp
public void OnStartCore()
{
    var s = Core.Instance.sources;
    this.AddQuest(s);
    this.RegisterMeteorZone(s);
    this.RegisterAstralRiftZone(s);
}

/// <summary>
/// Registers the srg_astral_rift zone as a full Nefia using Zone_RandomDungeon.
/// Gets multi-floor layout, boss level, fog of war, ore, shrines, and traps
/// from the vanilla dungeon pipeline. Astral theming added post-generation.
/// </summary>
private void RegisterAstralRiftZone(SourceManager sources)
{
    var zone = new SourceZone.Row();
    zone.id = "srg_astral_rift";
    zone.parent = "";
    zone.name_JP = "星霊の裂け目";
    zone.name = "Astral Rift";
    zone.type = "Zone_RandomDungeon";
    zone.LV = 0;
    zone.chance = 0;
    zone.faction = "";
    zone.value = 0;
    zone.idProfile = "Lesimas";
    zone.idFile = new string[0];
    zone.idBiome = "Dungeon";
    zone.idGen = "";
    zone.idPlaylist = "Dungeon";
    zone.tag = new string[0];  // NOT "random" — spawned by our code only
    zone.cost = 0;
    zone.dev = 0;
    zone.image = "";
    zone.pos = new int[] { 0, 0, 391 };
    zone.questTag = new string[0];
    zone.textFlavor_JP = "次元の裂け目から異界のエネルギーが漏れ出ている。";
    zone.textFlavor = "Otherworldly energy bleeds from a dimensional rift.";
    zone.detail_JP = "";
    zone.detail = "";

    sources.zones.rows.Add(zone);
    sources.zones.map[zone.id] = zone;
    Log("Registered srg_astral_rift zone type.");
}
```

#### [MODIFY] [SkyreaderGuild.cs](file:///c:/Users/mcounts/Documents/ElinMods/SkyreaderGuild/SkyreaderGuild.cs) — Post-gen theming

```csharp
/// <summary>
/// Adds astral-themed content to rift dungeons after the vanilla Nefia 
/// generation has run (rooms, corridors, stairs, ore, shrines, mobs).
/// Runs as a postfix on the complete Zone_RandomDungeon pipeline.
/// </summary>
[HarmonyPatch(typeof(Zone_RandomDungeon), "OnGenerateMap")]
public static class AstralRiftThemingPatch
{
    public static void Postfix(Zone_RandomDungeon __instance)
    {
        if (__instance.id != "srg_astral_rift") return;

        SkyreaderGuild.Log($"Applying astral theming to rift floor lv={__instance.lv}");

        // Scatter meteorite source items in the dungeon
        int lootCount = 2 + EClass.rnd(3);
        for (int i = 0; i < lootCount; i++)
        {
            Point p = EClass._map.bounds.GetRandomSurface();
            if (p != null && !p.HasBlock && !p.HasThing)
            {
                __instance.AddThing("srg_meteorite_source", p);
            }
        }

        // Extra mobs for increased difficulty
        int extraMobs = 1 + EClass.rnd(3);
        for (int i = 0; i < extraMobs; i++)
        {
            __instance.SpawnMob();
        }

        SkyreaderGuild.Log($"Astral rift floor themed: {lootCount} meteorite sources, {extraMobs} extra mobs");
    }
}
```

---

### Component 3: Convenience Portal

When the Dimensional Gateway Skysign effect triggers, the rift spawns on the overworld AND a portal object spawns near the Touched NPC in town. The portal links to the rift zone by UID and teleports the player there on use.

#### How zone teleportation works in Elin

From [TraitTeleporter](file:///c:/Users/mcounts/Documents/ElinMods/Elin-Decompiled-main/Elin/TraitTeleporter.cs) and [TraitMoongate](file:///c:/Users/mcounts/Documents/ElinMods/Elin-Decompiled-main/Elin/TraitMoongate.cs):
- `EClass.pc.MoveZone(zone, ZoneTransition.EnterState.Teleport)` instantly moves the player to a zone
- Zone UIDs are resolved via `RefZone.Get(uid)` 
- Things store zone references using `owner.c_uidZone`

Our portal is simpler than either — it's a one-way convenience shortcut to a specific zone.

#### [NEW] [TraitAstralPortal.cs](file:///c:/Users/mcounts/Documents/ElinMods/SkyreaderGuild/TraitAstralPortal.cs)

```csharp
using SkyreaderGuild;

/// <summary>
/// A temporary portal spawned in town when the Dimensional Gateway Skysign
/// triggers. Stores the rift zone's UID and teleports the player directly
/// to the rift entrance on interaction. Auto-destroys if the linked rift
/// has expired or been destroyed.
/// </summary>
public class TraitAstralPortal : TraitItem
{
    public override bool CanStack => false;
    public override bool CanBeHeld => false;
    public override bool IsChangeFloorHeight => true;

    public override void TrySetAct(ActPlan p)
    {
        p.TrySetAct("Enter Astral Rift", () =>
        {
            Zone rift = RefZone.Get(owner.c_uidZone);
            if (rift == null || rift.destryoed)
            {
                Msg.SayRaw("The portal flickers and fades — the rift has closed.");
                owner.Destroy();
                return true;
            }

            Msg.SayRaw("You step through the shimmering portal...");
            EClass.pc.MoveZone(rift, ZoneTransition.EnterState.Teleport);
            return false;
        }, owner, CursorSystem.MoveZone);
    }

    /// <summary>
    /// Called each time the zone is visited. Check if the linked rift still exists;
    /// if not, clean up the portal.
    /// </summary>
    public override void OnSetPlaceState(PlaceState state)
    {
        if (state == PlaceState.installed)
        {
            Zone rift = RefZone.Get(owner.c_uidZone);
            if (rift == null || rift.destryoed)
            {
                SkyreaderGuild.SkyreaderGuild.Log("Astral portal self-destructing: linked rift no longer exists.");
                owner.Destroy();
            }
        }
    }
}
```

#### Portal spawning in `TraitAstralExtractor.RollSkysignEffect()`

When the Dimensional Gateway effect fires:

```csharp
if (roll < 20)
{
    // Dimensional Gateway — spawn rift on overworld + portal in town
    if (MeteorManager.TrySpawnAstralRift())
    {
        // Find the rift we just spawned and create a portal near the NPC
        Zone rift = MeteorManager.FindLatestRift();
        if (rift != null)
        {
            Point portalPos = target.pos.GetNearestPoint(
                allowBlock: false, allowChara: false, allowInstalled: false);
            if (portalPos != null)
            {
                Thing portal = ThingGen.Create("srg_astral_portal");
                portal.c_uidZone = rift.uid;
                EClass._zone.AddCard(portal, portalPos).Install();
                Msg.SayRaw($"A shimmering portal materializes near {target.Name}! " +
                    "An astral rift has also appeared on the overworld.");
            }
        }
    }
    else
    {
        Msg.SayRaw("The cosmic energies dissipate...");
    }
}
```

#### `MeteorManager.FindLatestRift()` helper

```csharp
/// <summary>
/// Returns the most recently created astral rift zone, or null.
/// Used to link a newly spawned portal to its rift.
/// </summary>
public static Zone FindLatestRift()
{
    Region region = EClass.world.region;
    if (region == null) return null;
    Zone latest = null;
    foreach (Spatial child in region.children)
    {
        Zone z = child as Zone;
        if (z != null && !z.destryoed && z.id == "srg_astral_rift")
        {
            if (latest == null || z.uid > latest.uid)
                latest = z;
        }
    }
    return latest;
}
```

---

### Component 4: Active Site Caps

#### [MODIFY] [MeteorManager.cs](file:///c:/Users/mcounts/Documents/ElinMods/SkyreaderGuild/MeteorManager.cs)

```csharp
public const int MAX_ACTIVE_METEOR_SITES = 2;
public const int MAX_ACTIVE_RIFT_SITES = 1;

public static bool TrySpawnMeteor(bool force = false)
{
    // ... existing quest/chance checks ...
    
    if (!force && CountSites("srg_meteor") >= MAX_ACTIVE_METEOR_SITES)
    {
        SkyreaderGuild.Log("Meteor spawn skipped: at active site cap.");
        return false;
    }
    
    // ... rest of spawn logic unchanged ...
}

public static bool TrySpawnAstralRift()
{
    if (CountSites("srg_astral_rift") >= MAX_ACTIVE_RIFT_SITES)
    {
        SkyreaderGuild.Log("Astral rift spawn skipped: at active rift cap.");
        Msg.SayRaw("The cosmic energies dissipate — a rift already scars the land.");
        return false;
    }

    // ... same spawn logic as TrySpawnMeteor but for srg_astral_rift ...
    // Sets _dangerLv = Max(5, PC.LV), isRandomSite = true, dateExpire = 7 days
}

/// <summary>
/// Counts active guild-spawned sites of a specific zone id on the overworld.
/// Elin's built-in dateExpire system handles 7-day cleanup.
/// </summary>
public static int CountSites(string zoneId)
{
    int count = 0;
    Region region = EClass.world.region;
    if (region == null) return 0;
    foreach (Spatial child in region.children)
    {
        Zone z = child as Zone;
        if (z != null && !z.destryoed && z.id == zoneId)
            count++;
    }
    return count;
}

public static Zone FindLatestRift() { /* as above */ }
```

---

### Component 5: TraitAstralExtractor

#### [NEW] [TraitAstralExtractor.cs](file:///c:/Users/mcounts/Documents/ElinMods/SkyreaderGuild/TraitAstralExtractor.cs)

Brush-pattern consumable. When held, shows hint icons on Touched NPCs. Right-click for "Extract Starlight" action.

```csharp
using SkyreaderGuild;
using UnityEngine;

/// <summary>
/// Consumable for the Skysign Tracing loop. When held, shows hint icons 
/// on Meteor Touched NPCs (brush-tool pattern). Right-click a Touched NPC 
/// to extract cosmic energy, clear the Touched flag, and roll a Skysign effect.
/// </summary>
public class TraitAstralExtractor : TraitItem
{
    public override bool CanStack => true;

    /// <summary>
    /// Shows a hint icon above NPCs that have the Meteor Touched flag,
    /// but only when this item is being held. Same pattern as brush/shears.
    /// </summary>
    public override Emo2 GetHeldEmo(Chara c)
    {
        if (!EClass.game.quests.IsStarted<QuestSkyreader>()) return Emo2.none;
        if (c.GetInt(TagMeteorTouchedOnTownVisit.MeteorTouchedKey) > 0)
            return Emo2.hint;
        return Emo2.none;
    }

    /// <summary>
    /// Adds the "Extract Starlight" action when right-clicking a Touched NPC.
    /// </summary>
    public override void TrySetHeldAct(ActPlan p)
    {
        foreach (Chara c in p.pos.Charas)
        {
            if (c.GetInt(TagMeteorTouchedOnTownVisit.MeteorTouchedKey) > 0)
            {
                p.TrySetAct("Extract Starlight", () =>
                {
                    PerformExtraction(EClass.pc, c);
                    return true;
                }, c);
            }
        }
    }

    private void PerformExtraction(Chara user, Chara target)
    {
        // Clear the Touched flag
        target.SetInt(TagMeteorTouchedOnTownVisit.MeteorTouchedKey, 0);

        // Base rewards: Meteorite Source + GP
        for (int i = 0; i < 2; i++)
            user.Pick(ThingGen.Create("srg_meteorite_source"));

        QuestSkyreader quest = EClass.game.quests.Get<QuestSkyreader>();
        if (quest != null)
        {
            int gpReward = 50 + Mathf.Min(target.LV * 5, 100);
            quest.AddGuildPoints(gpReward);
            quest.touched_cleansed++;
        }

        RollSkysignEffect(user, target);
        owner.ModNum(-1, true);  // Consume the extractor
    }

    /// <summary>
    /// Rolls one of five Skysign effects (20% each).
    /// </summary>
    private void RollSkysignEffect(Chara user, Chara target)
    {
        int roll = EClass.rnd(100);

        if (roll < 20)
        {
            // Dimensional Gateway — spawn astral rift + portal in town
            if (MeteorManager.TrySpawnAstralRift())
            {
                Zone rift = MeteorManager.FindLatestRift();
                if (rift != null)
                {
                    Point portalPos = target.pos.GetNearestPoint(
                        allowBlock: false, allowChara: false, allowInstalled: false);
                    if (portalPos != null)
                    {
                        Thing portal = ThingGen.Create("srg_astral_portal");
                        portal.c_uidZone = rift.uid;
                        EClass._zone.AddCard(portal, portalPos).Install();
                    }
                }
                Msg.SayRaw($"A rift tears open near {target.Name}! A shimmering portal beckons.");
            }
            else
            {
                Msg.SayRaw("The cosmic energies dissipate...");
            }
        }
        else if (roll < 40)
        {
            // Alignment — temporary Reading skill buff
            Msg.SayRaw("Cosmic alignment! Your mind sharpens with extraordinary literacy.");
            user.AddCondition(Condition.Create(500, delegate(ConBuffStats con)
            {
                con.SetRefVal(285, (int)EffectId.BuffStats);
            }));
        }
        else if (roll < 60)
        {
            // Cosmic Attunement — boost target NPC stats
            Msg.SayRaw($"{target.Name} is infused with cosmic energy!");
            target.elements.ModBase(70, 5);  // STR
            target.elements.ModBase(71, 5);  // END
            target.elements.ModBase(72, 5);  // DEX
        }
        else if (roll < 80)
        {
            // Medical Success — increase NPC affinity
            Msg.SayRaw($"{target.Name} looks at you with gratitude.");
            target.ModAffinity(EClass.pc, 30);
        }
        else
        {
            // Astral Exposure — transform a nearby item's material
            Msg.SayRaw("The extractor overloads! Nearby items shimmer...");
            foreach (Thing t in EClass._map.things)
            {
                if (user.pos.Distance(t.pos) <= 3 && EClass.rnd(3) == 0)
                {
                    t.ChangeMaterial(MATERIAL.GetRandomMaterial(t.LV + 5));
                    Msg.SayRaw($"{t.Name} has been transformed!");
                    break;
                }
            }
        }
    }
}
```

---

### Component 6: Item Data Validation Script

#### [RENAME + MODIFY] `add_meteor_items.py` → [ensure_meteor_items.py](file:///c:/Users/mcounts/Documents/ElinMods/SkyreaderGuild/ensure_meteor_items.py)

Rename the script and add a validation/fix section. The script should:

1. **Ensure all mod items exist** — add missing rows (existing behavior)
2. **Validate existing items** — check trait, category, and other critical fields match expected values and fix any mismatches
3. **Add new items** — `srg_astral_portal` portal item
4. **Clean non-numeric sort values** (existing behavior)
5. **Normalize shared strings** (existing behavior)

New validation section:

```python
# Expected trait values for each mod item
expected_traits = {
    'srg_astral_extractor': 'AstralExtractor',
    'srg_meteor_core': 'MeteorCore',
    'srg_meteorite_source': 'ResourceMain',
    'srg_codex': 'AstrologicalCodex',
    'srg_astral_portal': 'AstralPortal',
    'srg_weave_stars': 'StarImbuement',
    'srg_starforge': 'StarImbuement',
}

# Validate and fix trait values
for row in ws.iter_rows(min_row=4, values_only=False):
    item_id = row[0].value
    if not isinstance(item_id, str) or item_id not in expected_traits:
        continue
    current_trait = row[col_map['trait'] - 1].value
    expected = expected_traits[item_id]
    if current_trait != expected:
        row[col_map['trait'] - 1].value = expected
        print(f"  FIX: {item_id} trait '{current_trait}' → '{expected}'")
        fixed += 1
```

New item to add:

```python
{
    'id': 'srg_astral_portal',
    'name_JP': 'astral portal',
    'name': 'astral portal',
    'category': 'furniture',
    'sort': '',
    '_idRenderData': 'obj_S',
    'tiles': 1208,         # Reuse a glowing/portal sprite
    'defMat': 'glass',
    'value': 0,
    'LV': 1,
    'weight': 99999,       # Cannot be picked up
    'trait': 'AstralPortal',
    'detail': 'A shimmering gateway to an astral rift. Step through to enter.',
},
```

---

### Component 7: Project Registration

#### [MODIFY] [SkyreaderGuild.csproj](file:///c:/Users/mcounts/Documents/ElinMods/SkyreaderGuild/SkyreaderGuild.csproj)

```xml
<Compile Include="TraitAstralExtractor.cs" />
<Compile Include="TraitAstralPortal.cs" />
```

---

## File Changes Summary

| File | Change | Description |
|------|--------|-------------|
| [SkyreaderGuild.cs](file:///c:/Users/mcounts/Documents/ElinMods/SkyreaderGuild/SkyreaderGuild.cs) | MODIFY | Add `TagMeteorTouchedOnTownVisit`, `RegisterAstralRiftZone`, `AstralRiftThemingPatch` |
| [MeteorManager.cs](file:///c:/Users/mcounts/Documents/ElinMods/SkyreaderGuild/MeteorManager.cs) | MODIFY | Add caps, `CountSites()`, `TrySpawnAstralRift()`, `FindLatestRift()` |
| [TraitAstralExtractor.cs](file:///c:/Users/mcounts/Documents/ElinMods/SkyreaderGuild/TraitAstralExtractor.cs) | NEW | Brush-pattern extractor with 5 Skysign effects + portal spawn |
| [TraitAstralPortal.cs](file:///c:/Users/mcounts/Documents/ElinMods/SkyreaderGuild/TraitAstralPortal.cs) | NEW | Convenience portal linking to rift zone |
| [SkyreaderGuild.csproj](file:///c:/Users/mcounts/Documents/ElinMods/SkyreaderGuild/SkyreaderGuild.csproj) | MODIFY | Add new .cs files |
| `add_meteor_items.py` → `ensure_meteor_items.py` | RENAME+MODIFY | Add validation, fix traits, add portal item |

---

## Verification Plan

### Build
```
python ensure_meteor_items.py   # Validate/fix xlsx, add portal item
dotnet build SkyreaderGuild.csproj
```
Copy DLL to `D:\Steam\steamapps\common\Elin\BepInEx\plugins\SkyreaderGuild\`

### In-Game Testing

1. **Trait fix** — Spawn `srg_astral_extractor` via debug → verify `TraitAstralExtractor` (not `TraitItem`)
2. **Town visit tagging** — Visit a town → check log for "Tagged" messages. Visit multiple times → max 3 Touched.
3. **Brush UX** — Hold extractor → hint icons on Touched NPCs. Right-click → "Extract Starlight" action.
4. **Extraction rewards** — Extract → 2× Meteorite Source, GP, `touched_cleansed++`.
5. **Skysign: Dimensional Gateway** — Verify rift appears on overworld as a Nefia-type icon AND portal spawns near the NPC in town. Interact with portal → teleported to rift. Enter rift from overworld → same dungeon.
6. **Rift dungeon** — Verify multi-floor Nefia: stairs, rooms, fog, mobs, boss floor. Meteorite sources on each floor.
7. **Portal cleanup** — Advance 7+ days until rift expires → verify portal self-destructs on next zone visit.
8. **Other Skysign effects** — Reading buff, stat boost, affinity, material transform.
9. **Meteor cap** — Force 2 meteors → 3rd rejected. Force rift → 2nd rejected.

### Log Locations
- BepInEx: `D:\Steam\steamapps\common\Elin\BepInEx\LogOutput.log`
- Unity: `C:\Users\mcounts\AppData\LocalLow\Lafrontier\Elin\Player.log`
