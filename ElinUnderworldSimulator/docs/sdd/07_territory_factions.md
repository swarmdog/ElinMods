# 7 · Territory & Factions

> Parent: [00_overview.md](./00_overview.md) · Risk: [06_risk_enforcement.md](./06_risk_enforcement.md) · Server API: [09_server_api.md](./09_server_api.md)

---

## 7.1 Territory Map

Territories are abstract market regions mapped to Elin's existing world geography. They exist purely as server-side entities — no new physical zones are created for territories. The player interacts with them through the Network UI.

### 7.1.1 Territory Definitions

| ID | Name | Elin Region | Heat Capacity | Base Demand | Risk Profile | Flavor |
|----|------|-------------|---------------|-------------|-------------|--------|
| `derphy_underground` | Derphy Underground | Derphy (criminal hub) | 200 | High volume, low potency | Low | The beating heart of Ylva's underworld. Everything moves through Derphy. |
| `kapul_docks` | Kapul Docks | Port Kapul | 120 | Medium volume, medium potency | Medium | Busy port means busy trade. Shipments blend with legitimate cargo. |
| `yowyn_fields` | Yowyn Farmlands | Yowyn | 80 | Low volume, low potency | Medium-High | Rural folk don't ask many questions, but neither do they hide well. |
| `palmia_markets` | Palmia Black Market | Palmia | 60 | Low volume, high potency | Very High | The capital's elite pay premium prices, but the guard presence is suffocating. |
| `mysilia_backways` | Mysilia Backways | Mysilia | 100 | Medium volume, medium potency | Medium | The nature-focused city's back alleys see more than the residents admit. |
| `lumiest_canal` | Lumiest Canals | Lumiest | 90 | Medium volume, high potency | Medium-High | The city of water has many hidden channels — literal and otherwise. |

### 7.1.2 Territory Data Model

```python
class Territory:
    id: str                    # "derphy_underground"
    name: str                  # "Derphy Underground"
    heat: int                  # 0-100 current heat level
    heat_capacity: int         # How much shipment volume before heat rises
    base_demand_volume: int    # Average orders generated per cycle
    base_demand_potency: int   # Average min_potency of generated orders
    controlling_faction_id: int # Null if uncontrolled
    control_score: dict        # {faction_id: influence_points} — determines control
    created_at: datetime
```

### 7.1.3 Demand Generation

The server generates new orders for each territory on a schedule (every 6 real-time hours):

```python
async def generate_orders_for_territory(territory, cycle):
    """Generate new Available orders based on territory demand profile."""
    order_count = random.randint(
        territory.base_demand_volume // 2,
        territory.base_demand_volume
    )
    
    for _ in range(order_count):
        client_type = weighted_random_choice(
            CLIENT_WEIGHTS, territory_modifier=territory.id
        )
        product_type = random.choice(get_available_products_for_tier(client_type))
        
        order = Order(
            territory_id=territory.id,
            client_type=client_type,
            product_type=product_type,
            min_quantity=CLIENT_VOLUME_RANGES[client_type].min,
            max_quantity=CLIENT_VOLUME_RANGES[client_type].max,
            min_potency=max(20, territory.base_demand_potency + random.randint(-10, 10)),
            max_toxicity=random.randint(30, 80),
            base_payout=calculate_base_payout(client_type, territory),
            deadline_hours=CLIENT_DEADLINES[client_type],
            status="available",
        )
        await db.create_order(order)
```

---

## 7.2 Faction System

### 7.2.1 Faction Data Model

```python
class Faction:
    id: int                  # Auto-increment
    name: str               # Player-chosen name (unique, 3-24 chars)
    leader_player_id: int    # Founder and leader
    max_members: int         # Default 10, increases with leader rank
    created_at: datetime

class FactionMember:
    faction_id: int
    player_id: int
    role: str               # "leader", "officer", "member"
    joined_at: datetime
```

### 7.2.2 Faction Operations

