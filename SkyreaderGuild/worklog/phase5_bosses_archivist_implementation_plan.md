# Phase 5: Boss Summoning & Astral Archivist - Implementation Plan

## Goal

Implement the endgame boss-summoning system for the Skyreader Guild using source-sheet imported character rows. Four cosmic bosses (`srg_umbryon`, `srg_solaris`, `srg_erevor`, `srg_quasarix`) are summoned from Codex-crafted scrolls. The Astral Archivist (`srg_archivist`) is summoned from the Understander-rank convergence scroll and should be neutral/recruitable. Boss kills award guild points and Meteorite Source.

## Current-State Findings

The workbook `SkyreaderGuild/LangMod/EN/SourceCard.xlsx` already has `Chara`, `Quest`, `CharaText`, and `Thing` sheets. The earlier plan's claim that there is no `Chara` sheet is stale.

The `Chara` sheet already contains placeholder rows for the Phase 5 NPCs, but they are incomplete:

- `srg_umbryon`, `srg_solaris`, `srg_quasarix`, and `srg_archivist` exist with mostly blank/wrong fields.
- Erevor is currently misspelled as `srg_ervor`; the implementation must normalize this to `srg_erevor`.
- The existing `srg_archivist` row has wrong fields, including `job = predator` and stale loot/work-style values. The ensure script must clear stale cells, not only fill blanks.

The five scroll Thing rows already exist, but all five currently have `trait = Item`. They must be fixed to `BossScroll` or `ArchivistScroll`.

The workbook currently needs shared-string normalization after edits. Keep using the existing `normalize_shared_strings()` path in `add_meteor_items.py`; do not save final source sheets with inline strings.

## Corrections To The Older Worklog

### TraitScroll signature

`TraitScroll.OnRead(Chara c)` is inherited from `Trait` and returns `void`. The older worklog uses `public override bool OnRead(Chara c)`, which will not compile. Implement:

```csharp
public override void OnRead(Chara c)
```

Do not consume the scroll on failure. Consume with `owner.ModNum(-1, true)` only after the summon succeeds.

### Trait class namespace

The project's existing item trait classes (`TraitMeteorCore`, `TraitAstralExtractor`, `TraitStarImbuement`, etc.) are in the global namespace. This matters because Elin's `Card.ApplyTrait()` creates a trait from `"Trait" + sourceCard.trait[0]`, then searches assemblies by that simple type name.

Therefore the new trait classes should also be global namespace classes:

```csharp
using SkyreaderGuild;

public class TraitBossScroll : TraitScroll
{
    // ...
}
```

Do not wrap them in `namespace SkyreaderGuild` unless the source-sheet trait mechanism is changed to instantiate fully-qualified type names, which it currently does not do.

### Boss abilities

The older worklog put spells in `elements`. That is wrong. Boss actions go in `actCombat`, for example:

```text
bolt_Darkness/50,miasma_Poison/30,ActDrainBlood/60
```

The `elements` column is for static element/stat/feature entries such as:

```text
life/100,resDarkness/20
```

### Source-sheet chara import

Use the `Chara` sheet in `SourceCard.xlsx` as the authority for boss and Archivist rows. Do not create the Phase 5 charas programmatically in `OnStartCore`.

This matches the user's preference and keeps characters inspectable/editable alongside the existing `srg_growth`, `srg_arkyn`, and Phase 5 placeholder rows.

## Logical Gaps To Close

- **Erevor id typo:** any scroll code must map to `srg_erevor`, and the sheet must not leave the typo row `srg_ervor` behind as a dead duplicate.
- **Summon GP exploit:** the Phase 5 goal says killing bosses yields GP. Do not award GP just for reading a boss scroll. Otherwise the player can gain progression before winning the encounter.
- **Archivist GP:** the Archivist is itself an Understander-rank reward. Do not award extra GP on summon unless a later design explicitly calls for an Archivist service reward.
- **Spawn tile safety:** `GetNearestPoint(false, true, true, false)` can choose the player's own occupied tile because `allowChara` is true and `ignoreCenter` is false. Use `allowChara: false`, `allowInstalled: false`, and `ignoreCenter: true`.
- **Thematic boss loot:** the older phase document mentions thematic equipment rewards, but the current goal only requires GP and Meteorite Source. Treat unique/thematic equipment as a follow-up balance pass unless the design is expanded now.
- **Archivist services:** the README mentions special interactions/services, but no concrete service design exists. Phase 5 should deliver summon + neutral/recruitable NPC; custom dialog/services need a separate spec.

