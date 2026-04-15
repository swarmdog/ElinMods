# 6 · Risk & Enforcement

> Parent: [00_overview.md](./00_overview.md) · Orders: [05_orders_reputation.md](./05_orders_reputation.md)

This document specifies the Heat system, Shadow Nerve resource, law enforcement events, and recovery mechanics.

---

## 6.1 Heat System

Heat is a per-territory risk accumulation that determines the probability and severity of enforcement events.

### 6.1.1 Heat Value

- **Range**: 0 – 100 per territory
- **Storage**: Server-side in `territories.heat` column
- **Visibility**: Displayed in Territory Overlay UI

### 6.1.2 Heat Accumulation

```python
def accumulate_heat(territory, shipment):
    """Called when a shipment is submitted to this territory."""
    base_heat = shipment.quantity * CLIENT_RISK_MULTIPLIER[shipment.client_type]
    traceability_factor = 1.0 + (shipment.avg_traceability / 100.0)
    territory_capacity = territory.heat_capacity  # Higher-capacity territories absorb more
    
    heat_delta = (base_heat * traceability_factor) / territory_capacity
    territory.heat = min(100, territory.heat + int(heat_delta))
```

**Heat capacity by territory** (see [07_territory_factions.md](./07_territory_factions.md)):

| Territory | Heat Capacity | Notes |
|-----------|--------------|-------|
| Derphy Underground | 200 | Criminal town — absorbs heat easily |
| Kapul Docks | 120 | Busy port — moderate cover |
| Yowyn Farmlands | 80 | Rural — low capacity, easy to notice |
| Palmia Markets | 60 | Capital city — very high surveillance |
| Mysilia Backways | 100 | Moderate — balanced |

**Additional heat sources** (see [§5.8 Overdose System](./05_orders_reputation.md)):

| Event | Heat Delta | Notes |
|-------|-----------|-------|
| Severe overdose | +5 | NPC collapses visibly; 20% guard alert chance |
| Fatal overdose | +15 (configurable) | NPC death; 100% guard alert; karma −5; rep −20 |

These OD-driven heat events bypass the server-side accumulation formula above — they are applied directly to the local territory on the client side via `UnderworldConfig.ODFatalHeatGain`.

### 6.1.3 Heat Decay

Server-side background job runs periodically (every in-game day equivalent):

```python
async def decay_heat_job():
    """Runs every cycle. Reduces heat in all territories."""
    territories = await db.fetch_all_territories()
    for t in territories:
        decay_rate = BASE_HEAT_DECAY  # Default: 2 points per cycle
        if t.controlling_faction_id:
            decay_rate += 1  # Faction control provides +1 decay
        t.heat = max(0, t.heat - decay_rate)
        await db.update_territory_heat(t.id, t.heat)
```

### 6.1.4 Heat Threshold Table

| Heat Range | Threat Level | Effects |
|-----------|-------------|---------|
| 0 – 30 | **Clear** | No enforcement events. Normal operations. |
| 31 – 50 | **Elevated** | 10% chance of **Inspection** per shipment. |
| 51 – 70 | **High** | 25% chance of Inspection. 5% chance of **Bust**. |
| 71 – 85 | **Critical** | 40% chance of Inspection. 15% chance of Bust. 5% chance of **Surveillance**. |
| 86 – 100 | **Lockdown** | All shipments inspected. 30% Bust. 15% Surveillance. 5% **Raid**. |

---

## 6.2 Shadow Nerve

### 6.2.1 Design

Shadow Nerve is a stamina-like resource that limits how many underworld operations the player can perform per cycle. Inspired by Torn's "nerve" system, adapted to Elin's time model.

| Property | Value |
|----------|-------|
| Max Nerve (Novice) | 50 |
| Max Nerve (Peddler) | 75 |
| Max Nerve (Supplier) | 100 |
| Max Nerve (Kingpin) | 130 |
| Max Nerve (Overlord) | 160 |
| Regen Rate | 5 per in-game hour (base) |
| Regen Boost | Consuming certain items (Elin food/drink) adds nerve instantly |

### 6.2.2 Nerve Costs

| Action | Cost |
|--------|------|
| Submit Street Buyer shipment | 5 |
| Submit Regular shipment | 10 |
| Submit Dependent shipment | 5 |
| Submit Broker shipment | 20 |
| Submit Syndicate shipment | 40 |
| Accept any order | 0 (free to accept) |
| View market | 0 (free) |

### 6.2.3 Nerve Storage

Nerve is stored **client-side** in the mod's local save data (not on the server) to avoid server roundtrips for every action. The server only validates that the player has sufficient nerve at shipment submission time by trusting the client value (acceptable for async non-competitive gameplay).

