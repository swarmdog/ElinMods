# 8 · Base Integration

> Parent: [00_overview.md](./00_overview.md) · Game Integration: [02_game_integration.md](./02_game_integration.md) · Crafting: [04_crafting_system.md](./04_crafting_system.md)

---

## 8.1 Starting Base Layout

When the player selects "Underworld Startup", the bootstrap ([§2.1.3](./02_game_integration.md#213-bootstrap-logic)) sets up the starting zone with underworld-specific infrastructure.

### 8.1.1 Base Zone

The starting zone is the vanilla `startSite` (Meadow) — the same plot used by the standard game. The Underworld scenario reuses this zone but populates it differently.

**Zone properties at bootstrap:**
- `ClaimZone()` called → player owns the land
- `QuestHome` advanced to phase 2 → building is enabled
- `AllowCriminal` → inherited from zone type (Meadow doesn't override, so base default applies)

### 8.1.2 Initial Placement

Items placed during bootstrap:

| Item | Position | Notes |
|------|----------|-------|
| Mixing Table (`uw_mixing_table`) | Center of zone | Primary crafting station |
| Contraband Chest (`uw_contraband_chest`) | Adjacent to mixing table | Used for shipping |
| Storage Chest (vanilla `chest`) | Near crafting area | General ingredient storage |
| Campfire (vanilla `bonfire`) | Nearby | Light source, cooking |

```csharp
private static void PlaceStarterFurniture(Zone zone)
{
    var map = zone._map;
    int cx = map.bounds.CenterX;
    int cz = map.bounds.CenterZ;
    
    // Mixing table at center
    PlaceThing("uw_mixing_table", cx, cz, map);
    
    // Contraband chest to the right
    PlaceThing("uw_contraband_chest", cx + 2, cz, map);
    
    // Storage chest nearby
    PlaceThing("chest", cx - 2, cz, map);
    
    // Basic Elin crafting infrastructure
    PlaceThing("bonfire", cx, cz + 3, map);        // Light + cooking
    PlaceThing("workbench", cx - 2, cz + 2, map);  // Basic crafting
    PlaceThing("well", cx + 2, cz + 3, map);       // Water source
    
    // Heat Monitor (starts unlocked — visual feedback from day 1)
    PlaceThing("uw_heat_monitor", cx + 3, cz + 2, map);
    
    // Dead Drop Board (shows local territory orders)
    PlaceThing("uw_dead_drop_board", cx - 3, cz, map);
}

private static void PlaceThing(string id, int x, int z, Map map)
{
    var thing = ThingGen.Create(id);
    thing.SetPos(x, z);
    map.things.Add(thing);
}
```

---

## 8.2 Infrastructure Progression

As the player advances in underworld rank, new infrastructure becomes available through craftable furniture.

### 8.2.1 Progression Table

| Rank | Infrastructure Unlocked | How Obtained |
|------|------------------------|-------------|
| **Novice** (0) | Mixing Table, Contraband Chest, Heat Monitor, Dead Drop Board, basic storage | Bootstrap (pre-placed) |
| **Novice** (100 rep) | Herbalist's Garden | Craft at Mixing Table using `uw_herb_whisper/5,soil/3,plank/4` |
| **Peddler** (500 rep) | Processing Vat, Territory Map | Craft at Mixing Table using `uw_mineral_crude/3,plank/6,ingot/2` |
| **Supplier** (2000 rep) | Advanced Lab, Concealment Locker, Syndicate Desk | Craft at Mixing Table using `uw_crystal_void/2,glass/8,ingot/6,plank/4` |
| **Kingpin** (5000 rep) | Safe House Room | Craft decoration item that grants heat reduction aura |
| **Overlord** (10000 rep) | Logistics Hub | Craft desk furniture that provides +20% max nerve |

### 8.2.2 New Infrastructure Detail

#### Herbalist's Garden (`uw_herbalists_garden`)
A dedicated soil bed that auto-grows whispervine and dreamroot. Placed as a 2×2 zone feature. Each in-game day, it produces 1-2 random raw herbs based on "garden yield" (configurable). Requires watering from a well within 5 tiles.

- **Trait**: `TraitHerbalistGarden : TraitFactory` — runs on day-tick, generates herbs into the player's storage chest.
- **Quality**: Garden-grown herbs have slightly lower potency (80% of foraged), but are reliable and risk-free.

#### Concealment Locker (`uw_concealment_locker`)
A hidden storage container that prevents its contents from being flagged as contraband. Items placed inside are invisible to the `IsCriminal` check — useful for storing finished product at your base without attracting heat to your home zone.

- **Trait**: `TraitConcealmentLocker : TraitContainer` — overrides the contraband detection during zone inspection events.
- **Capacity**: 2×4 grid with contraband-shielding property.

### 8.2.3 Rank-Gated Recipes

Rather than hard-coding rank checks, progression is gated naturally through ingredient availability:
- **Processing Vat** requires `uw_mineral_crude` — available from early mining
- **Advanced Lab** requires `uw_crystal_void` — only from deep dungeon exploration (level 10+)
- **High-tier contraband** requires outputs from both the mixing table and advanced lab

This mirrors how Elin naturally gates content through exploration depth rather than artificial locks.

---

## 8.3 Contraband Chest — `TraitContrabandChest`

### 8.3.1 Design

The Contraband Chest is the player's interface for submitting shipments to the underworld network. It extends Elin's container system with network integration.

```csharp
/// <summary>
/// A container that serves as the dead drop for underworld shipments.
/// Player places contraband items inside, then triggers a "Ship" action
/// to submit them to the server for order fulfillment.
/// 
/// Modeled on TraitShippingChest (see: Elin-Decompiled-main/Elin/TraitShippingChest.cs)
/// but with network integration instead of zone-faction shipping.
/// </summary>
public class TraitContrabandChest : TraitContainer
{
    /// <summary>
    /// Container grid dimensions (columns × rows).
    /// Stored in the trait parameters from SourceCard: "ContrabandChest,3,3,crate"
    /// </summary>
    public override int InvWidth => owner.IsInstalled ? 3 : 0;
    public override int InvHeight => owner.IsInstalled ? 3 : 0;
    
    /// <summary>
    /// Only the zone owner can open this chest.
    /// Prevents other players/NPCs from accessing contraband.
    /// </summary>
    public override bool CanOpenContainer => owner.IsInstalled 
        && EClass._zone.IsPCFaction;
    
    /// <summary>
    /// Custom action: "Ship Contents" — submits all items to the server.
    /// Appears as an additional button in the container UI.
    /// </summary>
    public override bool OnUse(Chara c)
    {
        if (!c.IsPC) return false;
        
        // Open the chest container UI with an additional "Ship" button
        var layer = EClass.ui.AddLayer<LayerInventory>();
        // Configure the layer to show chest contents + Ship button
        // When "Ship" is clicked, call SubmitShipment()
        return true;
    }
    
    private void SubmitShipment()
    {
        // Gather all items in the chest
        var items = owner.things.ToList();
        if (items.Count == 0)
        {
            Msg.Say("uw_chest_empty"); // "The crate is empty."
            return;
        }
        
        // Check nerve cost
        var activeOrder = UnderworldPlugin.Instance.Orders.GetActiveOrder();
        if (activeOrder == null)
        {
            Msg.Say("uw_no_active_order"); // "You have no active contracts."
            return;
        }
        
        int nerveCost = GetNerveCostForOrder(activeOrder);
        if (!UnderworldPlugin.Instance.Nerve.TrySpend(nerveCost))
        {
            Msg.Say("uw_insufficient_nerve"); // "You don't have the nerve..."
            return;
        }
        
        // Build payload summary
        var payload = BuildPayloadSummary(items);
        
        // Submit to server (async)
        UnderworldPlugin.Instance.NetworkClient.SubmitShipment(
            activeOrder.Id, payload,
            onSuccess: (result) => {
                // Remove items from chest
                foreach (var item in items) item.Destroy();
                Msg.Say("uw_shipped"); // "The dead drop has been collected."
            },
            onFailure: (error) => {
                Msg.Say("uw_ship_failed"); // "No one came for the drop..."
            }
        );
    }
    
    private ShipmentPayload BuildPayloadSummary(List<Thing> items)
    {
        return new ShipmentPayload
        {
            Quantity = items.Sum(i => i.Num),
            AvgPotency = (int)items.Average(i => 
                i.elements.GetBase("uw_potency")),
            AvgToxicity = (int)items.Average(i => 
                i.elements.GetBase("uw_toxicity")),
            AvgTraceability = (int)items.Average(i => 
                i.elements.GetBase("uw_traceability")),
            ItemIds = items.Select(i => i.id).ToList(),
        };
    }
}
```

### 8.3.2 Visual State

The chest uses `altTiles` for an empty state (like the shipping chest). When items are inside, it shows the full tile; when empty, the alternative tile.

---

## 8.4 Resident Integration

Elin's base system supports resident NPCs who perform work tasks. The underworld mod can assign custom roles to residents.

### 8.4.1 Resident Roles (Future Enhancement)

| Role | Effect | Implementation |
|------|--------|----------------|
| **Grower** | Auto-harvests herbs from garden plots | Zone work task: harvest `herb` category |
| **Processor** | Reduces Processing Vat time by 20% | Zone modifier on `DecaySpeedChild` |
| **Guard** | Reduces raid difficulty | Zone modifier on raid NPC count |
| **Smuggler** | +10% payout bonus on shipments | Modifier applied during `CalculatePayout()` |
| **Street Dealer** | Handles automated small-time dealing | NPC gains loyalty-tracking elements; auto-sells to towns on day-tick. Income = `(Silver Tongue / 2)%` of manual dealing rates. |

These roles use Elin's existing `FactionBranch` work assignment system and could be implemented as custom work policies or element modifiers. Street Dealer is the most complex — effectively automating the §5.6 dealing loop.

---

## 8.5 Configuration & Tunability

### 8.5.1 Client-Side Config (BepInEx)

```csharp
// ── Base ──
ConfigGardenYieldPerDay = Config.Bind("Base", "GardenYieldPerDay", 2,
    "Max herbs produced per day by Herbalist's Garden.");
ConfigGardenPotencyMultiplier = Config.Bind("Base", "GardenPotencyMultiplier", 80,
    "Percent potency of garden-grown herbs vs foraged (100 = identical).");
ConfigConcealmentLockerSlots = Config.Bind("Base", "ConcealmentLockerSlots", 8,
    "Number of item slots in the Concealment Locker.");
ConfigNerveCostMultiplier = Config.Bind("Base", "NerveCostMultiplier", 100,
    "Percent multiplier on nerve costs for shipping (100 = normal).");
ConfigSafehouseHeatReduction = Config.Bind("Base", "SafehouseHeatReduction", 5,
    "Daily heat reduction from Safe House Room.");
```

### 8.5.2 Config Reference Table

| Config Key | Type | Default | Used In |
|------------|------|---------|--------|
| `GardenYieldPerDay` | int | 2 | Herbalist's Garden day-tick |
| `GardenPotencyMultiplier` | int | 80 | Garden herb quality |
| `ConcealmentLockerSlots` | int | 8 | Concealment Locker capacity |
| `NerveCostMultiplier` | int | 100 | Shipment nerve check |
| `SafehouseHeatReduction` | int | 5 | Safe House heat aura |

---

## 8.6 Testing & Verification

### Bootstrap Placement Tests

| Test | Steps | Expected |
|------|-------|----------|
| Mixing table placed | Start Underworld game → check zone | Mixing table visible at center |
| Chest placed | Start Underworld game → check zone | Contraband chest adjacent to table |
| Elin workbench placed | Start Underworld game → check zone | Vanilla workbench present for crafting |
| Well placed | Start Underworld game → check zone | Water source available |
| Heat Monitor placed | Start Underworld game → check zone | Heat Monitor visible, glowing |
| Dead Drop Board placed | Start Underworld game → check zone | Board present, opens market screen |
| Furniture interactable | Click on mixing table | Crafting UI opens with underworld recipes |
| Chest interactable | Click on contraband chest | Container UI opens with Ship button |

### Contraband Chest Tests

| Test | Steps | Expected |
|------|-------|----------|
| Empty chest ship | Click Ship on empty chest | "The crate is empty." message |
| No active order | Place items, Ship, but no order accepted | "No active contracts." message |
| Successful submit | Accept order, place items, Ship | Items removed, server receives payload |
| Insufficient nerve | Accept order, drain nerve, Ship | "You don't have the nerve..." message |
| Network offline | Accept order, disable server, Ship | "No one came for the drop..." message |
| Payload accuracy | Place 5 items in chest, Ship, check server | Server receives correct quantity, avg potency, avg toxicity |

### Infrastructure Progression Tests

| Test | Expected |
|------|----------|
| Processing Vat craftable at Peddler rank | Recipe appears in Mixing Table at ≥500 rep |
| Advanced Lab requires rare materials | Cannot craft without `uw_crystal_void` |
| Herbalist's Garden produces herbs | Place garden, wait 1 day, check storage → 1-2 herbs appeared |
| Garden quality | Garden herbs have ~80% potency of foraged equivalents |
| Concealment Locker hides items | Place contraband in locker → `IsCriminal` not triggered in zone |
| Safe House effect | Place safe house room item → territory heat −5/day |
| Config override | Set `GardenYieldPerDay=5` → garden produces 5 herbs/day |