| Operation | Endpoint | Requirements |
|-----------|----------|-------------|
| Create faction | `POST /api/factions/create` | Player rank ≥ Supplier, not in a faction |
| Join faction | `POST /api/factions/join` | Not in a faction, faction has open slots |
| Leave faction | `POST /api/factions/leave` | Not the leader (leader must disband or transfer) |
| Disband faction | `DELETE /api/factions/{id}` | Must be leader, all members removed |
| Promote member | `POST /api/factions/{id}/promote` | Must be leader |

### 7.2.3 Faction Benefits

| Benefit | Description |
|---------|-------------|
| **Shared Heat Resistance** | Faction control of a territory gives all members +1 heat decay in that territory |
| **Order Priority** | Faction members in controlling territory see orders 1 hour before non-members |
| **Payout Bonus** | +10% payout in faction-controlled territories |
| **Coordination Bonus** | When 3+ faction members ship to the same territory in one cycle, all get +5% payout |

---

## 7.3 Warfare Resolution

### 7.3.1 Influence Accumulation

Every shipment a player makes to a territory contributes influence points to their faction:

```python
def accumulate_influence(shipment, territory):
    """Called when a shipment is resolved in a territory."""
    if shipment.player.faction_id is None:
        return  # Solo players don't influence territory control
    
    # Influence = shipment value × satisfaction
    influence = shipment.effective_payout * shipment.satisfaction_score
    
    faction_id = shipment.player.faction_id
    territory.control_score[faction_id] = (
        territory.control_score.get(faction_id, 0) + int(influence)
    )
```

### 7.3.2 Control Resolution

Server-side background job runs daily:

```python
async def resolve_territory_control():
    """Daily job: determine which faction controls each territory."""
    territories = await db.fetch_all_territories()
    
    for territory in territories:
        scores = territory.control_score  # {faction_id: total_influence}
        if not scores:
            territory.controlling_faction_id = None
            continue
        
        # Decay all scores by 10% per day (prevents permanent lockout)
        for fid in scores:
            scores[fid] = int(scores[fid] * 0.90)
        
        # Faction with highest score controls
        top_faction = max(scores, key=scores.get)
        runner_up = sorted(scores.values(), reverse=True)[1] if len(scores) > 1 else 0
        
        # Must have >20% lead to maintain control
        if scores[top_faction] > runner_up * 1.2:
            territory.controlling_faction_id = top_faction
        else:
            territory.controlling_faction_id = None  # Contested — no one controls
        
        # Clean up factions with zero influence
        territory.control_score = {
            fid: s for fid, s in scores.items() if s > 0
        }
        
        await db.update_territory(territory)
```

### 7.3.3 Territory Change Notifications

When control changes, all affected players receive a notification on their next `/shipments/results` poll:

```python
# Included in the results response
{
    "territory_changes": [
        {
            "territory_id": "kapul_docks",
            "old_faction": "Night Owls",
            "new_faction": "Crimson Syndicate",
            "message": "Kapul Docks has fallen to the Crimson Syndicate."
        }
    ]
}
```

---

## 7.4 Faction Management — Furniture & NPCs

Faction operations are accessed through specific furniture placed in the player's base and through the Fixer NPC. The Fixer is the primary management interface.

### 7.4.1 The Fixer as Permanent Recruit

The Fixer begins as a static NPC in the player's starting zone (§2.1.3) but can be **recruited as a permanent party member** during the Peddler rank promotion. This makes the Fixer a persistent companion who travels with the player, providing access to the underworld network from anywhere.

**Recruitment flow:**
1. Player reaches **Peddler** rank (500 total rep)
2. Fixer offers new dialog: *"You've proven useful. How about I tag along — keep things running smooth no matter where we go?"*
3. On acceptance, the Fixer joins the player's party as a permanent ally using Elin's `Chara.party.AddMember()` system
4. As a party member, the Fixer can be interacted with at any time to open the Network Panel — no need to return to a specific town

**Implementation:**
```csharp
// In TraitUnderworldFixer.OnUse():
if (c.IsPC && !owner.IsPCParty)
{
    int rank = UnderworldPlugin.Instance.Reputation.GlobalRank;
    if (rank >= UnderworldRank.Peddler && !recruitDialogShown)
    {
        ShowRecruitDialog(() => {
            // Recruit the Fixer as a permanent party member
            owner.SetGlobal(EClass.pc.currentZone, owner.pos.x, owner.pos.z);
            EClass.pc.party.AddMember(owner);
            owner.SetFaction(EClass.Home);
            Msg.Say("uw_fixer_joined"); // "The Fixer nods. 'Partners, then.'"
            recruitDialogShown = true;
        });
        return true;
    }
}
```

