"""
Validate the Skyreader Guild HQ layout geometry.
Simulates the floor/wall/void generation and checks connectivity.
"""
import numpy as np
from collections import deque

MAP_SIZE = 50

# Room definitions: (id, center_x, center_z, predicate_func)
# Predicate takes (x, z) and returns True if floor
rooms = {
    "entry": {
        "center": (9, 24),
        "pred": lambda x, z: 4 <= x <= 14 and 20 <= z <= 28,
        "rank": 0,
    },
    "atrium": {
        "center": (25, 25),
        "pred": lambda x, z: (x - 25)**2 + (z - 25)**2 <= 64,
        "rank": 0,
    },
    "study": {
        "center": (12, 13),
        "pred": lambda x, z: abs(x - 12) + abs(z - 13) <= 5,
        "rank": 1,
    },
    "observatory": {
        "center": (25, 40),
        "pred": lambda x, z: (x - 25)**2 + (z - 40)**2 <= 25,
        "rank": 2,
    },
    "forge": {
        "center": (40, 25),
        "pred": lambda x, z: (x - 40)**2 + (z - 25)**2 <= 25,
        "rank": 3,
    },
    "sanctum": {
        "center": (25, 10),
        "pred": lambda x, z: abs(x - 25) + abs(z - 10) <= 5,
        "rank": 4,
    },
}

# Corridors: list of (minX, minZ, maxX, maxZ) rectangular segments
corridors = {
    "entry_to_atrium": [(14, 23, 18, 25)],
    "atrium_to_study": [(17, 13, 19, 15), (18, 15, 20, 19)],  # L-shape: east then south
    "atrium_to_observatory": [(24, 33, 26, 36)],
    "atrium_to_forge": [(33, 24, 36, 26)],
    "atrium_to_sanctum": [(24, 15, 26, 18)],
}

# Barriers: tiles that get sealed when rank is too low
barriers = {
    "study": [(15, 13), (15, 14), (15, 15)],
    "observatory": [(24, 34), (25, 34), (26, 34)],
    "forge": [(34, 24), (34, 25), (34, 26)],
    "sanctum": [(24, 16), (25, 16), (26, 16)],
}

# Build floor grid
is_floor = np.zeros((MAP_SIZE, MAP_SIZE), dtype=bool)

# Mark room interiors
for name, room in rooms.items():
    count = 0
    for x in range(MAP_SIZE):
        for z in range(MAP_SIZE):
            if room["pred"](x, z):
                is_floor[x, z] = True
                count += 1
    print(f"Room '{name}': {count} floor tiles, center={room['center']}")

# Mark corridor interiors
for name, segs in corridors.items():
    count = 0
    for (minX, minZ, maxX, maxZ) in segs:
        for x in range(minX, maxX + 1):
            for z in range(minZ, maxZ + 1):
                if 0 <= x < MAP_SIZE and 0 <= z < MAP_SIZE:
                    is_floor[x, z] = True
                    count += 1
    print(f"Corridor '{name}': {count} floor tiles")

total_floor = int(is_floor.sum())
print(f"\nTotal floor tiles: {total_floor}")

# Build wall shell (8-directional, 2 passes)
is_wall = np.zeros((MAP_SIZE, MAP_SIZE), dtype=bool)
offsets_8 = [(-1,-1),(-1,0),(-1,1),(0,-1),(0,1),(1,-1),(1,0),(1,1)]

# Pass 1: inner shell
for x in range(MAP_SIZE):
    for z in range(MAP_SIZE):
        if is_floor[x, z]:
            for dx, dz in offsets_8:
                nx, nz = x + dx, z + dz
                if 0 <= nx < MAP_SIZE and 0 <= nz < MAP_SIZE:
                    if not is_floor[nx, nz]:
                        is_wall[nx, nz] = True

inner_wall = int(is_wall.sum())
print(f"Inner wall shell: {inner_wall} tiles")

