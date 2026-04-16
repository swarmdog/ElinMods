# Small-Time Dealing System — Implementation Plan (v3)

Implement SDD `05_small_time_dealing.md` §5.6–§5.13 (single-player features only).

## Current State — Gap Summary

The §04 implementation provides a working dealing skeleton. See the [full gap table in v2](#) for details. The key gaps are: no archetype system, flat acceptance/payout formulas, no proper OD/withdrawal conditions for NPCs, no Sample Kit mechanic, no dealing config entries, and a minimal ledger UI.

---

## Research Findings

### Silver Tongue → Negotiation Skill (291)

Elin has a built-in **negotiation** skill (`SKILL.negotiation = 291`):

- Used for shop price negotiation at [CalcMoney.cs:L7](file:///c:/Users/mcounts/Documents/ElinMods/Elin-Decompiled-main/Elin/CalcMoney.cs#L7)
- Used for NPC persuasion at [Affinity.cs:L179](file:///c:/Users/mcounts/Documents/ElinMods/Elin-Decompiled-main/Elin/Affinity.cs#L179)
- Gains XP from gift-giving, recruiting, shop transactions — all social interactions

**Decision**: Use `EClass.pc.Evalue(291)` as "Silver Tongue." Grant negotiation XP (30) on each successful deal. No custom element needed.

### Sample Kit → `IsCriminal` Harmony Patch + Container

Elin's `IsCriminal` property at [Player.cs:L1408-1418](file:///c:/Users/mcounts/Documents/ElinMods/Elin-Decompiled-main/Elin/Player.cs#L1408-L1418) is a simple getter:
```csharp
public bool IsCriminal { get { if (karma < 0) return !EClass.pc.HasCondition<ConIncognito>(); return false; } }
```

It feeds into 5 critical downstream systems:
1. **`Zone.RefreshCriminal()`** [Zone.cs:L3630-3644](file:///c:/Users/mcounts/Documents/ElinMods/Elin-Decompiled-main/Elin/Zone.cs#L3630-L3644) — toggles guard hostility
2. **`DramaCustomSequence.Build`** [L1007](file:///c:/Users/mcounts/Documents/ElinMods/Elin-Decompiled-main/Elin/DramaCustomSequence.cs#L1007) — blocks shop access
3. **`Chara.cs:L6701`** [L6701](file:///c:/Users/mcounts/Documents/ElinMods/Elin-Decompiled-main/Elin/Chara.cs#L6701) — guards attack PC party
4. **`GoalCombat.cs:L143`** [L143](file:///c:/Users/mcounts/Documents/ElinMods/Elin-Decompiled-main/Elin/GoalCombat.cs#L143) — combat targeting
5. **`WindowChara.cs:L955`** [L955](file:///c:/Users/mcounts/Documents/ElinMods/Elin-Decompiled-main/Elin/WindowChara.cs#L955) — UI display

**Design**: A Harmony **postfix** on `Player.get_IsCriminal` adds **probabilistic** contraband detection:
- If vanilla result is already `true` (karma-based), return as-is
- If `false`, check a **cached detection flag** on `UnderworldRuntime`
- The detection flag is rolled on **zone entry** and periodically (every in-game hour via `ProcessWorldTick`):
  ```
  detectionChance = Config.ContrabandDetectionBase       // 60%
                  - stealth(152) * Config.StealthReduction   // 1.5% per level
                  - negotiation(291) * Config.NegotiationReduction  // 0.5% per level
  ```
  Clamped to [5, 95]. If rolled success → `_contrabandDetected = true` until player stashes the items or leaves the zone.
- Items inside a `TraitUwSampleKit` container are **always concealed** — they never count as exposed, and the roll is never made if all contraband is in kits
- The Sample Kit becomes a `TraitContainer` with a 3×1 grid

This creates a skill-scaling risk gradient:
- **Low stealth/negotiation**: ~60% chance guards spot your stash → hostile, shops blocked
- **High stealth (30+)**: ~15% chance → you can often walk through towns unchallenged
- **High both (30/30)**: ~5% floor → nearly invisible, but never zero risk
- **Sample Kit**: 0% — guaranteed concealment, but limited to 3 slots

### NPC Archetype Classification

Elin's trait hierarchy provides clean classification:

| Elin Type | SDD Archetype | Detection |
|-----------|---------------|-----------|
| `TraitGuard` | Guard | `c.trait is TraitGuard` |
| `TraitMerchant`+subs, `TraitMayor`, `TraitElder`, `IsWealthy` | Noble | Type checks |
| `TraitTrainer` with `IDTrainer == "mind"`, healer-type | Scholar | Trait + param check |
| Juere race, thief guild NPCs | Rogue | Race/source check |
| `TraitCitizen` (base, not merchant/guard/etc) | Laborer | Default citizen |
| All others (adventurers, monsters, etc.) | Adventurer | Fallback |

---

## Proposed Changes

### Component 1 — NPC Archetype Classifier

#### [NEW] [Systems/UnderworldArchetypeService.cs](file:///c:/Users/mcounts/Documents/ElinMods/ElinUnderworldSimulator/Systems/UnderworldArchetypeService.cs)

```csharp
enum NpcArchetype { Laborer, Adventurer, Noble, Scholar, Rogue, Guard }
```

- `Classify(Chara c)` — priority-ordered:
  1. `TraitGuard` → Guard
  2. `TraitMerchant` / `TraitMayor` / `TraitElder` / `IsWealthy` → Noble
  3. `TraitTrainer` with `IDTrainer == "mind"` → Scholar
  4. Juere race (`c.race.id == "juere"`) → Rogue
  5. `TraitCitizen` (generic) → Laborer
  6. Fallback → Adventurer
- `GetAcceptModifier(NpcArchetype)` → SDD values (+10, 0, −25, −35, +20, −999)
- `GetPayMultiplier(NpcArchetype)` → SDD values (0.6, 0.8, 1.5, 1.8, 0.7)

---

### Component 2 — Sample Kit Concealment via `IsCriminal` Patch

This is the cornerstone mechanic. Carrying contraband in the open is risky — your skills determine how well you hide it. The Sample Kit is guaranteed concealment.

#### [MODIFY] [Traits/TraitSampleKit.cs](file:///c:/Users/mcounts/Documents/ElinMods/ElinUnderworldSimulator/Traits/TraitSampleKit.cs)

- Change base class from `TraitItem` → `TraitContainer`
- Override grid size to 3×1 (matches SDD `SampleKitSlots = 3`)
- Override `CanOpenContainer => true`
- Flavor: *"A pouch lined with scent-dampening cloth. Items stored within won't draw attention."*

#### [MODIFY] [UnderworldRuntime.cs](file:///c:/Users/mcounts/Documents/ElinMods/ElinUnderworldSimulator/UnderworldRuntime.cs)

Add contraband detection state and roll logic:

```csharp
// ── Transient state (not serialized) ──
private static bool _contrabandDetected;
private static int _lastDetectionZoneUid = -1;

/// <summary>
/// Called on zone entry and hourly from ProcessWorldTick.
/// Rolls detection chance for exposed contraband based on skills.
/// </summary>
internal static void RollContrabandDetection()
{
    _contrabandDetected = false;

    if (EClass.pc == null || EClass._zone == null) return;
    if (!EClass._zone.HasLaw || EClass._zone.AllowCriminal || EClass._zone.IsPCFaction) return;

    // No exposed contraband → no roll needed
    if (!HasExposedContraband(EClass.pc)) return;

    // Skill-based detection chance
    int stealth = EClass.pc.Evalue(152);       // SKILL.stealth
    int negotiation = EClass.pc.Evalue(291);   // SKILL.negotiation

    float chance = UnderworldConfig.ContrabandDetectionBase.Value
                 - stealth * UnderworldConfig.ContrabandStealthReduction.Value
                 - negotiation * UnderworldConfig.ContrabandNegotiationReduction.Value;

    chance = Math.Clamp(chance, 5f, 95f);

    _contrabandDetected = EClass.rnd(100) < (int)chance;

    if (_contrabandDetected)
    {
        // Grant stealth XP for close calls (detected = you failed, but tried)
        EClass.pc.ModExp(152, 15);
    }
    else
    {
        // Grant stealth XP for success (you hid it well)
        EClass.pc.ModExp(152, 5);
    }

    _lastDetectionZoneUid = EClass._zone.uid;
}

/// <summary>Called by IsCriminal postfix — O(1) cached check.</summary>
internal static bool IsContrabandDetected => _contrabandDetected;

/// <summary>Clears detection flag (when player stashes items or leaves zone).</summary>
internal static void ClearContrabandDetection()
{
    _contrabandDetected = false;
}

private static bool HasExposedContraband(Chara pc)
{
    foreach (Thing t in pc.things.List(onlyAccessible: true))
    {
        if (IsInsideSampleKit(t)) continue;
        if (UnderworldDrugCatalog.IsContraband(t.id)) return true;
    }
    return false;
}

private static bool IsInsideSampleKit(Thing t)
{
    Card parent = t.parentCard;
    while (parent != null)
    {
        if (parent is Thing thing && thing.trait is TraitUwSampleKit) return true;
        parent = parent.parentCard;
    }
    return false;
}
```

**Integration points:**
- `ProcessWorldTick()`: Call `RollContrabandDetection()` hourly (alongside existing nerve/heat ticks)
- Zone entry hook (existing Harmony patch or `OnEnterZone`): Call `RollContrabandDetection()`
- When player moves an item into/out of Sample Kit: Call `RollContrabandDetection()` to re-evaluate

#### [MODIFY] [Patches/UnderworldGameplayPatches.cs](file:///c:/Users/mcounts/Documents/ElinMods/ElinUnderworldSimulator/Patches/UnderworldGameplayPatches.cs)

New Harmony postfix: `Player.get_IsCriminal`

```csharp
[HarmonyPostfix]
[HarmonyPatch(typeof(Player), "get_IsCriminal")]
static void Postfix_IsCriminal(ref bool __result)
{
    // Don't override if already criminal (karma) or incognito
    if (__result) return;
    if (EClass.pc?.HasCondition<ConIncognito>() == true) return;

    // Use cached probabilistic result
    __result = UnderworldRuntime.IsContrabandDetected;
}
```

The postfix is now an **O(1) flag read** — all the inventory scanning and probability math happen in the hourly tick, not on every property access.

#### [MODIFY] [Content/UnderworldDrugCatalog.cs](file:///c:/Users/mcounts/Documents/ElinMods/ElinUnderworldSimulator/Content/UnderworldDrugCatalog.cs)

- Add `static bool IsContraband(string itemId)` — returns true for all drug product IDs (the 21 products already cataloged)
- Use a `HashSet<string>` built from `AllProducts` for O(1) lookup

---

### Component 3 — Acceptance & Refusal Rewrite

#### [MODIFY] [UnderworldDealService.cs](file:///c:/Users/mcounts/Documents/ElinMods/ElinUnderworldSimulator/UnderworldDealService.cs)

**`OfferSample` rewrite:**
```
chance = Config.SampleAcceptChanceBase            // 50
       + (EClass.pc.Evalue(291) * 2)              // Negotiation bonus
       + ArchetypeModifier[archetype]              // Per-archetype
       - (10 if guard within GuardDetectionRadius) // Guard proximity
```
Clamped to [5, 95].

**Guard proximity scan:**
```csharp
bool guardNearby = EClass._map.charas.Any(c =>
    c.trait is TraitGuard && c.Dist(EClass.pc) <= Config.GuardDetectionRadius);
```

**Refusal handling:**
- Set `state.CooldownExpiresRaw = now + 72 * 60` on refusal
- Check cooldown before showing offer option in `AppendChoices`
- Noble/Scholar refusal: 25% → `EClass.player.ModKarma(-2)` + call guard pattern from [Point.cs:L898-916](file:///c:/Users/mcounts/Documents/ElinMods/Elin-Decompiled-main/Elin/Point.cs#L898-L916)

**Negotiation XP on acceptance:**
- `EClass.pc.ModExp(291, 30)` per successful deal

---

### Component 4 — Loyalty Tier System

#### [MODIFY] [UnderworldDealService.cs](file:///c:/Users/mcounts/Documents/ElinMods/ElinUnderworldSimulator/UnderworldDealService.cs)

- `GetLoyaltyTier(int loyalty)` → `"prospect"` (1–2), `"regular"` (3–7), `"devoted"` (8–14), `"hooked"` (15+)
- `EnsurePendingOrder`: Prospect=1 qty (50% chance), Regular=1–3, Devoted=2–5, Hooked=3–8
- Loyalty pay multipliers: 0.8, 1.0, 1.2, 1.3

---

### Component 5 — Payout Formula Alignment

#### [MODIFY] [UnderworldDealService.cs](file:///c:/Users/mcounts/Documents/ElinMods/ElinUnderworldSimulator/UnderworldDealService.cs)

```csharp
int payout = (int)(basePrice
    * archetypePayMult            // 0.6–1.8
    * loyaltyPayMult              // 0.8–1.3
    * addictionDesperationBonus   // 1.0 + max(0, addiction-30)*0.005
    * negotiationSkillBonus       // 1.0 + Evalue(291)/200.0
    * Config.DealingPayoutMultiplier / 100.0)
    * qty;
```

---

### Component 6 — Overdose, Withdrawal & Addiction Conditions

#### [NEW] [Conditions/ConUWOverdose.cs](file:///c:/Users/mcounts/Documents/ElinMods/ElinUnderworldSimulator/Conditions/ConUWOverdose.cs)

Per SDD §5.8.4:
- Extends `BadCondition`, `UseElements => true`, `PreventRegen => true`
- Phase 0 (mild): SPD −10, STR −5
- Phase 1 (moderate): SPD −20, STR −10, WIL −10
- Phase 2 (severe): SPD −30, STR −15, WIL −15 + `ConParalyze(50)`
- `Tick()`: Recovery via `EClass.rnd(10) == 0 → Mod(-1)`, severe: periodic `owner.Vomit()`

#### [MODIFY] [UnderworldDealService.cs](file:///c:/Users/mcounts/Documents/ElinMods/ElinUnderworldSimulator/UnderworldDealService.cs)

**`MaybeTriggerOverdose` rewrite** per SDD §5.8.1:
```csharp
if (addiction < Config.ODAddictionThreshold) return ODResult.None;

int potencyExcess = Math.Max(0, potency - (tolerance * 2));
float toxFactor = toxicity / 100f;
float addFactor = Math.Max(0f, (addiction - 60) / 40f);
float odChance = Config.ODBaseChance
    + potencyExcess * Config.ODPotencyFactor
    + toxFactor * Config.ODToxicityFactor;
odChance *= (1f + addFactor);
odChance = Math.Min(odChance, Config.ODMaxChance);
```

- On OD: apply `ConUWOverdose` instead of `ConFaint`/`ConDrunk`
- Fatal: `DamageHP(hp+1)`, config-driven heat/karma/rep penalties
- New `CascadeCustomerFlight(npc)`: for each customer in same zone, `Config.CustomerFlightChance` → loyalty −2

**NPC withdrawal:**
- When `GetWithdrawalStage(state) >= 1` and NPC Chara is loaded in zone, apply `ConUWWithdrawal`
- On serve: remove condition, `state.Loyalty += 3`

**Antidote loyalty:**
- `TryUseAntidoteOn`: on severe OD save → `state.Loyalty += 3`

---

### Component 7 — Dealing Config Entries

#### [MODIFY] [Systems/UnderworldConfig.cs](file:///c:/Users/mcounts/Documents/ElinMods/ElinUnderworldSimulator/Systems/UnderworldConfig.cs)

```csharp
// ── Dealing ──
ConfigEntry<int> SampleAcceptChanceBase;        // 50
ConfigEntry<int> DealingPayoutMultiplier;        // 100
ConfigEntry<int> SampleKitSlots;                 // 3
ConfigEntry<int> GuardDetectionRadius;           // 5

// ── Addiction ──
ConfigEntry<float> AddictionGainPerPotency;      // 0.3
ConfigEntry<float> ToleranceGainPerPotency;      // 0.1
ConfigEntry<bool> AddictionNaturalDecayEnabled;   // false
ConfigEntry<float> AddictionPriceBonusPerPoint;   // 0.005

// ── Overdose ──
ConfigEntry<int> ODAddictionThreshold;           // 61
ConfigEntry<float> ODBaseChance;                  // 0.02
ConfigEntry<float> ODPotencyFactor;               // 0.005
ConfigEntry<float> ODToxicityFactor;              // 0.10
ConfigEntry<float> ODMaxChance;                   // 0.40
ConfigEntry<float> ODFatalChance;                 // 0.15
ConfigEntry<int> ODFatalHeatGain;                // 15
ConfigEntry<int> ODFatalKarmaPenalty;             // -5
ConfigEntry<int> ODFatalRepPenalty;               // -20
ConfigEntry<float> CustomerFlightChance;          // 0.30
```

---

### Component 8 — Dealer's Ledger UI Upgrade

#### [MODIFY] [DealerLedgerDialog.cs](file:///c:/Users/mcounts/Documents/ElinMods/ElinUnderworldSimulator/DealerLedgerDialog.cs)

- Group customers by `state.ZoneName` with zone headers
- Per-customer row: `Name (archetype) | LoyaltyTier | AddictionBar | Status | Wants`
- Addiction tier bar: ○ Clean, ░ Casual, ▓ Dependent, ▓▓ Addicted, ⚠ Severe
- Alerts section: ⚠ OD-risk customers, 💀 recent deaths
- `FontColor.Warning` for OD-risk, `FontColor.Bad` for deaths

---

### Component 9 — Save Data Additions

#### [MODIFY] [UnderworldSaveData.cs](file:///c:/Users/mcounts/Documents/ElinMods/ElinUnderworldSimulator/UnderworldSaveData.cs)

Add to `CustomerState`:
```csharp
[JsonProperty]
public int CooldownExpiresRaw;    // Refusal cooldown expiry

[JsonProperty]
public bool IsDead;               // Track death for ledger alerts
```

---

### Component 10 — Cleanup

#### [DELETE] Root-level duplicate traits (superseded by `Traits/`):
- `TraitUwAntidoteVial.cs`, `TraitUwContrabandChest.cs`, `TraitUwDealerLedger.cs`, `TraitUwMixingTable.cs`, `TraitUwSampleKit.cs`

---

## Verification Plan

### Build Verification
- `dotnet build` must succeed with no errors

### Manual In-Game Testing

| # | Test | Steps | Expected |
|---|------|-------|----------|
| 1 | **Contraband detection** | Carry loose drug, enter Palmia | `IsCriminal` → true, guards hostile, shops blocked |
| 2 | **Sample Kit concealment** | Put same drug in Sample Kit, re-enter Palmia | `IsCriminal` → false, guards friendly, shops open |
| 3 | **Incognito override** | Carry loose drug + cast Incognito | `IsCriminal` → false (Incognito bypasses both karma and contraband) |
| 4 | **Archetype classifier** | Talk to various NPCs | Logger shows correct archetype per NPC trait |
| 5 | **Guard-blocked offer** | Talk to city guard | No "Offer a sample" option |
| 6 | **Sample acceptance** | Offer to rogue NPC | ~70% acceptance |
| 7 | **Refusal + cooldown** | Offer to scholar, get refused, try immediately | Cooldown blocks re-offer for 72h |
| 8 | **Noble refusal guard alert** | Offer to noble, get refused | 25% chance karma −2 + guard called |
| 9 | **Negotiation XP** | Complete sample + deal | `ModExp(291, 30)` fires |
| 10 | **Loyalty tiers** | Build loyalty to 3/8/15 | Volume ranges change per tier |
| 11 | **Payout scaling** | Sell to noble vs laborer | Noble pays ~2.5× more |
| 12 | **Addiction progression** | 10 deals to same NPC | Addiction grows proportional to potency |
| 13 | **NPC withdrawal** | Skip serving addicted NPC | NPC gains `ConUWWithdrawal` with stat debuffs |
| 14 | **Serve withdrawing NPC** | Fulfill order for withdrawing NPC | `ConUWWithdrawal` removed, loyalty +3 |
| 15 | **OD trigger** | Set addiction high, sell potent product | `ConUWOverdose` applied, correct phase |
| 16 | **Fatal OD cascade** | Fatal OD with 5 nearby customers | ~1–2 lose loyalty, heat +15, karma −5 |
| 17 | **Antidote loyalty** | OD NPC (severe), use antidote | Condition removed, loyalty +3 |
| 18 | **Ledger UI** | Read Dealer's Ledger | Zone-grouped list, addiction bars, alerts |
| 19 | **Config override** | `DealingPayoutMultiplier=200` | Payouts doubled |
