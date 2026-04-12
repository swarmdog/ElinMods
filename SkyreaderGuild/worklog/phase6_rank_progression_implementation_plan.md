# Phase 6 Rank Progression - Implementation Plan

## Summary

Finish Skyreader Guild progression by making `QuestSkyreader` the authority for GP, rank thresholds, rank names, recipe unlocks, meteor spawn-distance bonuses, meteor reward multipliers, and quest journal/tracker text.

The current code already has the `GuildRank` enum, stored GP/stat fields, partial `AddGuildPoints`, codex gating, and meteor spawn-radius changes. Phase 6 should consolidate those scattered pieces into stable quest APIs, improve player-facing text, and apply the missing Principal Starseeker reward multiplier.

Use plain ASCII in new user-facing strings. Several existing strings display mojibake in the source file, so avoid adding new Unicode symbols until the file encoding is intentionally cleaned up.

## Key Implementation Changes

### QuestSkyreader progression API

Update `QuestSkyreader` in `SkyreaderGuild.cs`:

- Keep the existing `GuildRank` thresholds:
  - `Wanderer = 0`
  - `Seeker = 200`
  - `Researcher = 500`
  - `CosmosAddled = 1500`
  - `CosmosApplied = 3000`
  - `Understander = 5000`
  - `PrincipalStarseeker = 10000`
- Clamp negative GP awards to no-op in `AddGuildPoints(int amount)`.
- Capture `oldRank`, add GP, compute `newRank`, log the award, and call `OnRankUp(oldRank, newRank)` only when the rank increases.
- Add `FormatRankName(GuildRank rank)` and use it everywhere player-facing text currently prints raw enum values.
- Add `GetNextRank()` using the explicit rank order, not reflection order.
- Add `CanUseRecipe(string recipeId)` as the central rank gate:
  - `srg_weave_stars`, `srg_starforge`: `Seeker`
  - four boss scrolls: `CosmosApplied`
  - `srg_scroll_convergence`: `Understander`
  - all other recipes: allowed
- Add `GetMeteorSpawnDistanceReduction()` with descending checks:
  - `Understander` and above: `8`
  - `CosmosApplied`: `6`
  - `CosmosAddled`: `4`
  - `Researcher`: `2`
  - below Researcher: `0`
- Add `GetMeteorRewardMultiplier()` returning `2f` at `PrincipalStarseeker`, otherwise `1f`.

### Rank-up messaging and journal text

Replace the current minimal rank-up message with `OnRankUp(oldRank, newRank)`:

- Announce the final new rank only.
- Include one short unlock sentence based on the new rank:
  - Seeker: Weave the Stars and Starforge.
  - Researcher: closer meteor reports.
  - Cosmos-Addled: deeper cosmic disturbances.
  - Cosmos-Applied: Ultima Projection boss scrolls.
  - Understander: Astral Convergence Archivist scroll.
  - Principal Starseeker: doubled meteor core rewards.

Replace `GetDetailText(bool onJournal = false)` content after `base.GetDetailText(onJournal)` with:

- Guild heading.
- Current rank using `FormatRankName`.
- Current GP and next-rank progress, or max-rank status.
- Activity stats: meteors analyzed and touched cleansed.
- Current unlocked benefits based on rank.

Replace `GetTrackerText()` with compact progress:

- Before max rank: `Skyreader: Seeker - 250/500 GP`
- At max rank: `Skyreader: Principal Starseeker - 10300 GP (MAX)`

### Consumers of the progression API

Update `TraitAstrologicalCodex.Contains(RecipeSource r)`:

- Keep the existing `base.Contains(r)` and quest-null checks.
- Replace the local switch with `return quest.CanUseRecipe(r.id);`.

Update `MeteorManager.GetSpawnRadius(QuestSkyreader quest)`:

- Compute `Mathf.Max(4, BASE_SPAWN_RADIUS - quest.GetMeteorSpawnDistanceReduction())`.
- This makes the radius progression `12 -> 10 -> 8 -> 6 -> 4` while keeping 4 as the minimum.
- Keep the current spawn chance behavior unless a later balance pass changes it.

Update `TraitMeteorCore.OnUse(Chara c)`:

- Fetch `QuestSkyreader` before calculating rewards.
- Apply `quest.GetMeteorRewardMultiplier()` to meteor core item rewards only:
  - base Meteorite Source count;
  - base junk/treasure count;
  - extra insight bonus fragments.
