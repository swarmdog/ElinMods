# Phase 1: Guild Induction — Detailed Implementation Plan

## Overview

Phase 1 polishes the existing induction flow and fixes several bugs in the current `SkyreaderGuild.cs`. This is the entry point: player finds a Yith Growth in a Nefia, kills it, gets a Starchart, reads the Starchart, and joins the guild.

## Current State

The existing code handles:
- Yith Growth spawning in Nefia (level 15+, 20% chance) via `Zone.OnVisit` postfix
- Starchart drop on Yith Growth death via `Card.SpawnLoot` postfix
- Quest initiation on Starchart read via `TraitScrollMap.OnRead` prefix
- `QuestSkyreader` class with GP, rank tracking, and basic stat fields

## Bug Fixes Required

### 1.1 — `YithDropStarchart` null safety

**File:** `SkyreaderGuild.cs`, line 124

**Problem:** The `SpawnLoot` patch accesses `__instance.Chara` without checking if the Card is actually a `Chara`. When a Thing (like a chest) has `SpawnLoot` called, this throws a `NullReferenceException`.

**Fix:**
```diff
 public static void Postfix(ref Card __instance)
 {
+    if (!__instance.isChara) return;
     SkyreaderGuild.Log("Checking " + __instance.Chara.id.ToString() + "...");
```

### 1.2 — `FindZone("somewhere")` null safety

**File:** `SkyreaderGuild.cs`, line 93

**Problem:** `EClass.world.FindZone("somewhere")` returns `null` if the "somewhere" zone doesn't exist in the current region (it's actually defined in Asseria, not Ntyris). Even if it exists, placing a global NPC in "somewhere" makes them unreachable.

**Fix:** The Arkyn NPC should be a global chara placed in the `SpatialManager.Somewhere` zone (which has a special accessor). Alternatively, use `EClass.game.spatials.Somewhere`.

```diff
-    EClass.world.FindZone("somewhere").AddCard(c);
+    var somewhere = EClass.game.spatials.Somewhere;
+    if (somewhere != null)
+    {
+        somewhere.AddCard(c);
+    }
+    else
+    {
+        SkyreaderGuild.Log("Warning: 'somewhere' zone not found, Arkyn created but not placed.");
+    }
```

### 1.3 — Remove unused `using` directive

**File:** `SkyreaderGuild.cs`, line 3

**Problem:** `System.Runtime.Remoting.Messaging` doesn't exist in Unity/.NET Standard 2.1 and will cause build warnings or errors.

**Fix:** Remove `using System.Runtime.Remoting.Messaging;`

### 1.4 — Starchart consumption edge case

**File:** `SkyreaderGuild.cs`, line 109

**Problem:** `__instance.owner.ModNum(-1, true)` consumes the Starchart, but the `true` parameter means "destroy if zero." This is correct, but we should also check that the quest was actually started successfully before consuming.

**Fix:** Move consumption after the quest start block and make it unconditional (the Starchart is always consumed on read, even if the player is already in the guild).

## New Features

### 1.5 — `QuestSkyreader.GetDetailText()` override

**Purpose:** Display guild rank, GP progress, and available activities in the quest journal.

**Reference:** `QuestGuild.GetDetailText()` in `QuestGuild.cs` (lines 12-60).

**Implementation:**
```csharp
public override string GetDetailText()
{
    var rank = GetCurrentRank();
    var nextRank = GetNextRank();
    
    string text = $"Rank: {rank}\n";
    text += $"Guild Points: {gp}\n";
    
    if (nextRank != null)
    {
        text += $"Next Rank: {nextRank} ({(int)nextRank.Value - gp} GP needed)\n";
    }
    
    text += $"\nMeteors Found: {meteors_found}";
    text += $"\nTouched Cleansed: {touched_cleansed}";
    
    return text;
}
```

### 1.6 — `QuestSkyreader.GetTrackerText()` override

**Purpose:** Show current rank and GP in the quest tracker widget (top-right HUD).

**Reference:** `ItemQuestTracker.cs` shows how tracker lines are assembled.

**Implementation:**
```csharp
public override string GetTrackerText()
{
    return $"⚆ {GetCurrentRank()} — {gp} GP";
}
```

### 1.7 — `GetNextRank()` helper method

Returns the next `GuildRank` threshold, or null if at max rank:
```csharp
public GuildRank? GetNextRank()
{
    var current = GetCurrentRank();
    var values = (GuildRank[])Enum.GetValues(typeof(GuildRank));
    int idx = Array.IndexOf(values, current);
    if (idx < values.Length - 1)
        return values[idx + 1];
    return null;
}
```

## Data Dependencies

### SourceCard.xlsx — Chara Sheet

The `srg_growth` (Yith Growth) and `srg_arkyn` (Arkyn) characters must be defined. Verify:
- `srg_growth`: hostility = Enemy, appropriate level (10-20), uses existing slime/blob sprite
- `srg_arkyn`: hostility = Neutral, global NPC, uses existing scholar sprite

### SourceCard.xlsx — Thing Sheet

The `srg_starchart` item must be defined:
- Trait: `TraitScrollMap` (reuses existing scroll map behavior)
- Category: scroll/book
- Weight/value appropriate for a quest item

### SourceCard.xlsx — CharaText Sheet

Dialog lines for Arkyn NPC (if applicable).

## Verification

1. Build the mod DLL — no compile errors
2. Load game with BepInEx — check console for `[SkyreaderGuild]` messages, no exceptions
3. Use debug to enter a Nefia level 15+ — verify Yith Growth spawn chance works
4. Kill the Yith Growth — verify Starchart drops (check no crash on non-Chara `SpawnLoot`)
5. Read the Starchart — verify quest starts, message displays, Starchart consumed
6. Open journal — verify quest detail text shows rank/GP
7. Check quest tracker widget — verify tracker text shows

## File Changes Summary

| File | Change Type | Description |
|------|-------------|-------------|
| `SkyreaderGuild.cs` | MODIFY | Fix 4 bugs + add 3 new QuestSkyreader methods |
| `Assets/SourceCard.xlsx` | VERIFY | Ensure Chara/Thing rows are correct |