## Component 1: Source Sheet Ensure Script

### Modify `SkyreaderGuild/add_meteor_items.py`

Extend the existing script so it manages both `Thing` and `Chara` sheets.

Keep the current shared-string normalization. The script should always call `normalize_shared_strings(SOURCE_CARD)` after saving or validating, because Elin's NPOI importer expects `sharedStrings.xml`.

### Refactor column maps

Rename the current `COL` map to `THING_COL`, then add a `CHARA_COL` map matching the `Chara` sheet:

```python
CHARA_COL = {
    "id": 1,
    "_id": 2,
    "name_JP": 3,
    "name": 4,
    "aka_JP": 5,
    "aka": 6,
    "idActor": 7,
    "sort": 8,
    "size": 9,
    "_idRenderData": 10,
    "tiles": 11,
    "tiles_snow": 12,
    "colorMod": 13,
    "components": 14,
    "defMat": 15,
    "LV": 16,
    "chance": 17,
    "quality": 18,
    "hostility": 19,
    "biome": 20,
    "tag": 21,
    "trait": 22,
    "race": 23,
    "job": 24,
    "tactics": 25,
    "aiIdle": 26,
    "aiParam": 27,
    "actCombat": 28,
    "mainElement": 29,
    "elements": 30,
    "equip": 31,
    "loot": 32,
    "category": 33,
    "filter": 34,
    "gachaFilter": 35,
    "tone": 36,
    "actIdle": 37,
    "lightData": 38,
    "idExtra": 39,
    "bio": 40,
    "faith": 41,
    "works": 42,
    "hobbies": 43,
    "idText": 44,
    "moveAnime": 45,
    "factory": 46,
    "components_2": 47,
    "recruitItems": 48,
    "detail_JP": 49,
    "detail": 50,
}
```

Note: `SourceChara.CreateRow()` reads `components` twice, once at column 14 and again at column 47. The later column wins. In the script, prefer `components_2` for the final Chara components column and leave the earlier `components` blank unless needed.

### Fix existing Thing scroll rows

The rows already exist. The script should ensure the final fields, not blindly append duplicates.

| id | category | sort | trait | components | tag |
| --- | --- | --- | --- | --- | --- |
| `srg_scroll_twilight` | `scroll` | `book_scroll` | `BossScroll` | `srg_meteorite_source/5,zettel/1` | `noShop` |
| `srg_scroll_radiance` | `scroll` | `book_scroll` | `BossScroll` | `srg_meteorite_source/5,zettel/1` | `noShop` |
| `srg_scroll_abyss` | `scroll` | `book_scroll` | `BossScroll` | `srg_meteorite_source/5,zettel/1` | `noShop` |
| `srg_scroll_nova` | `scroll` | `book_scroll` | `BossScroll` | `srg_meteorite_source/5,zettel/1` | `noShop` |
| `srg_scroll_convergence` | `scroll` | `book_scroll` | `ArchivistScroll` | `srg_meteorite_source/2,meat/10` | `noShop` |

Keep the existing paper/zettel theme. `meat/10` is the concrete source id for corpses in `SourceCard_Thing.md`.

### Add `EXPECTED_CHARAS`

Manage the five Phase 5 Chara rows through a new dictionary. Include blank values for stale columns that must be cleared, especially on `srg_archivist`.

Recommended rows:

