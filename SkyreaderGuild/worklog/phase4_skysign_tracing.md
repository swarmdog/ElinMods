# Phase 4: Skysign Tracing — Detailed Implementation Plan

## Overview

Players use Astral Extractors on "Meteor Touched" NPCs/items to trigger cosmic effects and earn GP/Meteorite Source. This phase adds a discovery mechanic to the game's town/overworld exploration.

## Meteor Touched System

### Detection & Tagging

When the player visits a town zone, there's a chance that 1-2 NPCs become "Meteor Touched." This is tracked using the `Chara.SetInt` / `Chara.GetInt` system (custom int keys on the character).

**Custom Int Key:** We'll use a high int key (e.g., `9001`) to store the "touched" flag.

### Harmony Patch: Tag NPCs on Town Visit

```csharp
[HarmonyPatch(typeof(Zone), "OnVisit")]
public static class TagMeteorTouchedOnTownVisit
{
    public static void Postfix(Zone __instance)
    {
        // Only in town/civilized zones
        if (!(__instance is Zone_Town) && !(__instance is Zone_Civilized)) return;
        
        // Only for guild members
        if (!EClass.game.quests.IsStarted<QuestSkyreader>()) return;
        
        // 30% chance to tag NPCs on each visit
        if (EClass.rnd(100) >= 30) return;
        
        // Find eligible NPCs (non-hostile, non-global, not already touched)
        var eligibleNpcs = __instance.map.charas
            .Where(c => !c.IsHostile() 
                     && !c.IsPCFaction 
                     && c.GetInt(MeteorTouchedKey) == 0)
            .ToList();
        
        if (eligibleNpcs.Count == 0) return;
        
        // Tag 1-2 random NPCs
        int count = 1 + EClass.rnd(2);
        for (int i = 0; i < count && eligibleNpcs.Count > 0; i++)
        {
            var npc = eligibleNpcs.RandomItem();
            npc.SetInt(MeteorTouchedKey, 1);
            eligibleNpcs.Remove(npc);
            SkyreaderGuild.Log($"Tagged {npc.Name} as Meteor Touched");
        }
    }
    
    public const int MeteorTouchedKey = 9001;
}
```

### Visual Indicator

Guild members see a subtle glowing effect on Touched NPCs. This is implemented via a `TCOrbitChara` patch (the orbit text/icon system that shows status indicators above characters).

```csharp
[HarmonyPatch(typeof(TCOrbitChara), "RefreshStatus")]
public static class MeteorTouchedVisualPatch
{
    public static void Postfix(TCOrbitChara __instance)
    {
        if (!EClass.game.quests.IsStarted<QuestSkyreader>()) return;
        
        Chara c = __instance.owner as Chara;
        if (c == null) return;
        
        if (c.GetInt(TagMeteorTouchedOnTownVisit.MeteorTouchedKey) > 0)
        {
            // Add a star emoji/indicator to the orbit display
            // The exact implementation depends on the TCOrbit rendering pipeline
            // Simplest approach: append to the chara's name display temporarily
        }
    }
}
```

**Alternative (simpler):** Instead of a visual patch, add a `Msg.SayRaw` hint when the player gets close to a Touched NPC:

```csharp
[HarmonyPatch(typeof(ActPlan), "ShowContextMenu")]  
// or patch the interaction menu to show a "Touched by Starlight" hint
```

## TraitAstralExtractor Implementation

### `TraitAstralExtractor.cs` (NEW)

The Astral Extractor is a consumable item. When used, it opens a target selector. Using it on a Meteor Touched NPC triggers a Skysign effect.

