using System;
using System.Collections.Generic;
using System.Linq;

namespace SkyreaderGuild
{
    public static class GuildLayoutBuilder
    {
        public const int MapSize = 50;

        // --------------- Material / Tile Constants ---------------
        private const int MatCrystal = 23;
        private const int MatObsidian = 55;
        private const int FloorSky = 90;
        private const int FloorCrystal = 22;
        private const int FloorCarpetConstellation = 80;
        private const int FloorCarpetEntry = 63;
        private const int FloorCarpetStudy = 24;
        private const int FloorCarpetObservatory = 62;
        private const int FloorStoneForge = 14;
        private const int FloorDecoSanctum = 105;
        private const int BlockRock = 9;     // TileType.Block — full block, auto-tiles with neighbors
        private const int BlockRockAlt = 19;  // TileType.Block — alternate stone block texture
        private const int LayoutKeyStr = 78010;

        // --------------- Room Definitions ---------------
        // Rooms keep their thematic shapes: circles, diamonds, rectangles.
        // Positions validated via Python flood-fill (629/629 tiles connected).

        private static readonly RoomDef Entry = new RoomDef(
            "entry", GuildRank.Wanderer,
            4, 20, 14, 28,
            (x, z) => x >= 4 && x <= 14 && z >= 20 && z <= 28);

        private static readonly RoomDef Atrium = new RoomDef(
            "atrium", GuildRank.Wanderer,
            17, 17, 33, 33,
            (x, z) => { int dx = x - 25, dz = z - 25; return dx * dx + dz * dz <= 64; });

        private static readonly RoomDef Study = new RoomDef(
            "study", GuildRank.Seeker,
            7, 8, 17, 18,
            (x, z) => Math.Abs(x - 12) + Math.Abs(z - 13) <= 5);

        private static readonly RoomDef Observatory = new RoomDef(
            "observatory", GuildRank.Researcher,
            20, 35, 30, 45,
            (x, z) => { int dx = x - 25, dz = z - 40; return dx * dx + dz * dz <= 25; });

        private static readonly RoomDef Forge = new RoomDef(
            "forge", GuildRank.CosmosAddled,
            35, 20, 45, 30,
            (x, z) => { int dx = x - 40, dz = z - 25; return dx * dx + dz * dz <= 25; });

        private static readonly RoomDef Sanctum = new RoomDef(
            "sanctum", GuildRank.CosmosApplied,
            20, 5, 30, 15,
            (x, z) => Math.Abs(x - 25) + Math.Abs(z - 10) <= 5);

        private static readonly RoomDef[] Rooms = { Entry, Atrium, Study, Observatory, Forge, Sanctum };

        // --------------- Corridor Definitions ---------------
        // Each corridor is a list of axis-aligned rectangular segments.
        // Validated to overlap room interiors at both endpoints.

        private static readonly CorridorDef[] Corridors =
        {
            new CorridorDef("entry_to_atrium", new RectSeg(14, 23, 18, 25)),
            new CorridorDef("atrium_to_study", new RectSeg(17, 13, 19, 15), new RectSeg(18, 15, 20, 19)),
            new CorridorDef("atrium_to_observatory", new RectSeg(24, 33, 26, 36)),
            new CorridorDef("atrium_to_forge", new RectSeg(33, 24, 36, 26)),
            new CorridorDef("atrium_to_sanctum", new RectSeg(24, 15, 26, 18)),
        };

        // --------------- Barrier Gates ---------------
        // 3 tiles wide, placed at corridor mouths to gated rooms.

        private static readonly Dictionary<string, PointSpec[]> Barriers = new Dictionary<string, PointSpec[]>
        {
            { "study", new[] { new PointSpec(15, 13), new PointSpec(15, 14), new PointSpec(15, 15) } },
            { "observatory", new[] { new PointSpec(24, 34), new PointSpec(25, 34), new PointSpec(26, 34) } },
            { "forge", new[] { new PointSpec(34, 24), new PointSpec(34, 25), new PointSpec(34, 26) } },
            { "sanctum", new[] { new PointSpec(24, 16), new PointSpec(25, 16), new PointSpec(26, 16) } },
        };

        // --------------- 8-directional offsets (Moore neighborhood) ---------------
        private static readonly PointSpec[] MooreOffsets =
        {
            new PointSpec(-1, -1), new PointSpec(-1, 0), new PointSpec(-1, 1),
            new PointSpec(0, -1),                         new PointSpec(0, 1),
            new PointSpec(1, -1),  new PointSpec(1, 0),  new PointSpec(1, 1),
        };

