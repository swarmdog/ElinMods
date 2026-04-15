using System;
using System.Collections.Generic;
using System.Linq;

namespace SkyreaderGuild
{
    /// <summary>
    /// Post-generation reshaping pass for Astral Rift dungeons.
    /// After Elin's MapGenDungen produces a standard rectangular-room dungeon,
    /// this class scans for room bounding boxes and carves them into non-rectangular
    /// shapes (circles, crescents, crosses, stars, etc.) while guaranteeing connectivity.
    /// </summary>
    public static class RiftLayoutShaper
    {
        // ===================== Constants =====================

        private const int MinRoomDimension = 4;
        private const int MinRoomArea = 16;
        private const float MaxCorridorAspect = 3.0f;
        private const int StairProtectRadius = 2;
        private const int DoorProtectRadius = 1;

        // ===================== Shape Types =====================

        private enum ShapeType
        {
            Circle,
            Ellipse,
            Diamond,
            Cross,
            Crescent,
            LShape,
            Star,
            Irregular
        }

        // ===================== Depth-Gated Weights =====================
        // [shapeIndex][depthBracket]  — brackets: 0 = depth 1-2, 1 = depth 3-4, 2 = depth 5+

        private static readonly int[,] ShapeWeights =
        {
            // Circle, Ellipse, Diamond, Cross, Crescent, LShape, Star, Irregular
            { 30, 25, 25, 10,  5,  5,  0,  0 }, // depth 1-2
            { 20, 15, 20, 15, 10, 10,  5,  5 }, // depth 3-4
            { 15, 10, 15, 15, 10, 10, 15, 10 }, // depth 5+
        };

        private static readonly ShapeType[] AllShapes = (ShapeType[])Enum.GetValues(typeof(ShapeType));

        // ===================== Room Detection Data =====================

        private class DetectedRoom
        {
            public int MinX, MinZ, MaxX, MaxZ;
            public HashSet<long> FloorCells;
            public int CenterX, CenterZ;
            public int Width => MaxX - MinX + 1;
            public int Height => MaxZ - MinZ + 1;
            public int Area => FloorCells.Count;
        }

        // ===================== Cell Backup for Revert =====================

        private struct CellBackup
        {
            public int X, Z;
            public byte Block, BlockMat;
            public byte Floor, FloorMat;
        }

        // ===================== Public API =====================

        /// <summary>
        /// Reshapes rooms in the current map into non-rectangular forms.
        /// Call from AstralRiftThemingPatch.Postfix BEFORE loot/spawn placement.
        /// </summary>
        public static void Reshape(Zone zone)
        {
            if (!SkyreaderGuild.ConfigEnableRiftLayouts.Value)
            {
                SkyreaderGuild.Log("Rift layout reshaping disabled by config.");
                return;
            }

            Map map = EClass._map;
            if (map == null) return;

            int size = map.Size;
            HashSet<long> protectedCells = MarkProtectedCells(map, size);
            List<DetectedRoom> rooms = DetectRooms(map, size, protectedCells);

            if (rooms.Count == 0)
            {
                SkyreaderGuild.Log("Rift reshaper: no qualifying rooms found.");
                return;
            }

            // Deterministic shape selection seeded by zone UID
            Rand.SetSeed(zone.uid);

            int depthBracket = GetDepthBracket(zone.lv);
            int shapedCount = 0;
            HashSet<long> allNewWalls = new HashSet<long>();
            Dictionary<string, int> shapeCounts = new Dictionary<string, int>();

            for (int i = 0; i < rooms.Count; i++)
            {
                DetectedRoom room = rooms[i];

                if (room.Area < MinRoomArea) continue;
                if (room.Width < MinRoomDimension || room.Height < MinRoomDimension) continue;

                float aspect = (float)Math.Max(room.Width, room.Height) / Math.Min(room.Width, room.Height);
                if (aspect > MaxCorridorAspect) continue;

                ShapeType shape = SelectShape(depthBracket);

                // Back up original cells for potential revert
                List<CellBackup> backups = BackupRoomCells(map, room);

                HashSet<long> newWalls = ApplyShape(map, room, shape, protectedCells, size);

                // Verify this room didn't break connectivity
                if (!VerifyStairConnectivity(map, size))
                {
                    RevertCells(map, backups);
                    SkyreaderGuild.Log($"Rift reshaper: reverted room {i} ({shape}) — connectivity broken.");
                    continue;
                }

                foreach (long key in newWalls)
                    allNewWalls.Add(key);

                string shapeName = shape.ToString();
                if (!shapeCounts.ContainsKey(shapeName)) shapeCounts[shapeName] = 0;
                shapeCounts[shapeName]++;

                shapedCount++;
            }

            Rand.SetSeed();

            // Smooth newly created wall edges
            if (allNewWalls.Count > 0)
                SmoothNewWalls(map, size, allNewWalls);

            map.RefreshAllTiles();
            SkyreaderGuild.Log($"Reshaped {shapedCount}/{rooms.Count} rooms on rift floor lv={zone.lv}.");

            // Submit dominant shape as a geometry sample to the server
            if (shapedCount > 0)
            {
                string dominantShape = null;
                int maxCount = 0;
                foreach (var kv in shapeCounts)
                {
                    if (kv.Value > maxCount) { dominantShape = kv.Key; maxCount = kv.Value; }
                }
                if (dominantShape != null)
                {
                    int dangerBand = Math.Max(1, Math.Min(10, Math.Abs(zone.lv)));
                    SkyreaderGuild.SubmitGeometrySample(dangerBand, dominantShape, shapedCount);
                }
            }
        }

