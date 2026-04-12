# Phase 2: Meteor Retrieval — Detailed Implementation Plan

## Overview

The primary gameplay loop: meteors periodically appear on the overworld as temporary zones. Players travel to them, interact with the meteor core, and receive Meteorite Source + random treasures. After analysis, a post-event is rolled.

## Architecture

### Zone Type Strategy

**Key Constraint:** `SpatialGen.Create` uses `ClassCache.Create<Spatial>(row.type, "Elin")` — the `"Elin"` assembly parameter means it only resolves types from the base game assembly. Our mod-defined `Zone_MeteorSite` class lives in a different assembly.

**Solution:** Use `Zone_RandomDungeon` as the zone type in our SourceZone row, and use a **Harmony patch on `Zone.OnGenerateMap`** (or `Zone.OnVisit`) to detect when a meteor zone is entered and apply our custom generation logic. We identify our meteor zones by their `id` field (`srg_meteor`).

This avoids the assembly resolution problem entirely while still giving us full control over the zone's content.

### Alternative (not chosen): Patch `ClassCache.Create` to search all loaded assemblies. This is fragile and could interfere with other mods.

## Zone Source Registration

At `OnStartCore`, register a new zone row in `SourceZone`:

```csharp
private void RegisterMeteorZone(SourceManager sources)
{
    var zone = new SourceZone.Row();
    zone.id = "srg_meteor";
    zone.parent = "";           // no parent — created dynamically
    zone.name_JP = "隕石の衝突地点";
    zone.name = "Meteor Impact Site";
    zone.type = "Zone_RandomDungeon";  // use vanilla type for ClassCache compat
    zone.LV = 0;                // set dynamically at creation time
    zone.chance = 0;            // never spawns randomly
    zone.faction = "";
    zone.value = 0;
    zone.idProfile = "Lesimas"; // dungeon profile for multi-level support
    zone.idFile = new string[0];
    zone.idBiome = "Plain";     // open terrain biome
    zone.idGen = "";
    zone.idPlaylist = "Dungeon";
    zone.tag = new string[0];   // NOT "random" — we control spawning
    zone.cost = 0;
    zone.dev = 0;
    zone.image = "";
    zone.pos = new int[] { 0, 0, 389 }; // icon: same as grassland
    zone.questTag = new string[0];
    zone.textFlavor_JP = "星の破片が散乱している。";
    zone.textFlavor = "Fragments of a fallen star litter the ground.";
    zone.detail_JP = "";
    zone.detail = "";
    
    sources.zones.rows.Add(zone);
    sources.zones.map[zone.id] = zone;
}
```

## MeteorManager — Static Helper Class

### `MeteorManager.cs` (NEW)

```
namespace SkyreaderGuild
{
    public static class MeteorManager
    {
        // Config
        public const int BASE_SPAWN_CHANCE_PERCENT = 15;    // per day
        public const int METEOR_EXPIRE_HOURS = 10080;       // 7 days
        public const int BASE_SPAWN_RADIUS = 12;            // tiles from PC
        public const int MIN_DANGER_LV = 5;
        
        public static bool TrySpawnMeteor()
        {
            // 1. Check if player is a guild member
            if (!EClass.game.quests.IsStarted<QuestSkyreader>()) return false;
            
            // 2. RNG check
            int chance = BASE_SPAWN_CHANCE_PERCENT;
            // Higher ranks get slightly better chances
            var quest = EClass.game.quests.Get<QuestSkyreader>();
            if (quest.GetCurrentRank() >= GuildRank.Researcher)
                chance += 5;
            
            if (EClass.rnd(100) >= chance) return false;
            
            // 3. Get spawn radius (decreases at higher ranks per design doc)
            int radius = GetSpawnRadius(quest);
            
            // 4. Create the zone using Region.CreateRandomSite pattern
            var region = EClass.world.region;
            region.InitElomap();
            
            // Get a valid point near the player
            var pcZone = EClass._zone.GetTopZone();
            int orgX = pcZone.IsRegion
                ? (EClass.pc.pos.x + EClass.scene.elomap.minX)
                : pcZone.x;
            int orgY = pcZone.IsRegion
                ? (EClass.pc.pos.z + EClass.scene.elomap.minY)
                : pcZone.y;
            
            Point pos = region.GetRandomPoint(orgX, orgY, radius, true);
            if (pos == null) return false;
            
            // 5. Create the zone
            Zone zone = SpatialGen.Create("srg_meteor", region, true, pos.x, pos.z) as Zone;
            if (zone == null) return false;
            
            // Set danger level based on player level
            int lv = Mathf.Max(MIN_DANGER_LV,
                EClass.pc.FameLv * (75 + EClass.rnd(50)) / 100);
            zone._dangerLv = lv;
            zone.isRandomSite = true;
            zone.dateExpire = EClass.world.date.GetRaw() + METEOR_EXPIRE_HOURS;
            
            // Register on elomap
            region.elomap.SetZone(zone.x, zone.y, zone);
            region.elomap.objmap.UpdateMeshImmediate();
            
            // Notify player
            Msg.SayRaw("A streak of light crosses the sky! A meteor has fallen nearby.");
            
            SkyreaderGuild.Log($"Spawned meteor site at ({zone.x},{zone.y}), DangerLv={lv}");
            return true;
        }
        
        private static int GetSpawnRadius(QuestSkyreader quest)
        {
            var rank = quest.GetCurrentRank();
            switch (rank)
            {
                case GuildRank.PrincipalStarseeker: return 4;
                case GuildRank.Understander:        return 6;
                case GuildRank.CosmosApplied:       return 8;
                case GuildRank.CosmosAddled:        return 8;
                case GuildRank.Researcher:          return 10;
                default:                            return BASE_SPAWN_RADIUS;
            }
        }
    }
}
```