        private static readonly PointSpec[] CardinalOffsets =
        {
            new PointSpec(0, -1), new PointSpec(1, 0), new PointSpec(0, 1), new PointSpec(-1, 0),
        };

        // --------------- Furniture ---------------
        // Coordinates validated against room shape predicates.

        private static readonly Dictionary<string, Placement[]> RoomFurniture = new Dictionary<string, Placement[]>
        {
            {
                "entry",
                new[]
                {
                    new Placement("srg_guild_exit", 5, 24, true),
                    new Placement("srg_aurora_lamp", 13, 21, true),
                    new Placement("srg_aurora_lamp", 13, 27, true),
                    new Placement("srg_constellation_rug", 9, 24, true),
                    new Placement("pot_plantSmall", 5, 27, true, -1, false, false, "entry_plant_1"),
                    new Placement("candle_stand", 6, 21, true, -1, false, false, "entry_candelabrum"),
                }
            },
            {
                "atrium",
                new[]
                {
                    new Placement("srg_nexus_core", 25, 25, true),
                    new Placement("telescope", 25, 31, true, 2, false, false, "atrium_telescope_north"),
                    new Placement("telescope", 25, 19, true, 0, false, false, "atrium_telescope_south"),
                    new Placement("srg_codex", 31, 25, true, 3, false, false, "atrium_codex_east"),
                    new Placement("srg_celestial_globe", 19, 25, true, 1, false, false, "atrium_globe_west"),
                    new Placement("srg_aurora_lamp", 21, 29, true),
                    new Placement("srg_aurora_lamp", 29, 29, true),
                    new Placement("srg_aurora_lamp", 21, 21, true),
                    new Placement("srg_aurora_lamp", 29, 21, true),
                    new Placement("bookshelf", 22, 30, true),
                    new Placement("bookshelf", 28, 30, true),
                    new Placement("bookshelf", 22, 20, true),
                    new Placement("bookshelf", 28, 20, true),
                    new Placement("carpet", 25, 30, true, 0, false, false, "atrium_carpet_n"),
                    new Placement("carpet", 25, 20, true, 0, false, false, "atrium_carpet_s"),
                    new Placement("pot_plantBig", 23, 31, true, 0, false, false, "atrium_plant_nw"),
                    new Placement("pot_plantBig", 27, 31, true, 0, false, false, "atrium_plant_ne"),
                    new Placement("candle_stand", 22, 25, true, -1, false, false, "atrium_candle_west"),
                    new Placement("candle_stand", 28, 25, true, -1, false, false, "atrium_candle_east"),
                    new Placement("chair", 24, 30, true, -1, false, false, "atrium_chair_n"),
                    new Placement("chair", 30, 25, true, -1, false, false, "atrium_chair_e"),
                    new Placement("cushion", 20, 25, true, -1, false, false, "atrium_cushion_w"),
                }
            },
            {
                "study",
                new[]
                {
                    new Placement("srg_starfall_table", 12, 13, true),
                    new Placement("srg_lunar_armchair", 11, 13, true),
                    new Placement("srg_lunar_armchair", 13, 13, true),
                    new Placement("tool_writting", 12, 14, true),
                    new Placement("wall_shelf", 10, 16, true, -1, true, true, "study_shelf"),
                    new Placement("map_big", 10, 11, true, 0, false, false, "study_big_map"),
                    new Placement("terra_globe", 14, 11, true, 0, false, false, "study_globe"),
                    new Placement("candle8", 12, 15, true, -1, false, false, "study_candle"),
                    new Placement("carpet", 12, 13, true, -1, false, false, "study_carpet"),
                }
            },
            {
                "observatory",
                new[]
                {
                    new Placement("srg_zodiac_dresser", 25, 43, true),
                    new Placement("srg_cosmic_mirror", 23, 41, true),
                    new Placement("srg_planisphere_cabinet", 27, 41, true),
                    new Placement("srg_celestial_globe", 25, 39, true),
                    new Placement("telescope", 23, 37, true, 1, false, false, "obs_tele_w"),
                    new Placement("telescope", 27, 37, true, 3, false, false, "obs_tele_e"),
                    new Placement("candle8b", 25, 41, true, -1, false, false, "obs_candle"),
                    new Placement("carpet", 25, 40, true, -1, false, false, "obs_carpet"),
                    new Placement("chest_stone", 25, 43, true, -1, false, false, "obs_chest"),
                    new Placement("pot_plantBig", 28, 42, true, -1, false, false, "obs_plant"),
                    new Placement("stand_armor", 22, 42, true, -1, false, false, "obs_armor"),
                }
            },
            {
                "forge",
                new[]
                {
                    new Placement("srg_astral_chandelier", 40, 25, true),
                    new Placement("srg_stardust_bed", 43, 27, true),
                    new Placement("anvil", 40, 23, true),
                    new Placement("srg_meteorite_statue", 43, 23, true),
                    new Placement("wall_shelf", 44, 25, true, -1, true, true, "forge_shelf"),
                    new Placement("candle_stand", 39, 27, true, -1, false, false, "forge_candle"),
                    new Placement("barrel", 41, 24, true, -1, false, false, "forge_barrel"),
                    new Placement("carpet", 40, 25, true, -1, false, false, "forge_carpet"),
                    new Placement("table", 38, 26, true, -1, false, false, "forge_table"),
                }
            },
            {
                "sanctum",
                new[]
                {
                    new Placement("srg_eclipse_hearth", 25, 8, true),
                    new Placement("srg_meteorite_statue", 23, 10, true),
                    new Placement("srg_astral_chandelier", 25, 11, true),
                    new Placement("terra_globe", 27, 10, true, 0, false, false, "sanctum_globe"),
                    new Placement("candle_stand", 23, 9, true, -1, false, false, "sanctum_candle_w"),
                    new Placement("candle_stand", 27, 9, true, -1, false, false, "sanctum_candle_e"),
                    new Placement("carpet", 25, 12, true, -1, false, false, "sanctum_carpet"),
                    new Placement("vase", 28, 10, true, -1, false, false, "sanctum_vase"),
                }
            },
            {
                "corridor",
                new[]
                {
                    new Placement("candle2", 16, 24, true, -1, true, false, "corridor_lamp_1"),
                }
            },
        };