| id | _id | name | race | job | hostility | LV | chance | quality | tag | trait | actCombat | mainElement | elements | render | tiles |
| --- | ---: | --- | --- | --- | --- | ---: | ---: | ---: | --- | --- | --- | --- | --- | --- | ---: |
| `srg_umbryon` | 900501 | Umbryon, Herald of Eternal Rot | `lich` | `warrior` | `Enemy` | 35 | 0 | 3 | `boss,noRandomProduct` | `UniqueMonster` | `bolt_Darkness/50,miasma_Poison/30,ActDrainBlood/60` | `Darkness` | `life/100,resDarkness/20,END/10,WIL/10` | blank | 1502 |
| `srg_solaris` | 900502 | Solaris, Inferno of the Fallen Star | `spirit` | `warmage` | `Enemy` | 40 | 0 | 3 | `boss,noRandomProduct` | `UniqueMonster` | `breathe_Fire/70,SpMeteor/10,bolt_Fire/50` | `Fire` | `resFire/30,life/80,MAG/15,mana/30` | blank | 1713 |
| `srg_erevor` | 900503 | Erevor, The Abyssal Maw | `dragon` | `predator` | `Enemy` | 45 | 0 | 3 | `boss,noRandomProduct` | `UniqueMonster` | `SpGravity/50,ActRush,breathe_Void/30` | `Impact` | `life/100,PDR/30,STR/15,END/15` | `chara_L` | 104 |
| `srg_quasarix` | 900504 | Quasarix, Devourer of Light | `god` | `gunner` | `Enemy` | 50 | 0 | 3 | `boss,noRandomProduct` | `UniqueMonster` | `SpSilence/50,ActGazeDim/30,bolt_Magic/60,SpBane/20` | `Darkness` | `resMagic/20,resDarkness/20,life/80,mana/50,MAG/15` | blank | 2317 |
| `srg_archivist` | 900505 | Astral Archivist | `elea` | `pianist` | `Neutral` | 30 | 0 | 4 | `neutral,noRandomProduct` | `UniqueChara` | `SpHealHeavy/60,SpHero/50/pt` | blank | `featHealer/1,reading/10,MAG/10,WIL/10` | blank | 1216 |

Notes:

- Use `lich`, not `undead`, for Umbryon. `undead` is a race tag, not a valid race id. The current source data has `lich`, `zombie`, `wraith`, `ghost`, etc.
- Use `elea`, not `ether`, for the Archivist. `ether` is not a valid race id in the current race data.
- `UniqueMonster` does not make a Chara globally unique; it only changes monster trait behavior. That is desirable for repeatable summoned bosses.
- `UniqueChara` does make the Archivist unique. Combined with `SetGlobal()` in the scroll trait, this supports duplicate prevention and persistence.
- Set `chance = 0` and `tag = noRandomProduct` so these rows are not random spawn/product candidates.
- The stat modifiers in `elements` are conservative. They partially express the older worklog's stat-block intent without hand-authoring a separate stat system.
- If a listed sprite id looks wrong in-game, swap only `tiles`/`_idRenderData`; keep ids and mechanics stable.

### Handle the Erevor typo row

Add cleanup logic before ensuring chara rows:

```python
def normalize_chara_typos(ws):
    rows = find_rows(ws)
    typo = rows.get("srg_ervor")
    correct = rows.get("srg_erevor")
    if typo is not None and correct is None:
        ws.cell(row=typo, column=CHARA_COL["id"]).value = "srg_erevor"
    elif typo is not None and correct is not None:
        ws.delete_rows(typo, 1)
```

After deleting a row, rebuild the row map before applying `EXPECTED_CHARAS`.

### Clear stale cells

For Chara rows, the script should write every managed field in `EXPECTED_CHARAS`, including blanks. This prevents stale values like the current Archivist `job = predator`, `loot = warrior`, `faith = eyth`, and `works = merchant` from surviving.

Use the existing `set_field()` pattern, but pass the column map:

```python
def set_field(ws, row, col_map, key, value):
    col = col_map[key]
    if value == "":
        value = None
    # existing compare/write/log behavior
```

## Component 2: Boss Scroll Trait

### Add `SkyreaderGuild/TraitBossScroll.cs`

Add the file to the global namespace and include it in `SkyreaderGuild.csproj`.