```csharp
namespace SkyreaderGuild
{
    public class TraitAstralExtractor : TraitDrink
    {
        public override bool OnUse(Chara c)
        {
            // The player must target a Touched NPC
            // For simplicity, check if there's a Touched NPC nearby
            // and auto-target the closest one
            
            Chara target = FindNearbyTouchedNPC(c);
            if (target == null)
            {
                Msg.SayRaw("There are no meteor-touched beings nearby.");
                return false;
            }
            
            // Clear the Touched flag
            target.SetInt(TagMeteorTouchedOnTownVisit.MeteorTouchedKey, 0);
            
            // Grant rewards
            int sourceCount = 2;
            for (int i = 0; i < sourceCount; i++)
            {
                c.Pick(ThingGen.Create("srg_meteorite_source"));
            }
            
            // GP reward scaled to target level
            var quest = EClass.game.quests.Get<QuestSkyreader>();
            if (quest != null)
            {
                int gpReward = 50 + Mathf.Min(target.LV * 5, 100); // 50-150 GP
                quest.AddGuildPoints(gpReward);
                quest.touched_cleansed++;
            }
            
            // Roll Skysign effect
            RollSkysignEffect(c, target);
            
            // Consume the extractor
            owner.ModNum(-1, true);
            
            return true;
        }
        
        private Chara FindNearbyTouchedNPC(Chara user)
        {
            foreach (Chara c in EClass._map.charas)
            {
                if (c != user 
                    && c.GetInt(TagMeteorTouchedOnTownVisit.MeteorTouchedKey) > 0
                    && user.pos.Distance(c.pos) <= 3)
                {
                    return c;
                }
            }
            return null;
        }
        
        private void RollSkysignEffect(Chara user, Chara target)
        {
            int roll = EClass.rnd(100);
            
            if (roll < 20)
            {
                // Dimensional Gateway — spawn an astral dungeon nearby
                Msg.SayRaw($"A rift opens near {target.Name}! A gateway to an astral realm appears.");
                // Create a temporary nefia-like zone nearby
                // (Uses meteor zone code from Phase 2 with different flavor)
            }
            else if (roll < 40)
            {
                // Alignment — temporary Literacy skill bonus
                Msg.SayRaw($"Cosmic alignment! Your mind sharpens.");
                // Grant a temporary buff (Literacy +10 for 24 hours)
                // Use ConBuffStats or similar condition
                user.AddCondition(Condition.Create("ConBuffStats", 500));
            }
            else if (roll < 60)
            {
                // Cosmic Attunement — enhance adventurer NPC
                Msg.SayRaw($"{target.Name} is infused with cosmic energy!");
                // Boost target's stats temporarily
                target.elements.ModBase(70, 5); // +5 STR
                target.elements.ModBase(71, 5); // +5 END
                target.elements.ModBase(72, 5); // +5 DEX
            }
            else if (roll < 80)
            {
                // Medical Success — increase affinity
                Msg.SayRaw($"{target.Name} looks at you with gratitude.");
                // Increase favor/affinity
                target.affinity.Mod(30);
            }
            else
            {
                // Astral Exposure — transform a random nearby item
                Msg.SayRaw("The extractor overloads! Nearby items shimmer...");
                // Find a random item on the ground nearby and reroll it
                foreach (Thing t in EClass._map.things)
                {
                    if (user.pos.Distance(t.pos) <= 3 && EClass.rnd(3) == 0)
                    {
                        // Change material to a random higher-tier material
                        t.ChangeMaterial(MATERIAL.GetRandomMaterial(t.LV + 5));
                        Msg.SayRaw($"{t.Name} has been transformed!");
                        break;
                    }
                }
            }
        }
    }
}
```

## SourceCard.xlsx Additions

The `srg_astral_extractor` Thing row was defined in Phase 3. Ensure:
- Trait: `SkyreaderGuild.TraitAstralExtractor`
- Category: potion/consumable
- Stackable: yes
- Uses existing potion sprite

## Verification

1. Visit a town as a guild member — check console for "Tagged ... as Meteor Touched"
2. Look for visual indicators on Touched NPCs (or check via debug)
3. Craft an Astral Extractor at the Codex (Phase 3 prerequisite)
4. Use the Extractor near a Touched NPC — verify effect triggers
5. Verify Meteorite Source + GP rewards granted
6. Verify `touched_cleansed` counter incremented in quest tracker
7. Test each Skysign effect (may need repeated attempts or debug forcing)
8. Use Extractor with no Touched NPC nearby — verify "no touched beings" message

## Dependencies

- Phase 1 (guild membership)
- Phase 2 (Meteorite Source existence)  
- Phase 3 (Astral Extractor crafting)

## File Changes Summary

| File | Change Type | Description |
|------|-------------|-------------|
| `TraitAstralExtractor.cs` | NEW | Extractor consumable trait |
| `SkyreaderGuild.cs` | MODIFY | Add Touched NPC tagging patch |
| `Assets/SourceCard.xlsx` | VERIFY | Ensure Extractor Thing row is correct |

## Open Design Questions

1. **Target Selection**: The current approach auto-targets the nearest Touched NPC within 3 tiles. Should we instead open a target picker UI (like `ActTelekinesis` does)?

2. **Dimensional Gateway**: The "Dimensional Gateway" Skysign effect should create an astral-themed Nefia. Should this reuse the meteor zone infrastructure from Phase 2 with different biome/content, or be a separate zone type?

3. **Touched Persistence**: Touched flags persist on the NPC via `SetInt`. Should they expire after a certain time, or remain until cleansed?