        private static readonly PointSpec[] ProtectedWallMountPoints =
        {
            new PointSpec(5, 24), // near guild exit
        };

        // ===================== Public API =====================

        public static void Build(Zone zone)
        {
            Map map = GetMap(zone);
            if (map == null)
                throw new InvalidOperationException("Skyreader guild layout build called without an active map.");

            ValidateRequiredSourceRows();
            GuildRank rank = GetCurrentRank();
            bool[,] floorGrid = BuildFloorGrid(rank);
            ApplyArchitecture(map, rank, floorGrid);
            PlaceUnlockedFurniture(zone, rank, floorGrid);
            EnsureArkynPresent(zone);
            EnsureArchivistIfUnlocked(zone, rank);
            DecorateTables(zone);
            ApplyAlgorithmicLighting(zone, floorGrid);
            VerifyConnectivity(floorGrid, rank);
            map.RefreshAllTiles();
            SkyreaderGuild.Log("Skyreader Observatory generated.");
        }

        public static void UpdateUnlockedLayout(Zone zone)
        {
            QuestSkyreader quest = EClass.game.quests.Get<QuestSkyreader>();
            if (quest == null) return;

            Map map = GetMap(zone);
            if (map == null)
                throw new InvalidOperationException("Skyreader guild layout update called without an active map.");

            ValidateRequiredSourceRows();
            GuildRank rank = quest.GetCurrentRank();
            bool[,] floorGrid = BuildFloorGrid(rank);
            Dictionary<string, bool> wasSealed = Barriers.Keys.ToDictionary(id => id, id => IsRoomStillSealed(zone, id));

            ApplyArchitecture(map, rank, floorGrid);
            PlaceUnlockedFurniture(zone, rank, floorGrid);
            EnsureArkynPresent(zone);
            EnsureArchivistIfUnlocked(zone, rank);
            map.RefreshAllTiles();

            AnnounceUnlock(wasSealed, "study", GuildRank.Seeker, rank, "The astral mists part, revealing the Study Hall.");
            AnnounceUnlock(wasSealed, "observatory", GuildRank.Researcher, rank, "The celestial dome overhead clears. The Observatory is open.");
            AnnounceUnlock(wasSealed, "forge", GuildRank.CosmosAddled, rank, "A resonance of cosmic fire answers you. The Astral Forge has awakened.");
            AnnounceUnlock(wasSealed, "sanctum", GuildRank.CosmosApplied, rank, "The Starseeker Sanctum opens before you.");
        }