```csharp
// In UnderworldPlugin or a dedicated NerveManager class
public class NerveTracker
{
    public int CurrentNerve { get; private set; }
    public int MaxNerve => GetMaxNerveForRank(Reputation.GlobalRank);
    
    private DateTime lastRegenTick;
    
    public void Update()
    {
        // Regen every in-game hour
        var elapsed = EClass.world.date.GetElapsed(lastRegenTick);
        int hours = elapsed.hours;
        if (hours > 0)
        {
            CurrentNerve = Math.Min(MaxNerve, CurrentNerve + hours * 5);
            lastRegenTick = EClass.world.date.GetRaw();
        }
    }
    
    public bool TrySpend(int cost)
    {
        if (CurrentNerve < cost) return false;
        CurrentNerve -= cost;
        return true;
    }
}
```

---

## 6.3 Enforcement Events

### 6.3.1 Inspection

- **Trigger**: Random chance during shipment resolution (server-side)
- **Effect**: Partial contraband confiscation (10-30% of shipment volume)
- **Payout**: Reduced proportionally
- **Heat**: No additional heat increase
- **Notification**: Client receives "Inspected — partial delivery" message in shipment results

```python
def resolve_inspection(shipment, inspection_severity):
    """Reduce effective shipment volume due to inspection."""
    confiscation_rate = random.uniform(0.10, 0.30) * inspection_severity
    shipment.effective_quantity = int(shipment.quantity * (1 - confiscation_rate))
    return shipment
```

### 6.3.2 Bust