        // ===================== Protected Cell Marking =====================

        private static HashSet<long> MarkProtectedCells(Map map, int size)
        {
            HashSet<long> protectedCells = new HashSet<long>();

            foreach (Thing thing in map.things)
            {
                if (thing == null || thing.isDestroyed || thing.pos == null) continue;

                int radius = 0;
                if (thing.trait is TraitStairsUp || thing.trait is TraitStairsDown || thing.trait is TraitStairsLocked)
                    radius = StairProtectRadius;
                else if (thing.trait is TraitDoor)
                    radius = DoorProtectRadius;

                if (radius <= 0) continue;

                int tx = thing.pos.x, tz = thing.pos.z;
                for (int dx = -radius; dx <= radius; dx++)
                {
                    for (int dz = -radius; dz <= radius; dz++)
                    {
                        int nx = tx + dx, nz = tz + dz;
                        if (nx >= 0 && nz >= 0 && nx < size && nz < size)
                            protectedCells.Add(CellKey(nx, nz));
                    }
                }
            }

            return protectedCells;
        }

        // ===================== Room Detection =====================

        private static List<DetectedRoom> DetectRooms(Map map, int size, HashSet<long> protectedCells)
        {
            bool[,] visited = new bool[size, size];
            List<DetectedRoom> rooms = new List<DetectedRoom>();

            for (int x = 1; x < size - 1; x++)
            {
                for (int z = 1; z < size - 1; z++)
                {
                    if (visited[x, z]) continue;
                    if (map.cells[x, z]._block != 0) continue;
                    if (map.cells[x, z].impassable) continue;

                    // Flood-fill to find contiguous floor region
                    HashSet<long> region = new HashSet<long>();
                    Queue<long> queue = new Queue<long>();
                    int minX = x, maxX = x, minZ = z, maxZ = z;

                    long startKey = CellKey(x, z);
                    visited[x, z] = true;
                    queue.Enqueue(startKey);
                    region.Add(startKey);

                    while (queue.Count > 0)
                    {
                        long key = queue.Dequeue();
                        int cx = (int)(key >> 16);
                        int cz = (int)(key & 0xFFFF);

                        int[] dxs = { 0, 0, 1, -1 };
                        int[] dzs = { 1, -1, 0, 0 };
                        for (int d = 0; d < 4; d++)
                        {
                            int nx = cx + dxs[d], nz = cz + dzs[d];
                            if (nx < 0 || nz < 0 || nx >= size || nz >= size) continue;
                            if (visited[nx, nz]) continue;
                            if (map.cells[nx, nz]._block != 0) continue;
                            if (map.cells[nx, nz].impassable) continue;

                            visited[nx, nz] = true;
                            long nkey = CellKey(nx, nz);
                            queue.Enqueue(nkey);
                            region.Add(nkey);

                            if (nx < minX) minX = nx;
                            if (nx > maxX) maxX = nx;
                            if (nz < minZ) minZ = nz;
                            if (nz > maxZ) maxZ = nz;
                        }
                    }

                    int width = maxX - minX + 1;
                    int height = maxZ - minZ + 1;

                    // Skip corridors (narrow regions)
                    if (width <= 2 || height <= 2) continue;

                    rooms.Add(new DetectedRoom
                    {
                        MinX = minX,
                        MinZ = minZ,
                        MaxX = maxX,
                        MaxZ = maxZ,
                        FloorCells = region,
                        CenterX = (minX + maxX) / 2,
                        CenterZ = (minZ + maxZ) / 2,
                    });
                }
            }

            return rooms;
        }