## Harmony Patches

### Patch 1: `GameDate.AdvanceDay` — Meteor Spawn Trigger

```csharp
[HarmonyPatch(typeof(GameDate), "AdvanceDay")]
public static class MeteorSpawnOnDayAdvance
{
    public static void Postfix()
    {
        MeteorManager.TrySpawnMeteor();
    }
}
```

### Patch 2: `Zone.OnGenerateMap` — Custom Meteor Zone Generation

When a zone with id `srg_meteor` generates its map, we override the normal dungeon generation to create a flat, open-air impact site.

```csharp
[HarmonyPatch(typeof(Zone_RandomDungeon), "OnGenerateMap")]
public static class MeteorZoneGenerationPatch
{
    public static bool Prefix(Zone_RandomDungeon __instance)
    {
        if (__instance.id != "srg_meteor") return true; // let vanilla handle
        
        // Custom generation: flat terrain, single floor, no rooms
        PopulateMeteorZone(__instance);
        
        return false; // skip vanilla OnGenerateMap
    }
    
    private static void PopulateMeteorZone(Zone zone)
    {
        var map = EClass._map;
        
        // The MapGenDungen already created the base terrain.
        // We now populate it with meteor content.
        
        // 1. Place the Meteor Core at center
        Point center = map.bounds.GetCenterPos();
        var meteorCore = zone.AddThing("srg_meteor_core", center);
        SkyreaderGuild.Log($"Placed meteor core at {center}");
        
        // 2. Place 3-5 debris objects around the core
        int debrisCount = 3 + EClass.rnd(3);
        for (int i = 0; i < debrisCount; i++)
        {
            Point p = map.GetRandomSurface(center.x, center.z, 6);
            if (p != null && !p.HasBlock && !p.HasThing)
            {
                zone.AddThing("srg_debris", p);
            }
        }
        
        // 3. Place 3-5 ruined items (random from loot tables)
        int lootCount = 3 + EClass.rnd(3);
        for (int i = 0; i < lootCount; i++)
        {
            Point p = map.GetRandomSurface(center.x, center.z, 8);
            if (p != null && !p.HasBlock)
            {
                Thing t = ThingGen.CreateFromCategory("junk", zone.DangerLv);
                zone.AddCard(t, p);
            }
        }
        
        // 4. Possibly place raw ores (30% chance each, up to 3)
        for (int i = 0; i < 3; i++)
        {
            if (EClass.rnd(100) < 30)
            {
                Point p = map.GetRandomSurface(center.x, center.z, 8);
                if (p != null && !p.HasBlock)
                {
                    Thing ore = ThingGen.CreateFromCategory("ore", zone.DangerLv);
                    zone.AddCard(ore, p);
                }
            }
        }
        
        // 5. Spawn a few hostile creatures around the impact
        int mobCount = 2 + EClass.rnd(3);
        for (int i = 0; i < mobCount; i++)
        {
            Point p = zone.GetRandomVisitPos(EClass.pc);
            if (p != null)
            {
                zone.SpawnMob(p, SpawnSetting.CurrentLevel());
            }
        }
    }
}
```

## Meteor Core Interaction

### Trait: `TraitMeteorCore` (subclass of `TraitItem`)

When the player interacts with the meteor core (via right-click → Examine / Analyze):