- Do not multiply GP. GP progression should remain predictable.
- If the quest is missing, use multiplier `1f` and keep the current item reward behavior.

### Save safety

In `SkyreaderGuild.Awake()` or `OnStartCore()`, register the quest fallback:

```csharp
ModUtil.RegisterSerializedTypeFallback(
    "SkyreaderGuild",
    "SkyreaderGuild.QuestSkyreader",
    "QuestDummy");
```

This is specifically for save-load safety if the mod is removed. It should not be used to hide active implementation errors.

## Prior Phase Review Addendum

### Issue: chickchick/chickchicken spawns

Root cause: the mod `SourceCard.xlsx` imported a test Chara row named `chickchicken` with a huge spawn chance and normal biome data. Elin treated it as a valid random Chara source, so generic `SpawnMob()` calls could select it.

Solution: keep the lightweight fix already added to `add_meteor_items.py`: delete forbidden test Chara rows (`chickchicken`, common typo variants, and `test_chicken`) before saving the workbook, then preserve shared strings. During deploy, stale generated `SourceLocalization.json` entries for removed rows can be deleted or allowed to regenerate; they do not create source rows by themselves.

### Issue: older custom Chara rows are incomplete

Root cause: Phase 5 managed the boss and Archivist rows, but earlier custom charas such as `srg_growth` and `srg_arkyn` still have incomplete source-sheet fields. They currently depend on permissive game behavior and are harder to diagnose than the managed Phase 5 rows.

Solution: add a small Phase 6 source-data hygiene task: extend `add_meteor_items.py` to manage `srg_growth` and `srg_arkyn` with complete source rows, `chance = 0`, valid race/job/render fields, and `noRandomProduct` where appropriate. This should be done in the same style as the Phase 5 `EXPECTED_CHARAS` rows.

### Issue: meteor event theming still uses generic random Chara selection

Root cause: the meteor site population and meteor core post-events still call generic `SpawnMob()` in some paths. After removing `chickchicken`, this is no longer the urgent bug, but the code can still produce thematically odd results for events like "mercenaries emerge."

Solution: do not include a broad spawn refactor in Phase 6 unless the bug returns. If revisited, make only the mercenary event explicit first by spawning verified vanilla mercenary ids and forcing hostility.

### Issue: noisy SpawnLoot logging

Root cause: the starchart drop patch logs every Chara death before checking whether the Chara is `srg_growth`. This made the chicken investigation noisier than necessary.

Solution: move the log after the `srg_growth` id check, or remove it entirely unless debugging the invite drop.

## Test Plan

- Run `python SkyreaderGuild/add_meteor_items.py`.
- Verify `SourceCard.xlsx` still contains `xl/sharedStrings.xml` and no `inlineStr` cells.
- Verify forbidden chicken/test Chara rows remain absent.
- Run `python -m py_compile SkyreaderGuild/add_meteor_items.py`.
- Run `dotnet build SkyreaderGuild/SkyreaderGuild.csproj -c Release`.
- Start a save at low GP and verify the journal/tracker show Wanderer progress.
- Award GP through meteor analysis and extractor use; verify GP persists and rank-up messages fire only when crossing thresholds.
- Verify codex recipes unlock at Seeker, Cosmos-Applied, and Understander through `QuestSkyreader.CanUseRecipe`.
- Verify meteor spawn radius tightens at Researcher and later ranks.
- At Principal Starseeker, analyze a meteor core and verify item rewards are doubled while GP is not doubled.
- Save and reload, then confirm GP, rank, meteors analyzed, and touched cleansed persist.
- Check both runtime logs after load and testing:
  - `D:\Steam\steamapps\common\Elin\BepInEx\LogOutput.log`
  - `C:\Users\mcounts\AppData\LocalLow\Lafrontier\Elin\Player.log`

## Files Expected To Change During Implementation

- `SkyreaderGuild/SkyreaderGuild.cs`
- `SkyreaderGuild/MeteorManager.cs`
- `SkyreaderGuild/TraitAstrologicalCodex.cs`
- `SkyreaderGuild/TraitMeteorCore.cs`
- `SkyreaderGuild/add_meteor_items.py`
- `SkyreaderGuild/LangMod/EN/SourceCard.xlsx`