        // ===================== Shape Selection =====================

        private static int GetDepthBracket(int lv)
        {
            int depth = Math.Abs(lv);
            if (depth <= 2) return 0;
            if (depth <= 4) return 1;
            return 2;
        }

        private static ShapeType SelectShape(int depthBracket)
        {
            int totalWeight = 0;
            for (int i = 0; i < AllShapes.Length; i++)
                totalWeight += ShapeWeights[depthBracket, i];

            int roll = EClass.rnd(totalWeight);
            int cumulative = 0;
            for (int i = 0; i < AllShapes.Length; i++)
            {
                cumulative += ShapeWeights[depthBracket, i];
                if (roll < cumulative) return AllShapes[i];
            }

            return ShapeType.Circle; // fallback
        }

        // ===================== Shape Application =====================

        private static HashSet<long> ApplyShape(Map map, DetectedRoom room, ShapeType shape,
            HashSet<long> protectedCells, int size)
        {
            HashSet<long> newWalls = new HashSet<long>();

            int cx = room.CenterX, cz = room.CenterZ;
            int hw = room.Width / 2;
            int hh = room.Height / 2;

            // Sample wall material from adjacent blocks
            byte wallMat = 0, wallBlock = 0;
            FindAdjacentWallMaterial(map, room, size, out wallMat, out wallBlock);

            Func<int, int, bool> predicate = GetShapePredicate(shape, hw, hh);

            foreach (long key in room.FloorCells)
            {
                int x = (int)(key >> 16);
                int z = (int)(key & 0xFFFF);

                if (protectedCells.Contains(key)) continue;

                int dx = x - cx;
                int dz = z - cz;

                if (!predicate(dx, dz))
                {
                    // Convert floor → wall
                    map.SetBlock(x, z, wallMat, wallBlock);
                    newWalls.Add(key);
                }
            }

            SkyreaderGuild.Log($"Rift reshaper: applied {shape} to room at ({room.MinX},{room.MinZ})-({room.MaxX},{room.MaxZ}), carved {newWalls.Count} cells.");
            return newWalls;
        }

        private static Func<int, int, bool> GetShapePredicate(ShapeType shape, int hw, int hh)
        {
            switch (shape)
            {
                case ShapeType.Circle:
                {
                    int r = Math.Min(hw, hh) - 1;
                    int rSq = r * r;
                    return (dx, dz) => dx * dx + dz * dz <= rSq;
                }

                case ShapeType.Ellipse:
                {
                    double a = Math.Max(1, hw - 1);
                    double b = Math.Max(1, hh - 1);
                    double aSq = a * a, bSq = b * b;
                    return (dx, dz) => (dx * dx) / aSq + (dz * dz) / bSq <= 1.0;
                }

                case ShapeType.Diamond:
                {
                    int r = Math.Min(hw, hh) - 1;
                    return (dx, dz) => Math.Abs(dx) + Math.Abs(dz) <= r;
                }

                case ShapeType.Cross:
                {
                    int armW = Math.Max(1, hw / 3);
                    int armH = Math.Max(1, hh / 3);
                    return (dx, dz) => Math.Abs(dx) <= armW || Math.Abs(dz) <= armH;
                }

                case ShapeType.Crescent:
                {
                    int outerR = Math.Min(hw, hh) - 1;
                    int innerR = Math.Max(1, outerR - 1);
                    int off = Math.Max(1, outerR / 2);
                    int outerRSq = outerR * outerR;
                    int innerRSq = innerR * innerR;
                    return (dx, dz) =>
                    {
                        if (dx * dx + dz * dz > outerRSq) return false;
                        int shiftedDx = dx - off;
                        return shiftedDx * shiftedDx + dz * dz > innerRSq;
                    };
                }

                case ShapeType.LShape:
                {
                    // Keep 3 of 4 quadrants to form an L
                    int cutQuadrant = EClass.rnd(4);
                    return (dx, dz) =>
                    {
                        // quadrant 0: +x,+z  1: -x,+z  2: -x,-z  3: +x,-z
                        bool inCut = false;
                        switch (cutQuadrant)
                        {
                            case 0: inCut = dx > 0 && dz > 0; break;
                            case 1: inCut = dx < 0 && dz > 0; break;
                            case 2: inCut = dx < 0 && dz < 0; break;
                            case 3: inCut = dx > 0 && dz < 0; break;
                        }
                        return !inCut;
                    };
                }

                case ShapeType.Star:
                {
                    int r = Math.Min(hw, hh) - 1;
                    return (dx, dz) =>
                    {
                        double dist = Math.Sqrt(dx * dx + dz * dz);
                        double angle = Math.Atan2(dz, dx);
                        double edgeR = r * (0.55 + 0.45 * Math.Cos(5.0 * angle));
                        return dist <= edgeR;
                    };
                }

                case ShapeType.Irregular:
                {
                    // Cellular automata cave: start filled, randomly remove ~40%, then smooth
                    int r = Math.Min(hw, hh) - 1;
                    int rSq = r * r;
                    // Use a simple noise-based approach: circle base with random holes
                    return (dx, dz) =>
                    {
                        if (dx * dx + dz * dz > rSq) return false;
                        // Deterministic pseudo-noise based on position
                        int hash = ((dx * 7919) ^ (dz * 6271)) & 0x7FFFFFFF;
                        return hash % 100 >= 25; // keep 75% of cells within circle
                    };
                }

                default:
                    return (dx, dz) => true; // no-op, keep all floor
            }
        }