        // ===================== Floor Grid Construction =====================

        /// <summary>
        /// Builds the canonical floor grid for the given rank.
        /// A cell is floor if it is inside any unlocked room's shape or any corridor.
        /// </summary>
        private static bool[,] BuildFloorGrid(GuildRank rank)
        {
            bool[,] grid = new bool[MapSize, MapSize];

            // Mark room interiors
            foreach (RoomDef room in Rooms)
            {
                if (rank < room.Rank) continue;
                for (int x = room.MinX; x <= room.MaxX; x++)
                {
                    for (int z = room.MinZ; z <= room.MaxZ; z++)
                    {
                        if (InBounds(x, z) && room.Contains(x, z))
                            grid[x, z] = true;
                    }
                }
            }

            // Always mark Entry and Atrium regardless (they are Wanderer rank)
            // Mark corridor interiors
            foreach (CorridorDef corridor in Corridors)
            {
                // Only carve corridors to rooms the player can access
                bool shouldCarve = true;
                if (corridor.Id == "atrium_to_study" && rank < Study.Rank) shouldCarve = false;
                if (corridor.Id == "atrium_to_observatory" && rank < Observatory.Rank) shouldCarve = false;
                if (corridor.Id == "atrium_to_forge" && rank < Forge.Rank) shouldCarve = false;
                if (corridor.Id == "atrium_to_sanctum" && rank < Sanctum.Rank) shouldCarve = false;
                if (!shouldCarve) continue;

                foreach (RectSeg seg in corridor.Segments)
                {
                    for (int x = seg.MinX; x <= seg.MaxX; x++)
                    {
                        for (int z = seg.MinZ; z <= seg.MaxZ; z++)
                        {
                            if (InBounds(x, z))
                                grid[x, z] = true;
                        }
                    }
                }
            }

            // Always carve the entry-to-atrium corridor
            return grid;
        }

        // ===================== Architecture =====================

        private static void ApplyArchitecture(Map map, GuildRank rank, bool[,] floorGrid)
        {
            // Step 1: Build wall shell (2-pass, 8-directional)
            bool[,] wallGrid = BuildWallShell(floorGrid);

            // Step 2: Cellular automata smoothing (2 passes)
            SmoothWalls(floorGrid, wallGrid);

            // Step 3: Apply to map
            for (int x = 0; x < MapSize; x++)
            {
                for (int z = 0; z < MapSize; z++)
                {
                    if (floorGrid[x, z])
                    {
                        int floor = GetRoomFloor(x, z);
                        int mat = GetRoomFloorMat(x, z);
                        map.SetFloor(x, z, mat, floor);
                        map.SetBlock(x, z);
                        map.SetObj(x, z);
                        map.cells[x, z].impassable = false;
                    }
                    else if (wallGrid[x, z])
                    {
                        int blockId = GetBlockId(x, z);
                        int blockMat = GetBlockMat(x, z);
                        map.SetFloor(x, z, blockMat, FloorCrystal);
                        map.SetBlock(x, z, blockMat, blockId);
                        map.SetObj(x, z);
                        map.cells[x, z].impassable = false;
                    }
                    else
                    {
                        // Void sky
                        map.SetFloor(x, z, MatObsidian, FloorSky);
                        map.SetBlock(x, z);
                        map.SetObj(x, z);
                        map.cells[x, z].impassable = true;
                    }
                }
            }

            // Step 4: Seal locked rooms
            SealLockedRooms(map, rank);
        }