# Pass 2: outer shell
is_wall2 = is_wall.copy()
for x in range(MAP_SIZE):
    for z in range(MAP_SIZE):
        if is_wall[x, z]:
            for dx, dz in offsets_8:
                nx, nz = x + dx, z + dz
                if 0 <= nx < MAP_SIZE and 0 <= nz < MAP_SIZE:
                    if not is_floor[nx, nz] and not is_wall[nx, nz]:
                        is_wall2[nx, nz] = True

total_wall = int(is_wall2.sum())
print(f"Total wall (2-pass): {total_wall} tiles")

# Cellular automata smoothing (2 passes)
for pass_num in range(2):
    new_wall = is_wall2.copy()
    for x in range(1, MAP_SIZE - 1):
        for z in range(1, MAP_SIZE - 1):
            if is_floor[x, z]:
                continue  # never touch floor
            neighbors = sum(1 for dx, dz in offsets_8
                          if is_wall2[x+dx, z+dz])
            if is_wall2[x, z] and neighbors < 4:
                new_wall[x, z] = False  # remove thin protrusions
            elif not is_wall2[x, z] and not is_floor[x, z] and neighbors >= 5:
                new_wall[x, z] = True  # fill small gaps
    changed = int((new_wall != is_wall2).sum())
    is_wall2 = new_wall
    print(f"CA smoothing pass {pass_num+1}: {changed} cells changed")

# Connectivity check (flood fill from entry center)
print("\n--- Connectivity Check (all rooms unlocked) ---")
entry_center = rooms["entry"]["center"]
visited = np.zeros((MAP_SIZE, MAP_SIZE), dtype=bool)
queue = deque()
queue.append(entry_center)
visited[entry_center[0], entry_center[1]] = True
fill_count = 0

while queue:
    x, z = queue.popleft()
    fill_count += 1
    for dx, dz in [(-1,0),(1,0),(0,-1),(0,1)]:
        nx, nz = x + dx, z + dz
        if 0 <= nx < MAP_SIZE and 0 <= nz < MAP_SIZE:
            if is_floor[nx, nz] and not visited[nx, nz]:
                visited[nx, nz] = True
                queue.append((nx, nz))

print(f"Flood fill from entry {entry_center}: reached {fill_count}/{total_floor} floor tiles")

for name, room in rooms.items():
    cx, cz = room["center"]
    reachable = visited[cx, cz]
    status = "OK" if reachable else "DISCONNECTED"
    print(f"  {name} center ({cx},{cz}): {status}")

# Check that barrier tiles are ON corridor floor
print("\n--- Barrier Validation ---")
for room_name, tiles in barriers.items():
    for (bx, bz) in tiles:
        on_floor = is_floor[bx, bz]
        status = "OK (on floor)" if on_floor else "ERROR (not on floor!)"
        print(f"  {room_name} barrier ({bx},{bz}): {status}")

# Check embark point
embark = (9, 24)
print(f"\nEmbark point {embark}: floor={is_floor[embark[0], embark[1]]}")

# Verify room centers are on floor
print("\n--- Room Center Validation ---")
for name, room in rooms.items():
    cx, cz = room["center"]
    on_floor = is_floor[cx, cz]
    print(f"  {name} ({cx},{cz}): {'OK' if on_floor else 'NOT ON FLOOR'}")

# Print ASCII map
print("\n--- ASCII Map (X horizontal, Z vertical, top=high Z) ---")
for z in range(MAP_SIZE - 1, -1, -1):
    row = ""
    for x in range(MAP_SIZE):
        if (x, z) == embark:
            row += "P"
        elif any((x, z) == room["center"] for room in rooms.values()):
            row += "C"
        elif any((x, z) in tiles for tiles in barriers.values()):
            if is_floor[x, z]:
                row += "B"
            else:
                row += "b"
        elif is_floor[x, z]:
            row += "."
        elif is_wall2[x, z]:
            row += "#"
        else:
            row += " "
    print(row)