- **Trigger**: Random chance at High+ heat levels
- **Effect**: Entire shipment lost. Gold penalty (5% of player's total gold, server-tracked). Temporary "Under Investigation" flag (blocks high-risk orders for 3 in-game days).
- **Rep**: −20% of order's potential rep gain
- **Heat**: +10 to territory

### 6.3.3 Surveillance

- **Trigger**: At Critical+ heat levels
- **Effect**: Heat spike in territory (+15). All future shipments to this territory for 5 days have doubled inspection chance.
- **Duration**: 5 in-game days, tracked server-side

### 6.3.4 Raid

- **Trigger**: Only at Lockdown heat level (86-100). 5% per shipment.
- **Effect**: **In-game combat event** in the player's base zone
- **Implementation**: Hostile guard NPCs spawn in the player's zone using Elin's encounter spawning.  This will need sufficient design to make it impactful and thematic.

```csharp
/// <summary>
/// Spawns enforcement NPCs in the player's home zone when a raid triggers.
/// Uses the same CharaGen.Create pattern as dungeon monster spawning.
/// Reference: Zone_Dungeon.OnGenerateMap() spawning logic.
/// </summary>
public static void TriggerRaid(Zone playerBase, int raidIntensity)
{
    int guardCount = 3 + raidIntensity; // 3-8 guards depending on heat
    
    for (int i = 0; i < guardCount; i++)
    {
        // Create hostile guard NPCs
        var guard = CharaGen.Create("guard"); // Vanilla guard NPC
        guard.hostility = Hostility.Enemy;
        guard.SetLv(10 + raidIntensity * 5);
        
        // Place at zone edges (entering the base)
        var pos = playerBase._map.bounds.GetRandomEdgePoint();
        guard.SetPos(pos);
        playerBase._map.charas.Add(guard);
    }
    
    // Notify player
    Msg.Say("law_raid"); // Localized message
    UnderworldPlugin.Log.LogWarning($"RAID triggered! {guardCount} guards spawned.");
}
```

**Source reference**: [Zone_Dungeon.cs L100-L109](file:///c:/Users/mcounts/Documents/ElinMods/Elin-Decompiled-main/Elin/Zone_Dungeon.cs#L100-L109) — how the vanilla game spawns NPCs during zone generation.

---

## 6.4 Recovery Mechanics

| Method | Cost | Effect | Cooldown |
|--------|------|--------|----------|
| **Bribe** | 5000-50000 gold (scales with heat) | −20 heat in target territory | 24h real-time |
| **Cool Down** | Time (do nothing) | Natural decay (§6.1.3) | Passive |
| **Decoy Shipment** | Loss of materials (send worthless items) | −10 heat, but wastes nerve | None |
| **Safe House** | Furniture (safe house room in base) | −5 heat per in-game day while on base | Passive |

---

## 6.5 Configuration & Tunability

All risk and enforcement values are exposed via config, split between client and server:

### 6.5.1 Client-Side Config (BepInEx)

```csharp
// ── Nerve ──
ConfigNerveRegenPerHour = Config.Bind("Nerve", "NerveRegenPerHour", 2,
    "Nerve points regenerated per in-game hour.");
ConfigNerveMaxBase = Config.Bind("Nerve", "NerveMaxBase", 100,
    "Base maximum nerve before rank/skill bonuses.");
ConfigNerveShipmentCostBase = Config.Bind("Nerve", "NerveShipmentCostBase", 10,
    "Base nerve cost per shipment submission.");
```

### 6.5.2 Server-Side Config (config.py)

```python
# Heat thresholds (percent of max heat)
HEAT_THRESHOLD_ELEVATED = 30
HEAT_THRESHOLD_HIGH = 50
HEAT_THRESHOLD_CRITICAL = 70
HEAT_THRESHOLD_LOCKDOWN = 85

# Heat accumulation
HEAT_GAIN_PER_SHIPMENT_BASE = 5   # Base heat added per shipment
HEAT_TRACEABILITY_MULTIPLIER = 0.1  # Additional heat per traceability point
HEAT_DECAY_PER_CYCLE = 2          # Heat decayed per background job cycle
HEAT_DECAY_INTERVAL = 3600        # Seconds between decay cycles

# Enforcement probabilities (per shipment, at threshold level)
ENFORCEMENT_INSPECT_CHANCE_ELEVATED = 0.10    # 10% at elevated
ENFORCEMENT_BUST_CHANCE_HIGH = 0.05           # 5% at high
ENFORCEMENT_RAID_CHANCE_LOCKDOWN = 0.01       # 1% at lockdown (per shipment)

# Recovery costs
BRIBE_COST_BASE = 5000
BRIBE_COST_PER_HEAT = 500         # Cost scales: base + (heat * per_heat)
BRIBE_HEAT_REDUCTION = 20
BRIBE_COOLDOWN_HOURS = 24
DECOY_HEAT_REDUCTION = 10
```

### 6.5.3 Config Reference Table

| Config Key | Type | Default | Side | Used In |
|------------|------|---------|------|---------|
| `NerveRegenPerHour` | int | 2 | Client | Nerve ticker |
| `NerveMaxBase` | int | 100 | Client | NerveTracker |
| `NerveShipmentCostBase` | int | 10 | Client | Chest ship action |
| `HEAT_THRESHOLD_ELEVATED` | int | 30 | Server | Heat classification |
| `HEAT_GAIN_PER_SHIPMENT_BASE` | int | 5 | Server | Shipment resolution |
| `HEAT_DECAY_PER_CYCLE` | int | 2 | Server | Background decay job |
| `ENFORCEMENT_INSPECT_CHANCE_ELEVATED` | float | 0.10 | Server | Enforcement roll |
| `BRIBE_COST_BASE` | int | 5000 | Server | Recovery endpoint |
| `BRIBE_HEAT_REDUCTION` | int | 20 | Server | Heat reduction |

---

## 6.6 Testing & Verification

### Heat System Tests (Server-side — pytest)

```python
async def test_heat_accumulates(client):
    """Submitting a shipment increases territory heat."""
    before = await get_territory_heat(client, "derphy_underground")
    await submit_shipment(client, territory="derphy_underground")
    after = await get_territory_heat(client, "derphy_underground")
    assert after > before

async def test_heat_decays(client, time_machine):
    """Heat decreases over time via background job."""
    await set_territory_heat(client, "derphy_underground", 50)
    await run_heat_decay_job()
    after = await get_territory_heat(client, "derphy_underground")
    assert after < 50

async def test_heat_caps_at_100(client):
    """Heat cannot exceed 100."""
    await set_territory_heat(client, "derphy_underground", 99)
    await submit_large_shipment(client, territory="derphy_underground")
    after = await get_territory_heat(client, "derphy_underground")
    assert after <= 100
```

### Enforcement Event Tests

| Test | Setup | Expected |
|------|-------|----------|
| No events at low heat | Heat = 20, submit shipment | No inspection, no bust |
| Inspection at elevated heat | Heat = 45, submit 100 shipments | ~10% have partial confiscation |
| Bust at high heat | Heat = 65, submit 100 shipments | ~5% are busts (full loss) |
| Raid at lockdown | Heat = 95, submit 20 shipments | ~1 raid event triggers |

### Nerve Tests (Client-side)

| Test | Expected |
|------|----------|
| Nerve starts at max | New game → nerve == max for rank |
| Nerve spent on shipment | Submit order → nerve decreases by cost |
| Nerve blocks insufficient | Try to submit with nerve < cost → blocked, message shown |
| Nerve regenerates | Wait in-game hours → nerve increases |
| Nerve regen caps | Wait long time → nerve does not exceed max |
