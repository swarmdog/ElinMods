# Phase 5: Boss Summoning & Astral Archivist — Detailed Implementation Plan

## Overview

Endgame content: four cosmic bosses can be summoned via scrolls crafted at the Codex. Each boss has unique stats and abilities. The Astral Archivist is a recruitable NPC summoned via a special scroll at the Understander rank.

## Boss Design — Stats & Balance

### Reference: Existing Boss Stats

From `SourceChara_Chara.md`, comparable bosses (Corgon/Tephra tier, level 30-50):

| Chara | LV | HP | STR | END | DEX | PER | LER | WIL | MAG | CHA |
|-------|----|----|-----|-----|-----|-----|-----|-----|-----|-----|
| Corgon | 38 | ~800 | 40 | 35 | 30 | 25 | 20 | 30 | 25 | 15 |
| Tephra | 42 | ~900 | 35 | 30 | 35 | 30 | 25 | 35 | 40 | 20 |

### Skyreader Bosses — Stat Blocks

All bosses are designed to be challenging solo encounters for a party at the appropriate rank tier.

#### 1. Umbryon, Herald of Eternal Rot
- **Theme:** Darkness, decay, poison
- **LV:** 35
- **Race:** undead  
- **Class:** warrior
- **Stats:** STR 45, END 40, DEX 25, PER 20, LER 15, WIL 35, MAG 30, CHA 10
- **Hostility:** Enemy
- **Abilities:** Darkness bolt, Poison cloud, Life drain
- **Sprite:** Use existing undead boss sprite (e.g., lich or skeleton lord)
- **Loot:** High-quality dark-element weapon, 2–3 Meteorite Source

#### 2. Solaris, Inferno of the Fallen Star
- **Theme:** Fire, cosmic heat, explosion
- **LV:** 40
- **Race:** spirit
- **Class:** warmage
- **Stats:** STR 30, END 25, DEX 35, PER 30, LER 25, WIL 30, MAG 50, CHA 15
- **Hostility:** Enemy
- **Abilities:** Fire breath, Meteor (uses EffectMeteor!), Flame bolt
- **Sprite:** Use existing fire spirit/elemental sprite
- **Loot:** High-quality fire-element armor, 3–4 Meteorite Source

#### 3. Erevor, The Abyssal Maw
- **Theme:** Void, gravity, crushing force
- **LV:** 45
- **Race:** dragon
- **Class:** predator
- **Stats:** STR 55, END 50, DEX 20, PER 25, LER 10, WIL 40, MAG 35, CHA 5
- **Hostility:** Enemy
- **Abilities:** Gravity field, Crushing bite, Dimensional rift
- **Sprite:** Use existing dragon sprite
- **Loot:** High-quality void-element weapon, 3–5 Meteorite Source

#### 4. Quasarix, Devourer of Light
- **Theme:** Anti-magic, light absorption, silence
- **LV:** 50
- **Race:** god
- **Class:** gunner (ranged focus)
- **Stats:** STR 35, END 35, DEX 45, PER 50, LER 30, WIL 45, MAG 55, CHA 20
- **Hostility:** Enemy
- **Abilities:** Silence aura, Light absorption, Anti-magic bolt
- **Sprite:** Use existing god/angel sprite
- **Loot:** Legendary random equipment, 4–6 Meteorite Source, rare crafting materials

### Astral Archivist
- **Theme:** Knowledge, cosmic lore, support
- **LV:** 30
- **Race:** ether (or human)
- **Class:** pianist (support/caster)
- **Stats:** STR 15, END 20, DEX 20, PER 35, LER 50, WIL 40, MAG 40, CHA 35
- **Hostility:** Neutral (recruitable)
- **Abilities:** Healing, buff spells, identify
- **Sprite:** Use existing scholar/sage sprite
- **Trait:** `TraitUniqueChara` (allows recruitment)

## SourceCard.xlsx — Chara Sheet Additions

All bosses need rows in the Chara sheet. Key fields per `SourceChara_Chara.md` format:

```
id | name | race | class | hostility | LV | trait | elements | ...
```

| id | name | race | class | hostility | LV | trait |
|----|------|------|-------|-----------|-----|-------|
| srg_umbryon | Umbryon, Herald of Eternal Rot | undead | warrior | Enemy | 35 | TraitUniqueMonster |
| srg_solaris | Solaris, Inferno of the Fallen Star | spirit | warmage | Enemy | 40 | TraitUniqueMonster |
| srg_erevor | Erevor, The Abyssal Maw | dragon | predator | Enemy | 45 | TraitUniqueMonster |
| srg_quasarix | Quasarix, Devourer of Light | god | gunner | Enemy | 50 | TraitUniqueMonster |
| srg_archivist | Astral Archivist | ether | pianist | Neutral | 30 | TraitUniqueChara |

### Element/Spell Configuration

Boss abilities are configured via the `elements` field in `SourceChara`. Reference existing boss entries for format. Example for Umbryon:

```
elements: darknessBolt/30,poisonCloud/25,lifeDrain/20
```

The exact element IDs need to be cross-referenced with `SourceElement` data.

## TraitBossScroll Implementation

### `TraitBossScroll.cs` (NEW)

```csharp
namespace SkyreaderGuild
{
    public class TraitBossScroll : TraitScroll
    {
        // Map scroll item IDs to boss chara IDs
        private static readonly Dictionary<string, string> ScrollToBoss = new()
        {
            { "srg_scroll_twilight",  "srg_umbryon" },
            { "srg_scroll_radiance",  "srg_solaris" },
            { "srg_scroll_abyss",     "srg_erevor" },
            { "srg_scroll_nova",      "srg_quasarix" },
        };
        
        public override bool OnRead(Chara c)
        {
            string scrollId = owner.id;
            if (!ScrollToBoss.TryGetValue(scrollId, out string bossId))
            {
                SkyreaderGuild.Log($"Unknown boss scroll: {scrollId}");
                return false;
            }
            
            // Find a spawn point near the player
            Point spawnPoint = c.pos.GetNearestPoint(false, true, true, false);
            if (spawnPoint == null || spawnPoint.IsBlocked)
            {
                spawnPoint = EClass._map.GetRandomSurface(c.pos.x, c.pos.z, 3);
            }
            
            if (spawnPoint == null)
            {
                Msg.SayRaw("There isn't enough space to summon the creature.");
                return false;
            }
            
            // Spawn the boss
            Chara boss = CharaGen.Create(bossId, -1);
            EClass._zone.AddCard(boss, spawnPoint);
            boss.hostility = Hostility.Enemy;
            boss.c_originalHostility = Hostility.Enemy;
            boss.enemy = c; // Target the player
            
            // Visual/audio feedback
            Msg.SayRaw($"The scroll crumbles to dust as {boss.Name} materializes from the cosmic void!");
            
            // Grant GP for summoning (the real GP comes from killing)
            var quest = EClass.game.quests.Get<QuestSkyreader>();
            if (quest != null)
            {
                quest.AddGuildPoints(50);
            }
            
            // Consume the scroll
            owner.ModNum(-1, true);
            
            return true;
        }
    }
}
```

### Boss Kill Rewards

Add a `Card.SpawnLoot` postfix patch for boss kills:

```csharp
[HarmonyPatch(typeof(Card), "SpawnLoot")]
public static class BossKillRewardPatch
{
    private static readonly HashSet<string> BossIds = new()
    {
        "srg_umbryon", "srg_solaris", "srg_erevor", "srg_quasarix"
    };
    
    public static void Postfix(ref Card __instance)
    {
        if (!__instance.isChara) return;
        if (!BossIds.Contains(__instance.Chara.id)) return;
        
        var quest = EClass.game.quests.Get<QuestSkyreader>();
        if (quest == null) return;
        
        // GP reward scaled to boss level
        int gpReward = __instance.Chara.LV * 10; // 350-500 GP per boss
        quest.AddGuildPoints(gpReward);
        
        // Drop Meteorite Source
        int sourceCount = 2 + EClass.rnd(3); // 2-4
        Point dropPoint = __instance.GetRootCard().pos;
        for (int i = 0; i < sourceCount; i++)
        {
            Point p = dropPoint.GetNearestPoint(false, true, true, false) ?? dropPoint;
            EClass._zone.AddThing("srg_meteorite_source", p);
        }
        
        Msg.SayRaw($"The cosmic energy of {__instance.Chara.Name} dissipates, leaving fragments of meteoric ore.");
    }
}
```

