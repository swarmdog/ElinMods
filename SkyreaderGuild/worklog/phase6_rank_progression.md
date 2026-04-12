# Phase 6: Guild Rank Progression — Detailed Implementation Plan

## Overview

The backbone of the guild system: GP accumulates from all activities, crossing rank thresholds unlocks new features, and the quest journal/tracker displays progress.

## GuildRank Enum (Already Defined)

```csharp
public enum GuildRank
{
    Wanderer = 0,
    Seeker = 200,
    Researcher = 500,
    CosmosAddled = 1500,
    CosmosApplied = 3000,
    Understander = 5000,
    PrincipalStarseeker = 10000
}
```

## Rank Benefits Summary

| Rank | GP | Unlocks |
|------|-----|---------|
| Wanderer | 0 | Guild membership, basic meteor detection |
| Seeker | 200 | Weave the Stars, Starforge recipes |
| Researcher | 500 | Reduced meteor spawn radius, improved detection |
| Cosmos Addled | 1500 | Reforge recipe (future), stronger post-analysis events |
| Cosmos Applied | 3000 | Boss summon scrolls (Ultima Projection) |
| Understander | 5000 | Archivist summon scroll |
| Principal Starseeker | 10000 | 2x meteor rewards, maximum prestige |

## QuestSkyreader Enhancements

### `AddGuildPoints(int amount)` Method

```csharp
public void AddGuildPoints(int amount)
{
    GuildRank oldRank = GetCurrentRank();
    gp += amount;
    GuildRank newRank = GetCurrentRank();
    
    if (newRank != oldRank)
    {
        OnRankUp(oldRank, newRank);
    }
    
    SkyreaderGuild.Log($"Added {amount} GP (total: {gp}, rank: {newRank})");
}

private void OnRankUp(GuildRank oldRank, GuildRank newRank)
{
    string msg = $"Your standing in the Skyreader's Guild has risen! You are now a {FormatRankName(newRank)}.";
    
    // Add rank-specific unlock messages
    switch (newRank)
    {
        case GuildRank.Seeker:
            msg += " You can now use Weave the Stars and Starforge at the Codex.";
            break;
        case GuildRank.Researcher:
            msg += " Meteors now appear closer to your position.";
            break;
        case GuildRank.CosmosAddled:
            msg += " The cosmos seeps deeper into your understanding.";
            break;
        case GuildRank.CosmosApplied:
            msg += " You can now craft Ultima Projection scrolls to summon cosmic entities.";
            break;
        case GuildRank.Understander:
            msg += " The path to the Astral Archivist is revealed. Craft the Scroll of Astral Convergence.";
            break;
        case GuildRank.PrincipalStarseeker:
            msg += " You have reached the pinnacle. All meteor rewards are doubled.";
            break;
    }
    
    Msg.SayRaw(msg);
}

public static string FormatRankName(GuildRank rank)
{
    switch (rank)
    {
        case GuildRank.Wanderer: return "Wanderer";
        case GuildRank.Seeker: return "Seeker";
        case GuildRank.Researcher: return "Researcher";
        case GuildRank.CosmosAddled: return "Cosmos-Addled";
        case GuildRank.CosmosApplied: return "Cosmos-Applied";
        case GuildRank.Understander: return "Understander";
        case GuildRank.PrincipalStarseeker: return "Principal Starseeker";
        default: return rank.ToString();
    }
}
```

### Rank-Gating Helper Methods

```csharp
// Used by TraitAstrologicalCodex to gate recipes
public bool CanUseRecipe(string recipeId)
{
    var rank = GetCurrentRank();
    // Map recipe IDs to minimum rank
    // (This is a centralized check; the Codex trait delegates here)
    return true; // Default: available
}

// Used by MeteorManager to reduce spawn distance
public int GetMeteorSpawnDistanceReduction()
{
    var rank = GetCurrentRank();
    if (rank >= GuildRank.Researcher) return 2;
    if (rank >= GuildRank.CosmosAddled) return 4;
    if (rank >= GuildRank.CosmosApplied) return 6;
    if (rank >= GuildRank.Understander) return 8;
    return 0;
}

// Used by TraitMeteorCore to double rewards
public float GetMeteorRewardMultiplier()
{
    return GetCurrentRank() >= GuildRank.PrincipalStarseeker ? 2f : 1f;
}
```

### Journal Detail Text Override

