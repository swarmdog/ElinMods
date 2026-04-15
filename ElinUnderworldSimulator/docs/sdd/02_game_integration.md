# 2 · Game Integration

> Parent: [00_overview.md](./00_overview.md) · Architecture: [01_architecture.md](./01_architecture.md)

This document specifies every Harmony patch, Elin hook point, and game-system integration required by the mod. A developer implementing this document should be able to produce all C# patch classes and bootstrap logic without further research.

---

## 2.1 Startup Scenario — "Underworld Startup"

The mod injects a new starting scenario into Elin's character creation screen. When the player selects it, the game begins with a custom bootstrap that sets up the underworld economy instead of the vanilla Ylva main quest.

### 2.1.1 Elin's Scenario System

Elin's character creation uses a `listMode` array in `UICharaMaker` to enumerate available starting scenarios (prologues). Each prologue maps to meta-game state checked via `Game.Prologue`. The relevant call chain:

```
UICharaMaker.SetChara() → populates listMode with scenario options
UICharaMaker.ListModes() → renders the scenario selection UI
User selects a scenario → mode index stored
Game.StartNewGame() → initializes game, checks prologue
```

**Key source references:**
- [FastStart Plugin.cs L41-L73](Documents/ElinMods/FastStart/Plugin.cs#L41-L73) — Prologue injection pattern (proven working)
- [QuestMain.cs](Documents/ElinMods/Elin-Decompiled-main/Elin/QuestMain.cs) — Quest phase definitions

### 2.1.2 Harmony Patches — Scenario Registration

Four patches are required. Each is specified below with the exact target class, method, patch type, and implementation logic.

---

#### Patch 1: `Game.Prologue` Getter — Return valid prologue for custom index

**Target:** `Game` class, `Prologue` property getter
**Patch type:** Prefix
**Purpose:** When the game checks `Game.Prologue` and the stored mode is our custom index, return a valid prologue object to prevent null-reference errors.

```csharp
[HarmonyPatch(typeof(Game), nameof(Game.Prologue), MethodType.Getter)]
public static class PatchGamePrologue
{
    // Custom prologue index — must not collide with vanilla indices
    public const int UNDERWORLD_PROLOGUE_INDEX = 90;
    
    static bool Prefix(ref Prologue __result)
    {
        if (Game.Instance?.idPrologue == UNDERWORLD_PROLOGUE_INDEX)
        {
            // Return the standard prologue (index 0) as the base template
            // Our bootstrap handles the actual divergence in Game.StartNewGame
            __result = Prologue.list[0];
            return false; // skip original getter
        }
        return true; // let vanilla handle other prologues
    }
}
```

**Reference:** FastStart uses a similar approach at [L41-L54](Documents/ElinMods/FastStart/Plugin.cs#L41-L54) — it returns `Prologue.list[0]` as the template prologue for custom start indices.

---

#### Patch 2: `UICharaMaker.SetChara` — Register custom scenario in mode list

**Target:** `UICharaMaker`, `SetChara` method
**Patch type:** Postfix
**Purpose:** Append "Underworld Startup" to the scenario selection list after vanilla options are populated.

```csharp
[HarmonyPatch(typeof(UICharaMaker), nameof(UICharaMaker.SetChara))]
public static class PatchSetChara
{
    static void Postfix(UICharaMaker __instance)
    {
        // Add our custom mode to the mode list
        // The exact mechanism depends on UICharaMaker's listMode structure
        // FastStart pattern: modify the listMode dropdown after vanilla populates it
        
        // Insert at the end of the mode list
        // The index stored when selected maps to UNDERWORLD_PROLOGUE_INDEX
        var modeDropdown = __instance.ddMode; // DropdownGrid reference
        if (modeDropdown != null)
        {
            modeDropdown.items.Add("Underworld Startup");
        }
    }
}
```

**Reference:** [FastStart L56-L73](Documents/ElinMods/FastStart/Plugin.cs#L56-L73) — dropdown mutation after vanilla population.

---

#### Patch 3: `UICharaMaker.ListModes` — Map custom selection to prologue index

**Target:** `UICharaMaker`, `ListModes` method
**Patch type:** Postfix
**Purpose:** When the user selects the custom mode from the dropdown, store our prologue index (`90`) into `Game.Instance.idPrologue`.

```csharp
[HarmonyPatch(typeof(UICharaMaker), nameof(UICharaMaker.ListModes))]
public static class PatchListModes
{
    static void Postfix(UICharaMaker __instance)
    {
        // After vanilla renders the mode list, check if our custom mode was selected
        // If so, set the prologue ID to our custom constant
        int selectedIndex = __instance.ddMode?.selectedIndex ?? -1;
        int vanillaModeCount = /* count of vanilla modes before our injection */;
        
        if (selectedIndex >= vanillaModeCount)
        {
            Game.Instance.idPrologue = PatchGamePrologue.UNDERWORLD_PROLOGUE_INDEX;
        }
    }
}
```

**Reference:** [FastStart L75-L110](Documents/ElinMods/FastStart/Plugin.cs#L75-L110).

---

#### Patch 4: `Game.StartNewGame` — Bootstrap underworld on new game start

**Target:** `Game`, `StartNewGame` method
**Patch type:** Postfix
**Purpose:** After the vanilla game initialization runs, check if the player selected our scenario and run the full underworld bootstrap.

```csharp
[HarmonyPatch(typeof(Game), nameof(Game.StartNewGame))]
public static class PatchStartNewGame
{
    static void Postfix()
    {
        if (Game.Instance?.idPrologue != PatchGamePrologue.UNDERWORLD_PROLOGUE_INDEX)
            return;
        
        UnderworldStartupBootstrap.Apply();
    }
}
```

**Reference:** [FastStart L144-L189](Documents/ElinMods/FastStart/Plugin.cs#L144-L189).

---

### 2.1.3 Bootstrap Logic — `UnderworldStartupBootstrap.Apply()`

This is the core new-game setup. It executes once, immediately after `Game.StartNewGame` completes for our scenario. The implementation must follow this exact sequence:

```csharp
public static class UnderworldStartupBootstrap
{
    public static void Apply()
    {
        var player = EClass.pc;
        var game = EClass.game;
        var home = EClass.game.spatials.Find("startSite") as Zone;
        
        // ── Step 1: Claim Starting Zone ─────────────────────────────────
        // Identical to vanilla land claiming
        home.ClaimZone(player);
        
        // ── Step 2: Start QuestHome, advance to phase 2 (land claimed) ──
        var questHome = game.quests.Get<QuestHome>();
        if (questHome == null)
        {
            questHome = Quest.Create("QuestHome") as QuestHome;
            game.quests.Start(questHome);
        }
        questHome.ChangePhase(2); // land claimed & base operational
        
        // ── Step 3: DO NOT advance QuestMain ────────────────────────────
        // The vanilla main quest simply stays at phase 0.
        // This means the Ashland → Nymelle → exploration chain never activates.
        // The player can explore freely but receives no main quest prompts.
        // Verify: game.quests.Get<QuestMain>()?.phase should remain 0
        
        // ── Step 4: Spawn the Fixer NPC ─────────────────────────────────
        SpawnFixer(home);
        
        // ── Step 5: Grant Starter Items ─────────────────────────────────
        GrantStarterItems(player);
        
        // ── Step 6: Place Mixing Table in Base ──────────────────────────
        PlaceMixingTable(home);
        
        // ── Step 7: Suppress tutorial dialogs ───────────────────────────
        // Set flags so Elin doesn't interrupt with tutorial popups
        player.SetFlag("tutorial_done", true);
        
        UnderworldPlugin.Log.LogInfo("Underworld Startup bootstrap complete.");
    }
    
    private static void SpawnFixer(Zone zone)
    {
        // Create NPC using CharaGen pattern from FastStart
        // Reference: FastStart L441-L453
        var fixer = CharaGen.Create("uw_fixer");
        fixer.SetGlobal(zone, fixer.pos.x, fixer.pos.z);
        fixer.MoveHome(zone);
        
        UnderworldPlugin.Log.LogInfo($"Fixer NPC spawned in {zone.Name}");
    }
    
    private static void GrantStarterItems(Chara player)
    {
        // ── Underworld-specific equipment ──
        var underworldItems = new[]
        {
            ("uw_mixing_table", 1),    // Portable mixing table (furniture)
            ("uw_contraband_chest", 1), // Shipping chest
            ("uw_herb_whisper", 10),    // Basic herb ingredient
            ("uw_mineral_crude", 5),    // Crude mineral ingredient
        };
        
        // ── Basic Elin items for crafting & survival ──
        // The player needs standard tools to engage with Elin's
        // crafting, cooking, and gathering systems from the start.
        var basicItems = new[]
        {
            ("money", 5000),            // Starting gold
            ("axe", 1),                 // Lumberjacking
            ("pickaxe", 1),             // Mining
            ("hoe", 1),                 // Farming / digging
            ("bandage", 6),             // Basic healing
            ("backpack", 1),            // Additional inventory space
            ("torch", 3),               // Light source for dungeon runs
            ("ration", 5),              // Starting food
            ("potion_empty", 10),       // Empty bottles for crafting
            ("waterskin", 1),           // Water carrying
        };
        
        foreach (var (id, count) in underworldItems)
        {
            var thing = ThingGen.Create(id);
            thing.SetNum(count);
            player.AddThing(thing);
        }
        
        foreach (var (id, count) in basicItems)
        {
            var thing = ThingGen.Create(id);
            thing.SetNum(count);
            player.AddThing(thing);
        }
    }
    
    private static void PlaceMixingTable(Zone zone)
    {
        // Place mixing table at a reasonable location in the starting base
        var table = ThingGen.Create("uw_mixing_table");
        zone._map.things.Add(table);
        // Position near the center of the zone
        table.SetPos(zone._map.bounds.CenterX, zone._map.bounds.CenterZ);
    }
}
```

### 2.1.4 Key Difference from FastStart

| Aspect | FastStart | Underworld Startup |
|--------|-----------|-------------------|
| QuestMain handling | Replays phases 0-6+ to completion, unlocking all vanilla content | Leaves at phase 0 — vanilla quest chain never activates |
| Scenario intent | Skip the early game grind while continuing the vanilla storyline | Replace the vanilla storyline with a criminal economy progression |
| Starting items | Misc adventuring gear, crafting mats | Underworld-specific equipment: mixing table, contraband chest, raw ingredients |
| NPC spawning | Handles existing vanilla NPCs and quest givers | Spawns only the Fixer NPC; vanilla quest NPCs are inactive since quests aren't triggered |

---

## 2.2 Zone Registration

### 2.2.1 Custom Zones

The mod may register 1-2 custom zones for underworld-specific locations. These follow the exact same pattern used by [Zone_SkyreaderGuild](Documents/ElinMods/SkyreaderGuild/Zone_SkyreaderGuild.cs).

**Potential custom zones:**

| Zone ID | Zone Class | Purpose | Type |
|---------|-----------|---------|------|
| `uw_hideout` | `Zone_UnderworldHideout` | Player's secret processing facility | Interior, instanced |

Each zone requires:
1. A `Zone_` class inheriting from `Zone` (or a subclass like `Zone_Dungeon`)
2. An entry in `SourceGame.xlsx` Zone sheet following the [SourceGame_Zone schema](Documents/ElinMods/elin_readable_game_data/SourceGame_Zone.md)
3. Registration in `OnStartCore` or via CWL source sheet loading

**Zone class template:**

```csharp
public class Zone_UnderworldHideout : Zone
{
    public override bool AllowCriminal => true; // Obviously
    public override bool HasLaw => false;       // No law enforcement here
    
    public override void OnGenerateMap()
    {
        base.OnGenerateMap();
        // Custom map generation — layout builder pattern from GuildLayoutBuilder
        // We should also consider directly exporting our generated maps to .z and so they can be used via SourceGame
        // in that scenario, we would have a python pipeline to generate the .z or .map
    }
}
```

**Source patterns:**
- [Zone_Derphy.cs](Documents/ElinMods/Elin-Decompiled-main/Elin/Zone_Derphy.cs) — `AllowCriminal => true`
- [Zone_SkyreaderGuild](Documents/ElinMods/SkyreaderGuild/Zone_SkyreaderGuild.cs) — custom zone registration and map generation

### 2.2.2 Zone Source Sheet Entry

For any custom zone, add a row to `SourceGame.xlsx` Zone sheet:

```
id: uw_hideout
parent: ntyris
name_JP: アンダーワールドの隠れ家
name: Underworld Hideout
type: Zone_UnderworldHideout
LV: 10
faction: (empty — lawless)
value: 0
idFile: uw_hideout
tag: closed
```

Reference: [SourceGame_Zone.md column definitions](Documents/ElinMods/elin_readable_game_data/SourceGame_Zone.md) L3-L28.

---

## 2.3 NPC Integration — The Fixer

### 2.3.1 Chara Source Sheet Entry

The Fixer is a unique NPC with a custom chara entry in `SourceCard.xlsx` Chara sheet.

Column values (using [CHARA_COL mapping](Documents/ElinMods/SkyreaderGuild/worklog/scripts/add_meteor_items.py#L208-L259)):

| Column | Index | Value |
|--------|-------|-------|
| `id` | 1 | `uw_fixer` |
| `name_JP` | 3 | `フィクサー` |
| `name` | 4 | `The Fixer` |
| `aka_JP` | 5 | `闇の仲介人` |
| `aka` | 6 | `the shadow broker` |
| `sort` | 8 | (empty) |
| `_idRenderData` | 10 | `@chara` |
| `tiles` | 11 | 1552 (fallback; custom texture overrides) |
| `hostility` | 19 | `Friend` |
| `tag` | 21 | `noGlobal` |
| `trait` | 22 | `UnderworldFixer` |
| `race` | 23 | `juere` (the roguish race in Elin) |
| `job` | 24 | `thief` |
| `LV` | 16 | 30 |
| `detail` | 50 | `A shadowy figure who deals in connections. They can put you in touch with anyone — for the right price.` |

### 2.3.2 Fixer Trait

```csharp
public class TraitUnderworldFixer : TraitUnique
{
    // When the player interacts with the Fixer, open the Network Panel
    public override bool OnUse(Chara c)
    {
        if (c.IsPC)
        {
            UnderworldPlugin.Instance.UI.OpenNetworkPanel();
            return true;
        }
        return false;
    }
}
```

### 2.3.3 Fixer Placement

The Fixer spawns in **Derphy** — Elin's canonical criminal town where `AllowCriminal => true` ([Zone_Derphy.cs](Documents/ElinMods/Elin-Decompiled-main/Elin/Zone_Derphy.cs)).

For the "Underworld Startup" scenario, the Fixer initially spawns in the player's starting zone (via bootstrap) and later relocates to Derphy. For standalone exploration, the Fixer can be found in Derphy.

**Respawn handling**: The Fixer should be a global NPC (`SetGlobal`) that persists across zone reloads. This follows the same pattern as vanilla unique NPCs. If Derphy regenerates (monthly town reset), the Fixer is re-injected via a `Zone.OnVisit` postfix patch — identical to [SkyreaderGuild's portal injection](Documents/ElinMods/SkyreaderGuild/Zone_SkyreaderGuild.cs).

---

## 2.4 Criminal System Integration

Elin already has a robust criminal status system. The Underworld mod integrates with it rather than replacing it. Critically, vanilla criminal status (`IsCriminal`) is triggered by `karma < 0` and makes guards in lawful zones hostile — turning every town visit into potential combat. The mod must provide skills that let the player operate as a dealer without every town trip becoming a warzone.

### 2.4.1 Relevant Vanilla Systems

| Property/Method | Location | Purpose |
|-----------------|----------|---------|
| `Zone.AllowCriminal` | [Zone.cs](Documents/ElinMods/Elin-Decompiled-main/Elin/Zone.cs) | Virtual property — `true` for Derphy and lawless zones |
| `Zone.HasLaw` | [Zone.cs](Documents/ElinMods/Elin-Decompiled-main/Elin/Zone.cs) | `true` for towns with guards; `false` for wilderness/dungeons |
| `Player.IsCriminal` | [Player.cs L1408-L1418](Documents/ElinMods/Elin-Decompiled-main/Elin/Player.cs#L1408-L1418) | `karma < 0 && !HasCondition<ConIncognito>()` — criminal flag |
| `ConIncognito` | Elin condition system | Condition that masks criminal status — guards ignore the player while active |
| `TraitGuard` | [Chara.cs L6701](Documents/ElinMods/Elin-Decompiled-main/Elin/Chara.cs#L6701) | Guards aggro on PC party if `IsCriminal` and not in an instance zone |
| `TraitMerchantDrug.ShopType` | [TraitMerchantDrug.cs](Documents/ElinMods/Elin-Decompiled-main/Elin/TraitMerchantDrug.cs) | Returns `ShopType.Drug` — sells potions/drugs |
| `TraitMerchantBlack.CanSellStolenGoods` | [TraitMerchantBlack.cs](Documents/ElinMods/Elin-Decompiled-main/Elin/TraitMerchantBlack.cs) | Returns `true` — accepts stolen goods |
| `GuildThief.SellStolenPrice` | [GuildThief.cs L17-L24](Documents/ElinMods/Elin-Decompiled-main/Elin/GuildThief.cs#L17-L24) | Price formula for stolen goods: `price * 100 / (190 - rank * 2)` |

### 2.4.2 How the Mod Uses These Systems

1. **Contraband is flagged criminal**: Custom contraband items have a tag (`tag: "contraband"`) that triggers `IsCriminal` checks when carried in lawful zones. This means carrying contraband through Palmia courts risk — but Derphy is safe.

2. **Contraband sales leverage existing merchants**: The Drug Merchant (`TraitMerchantDrug`) and Black Market Merchant (`TraitMerchantBlack`) in Derphy can buy contraband at base rates. The Fixer's network offers better prices but with server-side resolution delay.

3. **Guild Thief synergy**: Players who also join the Thieves' Guild benefit from the `SellStolenPrice` formula for contraband sold to vanilla merchants. This creates a natural synergy — the Thieves' Guild improves vanilla merchant prices, while the Fixer's network handles bulk operations.

4. **Zone-based risk**: Operations in `HasLaw == true` zones (most towns) attract criminal status and potential guard aggro. Operations in `AllowCriminal` zones (Derphy, wilderness) are safe. This maps perfectly to the heat/enforcement system: high-heat shipments through lawful territories are riskier.

### 2.4.3 Custom Underworld Skills

Vanilla Elin's criminal system is designed around the Thieves' Guild, where outlaw status makes most towns extremely dangerous. Our player is a *dealer*, not a generalized criminal — they need to walk through Palmia to sell samples and through Port Kapul to meet contacts without every visit becoming open warfare.

These custom skills are registered as Element entries in `SourceGame.xlsx` Element sheet, following the same pattern as vanilla skills (type: `Skill`, group: `SKILL`, category: `skill`).

#### Skill 1: **Shadow Guise** (Disguise / Anti-Detection)

| Field | Value |
|-------|-------|
| ID | `uw_shadow_guise` (custom Element ID, 90000+ range) |
| Name | Shadow Guise |
| Type | `Skill` |
| Category | `skill` / `stealth` |
| Parent Attribute | `PER` |
| Description | The art of blending into crowds and appearing unremarkable. Reduces the chance of guard aggro while carrying contraband in lawful zones. |

**Mechanical effect**:
- At any level > 0, the player gains a periodic `ConIncognito`-like buff when entering lawful towns, with duration scaling with skill level.
- This is implemented as a Harmony postfix on `Zone.OnVisit`: when the player enters a `HasLaw` zone, apply a custom condition (`ConShadowGuise`) whose duration = `skill_level * SHADOW_GUISE_DURATION_PER_LEVEL` in-game minutes.
- While `ConShadowGuise` is active, the `IsCriminal` check returns `false` (same mechanism as `ConIncognito`).
- The condition wears off naturally — long visits or loitering in town become progressively riskier.
- **Skill training**: Passive gain whenever the player enters a lawful zone while carrying contraband and leaves without being detected.

```csharp
/// <summary>
/// Custom condition that masks criminal status in lawful zones.
/// Duration scales with Shadow Guise skill level.
/// Mechanically identical to ConIncognito but with underworld flavor.
/// </summary>
public class ConShadowGuise : Condition
{
    public override bool AllowCriminal => true; // Same effect as ConIncognito
    
    public override int GetPhase()
    {
        return value switch
        {
            >= 30 => 2, // Strong guise
            >= 10 => 1, // Moderate
            _ => 0,     // Weak
        };
    }
}

// Applied in Zone.OnVisit postfix:
public static void ApplyShadowGuise()
{
    if (!EClass._zone.HasLaw) return;
    
    int skillLevel = EClass.pc.elements.GetBase(UW_SHADOW_GUISE_ID);
    if (skillLevel <= 0) return;
    
    int duration = skillLevel * UnderworldConfig.ShadowGuiseDurationPerLevel.Value;
    EClass.pc.AddCondition<ConShadowGuise>(duration);
}
```

#### Skill 2: **Silver Tongue** (Bribery / Social Engineering)

| Field | Value |
|-------|-------|
| ID | `uw_silver_tongue` (custom Element ID) |
| Name | Silver Tongue |
| Type | `Skill` |
| Category | `skill` / `general` |
| Parent Attribute | `CHA` |
| Description | The ability to talk your way out of — or into — anything. Reduces karma penalties for criminal actions and improves prices when dealing with NPCs. |

**Mechanical effects**:
- **Karma shield**: When the player would lose karma from carrying contraband or other criminal actions, reduce the loss by `skill_level * SILVER_TONGUE_KARMA_REDUCTION_PCT / 100` (capped at 80% reduction). This slows the descent into deep negative karma.
- **Bribe on detection**: When guards would normally aggro, there's a `skill_level * 2`% chance to automatically spend gold (configurable base cost) to avoid the encounter. Msg: *"You slip a few coins to the guard. They suddenly find something very interesting to look at."*
- **Better street prices**: When selling contraband via the small-time dealing system (§5.6), payout is improved by `skill_level / 2`%.
- **Skill training**: Gains XP whenever a bribe succeeds, when selling to NPCs, or when completing deals in lawful zones.

#### Skill 3: **Nerve Conditioning** (Stamina / Endurance)

| Field | Value |
|-------|-------|
| ID | `uw_nerve_conditioning` (custom Element ID) |
| Name | Nerve Conditioning |
| Type | `Skill` |
| Category | `skill` / `general` |
| Parent Attribute | `WIL` |
| Description | Hardened resolve from a life of calculated risk. Increases maximum Shadow Nerve and accelerates regeneration. |

**Mechanical effects**:
- **Max nerve bonus**: `+skill_level * NERVE_CONDITIONING_BONUS_PER_LEVEL` to maximum nerve (stacks with rank-based max nerve).
- **Regen bonus**: Nerve regeneration per in-game hour increased by `skill_level / 10` (rounded down, minimum +0).
- **Skill training**: Gains XP whenever nerve is spent on an operation.

#### Skill Registration

All three skills are added to `.xlsx` Element sheet rows using the proven `add_meteor_items.py` pattern. They follow the same column structure as vanilla skills like `stealth` (ID 152) and `travel` (ID 240).

```python
UNDERWORLD_SKILLS = {
    90001: {
        "alias": "uw_shadow_guise",
        "name": "Shadow Guise",
        "aliasParent": "PER",
        "parentFactor": 20,
        "lvFactor": 100,
        "type": "Skill",
        "group": "SKILL",
        "category": "skill",
        "categorySub": "stealth",
        "detail": "The art of blending into crowds. Reduces detection in lawful zones.",
    },
    90002: {
        "alias": "uw_silver_tongue",
        "name": "Silver Tongue",
        "aliasParent": "CHA",
        "parentFactor": 20,
        "lvFactor": 100,
        "type": "Skill",
        "group": "SKILL",
        "category": "skill",
        "categorySub": "general",
        "detail": "The ability to talk your way out of trouble. Reduces karma loss and enables bribery.",
    },
    90003: {
        "alias": "uw_nerve_conditioning",
        "name": "Nerve Conditioning",
        "aliasParent": "WIL",
        "parentFactor": 10,
        "lvFactor": 100,
        "type": "Skill",
        "group": "SKILL",
        "category": "skill",
        "categorySub": "general",
        "detail": "Hardened resolve from a life of risk. Increases maximum nerve and regen rate.",
    },
}
```

#### Starter Skill Grants

During bootstrap (`UnderworldStartupBootstrap.Apply()`), the player receives initial levels in these skills:

```csharp
// ── Step 8: Grant Underworld Skills ──────────────────────────
player.elements.SetBase(90001, 5);  // Shadow Guise: level 5
player.elements.SetBase(90002, 3);  // Silver Tongue: level 3
player.elements.SetBase(90003, 1);  // Nerve Conditioning: level 1
```

This gives the player an immediate ability to navigate towns with some protection, with room to grow through use.

### 2.4.4 Dealing System Conditions

Two custom `BadCondition` subclasses support the addiction/overdose system (see [§5.7-5.8](./05_orders_reputation.md)). These follow the same pattern as [ConPoison](Documents/ElinMods/Elin-Decompiled-main/Elin/ConPoison.cs).

| Condition | Base Class | Applies To | Trigger | Cure |
|-----------|-----------|-----------|---------|------|
| `ConUWWithdrawal` | `BadCondition` | Dealing customers (Dependent+ addiction) | Not served within visit threshold | Serving the customer product |
| `ConUWOverdose` | `BadCondition` | Dealing customers (Addicted+ addiction) | OD roll at deal time (mild/severe) | Natural slow decay, or Alchemist's Reprieve item |

**Party member immunity**: Neither condition can be applied to `IsPCParty` or `IsPCFaction` characters. The dealing system excludes them entirely.

**ConUWWithdrawal phases**: SPD −5/−10/−15 and STR −5/−10 at increasing severity. Does not decay naturally — only cured by product. Severe phase causes periodic vomiting.

**ConUWOverdose phases**: SPD −10/−20/−30, STR −5/−10/−15, WIL −10/−15 at increasing severity. Phase 2 adds `ConParalyze`. Slow decay (1/10 tick rate). Severe phase causes vomiting.

Full implementation in [§5.7.4](./05_orders_reputation.md) and [§5.8.4](./05_orders_reputation.md).

### 2.4.5 Drug Consumption Conditions

14 custom `Condition` and `BadCondition` subclasses support the personal drug use system (see [§4.5](./04_farming_and_smoking.md)). These follow the pattern of vanilla [ConSmoking](Documents/ElinMods/Elin-Decompiled-main/Elin/ConSmoking.cs).

**Active drug buffs (Condition subclasses):**

| Condition | Drug | Key Stats | Duration |
|-----------|------|-----------|----------|
| `ConUWWhisperHigh` | Whisper Tonic | PV +15, DV +10, SPD −5 | p/5 |
| `ConUWShadowRush` | Shadow Elixir | SPD +15, extra action/3 turns | p/3 |
| `ConUWDreamHigh` | Dream Powder / Dream Cookie | INT +3, PER +2 | p/5 |
| `ConUWVoidRage` | Void Salts | STR +3, WIL −3 | p/5 |
| `ConUWCrimsonSurge` | Crimson Elixir | STR +3, END +3, MaxHP +50 | p/5 |
| `ConUWWhisperCalm` | Whispervine Roll | PV +10, sleepiness −1/tick | p/4 |
| `ConUWDreamCalm` | Dreamweed Joint | CHA +3 | p/5 |
| `ConUWBerserkerRage` | Berserker's Draught | STR +5, END +3 | p/4 |
| `ConUWShadowRushX` | Shadow Rush | SPD +20, extra action/2 turns | p/2 |
| `ConUWFrostbloom` | Frostbloom Elixir | END +4, HP regen +3/tick | p/6 |
| `ConUWAshveil` | Ashveil Incense | PER +5, + ConSeeInvisible | p/5 |

**Crash/comedown debuffs (BadCondition subclasses):**

| Condition | Triggered By | Key Stats | Duration |
|-----------|-------------|-----------|----------|
| `ConUWShadowCrash` | ConUWShadowRush expire | SPD −10, stamina drain | 30 |
| `ConUWBerserkerCrash` | ConUWBerserkerRage expire | STR −3, END −2, ConConfuse | 15 |
| `ConUWRushCrash` | ConUWShadowRushX expire | SPD −15, stamina drain, ConDim | 40 |

SourceGame_Stat entries (IDs 90100-90113) are specified in [§4.5.3](./04_farming_and_smoking.md).

### 2.4.6 Smoking Item Harmony Patch

Vanilla `Chara.TryUse()` (line 8098) only recognizes `id == "cigar"` for smokeable items. Our 4 smokeable items require a Harmony Prefix on `Chara.TryUse()` that checks a HashSet of our item IDs and delegates to `TraitItemProc.OnUse()`. Full implementation in [§4.4.1](./04_farming_and_smoking.md).

---

## 2.5 Configuration & Tunability

All gameplay-significant values in this module are exposed via BepInEx `ConfigEntry<T>` bindings, following the [SkyreaderGuild config pattern](Documents/ElinMods/SkyreaderGuild/SkyreaderGuild.cs#L243-L253). This allows players and server operators to adjust the experience without code changes.

### 2.5.1 Client-Side Config (BepInEx)

```csharp
// In UnderworldPlugin.Awake()

// ── Scenario ──
ConfigStartingGold = Config.Bind("Scenario", "StartingGold", 5000,
    "Gold granted during Underworld Startup.");

// ── Skills ──
ConfigShadowGuiseDurationPerLevel = Config.Bind("Skills", "ShadowGuiseDurationPerLevel", 10,
    "In-game minutes of disguise per Shadow Guise skill level when entering a lawful zone.");
ConfigSilverTongueKarmaReductionPct = Config.Bind("Skills", "SilverTongueKarmaReductionPct", 2,
    "Percent karma loss reduction per Silver Tongue level (max 80%).");
ConfigSilverTongueBribeBaseCost = Config.Bind("Skills", "SilverTongueBribeBaseCost", 500,
    "Base gold cost for an automatic guard bribe.");
ConfigNerveConditioningBonusPerLevel = Config.Bind("Skills", "NerveConditioningBonusPerLevel", 2,
    "Additional max nerve per Nerve Conditioning skill level.");

// ── Network ──
ConfigServerUrl = Config.Bind("Network", "ServerUrl", "http://localhost:8000",
    "URL of the Underworld backend server.");
ConfigPollIntervalSeconds = Config.Bind("Network", "PollIntervalSeconds", 300,
    "How often to poll the server for results (seconds).");
ConfigOfflineMode = Config.Bind("Network", "OfflineMode", false,
    "If true, all network features are disabled.");
```

### 2.5.2 Config Reference Table

| Config Key | Type | Default | Section | Used In |
|------------|------|---------|---------|--------|
| `StartingGold` | int | 5000 | Scenario | Bootstrap |
| `ShadowGuiseDurationPerLevel` | int | 10 | Skills | Zone.OnVisit patch |
| `SilverTongueKarmaReductionPct` | int | 2 | Skills | Karma loss patch |
| `SilverTongueBribeBaseCost` | int | 500 | Skills | Guard aggro patch |
| `NerveConditioningBonusPerLevel` | int | 2 | Skills | NerveTracker |
| `ServerUrl` | string | `http://localhost:8000` | Network | NetworkClient |
| `PollIntervalSeconds` | int | 300 | Network | Polling loop |
| `OfflineMode` | bool | false | Network | All network calls |

---

## 2.6 Save Compatibility

### 2.6.1 Mod Removal Safety

When the mod is disabled:
- Custom items (mixing table, contraband, chest) become "alchemical ash" — Elin's default behavior for items with unknown IDs
- Custom NPCs (Fixer) disappear — their chara IDs are no longer recognized
- Custom zones become inaccessible — the zone type classes don't exist
- Custom skills become orphaned element IDs — harmless, no crash
- No save corruption — Elin handles unknown entries gracefully

### 2.6.2 Mod State Persistence

Local mod state (accepted orders, cached reputation) is stored in one of two ways:

**Option A — Mod-specific save file** (recommended):
```csharp
// Save to a separate JSON file alongside the Elin save
var savePath = Path.Combine(Game.Instance.savePath, "underworld_state.json");
File.WriteAllText(savePath, JsonConvert.SerializeObject(state));
```

**Option B — Player element values** (for simple numeric state):
Use Elin's existing element system to store values on the player card. This survives mod removal as orphaned element IDs.

### 2.6.3 Server Data Independence

All multiplayer state (orders, shipments, reputation, territory) lives on the server. The client mod is a thin view into server state. Disabling the mod doesn't affect the server — the player's faction membership and reputation persist.

---

## 2.7 Testing & Verification

### Startup Scenario Tests

| Test | Steps | Expected Result |
|------|-------|-----------------|
| Scenario appears | New game → character creation | "Underworld Startup" appears in scenario dropdown |
| Scenario selectable | Select "Underworld Startup" → create character | Game starts without crash |
| Bootstrap items | Start Underworld game | Inventory contains: mixing table, chest, 10× whispervine, 5× crude moonite, 5000 gold, axe, pickaxe, hoe, bandages, torch, rations |
| Basic tool check | Start Underworld game → check inventory | Player has axe, pickaxe, hoe for Elin crafting/gathering |
| Fixer spawned | Start Underworld game → check starting zone | Fixer NPC present, interactable |
| QuestMain suppressed | Start Underworld game → check quest log | QuestMain at phase 0, no main quest prompts or NPCs |
| QuestHome active | Start Underworld game → check quest log | QuestHome at phase 2 (land claimed) |
| Vanilla scenarios unaffected | New game → select Meadow/Cave start | Vanilla scenarios work identically to unmodded game |

### Zone Registration Tests

| Test | Expected |
|------|----------|
| Custom zone loads | Enter `uw_hideout` zone → map generates, no errors in log |
| Zone properties | `uw_hideout.AllowCriminal == true`, `uw_hideout.HasLaw == false` |
| Zone persistence | Enter zone, save, reload → zone state preserved |

### Criminal System Integration Tests

| Test | Expected |
|------|----------|
| Contraband criminal flag | Carry contraband in Palmia → `IsCriminal` triggered |
| Derphy safe | Carry/sell contraband in Derphy → no criminal flag |
| Drug merchant interaction | Sell contraband to drug merchant → accepted, paid at base rate |
| Thief guild synergy | Join Thief guild → sell contraband to merchant → price improved by rank |

### Underworld Skill Tests

| Test | Steps | Expected |
|------|-------|----------|
| Shadow Guise activates | Enter Palmia with skill level 10 | `ConShadowGuise` applied, duration ≈ 100 minutes |
| Shadow Guise hides criminal | Enter lawful zone with karma < 0 | Guards do not aggro while condition active |
| Shadow Guise expires | Wait in town past duration | Condition expires, guards may aggro |
| Silver Tongue karma shield | Commit criminal act with skill level 20 | Karma loss reduced by ~40% |
| Silver Tongue bribe | Enter lawful zone, guard aggro triggers | Gold deducted, encounter avoided (prob: level × 2%) |
| Nerve Conditioning | Check max nerve at skill level 10 | Max nerve = base + 20 |
| Skill training | Enter/leave lawful zones with contraband | Shadow Guise skill XP increases |
| Config override | Set `ShadowGuiseDurationPerLevel=20` in config | Duration doubles |

### Save Compatibility Tests

| Test | Expected |
|------|----------|
| Save/load with mod | Save mid-game → reload → all mod state preserved |
| Disable mod, load save | Disable mod → load save → custom items become ash, no crash |
| Re-enable mod, load save | Re-enable mod → load save → mod state recoverable if save file exists |
| Fresh start after disable | Disable mod → new game → no traces of mod, vanilla scenarios work |

### Log Verification

After every test pass, check both log files for errors:
- BepInEx log: `D:\Steam\steamapps\common\Elin\BepInEx\LogOutput.log`
- Unity/player log: `C:\Users\someuser\AppData\LocalLow\Lafrontier\Elin\Player.log`