```csharp
using System.Collections.Generic;
using SkyreaderGuild;

public class TraitBossScroll : TraitScroll
{
    private static readonly Dictionary<string, string> ScrollToBoss = new Dictionary<string, string>
    {
        { "srg_scroll_twilight", "srg_umbryon" },
        { "srg_scroll_radiance", "srg_solaris" },
        { "srg_scroll_abyss", "srg_erevor" },
        { "srg_scroll_nova", "srg_quasarix" },
    };

    public override void OnRead(Chara c)
    {
        string scrollId = owner.id;
        if (!ScrollToBoss.TryGetValue(scrollId, out string bossId))
        {
            SkyreaderGuild.SkyreaderGuild.Log($"Unknown boss scroll id: {scrollId}");
            Msg.SayRaw("The scroll's sigils fail to align.");
            return;
        }

        Point spawnPoint = FindSummonPoint(c);
        if (spawnPoint == null || !spawnPoint.IsValid || spawnPoint.IsBlocked || spawnPoint.HasChara)
        {
            Msg.SayRaw("There isn't enough space to summon the creature.");
            return;
        }

        Chara boss = CharaGen.Create(bossId, -1);
        boss.hostility = Hostility.Enemy;
        boss.c_originalHostility = Hostility.Enemy;
        boss.enemy = c;
        EClass._zone.AddCard(boss, spawnPoint);

        Msg.SayRaw($"The scroll crumbles to dust as {boss.Name} materializes!");
        owner.ModNum(-1, true);
    }

    private static Point FindSummonPoint(Chara c)
    {
        Point p = c.pos.GetNearestPoint(
            allowBlock: false,
            allowChara: false,
            allowInstalled: false,
            ignoreCenter: true);

        if (p != null && p.IsValid && !p.IsBlocked && !p.HasChara)
        {
            return p;
        }

        p = EClass._map.bounds.GetRandomSurface(c.pos.x, c.pos.z, 4);
        return p != null ? p.GetNearestPoint(allowBlock: false, allowChara: false, allowInstalled: false) : null;
    }
}
```

Design decisions:

- No GP is awarded on summon.
- The scroll is consumed only after successful spawn.
- The boss id source is the scroll id mapping, not random selection.
- The typo `srg_ervor` is not supported in code; source data must be corrected to `srg_erevor`.

## Component 3: Archivist Scroll Trait

### Add `SkyreaderGuild/TraitArchivistScroll.cs`

Add the file to the global namespace and include it in `SkyreaderGuild.csproj`.

```csharp
using SkyreaderGuild;

public class TraitArchivistScroll : TraitScroll
{
    public override void OnRead(Chara c)
    {
        Chara existing = EClass.game.cards.globalCharas.Find("srg_archivist");
        if (existing != null)
        {
            Msg.SayRaw("The Astral Archivist is already present in this world.");
            return;
        }

        Point spawnPoint = FindSummonPoint(c);
        if (spawnPoint == null || !spawnPoint.IsValid || spawnPoint.IsBlocked || spawnPoint.HasChara)
        {
            Msg.SayRaw("There isn't enough space for the summoning.");
            return;
        }

        Chara archivist = CharaGen.Create("srg_archivist", -1);
        archivist.hostility = Hostility.Neutral;
        archivist.c_originalHostility = Hostility.Neutral;
        archivist.SetGlobal();
        EClass._zone.AddCard(archivist, spawnPoint);

        Msg.SayRaw("A figure materializes from streams of starlight. The Astral Archivist has arrived.");
        owner.ModNum(-1, true);
    }

    private static Point FindSummonPoint(Chara c)
    {
        Point p = c.pos.GetNearestPoint(
            allowBlock: false,
            allowChara: false,
            allowInstalled: false,
            ignoreCenter: true);

        if (p != null && p.IsValid && !p.IsBlocked && !p.HasChara)
        {
            return p;
        }

        p = EClass._map.bounds.GetRandomSurface(c.pos.x, c.pos.z, 4);
        return p != null ? p.GetNearestPoint(allowBlock: false, allowChara: false, allowInstalled: false) : null;
    }
}
```

Potential cleanup after implementation: if both traits need the same summon-point helper, move it into an internal static helper only after both classes compile. Do not add the abstraction first.

## Component 4: Boss Kill Reward Patch

### Modify `SkyreaderGuild/SkyreaderGuild.cs`

Add a Phase 5 patch class near the existing Harmony patches.

```csharp
[HarmonyPatch(typeof(Card), "SpawnLoot")]
public static class BossKillRewardPatch
{
    private static readonly HashSet<string> BossIds = new HashSet<string>
    {
        "srg_umbryon",
        "srg_solaris",
        "srg_erevor",
        "srg_quasarix",
    };

    public static void Postfix(ref Card __instance)
    {
        if (!__instance.isChara) return;
        Chara chara = __instance.Chara;
        if (chara == null || !BossIds.Contains(chara.id)) return;

        QuestSkyreader quest = EClass.game.quests.Get<QuestSkyreader>();
        if (quest != null)
        {
            int gpReward = chara.LV * 10;
            quest.AddGuildPoints(gpReward);
        }
        else
        {
            SkyreaderGuild.Log($"Boss reward GP skipped because QuestSkyreader is missing: {chara.id}");
        }

        int sourceCount = 2 + EClass.rnd(3);
        Point dropPoint = __instance.GetRootCard().pos;
        for (int i = 0; i < sourceCount; i++)
        {
            Point p = dropPoint.GetNearestPoint(allowBlock: false, allowChara: true, allowInstalled: true) ?? dropPoint;
            EClass._zone.AddThing("srg_meteorite_source", p);
        }

        Msg.SayRaw($"The cosmic energy of {chara.Name} dissipates, leaving fragments of meteoric ore.");
    }
}
```