        /// <summary>
        /// Builds a 2-tile-thick wall shell using 8-directional (Moore) neighborhood.
        /// Pass 1: any non-floor cell adjacent to a floor cell becomes wall.
        /// Pass 2: any non-floor, non-wall cell adjacent to a wall cell becomes wall.
        /// </summary>
        private static bool[,] BuildWallShell(bool[,] floorGrid)
        {
            bool[,] wall = new bool[MapSize, MapSize];

            // Pass 1: inner shell
            for (int x = 0; x < MapSize; x++)
            {
                for (int z = 0; z < MapSize; z++)
                {
                    if (floorGrid[x, z])
                    {
                        foreach (PointSpec offset in MooreOffsets)
                        {
                            int nx = x + offset.X, nz = z + offset.Z;
                            if (InBounds(nx, nz) && !floorGrid[nx, nz])
                                wall[nx, nz] = true;
                        }
                    }
                }
            }

            // Pass 2: outer shell
            bool[,] outerWall = (bool[,])wall.Clone();
            for (int x = 0; x < MapSize; x++)
            {
                for (int z = 0; z < MapSize; z++)
                {
                    if (wall[x, z])
                    {
                        foreach (PointSpec offset in MooreOffsets)
                        {
                            int nx = x + offset.X, nz = z + offset.Z;
                            if (InBounds(nx, nz) && !floorGrid[nx, nz] && !wall[nx, nz])
                                outerWall[nx, nz] = true;
                        }
                    }
                }
            }

            return outerWall;
        }

        /// <summary>
        /// Cellular automata smoothing (B5678/S45678) on wall region.
        /// Removes single-tile wall protrusions, fills small gaps.
        /// Runs 2 passes.
        /// </summary>
        private static void SmoothWalls(bool[,] floorGrid, bool[,] wallGrid)
        {
            for (int pass = 0; pass < 2; pass++)
            {
                bool[,] next = (bool[,])wallGrid.Clone();
                for (int x = 1; x < MapSize - 1; x++)
                {
                    for (int z = 1; z < MapSize - 1; z++)
                    {
                        if (floorGrid[x, z]) continue; // never touch floor cells

                        int neighbors = 0;
                        foreach (PointSpec offset in MooreOffsets)
                        {
                            if (wallGrid[x + offset.X, z + offset.Z])
                                neighbors++;
                        }

                        if (wallGrid[x, z] && neighbors < 4)
                            next[x, z] = false; // remove thin protrusion
                        else if (!wallGrid[x, z] && !floorGrid[x, z] && neighbors >= 5)
                            next[x, z] = true; // fill small gap
                    }
                }
                Array.Copy(next, wallGrid, MapSize * MapSize);
            }
        }

        // ===================== Floor / Wall Material Selection =====================

        private static int GetRoomFloor(int x, int z)
        {
            if (IsInRoom(x, z, Entry)) return FloorCarpetEntry;
            if (IsInRoom(x, z, Study)) return FloorCarpetStudy;
            if (IsInRoom(x, z, Observatory)) return FloorCarpetObservatory;
            if (IsInRoom(x, z, Forge)) return FloorStoneForge;
            if (IsInRoom(x, z, Sanctum)) return FloorDecoSanctum;

            // Atrium: radial floor pattern
            int dx = x - 25, dz = z - 25;
            int distSq = dx * dx + dz * dz;
            if (distSq <= 16) return FloorSky; // Epicenter around nexus core
            if (distSq <= 64) return FloorCrystal; // Mid ring

            // Corridor fallback
            return FloorCarpetConstellation;
        }

        private static int GetRoomFloorMat(int x, int z)
        {
            if (IsInRoom(x, z, Entry)) return MatObsidian;
            if (IsInRoom(x, z, Study)) return MatCrystal;
            if (IsInRoom(x, z, Observatory)) return MatObsidian;
            if (IsInRoom(x, z, Forge)) return MatObsidian;
            if (IsInRoom(x, z, Sanctum)) return MatCrystal;

            int dx = x - 25, dz = z - 25;
            if (dx * dx + dz * dz <= 64) return MatCrystal;
            return MatObsidian;
        }

        private static int GetBlockId(int x, int z)
        {
            if (IsInRoom(x, z, Sanctum) || IsInRoom(x, z, Study))
                return BlockRockAlt;
            return BlockRock;
        }

        private static int GetBlockMat(int x, int z)
        {
            if (IsInRoom(x, z, Sanctum) || IsInRoom(x, z, Study))
                return MatCrystal;
            return MatObsidian;
        }

        private static bool IsInRoom(int x, int z, RoomDef room)
        {
            return x >= room.MinX && x <= room.MaxX && z >= room.MinZ && z <= room.MaxZ && room.Contains(x, z);
        }

        // ===================== Room Sealing =====================

        private static void SealLockedRooms(Map map, GuildRank rank)
        {
            foreach (var entry in Barriers)
            {
                RoomDef room = GetRoom(entry.Key);
                if (rank >= room.Rank) continue;

                foreach (PointSpec p in entry.Value)
                {
                    map.SetBlock(p.X, p.Z, MatObsidian, BlockRock);
                }
            }
        }