        // ===================== Wall Material Detection =====================

        private static void FindAdjacentWallMaterial(Map map, DetectedRoom room, int size,
            out byte wallMat, out byte wallBlock)
        {
            // Sample material from existing wall blocks adjacent to the room
            wallMat = 0;
            wallBlock = 9; // default BlockRock

            for (int x = Math.Max(0, room.MinX - 1); x <= Math.Min(size - 1, room.MaxX + 1); x++)
            {
                for (int z = Math.Max(0, room.MinZ - 1); z <= Math.Min(size - 1, room.MaxZ + 1); z++)
                {
                    Cell cell = map.cells[x, z];
                    if (cell._block != 0)
                    {
                        wallMat = cell._blockMat;
                        wallBlock = cell._block;
                        return;
                    }
                }
            }
        }

        // ===================== Connectivity Verification =====================

        /// <summary>
        /// Verifies that all stairs are reachable from each other via floor cells.
        /// </summary>
        private static bool VerifyStairConnectivity(Map map, int size)
        {
            // Find all stair positions
            List<Point> stairPositions = new List<Point>();
            foreach (Thing thing in map.things)
            {
                if (thing == null || thing.isDestroyed) continue;
                if (thing.trait is TraitStairsUp || thing.trait is TraitStairsDown || thing.trait is TraitStairsLocked)
                {
                    if (thing.pos != null && thing.pos.IsValid)
                        stairPositions.Add(thing.pos.Copy());
                }
            }

            if (stairPositions.Count < 2) return true; // single stair or none, nothing to verify

            // Flood-fill from first stair
            Point start = stairPositions[0];
            bool[,] visited = new bool[size, size];
            Queue<long> queue = new Queue<long>();

            visited[start.x, start.z] = true;
            queue.Enqueue(CellKey(start.x, start.z));

            while (queue.Count > 0)
            {
                long key = queue.Dequeue();
                int cx = (int)(key >> 16);
                int cz = (int)(key & 0xFFFF);

                int[] dxs = { 0, 0, 1, -1 };
                int[] dzs = { 1, -1, 0, 0 };
                for (int d = 0; d < 4; d++)
                {
                    int nx = cx + dxs[d], nz = cz + dzs[d];
                    if (nx < 0 || nz < 0 || nx >= size || nz >= size) continue;
                    if (visited[nx, nz]) continue;
                    if (map.cells[nx, nz]._block != 0) continue;

                    visited[nx, nz] = true;
                    queue.Enqueue(CellKey(nx, nz));
                }
            }

            // Check all other stairs are reachable
            for (int i = 1; i < stairPositions.Count; i++)
            {
                Point p = stairPositions[i];
                if (!visited[p.x, p.z])
                    return false;
            }

            return true;
        }

        // ===================== Cell Backup / Revert =====================