Notes:

- This is a second `Card.SpawnLoot` postfix alongside `YithDropStarchart`. Harmony can chain both.
- The two patches check disjoint ids, so they should not interfere.
- Meteorite Source drops should still occur even if the quest lookup fails; only GP depends on the quest.

## Component 5: Project File

### Modify `SkyreaderGuild/SkyreaderGuild.csproj`

Because `<EnableDefaultCompileItems>false</EnableDefaultCompileItems>` is set, new files must be explicitly included:

```xml
<Compile Include="TraitBossScroll.cs" />
<Compile Include="TraitArchivistScroll.cs" />
```

Place them near the other trait includes.

## Verification Plan

### Source data verification

1. Run `python SkyreaderGuild/add_meteor_items.py`.
2. Confirm the script reports fixes for existing scroll traits if they are still `Item`.
3. Confirm `srg_ervor` is gone and `srg_erevor` exists.
4. Confirm `SourceCard.xlsx` contains `xl/sharedStrings.xml` after the script runs.
5. Inspect the `Chara` sheet:
   - Boss rows have valid `race`, `job`, `trait`, `actCombat`, `elements`, `tiles`.
   - Archivist row has `race = elea`, `job = pianist`, `hostility = Neutral`, `trait = UniqueChara`.
   - Stale Archivist fields from the placeholder row are cleared.

### Build verification

1. Run `dotnet build SkyreaderGuild/SkyreaderGuild.csproj`.
2. Fix compile errors before any in-game testing. Expected risk areas:
   - missing `using System.Collections.Generic`;
   - trait namespace mismatch;
   - `OnRead` accidentally returning `bool`;
   - new files missing from `.csproj`.

### In-game verification

1. Install/update the mod and launch Elin.
2. Check both logs after load:
   - `D:\Steam\steamapps\common\Elin\BepInEx\LogOutput.log`
   - `C:\Users\mcounts\AppData\LocalLow\Lafrontier\Elin\Player.log`
3. At the Astrological Codex, verify:
   - boss scrolls are visible only at `CosmosApplied`;
   - convergence scroll is visible only at `Understander`;
   - scroll rows use `TraitBossScroll` / `TraitArchivistScroll`, not `TraitItem`.
4. Read each boss scroll and verify:
   - scroll is consumed;
   - the correct boss id spawns;
   - the boss does not spawn on top of the player;
   - hostility is enemy;
   - abilities fire in combat.
5. Kill each boss and verify:
   - GP reward is `LV * 10`;
   - 2-4 Meteorite Source drops;
   - no starchart drop logic runs for bosses.
6. Read the Archivist scroll and verify:
   - Archivist spawns as neutral;
   - scroll is consumed;
   - Archivist is global/persistent after save-load;
   - a second Archivist scroll refuses to summon another copy and is not consumed.
7. Try recruitment interaction. If generic recruitment is not available, log it as a Phase 5 follow-up because custom Archivist dialog/services are not specified yet.

## File Changes Summary

| File | Change Type | Description |
| --- | --- | --- |
| `SkyreaderGuild/add_meteor_items.py` | MODIFY | Manage both Thing and Chara rows, fix scroll traits, normalize `srg_ervor`, clear stale Chara cells, preserve shared strings |
| `SkyreaderGuild/LangMod/EN/SourceCard.xlsx` | MODIFY | Source-imported Phase 5 boss and Archivist rows plus corrected scroll traits |
| `SkyreaderGuild/TraitBossScroll.cs` | NEW | Boss summon scroll trait in global namespace |
| `SkyreaderGuild/TraitArchivistScroll.cs` | NEW | Archivist summon scroll trait in global namespace |
| `SkyreaderGuild/SkyreaderGuild.cs` | MODIFY | Add boss kill reward postfix |
| `SkyreaderGuild/SkyreaderGuild.csproj` | MODIFY | Explicitly compile new trait files |