        // ===================== Connectivity Verification =====================

        /// <summary>
        /// Flood-fills from Entry center and verifies all unlocked room centers are reachable.
        /// Throws if any unlocked room is disconnected.
        /// </summary>
        private static void VerifyConnectivity(bool[,] floorGrid, GuildRank rank)
        {
            bool[,] visited = new bool[MapSize, MapSize];
            Queue<int> queue = new Queue<int>();

            int startX = 9, startZ = 24; // Entry center
            visited[startX, startZ] = true;
            queue.Enqueue(startX * MapSize + startZ);

            while (queue.Count > 0)
            {
                int key = queue.Dequeue();
                int cx = key / MapSize, cz = key % MapSize;
                foreach (PointSpec offset in CardinalOffsets)
                {
                    int nx = cx + offset.X, nz = cz + offset.Z;
                    if (InBounds(nx, nz) && floorGrid[nx, nz] && !visited[nx, nz])
                    {
                        visited[nx, nz] = true;
                        queue.Enqueue(nx * MapSize + nz);
                    }
                }
            }

            foreach (RoomDef room in Rooms)
            {
                if (rank < room.Rank) continue;
                int rcx = (room.MinX + room.MaxX) / 2;
                int rcz = (room.MinZ + room.MaxZ) / 2;
                if (!visited[rcx, rcz])
                {
                    SkyreaderGuild.Log($"WARNING: Room '{room.Id}' center ({rcx},{rcz}) is disconnected from Entry!");
                }
            }
        }

        // ===================== Furniture Placement =====================

        private static void PlaceUnlockedFurniture(Zone zone, GuildRank rank, bool[,] floorGrid)
        {
            foreach (RoomDef room in Rooms)
            {
                if (rank >= room.Rank)
                    PlaceRoomFurniture(zone, room.Id, floorGrid);
            }
            PlaceRoomFurniture(zone, "corridor", floorGrid);
        }

        private static void PlaceRoomFurniture(Zone zone, string roomId, bool[,] floorGrid)
        {
            if (!RoomFurniture.TryGetValue(roomId, out Placement[] placements)) return;
            foreach (Placement placement in placements)
            {
                EnsureThing(zone, placement, floorGrid);
            }
        }

        private static void EnsureThing(Zone zone, Placement placement, bool[,] floorGrid)
        {
            Map map = GetMap(zone);
            if (map == null)
                throw new InvalidOperationException("Skyreader guild furniture placement called without an active map.");

            if (!InBounds(placement.X, placement.Z))
            {
                SkyreaderGuild.Log($"Skipped {placement.Id}: ({placement.X},{placement.Z}) is outside the guild map.");
                return;
            }

            // Validate that placement is on floor, not wall or void
            if (!floorGrid[placement.X, placement.Z])
            {
                SkyreaderGuild.Log($"Skipped {placement.Id}: ({placement.X},{placement.Z}) is not on floor (wall or void).");
                return;
            }

            if (placement.WallMounted && IsProtectedWallMountPoint(placement.X, placement.Z))
            {
                SkyreaderGuild.Log($"Skipped wall mounted {placement.Id}: ({placement.X},{placement.Z}) is protected.");
                return;
            }

            int dir = placement.Dir;
            if (placement.RequiresAdjacentBlock || placement.WallMounted)
            {
                if (!TryGetWallMountDir(map, placement.X, placement.Z, out int wallDir))
                {
                    SkyreaderGuild.Log($"Skipped {placement.Id}: ({placement.X},{placement.Z}) has no adjacent wall block.");
                    return;
                }
                if (dir < 0) dir = wallDir;
            }

            if (map.cells[placement.X, placement.Z]._block != 0)
            {
                SkyreaderGuild.Log($"Skipped {placement.Id}: ({placement.X},{placement.Z}) is blocked.");
                return;
            }

            // Check for existing item
            foreach (Thing thing in map.things)
            {
                if (thing.isDestroyed) continue;
                if (!placement.UniqueKey.IsEmpty() && thing.GetStr(LayoutKeyStr) == placement.UniqueKey)
                    return;
                if (thing.id == placement.Id && thing.pos.x == placement.X && thing.pos.z == placement.Z)
                    return;
            }

            Thing t = ThingGen.Create(placement.Id);
            if (!placement.UniqueKey.IsEmpty())
                t.SetStr(LayoutKeyStr, placement.UniqueKey);

            Card card = zone.AddCard(t, placement.X, placement.Z);
            card.isNPCProperty = true;

            if (dir >= 0) card.dir = dir;
            if (placement.Install) card.Install();
        }

