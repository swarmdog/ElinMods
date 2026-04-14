rooms = {
    "entry": {"pred": lambda x,z: 4<=x<=14 and 20<=z<=28, "cx": 9, "cz": 24},
    "atrium": {"pred": lambda x,z: (x-25)**2+(z-25)**2<=64, "cx": 25, "cz": 25},
    "study": {"pred": lambda x,z: abs(x-12)+abs(z-13)<=5, "cx": 12, "cz": 13},
    "observatory": {"pred": lambda x,z: (x-25)**2+(z-40)**2<=25, "cx": 25, "cz": 40},
    "forge": {"pred": lambda x,z: (x-40)**2+(z-25)**2<=25, "cx": 40, "cz": 25},
    "sanctum": {"pred": lambda x,z: abs(x-25)+abs(z-10)<=5, "cx": 25, "cz": 10},
}

for name, r in rooms.items():
    tiles = []
    cx, cz = r["cx"], r["cz"]
    for x in range(50):
        for z in range(50):
            if r["pred"](x, z):
                dist = abs(x - cx) + abs(z - cz)
                tiles.append((x, z, dist))
    tiles.sort(key=lambda t: t[2])
    # Show tiles at various distances
    print(f"=== {name} center=({cx},{cz}) tiles={len(tiles)} ===")
    for d in range(0, 9):
        at_dist = [(x,z) for x,z,dd in tiles if dd == d]
        if at_dist:
            print(f"  dist={d}: {at_dist}")
