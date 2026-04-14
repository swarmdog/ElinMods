# Phase 8: Map Generation & Dynamic Zones Research

This document outlines the findings from researching Elin's decompiled map generation codebase, specifically evaluating how to generate zones programmatically and how to implement a dynamic, "unlocking" guild hall that expands as the player's guild rank increases.

## 1. Elin Map Generation Lifecycle
Maps in Elin are instantiated through the `Zone` data architecture. When a zone is entered, the game performs the following:

1.  **Data Lookup**: The game reads the `Zone` row in `SourceGame.xlsx`.
2.  **Generator Selection**: If `idFile` specifies a `.map` file, the game loads the static map built via the Map Editor. If `idGen` specifies a programmatic generator (like `MapGenRegion` or `MapGenDungen`), it dynamically builds the map using Noise matrices (Perlin noise).
3.  **Map Canvas Building (`OnGenerateTerrain`)**: The underlying grid (`EClass._map.cells`) is initialized via `map.CreateNew(Size)`. Floor and block (wall) IDs are assigned per coordinate.
4.  **Population (`Populate`)**: The game places foliage, monsters, interactables, and runs `Crawler` agents to carve paths.

## 2. Programmatically Building Zones (On The Fly)
To completely bypass the visual Map Editor and `.map` files, the game utilizes the `Map` class's grid manipulation methods. A zone subclass can overwrite `OnGenerateMap()` to draw a layout block-by-block.

-   **Floors**: `EClass._map.SetFloor(x, z, materialId, floorTileId)`
-   **Walls/Blocks**: `EClass._map.SetBlock(x, z, materialId, blockTileId)`
-   **Static Objects**: `EClass._map.SetObj(x, z, objectId)`
-   **Interactable Items/NPCs**: `EClass._zone.AddCard(ThingGen.Create("srg_codex"), x, z)`

> [!WARNING]
> While it is entirely possible to `for`-loop a 60x60 grid and mathematically calculate where every corridor, corner, bed, and pixel goes in C#, it is notoriously tedious to iterate on. 

## 3. The Unlocking Guild Concept (Skyreader Guild)
**The Goal**: Create an astrological guild on the astral plane that starts as a single small room and physically expands (adds new rooms/wings) as the player's `SkyreaderGuild.GuildRank` increases.

### Practical Implementation via Harmony / C#
Rather than building the *entire* base map from absolute scratch via nested arrays in C#, the most stable and developer-friendly way to achieve dynamic expansion in Elin is a **Hybrid Approach: Pre-built Maximum Canvas + Code-Driven Unlocks.**

#### Phase A: The Static Skeleton (Map Editor)
1.  Open Elin's Map Editor.
2.  Build the **Final, Maximum-Rank Guild** across the full canvas (e.g., 60x60). Lay out the central hall, the Tier 3 Observatory, the Tier 5 Conjuration Room, etc.
3.  Fill all the doorways leading to higher-tier rooms with a specific blocking wall (e.g., a custom indestructible "Astral Void Block").
4.  Export this as `srg_guild_base.map`.

#### Phase B: The Programmable Overlay (C#)
1.  **Create the Zone Class**: In our Skyreader C# project, create a new class:
    ```csharp
    public class Zone_SkyreaderGuild : Zone_Civilized
    {
        // Elin triggers this when the player crosses into the zone
        public override void OnActivate()
        {
            base.OnActivate();
            UpdateGuildLayout();
        }
    }
    ```
2.  **Define the Unlocks**: We specify exact coordinate boxes for each room.
    ```csharp
    public void UpdateGuildLayout()
    {
        GuildRank currentRank = QuestSkyreader.GetCurrentRank();
        
        // Remove the void walls and unlock the Observatory at Tier 3
        if (currentRank >= GuildRank.Researcher && !EClass.player.flags.Contains("srg_unlocked_obs"))
        {
            UnlockRoom(20, 40, 30, 50); // Removes blocking walls at bounding box coords
            EClass._zone.AddCard(CharaGen.Create("srg_archivist"), 25, 45); // Spawn NPC
            Msg.Say("The astral mists clear, revealing the Observatory.");
            EClass.player.flags.Add("srg_unlocked_obs");
        }
    }
    ```