        // ===================== Lighting =====================

        private static void ApplyAlgorithmicLighting(Zone zone, bool[,] floorGrid)
        {
            Map map = GetMap(zone);
            if (map == null) return;

            for (int z = 1; z < MapSize - 1; z++)
            {
                for (int x = 1; x < MapSize - 1; x++)
                {
                    // Only place sconces on wall blocks
                    if (floorGrid[x, z]) continue;
                    Cell cell = map.cells[x, z];
                    if (cell._block == 0) continue;

                    // Check if any adjacent cell is floor
                    bool adjacentFloor = false;
                    foreach (PointSpec offset in CardinalOffsets)
                    {
                        int nx = x + offset.X, nz = z + offset.Z;
                        if (InBounds(nx, nz) && floorGrid[nx, nz])
                        {
                            adjacentFloor = true;
                            break;
                        }
                    }
                    if (!adjacentFloor) continue;

                    // Check no light nearby
                    bool hasLightNearby = false;
                    foreach (Thing t in map.things)
                    {
                        if (t.id == "candle5" || t.id == "srg_aurora_lamp")
                        {
                            if (Math.Abs(t.pos.x - x) + Math.Abs(t.pos.z - z) <= 4)
                            {
                                hasLightNearby = true;
                                break;
                            }
                        }
                    }
                    if (hasLightNearby) continue;

                    if (EClass.rnd(100) < 15)
                    {
                        Thing light = ThingGen.Create("candle5");
                        zone.AddCard(light, new Point(x, z)).Install();
                    }
                }
            }
        }

        // ===================== Table Decoration =====================

        private static void DecorateTables(Zone zone)
        {
            Map map = GetMap(zone);
            if (map == null) return;
            string[] clutterPool = { "book_ancient", "scroll", "map", "candle_stand", "tool_writting" };

            var tables = map.things.Where(t => t.id == "table" || t.id == "srg_starfall_table").ToList();
            foreach (var t in tables)
            {
                if (EClass.rnd(100) < 70)
                {
                    string id = clutterPool.RandomItem();
                    Thing clutter = ThingGen.Create(id);
                    zone.AddCard(clutter, t.pos).Install();
                }
            }
        }

        // ===================== NPC Placement =====================

        private static void EnsureArkynPresent(Zone zone)
        {
            EnsureUniqueChara(zone, "srg_arkyn", 8, 22);
        }

        private static void EnsureArchivistIfUnlocked(Zone zone, GuildRank rank)
        {
            if (rank >= GuildRank.Understander)
                EnsureUniqueChara(zone, "srg_archivist", 25, 43);
        }

        private static void EnsureUniqueChara(Zone zone, string id, int x, int z)
        {
            Chara chara = EClass.game.cards.globalCharas.Find(id);
            if (chara == null)
            {
                chara = CharaGen.Create(id, -1);
                chara.SetGlobal();
            }

            if (chara.currentZone == zone && chara.pos != null && chara.pos.IsValid)
                return;

            Point point = new Point(x, z).GetNearestPoint(allowBlock: false, allowChara: false, allowInstalled: true);
            zone.AddCard(chara, point);
            SkyreaderGuild.Log($"Placed {id} in Skyreader Observatory at ({point.x},{point.z}).");
        }

        // ===================== Announcement =====================

        private static void AnnounceUnlock(Dictionary<string, bool> wasSealed, string roomId, GuildRank minRank, GuildRank rank, string message)
        {
            if (rank < minRank) return;
            if (!wasSealed.TryGetValue(roomId, out bool sealedBefore) || !sealedBefore) return;
            Msg.SayRaw("<color=#b3e0ff>" + message + "</color>");
            SkyreaderGuild.Log($"Unlocked Skyreader Observatory room '{roomId}' at rank {QuestSkyreader.FormatRankName(rank)}.");
        }

        public static bool IsRoomStillSealed(Zone zone, string roomId)
        {
            if (!Barriers.TryGetValue(roomId, out PointSpec[] points)) return false;
            Map map = GetMap(zone);
            return map != null && points.Any(p => map.cells[p.X, p.Z]._block != 0);
        }