        private static List<CellBackup> BackupRoomCells(Map map, DetectedRoom room)
        {
            List<CellBackup> backups = new List<CellBackup>();
            foreach (long key in room.FloorCells)
            {
                int x = (int)(key >> 16);
                int z = (int)(key & 0xFFFF);
                Cell cell = map.cells[x, z];
                backups.Add(new CellBackup
                {
                    X = x,
                    Z = z,
                    Block = cell._block,
                    BlockMat = cell._blockMat,
                    Floor = cell._floor,
                    FloorMat = cell._floorMat,
                });
            }
            return backups;
        }

        private static void RevertCells(Map map, List<CellBackup> backups)
        {
            foreach (CellBackup b in backups)
            {
                Cell cell = map.cells[b.X, b.Z];
                cell._block = b.Block;
                cell._blockMat = b.BlockMat;
                cell._floor = b.Floor;
                cell._floorMat = b.FloorMat;
            }
        }

        // ===================== Wall Smoothing =====================

        /// <summary>
        /// Single pass of cellular automata (B5678/S45678) on newly created wall cells
        /// and their immediate neighbors. Rounds off jagged 1-tile protrusions.
        /// </summary>
        private static void SmoothNewWalls(Map map, int size, HashSet<long> newWalls)
        {
            // Build candidate set: new walls + their Moore neighbors
            HashSet<long> candidates = new HashSet<long>();
            foreach (long key in newWalls)
            {
                int x = (int)(key >> 16);
                int z = (int)(key & 0xFFFF);
                for (int dx = -1; dx <= 1; dx++)
                {
                    for (int dz = -1; dz <= 1; dz++)
                    {
                        int nx = x + dx, nz = z + dz;
                        if (nx >= 1 && nz >= 1 && nx < size - 1 && nz < size - 1)
                            candidates.Add(CellKey(nx, nz));
                    }
                }
            }

            // Snapshot current block state for candidate cells
            Dictionary<long, bool> isWall = new Dictionary<long, bool>();
            foreach (long key in candidates)
            {
                int x = (int)(key >> 16);
                int z = (int)(key & 0xFFFF);
                isWall[key] = map.cells[x, z]._block != 0;
            }

            // Apply automata rule
            foreach (long key in candidates)
            {
                int x = (int)(key >> 16);
                int z = (int)(key & 0xFFFF);

                // Don't touch cells that were always floor (not new walls or their neighbors that are floor)
                if (!newWalls.Contains(key) && map.cells[x, z]._block == 0)
                    continue;

                int neighbors = 0;
                for (int dx = -1; dx <= 1; dx++)
                {
                    for (int dz = -1; dz <= 1; dz++)
                    {
                        if (dx == 0 && dz == 0) continue;
                        long nkey = CellKey(x + dx, z + dz);
                        if (isWall.TryGetValue(nkey, out bool nw))
                        {
                            if (nw) neighbors++;
                        }
                        else
                        {
                            // Outside candidate set — check map directly
                            if (map.cells[x + dx, z + dz]._block != 0)
                                neighbors++;
                        }
                    }
                }

                // S45678: wall survives if 4+ neighbors
                // B5678: floor becomes wall if 5+ neighbors
                if (map.cells[x, z]._block != 0 && neighbors < 4)
                {
                    // Remove thin wall protrusion — convert back to floor
                    // Use floor material from a neighbor
                    byte floorId = 0, floorMat = 0;
                    for (int dx = -1; dx <= 1 && floorId == 0; dx++)
                    {
                        for (int dz = -1; dz <= 1 && floorId == 0; dz++)
                        {
                            int nx = x + dx, nz = z + dz;
                            if (nx >= 0 && nz >= 0 && nx < size && nz < size)
                            {
                                Cell nc = map.cells[nx, nz];
                                if (nc._block == 0 && nc._floor != 0)
                                {
                                    floorId = nc._floor;
                                    floorMat = nc._floorMat;
                                }
                            }
                        }
                    }
                    if (floorId != 0)
                    {
                        map.cells[x, z]._block = 0;
                        map.cells[x, z]._blockMat = 0;
                        map.cells[x, z]._floor = floorId;
                        map.cells[x, z]._floorMat = floorMat;
                    }
                }
            }
        }

        // ===================== Helpers =====================

        private static long CellKey(int x, int z)
        {
            return ((long)x << 16) | (long)(z & 0xFFFF);
        }
    }
}