**Fixer combat behavior**: The Fixer uses the `thief` job class and has moderate combat stats. They can hold their own in dungeons but aren't a primary fighter. The Fixer's real value is mobile access to the network.

### 7.4.2 Faction Management Furniture

| Furniture ID | Name | Purpose | Unlock Rank | Interaction |
|-------------|------|---------|-------------|-------------|
| `uw_territory_map` | Territory Map | Wall-mounted map showing all territory statuses, heat levels, and controlling factions. Click to view detailed territory breakdown. | Novice | Opens Territory Panel (read-only) |
| `uw_faction_desk` | Syndicate Desk | Administrative desk for faction operations. Create, manage, and coordinate faction activities. | Supplier | Opens Faction Management Panel |
| `uw_dead_drop_board` | Dead Drop Board | A corkboard showing available network orders for the base's territory. Quick access to market without the Fixer. | Peddler | Opens Market Screen (filtered to local territory) |
| `uw_heat_monitor` | Heat Monitor | A crystal apparatus that displays real-time heat levels for all territories. Pulses red when any territory is Critical+. | Novice | Opens Territory Overlay |

**Furniture source row specs:**

```python
FACTION_FURNITURE = {
    "uw_territory_map": {
        "name": "territory map",
        "category": "crafter",
        "_tileType": "ObjBig",
        "_idRenderData": "@obj tall",
        "factory": "uw_mixing_table",
        "components": "parchment/3,ingot/1",
        "value": 1500,
        "weight": 5000,
        "trait": "TerritoryMap",
        "detail": "A well-worn map marked with symbols only the initiated would understand. "
                  "Each pin represents a market and its current... temperature.",
    },
    "uw_faction_desk": {
        "name": "syndicate desk",
        "category": "crafter",
        "_tileType": "ObjBig",
        "_idRenderData": "@obj",
        "factory": "uw_mixing_table",
        "components": "plank/6,ingot/2,parchment/2",
        "value": 4000,
        "weight": 12000,
        "trait": "FactionDesk",
        "detail": "A heavy oak desk with locked drawers and a concealed compartment. "
                  "From here, empires are coordinated.",
    },
    "uw_dead_drop_board": {
        "name": "dead drop board",
        "category": "crafter",
        "_tileType": "ObjBig",
        "_idRenderData": "@obj tall",
        "factory": "uw_mixing_table",
        "components": "plank/3,bolt/2",
        "value": 800,
        "weight": 3000,
        "trait": "DeadDropBoard",
        "detail": "A nondescript corkboard pinned with coded notes. "
                  "Each one represents a request from the network.",
    },
    "uw_heat_monitor": {
        "name": "heat monitor",
        "category": "crafter",
        "_tileType": "ObjBig",
        "_idRenderData": "@obj",
        "factory": "uw_mixing_table",
        "components": "uw_mineral_crystal/1,glass/3,ingot/2",
        "value": 3000,
        "weight": 8000,
        "trait": "HeatMonitor",
        "lightData": "0,255,200,200,3",  # red-tinted glow
        "detail": "A glass apparatus filled with dark fluid. "
                  "It reacts to the network's collective anxiety.",
    },
}
```

### 7.4.3 Faction Management Panel

Accessed via the Syndicate Desk or through the Fixer's dialog (if in party), the Faction Management Panel provides:

| Tab | Content | Actions Available |
|-----|---------|-------------------|
| **Overview** | Faction name, member count, controlled territories, total influence | — (read-only) |
| **Members** | List of faction members with roles, ranks, and contribution stats | Promote to officer (leader only) |
| **Territories** | Per-territory influence scores, control status, and heat | Set territory priority (focus shipments) |
| **Recruitment** | Open slots, invitation URL/code | Generate invite code |
| **Coordination** | Recent faction shipments, target territories, bonus status | View coordination bonus progress |

---

## 7.5 Configuration & Tunability