```csharp
public override string GetDetailText()
{
    var rank = GetCurrentRank();
    var nextRank = GetNextRank();
    
    string text = "";
    
    // Header
    text += $"⚆ Skyreader's Guild — {FormatRankName(rank)}\n";
    text += new string('─', 40) + "\n\n";
    
    // GP Progress
    text += $"Guild Points: {gp}";
    if (nextRank.HasValue)
    {
        int needed = (int)nextRank.Value - gp;
        text += $"  ({needed} to {FormatRankName(nextRank.Value)})";
    }
    text += "\n\n";
    
    // Statistics
    text += "Activities:\n";
    text += $"  Meteors Analyzed: {meteors_found}\n";
    text += $"  Touched Cleansed: {touched_cleansed}\n\n";
    
    // Current Rank Benefits
    text += "Current Benefits:\n";
    if (rank >= GuildRank.Seeker)
        text += "  • Weave the Stars / Starforge crafting\n";
    if (rank >= GuildRank.Researcher)
        text += "  • Improved meteor detection range\n";
    if (rank >= GuildRank.CosmosApplied)
        text += "  • Ultima Projection (boss summons)\n";
    if (rank >= GuildRank.Understander)
        text += "  • Astral Convergence (Archivist summon)\n";
    if (rank >= GuildRank.PrincipalStarseeker)
        text += "  • 2x meteor rewards\n";
    
    if (rank == GuildRank.Wanderer)
        text += "  • Basic meteor detection\n";
    
    return text;
}
```

### Tracker Text Override

```csharp
public override string GetTrackerText()
{
    var rank = GetCurrentRank();
    var nextRank = GetNextRank();
    
    string tracker = $"⚆ {FormatRankName(rank)}";
    if (nextRank.HasValue)
    {
        tracker += $" — {gp}/{(int)nextRank.Value} GP";
    }
    else
    {
        tracker += $" — {gp} GP (MAX)";
    }
    return tracker;
}
```

## GP Sources Summary

| Activity | GP Reward | Phase |
|----------|-----------|-------|
| Find & analyze meteor | 100-150 | Phase 2 |
| Cleanse Touched NPC | 50-150 | Phase 4 |
| Post-analysis Extra Insight | +50 bonus | Phase 2 |
| Summon a boss | 50 | Phase 5 |
| Kill a boss | 350-500 | Phase 5 |
| Summon the Archivist | 200 | Phase 5 |

### Progression Pace Estimate

| Rank | GP Required | Cumulative Activities (approx) |
|------|-------------|-------------------------------|
| Wanderer → Seeker | 200 | ~2 meteors |
| Seeker → Researcher | 300 more | ~2-3 more meteors or mixed |
| Researcher → CosmosAddled | 1000 more | ~7-10 mixed activities |
| CosmosAddled → CosmosApplied | 1500 more | ~10-15 mixed or boss kills |
| CosmosApplied → Understander | 2000 more | Boss kills + ongoing activity |
| Understander → Principal | 5000 more | Extended endgame play |

This gives a **gradual progression** curve: early ranks come fast (2-3 meteors), mid ranks require ongoing engagement, and the final rank is a long-term achievement.

## Saving/Serialization

The `QuestSkyreader` class uses `[JsonProperty]` attributes on its custom fields (`gp`, `meteors_found`, `touched_cleansed`). Since `QuestSequence` already supports JSON serialization, these fields persist automatically through the quest save system.

**Important:** Register our mod's assembly type with `GameSerializationBinder` fallback:
```csharp
// In Awake() or OnStartCore()
ModUtil.RegisterSerializedTypeFallback(
    "SkyreaderGuild",           // assembly name
    "SkyreaderGuild.QuestSkyreader",  // type name  
    "QuestDummy"                // fallback if mod is removed
);
```

This ensures that if the mod is removed, the save file doesn't crash — it falls back to `QuestDummy` (a harmless no-op quest).

## Verification

1. Start the guild at Wanderer rank — verify journal shows correct info
2. Earn GP via meteor analysis — verify rank-up messages trigger correctly
3. Cross each rank threshold — verify unlock messages are accurate
4. Check journal at each rank — verify benefits listed correctly
5. Check tracker widget — verify GP display updates in real-time
6. Reach Principal Starseeker — verify 2x reward multiplier applies
7. Save and reload — verify GP, rank, and stats persist
8. Remove mod and reload save — verify game loads without crash (fallback test)

## File Changes Summary

| File | Change Type | Description |
|------|-------------|-------------|
| `SkyreaderGuild.cs` | MODIFY | Add GP methods, rank-up logic, journal/tracker overrides |
| `SkyreaderGuild.cs` | MODIFY | Add serialization fallback registration |