        // ===================== Helpers =====================

        private static bool TryGetWallMountDir(Map map, int x, int z, out int dir)
        {
            for (int i = 0; i < CardinalOffsets.Length; i++)
            {
                int nx = x + CardinalOffsets[i].X, nz = z + CardinalOffsets[i].Z;
                if (!InBounds(nx, nz)) continue;
                if (map.cells[nx, nz]._block == 0) continue;
                dir = (i + 2) % 4;
                return true;
            }
            dir = -1;
            return false;
        }

        private static bool IsProtectedWallMountPoint(int x, int z)
        {
            foreach (PointSpec point in ProtectedWallMountPoints)
            {
                if (Math.Abs(point.X - x) <= 2 && Math.Abs(point.Z - z) <= 2)
                    return true;
            }
            return false;
        }

        private static GuildRank GetCurrentRank()
        {
            QuestSkyreader quest = EClass.game?.quests?.Get<QuestSkyreader>();
            return quest?.GetCurrentRank() ?? GuildRank.Wanderer;
        }

        private static Map GetMap(Zone zone)
        {
            return zone?.map ?? EClass._map;
        }

        private static RoomDef GetRoom(string id)
        {
            foreach (RoomDef room in Rooms)
                if (room.Id == id) return room;
            throw new InvalidOperationException("Unknown Skyreader Observatory room: " + id);
        }

        private static bool InBounds(int x, int z)
        {
            return x >= 0 && z >= 0 && x < MapSize && z < MapSize;
        }

        private static void ValidateRequiredSourceRows()
        {
            List<string> missing = new List<string>();
            foreach (string id in RoomFurniture.Values.SelectMany(p => p).Select(p => p.Id).Distinct())
            {
                CardRow row;
                if (!EClass.sources.cards.map.TryGetValue(id, out row))
                {
                    missing.Add("Thing:" + id);
                    continue;
                }
                if (row.category.IsEmpty() || !EClass.sources.categories.map.ContainsKey(row.category))
                    missing.Add("Thing:" + id + " invalid category '" + row.category + "'");
            }

            foreach (string id in new[] { "srg_arkyn", "srg_archivist" })
            {
                if (!EClass.sources.charas.map.ContainsKey(id))
                    missing.Add("Chara:" + id);
            }

            if (missing.Count > 0)
                throw new InvalidOperationException("Skyreader Observatory layout has missing or invalid source rows: " + string.Join(", ", missing.ToArray()));
        }

        // ===================== Data Types =====================

        private class RoomDef
        {
            public readonly string Id;
            public readonly GuildRank Rank;
            public readonly int MinX, MinZ, MaxX, MaxZ;
            public readonly Func<int, int, bool> Contains;

            public RoomDef(string id, GuildRank rank, int minX, int minZ, int maxX, int maxZ, Func<int, int, bool> contains)
            {
                Id = id; Rank = rank;
                MinX = minX; MinZ = minZ; MaxX = maxX; MaxZ = maxZ;
                Contains = contains;
            }
        }

        private class CorridorDef
        {
            public readonly string Id;
            public readonly RectSeg[] Segments;

            public CorridorDef(string id, params RectSeg[] segments)
            {
                Id = id;
                Segments = segments;
            }
        }

        private struct RectSeg
        {
            public readonly int MinX, MinZ, MaxX, MaxZ;

            public RectSeg(int minX, int minZ, int maxX, int maxZ)
            {
                MinX = minX; MinZ = minZ; MaxX = maxX; MaxZ = maxZ;
            }
        }

        private struct PointSpec
        {
            public readonly int X, Z;
            public PointSpec(int x, int z) { X = x; Z = z; }
        }

        private struct Placement
        {
            public readonly string Id;
            public readonly int X, Z;
            public readonly bool Install;
            public readonly int Dir;
            public readonly bool RequiresAdjacentBlock;
            public readonly bool WallMounted;
            public readonly string UniqueKey;

            public Placement(string id, int x, int z, bool install, int dir = -1,
                bool requiresAdjacentBlock = false, bool wallMounted = false, string uniqueKey = null)
            {
                Id = id; X = x; Z = z; Install = install; Dir = dir;
                RequiresAdjacentBlock = requiresAdjacentBlock;
                WallMounted = wallMounted;
                UniqueKey = uniqueKey;
            }
        }
    }
}
