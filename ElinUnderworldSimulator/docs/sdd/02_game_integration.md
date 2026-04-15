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
- [FastStart Plugin.cs L41-L73](file:///c:/Users/mcounts/Documents/ElinMods/FastStart/Plugin.cs#L41-L73) — Prologue injection pattern (proven working)
- [QuestMain.cs](file:///c:/Users/mcounts/Documents/ElinMods/Elin-Decompiled-main/Elin/QuestMain.cs) — Quest phase definitions

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

**Reference:** FastStart uses a similar approach at [L41-L54](file:///c:/Users/mcounts/Documents/ElinMods/FastStart/Plugin.cs#L41-L54) — it returns `Prologue.list[0]` as the template prologue for custom start indices.

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

**Reference:** [FastStart L56-L73](file:///c:/Users/mcounts/Documents/ElinMods/FastStart/Plugin.cs#L56-L73) — dropdown mutation after vanilla population.

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

**Reference:** [FastStart L75-L110](file:///c:/Users/mcounts/Documents/ElinMods/FastStart/Plugin.cs#L75-L110).

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

**Reference:** [FastStart L144-L189](file:///c:/Users/mcounts/Documents/ElinMods/FastStart/Plugin.cs#L144-L189).

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
        // Give the player basic underworld tools
        var items = new[]
        {
            ("uw_mixing_table", 1),    // Portable mixing table (furniture)
            ("uw_contraband_chest", 1), // Shipping chest
            ("uw_herb_basic", 10),      // Basic herb ingredient
            ("uw_mineral_crude", 5),    // Crude mineral ingredient
            ("money", 5000),            // Starting gold
        };
        
        foreach (var (id, count) in items)
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

The mod may register 1-2 custom zones for underworld-specific locations. These follow the exact same pattern used by [Zone_SkyreaderGuild](file:///c:/Users/mcounts/Documents/ElinMods/SkyreaderGuild/Zone_SkyreaderGuild.cs).

**Potential custom zones:**

| Zone ID | Zone Class | Purpose | Type |
|---------|-----------|---------|------|
| `uw_hideout` | `Zone_UnderworldHideout` | Player's secret processing facility | Interior, instanced |

Each zone requires:
1. A `Zone_` class inheriting from `Zone` (or a subclass like `Zone_Dungeon`)
2. An entry in `SourceGame.xlsx` Zone sheet following the [SourceGame_Zone schema](file:///c:/Users/mcounts/Documents/ElinMods/elin_readable_game_data/SourceGame_Zone.md)
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
    }
}
```

**Source patterns:**
- [Zone_Derphy.cs](file:///c:/Users/mcounts/Documents/ElinMods/Elin-Decompiled-main/Elin/Zone_Derphy.cs) — `AllowCriminal => true`
- [Zone_SkyreaderGuild](file:///c:/Users/mcounts/Documents/ElinMods/SkyreaderGuild/Zone_SkyreaderGuild.cs) — custom zone registration and map generation

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

Reference: [SourceGame_Zone.md column definitions](file:///c:/Users/mcounts/Documents/ElinMods/elin_readable_game_data/SourceGame_Zone.md) L3-L28.

---

## 2.3 NPC Integration — The Fixer

### 2.3.1 Chara Source Sheet Entry

The Fixer is a unique NPC with a custom chara entry in `SourceCard.xlsx` Chara sheet.

Column values (using [CHARA_COL mapping](file:///c:/Users/mcounts/Documents/ElinMods/SkyreaderGuild/worklog/scripts/add_meteor_items.py#L208-L259)):

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

The Fixer spawns in **Derphy** — Elin's canonical criminal town where `AllowCriminal => true` ([Zone_Derphy.cs](file:///c:/Users/mcounts/Documents/ElinMods/Elin-Decompiled-main/Elin/Zone_Derphy.cs)).

For the "Underworld Startup" scenario, the Fixer initially spawns in the player's starting zone (via bootstrap) and later relocates to Derphy. For standalone exploration, the Fixer can be found in Derphy.

**Respawn handling**: The Fixer should be a global NPC (`SetGlobal`) that persists across zone reloads. This follows the same pattern as vanilla unique NPCs. If Derphy regenerates (monthly town reset), the Fixer is re-injected via a `Zone.OnVisit` postfix patch — identical to [SkyreaderGuild's portal injection](file:///c:/Users/mcounts/Documents/ElinMods/SkyreaderGuild/Zone_SkyreaderGuild.cs).

---

## 2.4 Criminal System Integration

Elin already has a robust criminal status system. The Underworld mod integrates with it rather than replacing it.

### 2.4.1 Relevant Vanilla Systems

| Property/Method | Location | Purpose |
|-----------------|----------|---------|
| `Zone.AllowCriminal` | [Zone.cs](file:///c:/Users/mcounts/Documents/ElinMods/Elin-Decompiled-main/Elin/Zone.cs) | Virtual property — `true` for Derphy and lawless zones |
| `Zone.HasLaw` | [Zone.cs](file:///c:/Users/mcounts/Documents/ElinMods/Elin-Decompiled-main/Elin/Zone.cs) | `true` for towns with guards; `false` for wilderness/dungeons |
| `Chara.IsCriminal` | Elin character system | Player flag set on criminal actions |
| `TraitMerchantDrug.ShopType` | [TraitMerchantDrug.cs](file:///c:/Users/mcounts/Documents/ElinMods/Elin-Decompiled-main/Elin/TraitMerchantDrug.cs) | Returns `ShopType.Drug` — sells potions/drugs |
| `TraitMerchantBlack.CanSellStolenGoods` | [TraitMerchantBlack.cs](file:///c:/Users/mcounts/Documents/ElinMods/Elin-Decompiled-main/Elin/TraitMerchantBlack.cs) | Returns `true` — accepts stolen goods |
| `GuildThief.SellStolenPrice` | [GuildThief.cs L17-L24](file:///c:/Users/mcounts/Documents/ElinMods/Elin-Decompiled-main/Elin/GuildThief.cs#L17-L24) | Price formula for stolen goods: `price * 100 / (190 - rank * 2)` |

### 2.4.2 How the Mod Uses These Systems

1. **Contraband is flagged criminal**: Custom contraband items have a tag (`tag: "contraband"`) that triggers `IsCriminal` checks when carried in lawful zones. This means carrying contraband through Palmia courts risk — but Derphy is safe.

2. **Contraband sales leverage existing merchants**: The Drug Merchant (`TraitMerchantDrug`) and Black Market Merchant (`TraitMerchantBlack`) in Derphy can buy contraband at base rates. The Fixer's network offers better prices but with server-side resolution delay.

3. **Guild Thief synergy**: Players who also join the Thieves' Guild benefit from the `SellStolenPrice` formula for contraband sold to vanilla merchants. This creates a natural synergy — the Thieves' Guild improves vanilla merchant prices, while the Fixer's network handles bulk operations.

4. **Zone-based risk**: Operations in `HasLaw == true` zones (most towns) attract criminal status and potential guard aggro. Operations in `AllowCriminal` zones (Derphy, wilderness) are safe. This maps perfectly to the heat/enforcement system: high-heat shipments through lawful territories are riskier.

---

## 2.5 Save Compatibility

### 2.5.1 Mod Removal Safety

When the mod is disabled:
- Custom items (mixing table, contraband, chest) become "alchemical ash" — Elin's default behavior for items with unknown IDs
- Custom NPCs (Fixer) disappear — their chara IDs are no longer recognized
- Custom zones become inaccessible — the zone type classes don't exist
- No save corruption — Elin handles unknown entries gracefully

### 2.5.2 Mod State Persistence

Local mod state (accepted orders, cached reputation) is stored in one of two ways:

**Option A — Mod-specific save file** (recommended):
```csharp
// Save to a separate JSON file alongside the Elin save
var savePath = Path.Combine(Game.Instance.savePath, "underworld_state.json");
File.WriteAllText(savePath, JsonConvert.SerializeObject(state));
```

**Option B — Player element values** (for simple numeric state):
Use Elin's existing element system to store values on the player card. This survives mod removal as orphaned element IDs.

### 2.5.3 Server Data Independence

All multiplayer state (orders, shipments, reputation, territory) lives on the server. The client mod is a thin view into server state. Disabling the mod doesn't affect the server — the player's faction membership and reputation persist.

---

## 2.6 Testing & Verification

### Startup Scenario Tests

| Test | Steps | Expected Result |
|------|-------|-----------------|
| Scenario appears | New game → character creation | "Underworld Startup" appears in scenario dropdown |
| Scenario selectable | Select "Underworld Startup" → create character | Game starts without crash |
| Bootstrap items | Start Underworld game | Inventory contains: mixing table, chest, 10× basic herb, 5× crude mineral, 5000 gold |
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