### 7.5.1 Server-Side Config (config.py)

```python
# Territory
TERRITORY_HEAT_DECAY_RATE = 2        # Heat points decayed per cycle
TERRITORY_FACTION_DECAY_RATE = 0.90  # Daily influence score decay multiplier
TERRITORY_CONTROL_LEAD_PCT = 20      # Percent lead required for control
ORDER_GENERATION_INTERVAL = 21600    # Seconds between order generation (6h)

# Factions
FACTION_MAX_MEMBERS_BASE = 10
FACTION_COORDINATION_THRESHOLD = 3   # Members shipping to same territory
FACTION_COORDINATION_BONUS_PCT = 5   # Payout bonus for coordination
FACTION_CONTROL_PAYOUT_BONUS_PCT = 10  # Payout bonus in controlled territory
FACTION_CONTROL_HEAT_DECAY_BONUS = 1   # Extra heat decay in controlled territory

# Warfare
WARFARE_RESOLUTION_INTERVAL = 86400 # Seconds between warfare resolution (24h)
```

### 7.5.2 Client-Side Config (BepInEx)

```csharp
ConfigFixerRecruitEnabled = Config.Bind("Fixer", "RecruitEnabled", true,
    "Allow the Fixer to be recruited as a permanent party member.");
ConfigFixerRecruitMinRank = Config.Bind("Fixer", "RecruitMinRank", 1,
    "Minimum underworld rank to recruit the Fixer (0=Novice, 1=Peddler, etc.).");
```

---

## 7.6 Testing & Verification

### Territory Tests (Server-side — pytest)

```python
async def test_territory_exists(client):
    """All 6 territories are present in the database."""
    resp = await client.get("/api/territories")
    assert len(resp.json()) == 6

async def test_territory_heat_capacity(client):
    """Territory heat capacity matches specification."""
    resp = await client.get("/api/territories")
    derphy = next(t for t in resp.json() if t["id"] == "derphy_underground")
    assert derphy["heat_capacity"] == 200

async def test_order_generation(client, time_machine):
    """Territory generates orders per cycle."""
    await trigger_order_generation_cycle()
    resp = await client.get("/api/orders/available")
    assert len(resp.json()) > 0
```

### Faction Tests

```python
async def test_create_faction(client):
    """Player can create a faction."""
    resp = await client.post("/api/factions/create", json={"name": "Test Syndicate"})
    assert resp.status_code == 200
    assert resp.json()["name"] == "Test Syndicate"

async def test_join_faction(client, other_client):
    """Another player can join an existing faction."""
    faction = await create_test_faction(client)
    resp = await other_client.post("/api/factions/join", json={"faction_id": faction["id"]})
    assert resp.status_code == 200

async def test_territory_control(client, other_client):
    """Faction with highest influence controls territory."""
    # Both submit shipments to same territory
    # One faction submits more → they control
    await resolve_territory_control()
    # Verify controlling_faction matches the higher-shipping faction
```

### Warfare Resolution Tests

| Test | Setup | Expected |
|------|-------|----------|
| No factions, no control | No shipments | All territories uncontrolled |
| Single faction ships | Faction A ships to Derphy | Faction A controls Derphy |
| Two factions compete | A ships 1000, B ships 500 | A controls (>20% lead) |
| Close contest | A ships 600, B ships 550 | Contested — no controller |
| Influence decay | A controlled yesterday, no ships today | A's score drops 10% |

### Faction Furniture Tests

| Test | Steps | Expected |
|------|-------|----------|
| Territory Map opens | Place map → interact | Territory Panel shows all territories with heat bars |
| Syndicate Desk opens | Place desk → interact (requires Supplier rank) | Faction Management Panel opens |
| Dead Drop Board opens | Place board → interact | Market Screen filtered to local territory |
| Heat Monitor glow | Place monitor → territory reaches Critical | Monitor light pulses red |
| Fixer recruitment | Reach Peddler rank → talk to Fixer | Recruit dialog appears |
| Fixer in party | Recruit Fixer → travel to new zone | Fixer follows, NPC interaction still opens Network Panel |
| Config override | Set `FixerRecruitMinRank=0` | Fixer recruitabe at Novice rank |