## TraitArchivistScroll Implementation

### `TraitArchivistScroll.cs` (NEW)

```csharp
namespace SkyreaderGuild
{
    public class TraitArchivistScroll : TraitScroll
    {
        public override bool OnRead(Chara c)
        {
            // Check if Archivist already exists
            Chara existing = EClass.game.cards.globalCharas.Find("srg_archivist");
            if (existing != null)
            {
                Msg.SayRaw("The Astral Archivist is already present in this world.");
                return false;
            }
            
            Point spawnPoint = c.pos.GetNearestPoint(false, true, true, false);
            if (spawnPoint == null || spawnPoint.IsBlocked)
            {
                spawnPoint = EClass._map.GetRandomSurface(c.pos.x, c.pos.z, 3);
            }
            
            if (spawnPoint == null)
            {
                Msg.SayRaw("There isn't enough space for the summoning.");
                return false;
            }
            
            // Create the Archivist as a global NPC (persistent)
            Chara archivist = CharaGen.Create("srg_archivist", -1);
            archivist.SetGlobal();
            EClass._zone.AddCard(archivist, spawnPoint);
            
            // The Archivist is neutral and can be recruited
            archivist.hostility = Hostility.Neutral;
            archivist.c_originalHostility = Hostility.Neutral;
            
            Msg.SayRaw("A figure materializes from streams of starlight. The Astral Archivist has arrived.");
            
            // GP reward
            var quest = EClass.game.quests.Get<QuestSkyreader>();
            if (quest != null)
            {
                quest.AddGuildPoints(200);
            }
            
            // Consume scroll
            owner.ModNum(-1, true);
            
            return true;
        }
    }
}
```

## Verification

### Boss Testing
1. Craft each boss scroll at the Codex (Phase 3 prerequisite)
2. Use each scroll — verify boss spawns with correct stats
3. Fight each boss — verify difficulty is appropriate (challenging but beatable at tier)
4. Kill each boss — verify Meteorite Source + GP rewards
5. Verify loot drops are thematically appropriate

### Archivist Testing
1. Craft Scroll of Astral Convergence at Understander rank
2. Use the scroll — verify Archivist spawns as Neutral
3. Recruit the Archivist — verify they join the party
4. Save and load — verify Archivist persists (global NPC)
5. Attempt to summon again — verify "already present" message

## Dependencies

- Phase 1 (guild membership)
- Phase 3 (scroll crafting at Codex)
- Phase 6 (rank gates for Cosmos Applied / Understander)

## File Changes Summary

| File | Change Type | Description |
|------|-------------|-------------|
| `TraitBossScroll.cs` | NEW | Boss summon scroll trait |
| `TraitArchivistScroll.cs` | NEW | Archivist summon scroll trait |
| `SkyreaderGuild.cs` | MODIFY | Add boss kill reward patch |
| `Assets/SourceCard.xlsx` | MODIFY | Add 5 Chara rows (4 bosses + Archivist) |

## Balancing Notes

- Bosses are designed to be fought solo (or with the Archivist companion)
- GP rewards from bosses (350-500) mean killing all 4 bosses gives ~1400-2000 GP
- This is enough to jump from Researcher (500 GP) to CosmosAddled (1500 GP) or beyond
- The Archivist at Understander (5000 GP) is a meaningful endgame companion reward
- Boss scrolls cost 5 Meteorite Source each at the Codex — this gates boss access behind meteor farming