```csharp
public class TraitMeteorCore : TraitItem
{
    public override bool OnUse(Chara c)
    {
        // Grant Meteorite Source
        int sourceCount = 1 + EClass.rnd(4); // 1-4
        for (int i = 0; i < sourceCount; i++)
        {
            c.Pick(ThingGen.Create("srg_meteorite_source"));
        }
        
        // Grant random treasures
        int treasureCount = 2 + EClass.rnd(3); // 2-4
        for (int i = 0; i < treasureCount; i++)
        {
            Thing t = ThingGen.CreateFromCategory("resource", owner.pos.cell.GetSurface().zone.DangerLv);
            c.Pick(t);
        }
        
        // Update quest stats
        var quest = EClass.game.quests.Get<QuestSkyreader>();
        if (quest != null)
        {
            quest.meteors_found++;
            quest.AddGuildPoints(100 + EClass.rnd(51)); // 100-150 GP
        }
        
        Msg.SayRaw("You extract fragments from the meteor core. The starlight dims.");
        
        // Roll post-analysis event
        RollPostEvent(c);
        
        // Consume the core
        owner.ModNum(-1, true);
        
        return true;
    }
    
    private void RollPostEvent(Chara c)
    {
        int roll = EClass.rnd(100);
        if (roll < 25)
        {
            // Extra Insight — bonus resources
            Msg.SayRaw("Insight floods your mind! Extra resources materialize.");
            int bonus = 1 + EClass.rnd(3);
            for (int i = 0; i < bonus; i++)
            {
                c.Pick(ThingGen.Create("srg_meteorite_source"));
            }
        }
        else if (roll < 50)
        {
            // Touched Attack — spawn enhanced enemy + adds
            Msg.SayRaw("The meteor's energy lashes out! Something emerges from the fragments!");
            var boss = EClass._zone.SpawnMob(null, SpawnSetting.Boss(EClass._zone.DangerLv, EClass._zone.DangerLv));
            boss.hostility = Hostility.Enemy;
            boss.c_originalHostility = Hostility.Enemy;
            for (int i = 0; i < 2 + EClass.rnd(2); i++)
            {
                EClass._zone.SpawnMob(null, SpawnSetting.CurrentLevel());
            }
        }
        else if (roll < 75)
        {
            // Projection Bleedover — spawn mine traps
            Msg.SayRaw("Reality shimmers... the ground becomes unstable.");
            for (int i = 0; i < 3 + EClass.rnd(3); i++)
            {
                Point p = EClass._map.GetRandomSurface(c.pos.x, c.pos.z, 5);
                if (p != null && !p.HasThing)
                {
                    Thing trap = ThingGen.CreateFromCategory("trap", EClass._zone.DangerLv);
                    EClass._zone.AddCard(trap, p).Install();
                }
            }
        }
        else
        {
            // Hostile Caravan — spawn mercenary enemies
            Msg.SayRaw("A group of mercenaries emerges, attracted by the meteor's energy!");
            for (int i = 0; i < 3 + EClass.rnd(3); i++)
            {
                var merc = EClass._zone.SpawnMob(null, SpawnSetting.CurrentLevel());
                if (merc != null)
                {
                    merc.hostility = Hostility.Enemy;
                    merc.c_originalHostility = Hostility.Enemy;
                }
            }
        }
    }
}
```

## SourceCard.xlsx Additions

### Thing Sheet — New Rows

| id | name | trait | category | LV | value | weight | note |
|----|------|-------|----------|----|-------|--------|------|
| `srg_meteor_core` | Meteor Core | `SkyreaderGuild.TraitMeteorCore` | junk | 1 | 500 | 50 | Interactable object |
| `srg_meteorite_source` | Meteorite Source | (default TraitItem) | ore/resource | 1 | 200 | 5 | Crafting material |
| `srg_debris` | Impact Debris | (default TraitItem) | junk | 1 | 10 | 20 | Cosmetic |

### Sprite IDs

All items use existing sprite IDs for initial implementation:
- `srg_meteor_core`: Use a rock/boulder sprite (e.g., sprite from "rock" or "crystal" Things)
- `srg_meteorite_source`: Use an ore sprite (e.g., same sprite as "gem" items)
- `srg_debris`: Use a rubble sprite

## Verification

1. Join the guild (Phase 1 prerequisite)
2. Wait for day advance events — check console for meteor spawn messages
3. Find the meteor site on the overworld map — verify icon appears
4. Enter the meteor site — verify flat terrain, debris, and meteor core spawn
5. Interact with meteor core — verify Meteorite Source + treasure rewards
6. Verify post-analysis event fires (re-test multiple times for different rolls)
7. Check quest tracker — verify `meteors_found` incremented
8. Wait 7 in-game days — verify expired meteor zone is cleaned up

## File Changes Summary

| File | Change Type | Description |
|------|-------------|-------------|
| `SkyreaderGuild.cs` | MODIFY | Add zone registration in `OnStartCore` |
| `MeteorManager.cs` | NEW | Meteor spawn logic and post-analysis events |
| `TraitMeteorCore.cs` | NEW | Meteor core interaction trait |
| `Assets/SourceCard.xlsx` | MODIFY | Add Thing rows for meteor items |

## Key Design Decisions

1. **Using `Zone_RandomDungeon` as type**: Avoids the `ClassCache` assembly resolution issue. We detect our meteor zones by `id == "srg_meteor"` in Harmony patches.

2. **Single-floor zone**: Meteor sites are flat, open-air areas. We override `OnGenerateMap` to skip dungeon room generation and place our custom content instead.

3. **Expiration**: Meteor zones use the existing `dateExpire` system (7 days). The `Region.OnActivate` cleanup loop handles removal automatically.

4. **GP Rewards**: 100-150 GP per meteor, which means ~3-5 meteors to rank from Wanderer to Seeker (200 GP). This provides steady progression.
