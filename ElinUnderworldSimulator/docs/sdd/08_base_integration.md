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
    
    // Campfire for ambient light
    PlaceThing("bonfire", cx, cz + 3, map);
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
| **Novice** (0) | Mixing Table, Contraband Chest, basic storage | Bootstrap (pre-placed) |
| **Peddler** (500 rep) | Processing Vat | Craft at Mixing Table using `uw_mineral_crude/3,plank/6,ingot/2` |
| **Supplier** (2000 rep) | Advanced Lab | Craft at Mixing Table using `uw_crystal_void/2,glass/8,ingot/6,plank/4` |
| **Kingpin** (5000 rep) | Safe House Room | Craft decoration item that grants heat reduction aura |
| **Overlord** (10000 rep) | Logistics Hub | Craft desk furniture that provides +20% max nerve |

### 8.2.2 Rank-Gated Recipes

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

These roles use Elin's existing `FactionBranch` work assignment system and could be implemented as custom work policies or element modifiers. This is a Phase 2 enhancement.

---

## 8.5 Testing & Verification

### Bootstrap Placement Tests

| Test | Steps | Expected |
|------|-------|----------|
| Mixing table placed | Start Underworld game → check zone | Mixing table visible at center |
| Chest placed | Start Underworld game → check zone | Contraband chest adjacent to table |
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
| Safe House effect | Place safe house room item → territory heat −5/day |