3.  **The Code-Driven Modifications**: The `UnlockRoom` helper simply runs a loop to delete the custom void blocks, making the path walkable.
    ```csharp
    private void UnlockRoom(int startX, int startZ, int endX, int endZ)
    {
        for (int x = startX; x <= endX; x++) {
            for (int z = startZ; z <= endZ; z++) {
                if (EClass._map.cells[x,z]._block == ASTRAL_VOID_BLOCK_ID) {
                    EClass._map.SetBlock(x, z, 0); // Delete the wall
                }
            }
        }
        // Force Elin's rendering engine to update shadows and line-of-sight
        EClass._map.RefreshAllTiles();
    }
    ```

### Why this approach?
1.  **Serialization Safety**: Elin serializes modified map cells automatically in the player's save file. By removing blocks dynamically via C#, the game permanently remembers that the corridor is now open. We don't have to redefine the map every time they enter.
2.  **Visual Polish**: We can use the visual Map Editor to make the rooms gorgeous, while relying on the DLL purely for algorithmic gating and NPC injection.
3.  **Extensibility**: If we later add Tier 7, we just update the `.map` file to include another room and add one simple `UnlockRoom` coordinate block in C#. 

## 4. Algorithmic BSP/Procedural Generation
Yes, it is entirely possible to use well-known dungeon generation algorithms (like Binary Space Partitioning or Cellular Automata) to build the guild entirely programmatically in C#. In fact, Elin itself ships with an internal library called `Dungen` which handles standard BSP room generation for all its random Nefia and wilderness dungeons.

### How it would work:
1. **The Custom Generator**: We create a `MapGenSkyreader` class inheriting from `BaseMapGen`. This class overrides the base terrain generator and bypasses the static map loading entirely.
2. **The Layout Definition**: You can define your ideal rooms in C# (or JSON) via a data structure like this:
```csharp
public class GuildRoom {
    public string RoomName; // e.g. "Cosmic Observatory"
    public int RequestedWidth;
    public int RequestedHeight;
    public List<string> FurnitureToSpawn; // e.g. ["srg_celestial_globe", "srg_eclipse_hearth"]
}
```
3. **The BSP Engine**: When the generator runs, it starts with an empty canvas (e.g., an 80x80 void). Our algorithm splits the space into rectangles (`Rect`). It assigns a `GuildRoom` definition to each valid rectangle, and connects them via generated corridors.
4. **The Drawing Phase**: The algorithm loops through each assigned room. It paints the floor using `map.SetFloor()`, builds walls around the edges using `map.SetBlock()`, and finally places the requested furniture (`EClass._zone.AddCard`) at random offsets or specific predefined corners within that mathematical rectangle.

### Advantages:
- **Infinite Scalability**: Once the engine is written, you can easily add "The Astral Archives" to your list of rooms, and the algorithm will automatically allocate a wing for it on the next map generation.
- **Dynamic Restructuring**: You can fully randomize the layout every time the player enters the portal, turning the Guild into a rogue-like dungeon rather than a static hub.

### Disadvantages:
- **Visual Control & Polish**: It is notoriously difficult to guarantee a room "looks beautiful" algorithmically. You lose the bespoke, handcrafted feel of meticulously placing a carpet exactly beneath a table.
- **Implementation Time**: Writing a bespoke BSP layout generator in C# that intelligently spaces objects, connects corridors logically, and prevents clipping takes drastically more time than visually dragging and dropping assets in the Elin Map Editor.

## Conclusion
Adding a custom, hand-built zone is native to the engine. Expanding it dynamically based on mod variables (like Guild Rank) is best practically achieved by hooking the `Zone.OnActivate` lifecycle, reading current rank progress against a saved flag, and performing direct grid manipulation to carve paths through previously blocked terrain.
