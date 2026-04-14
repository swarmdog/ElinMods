#!/usr/bin/env python3
"""Inspect Elin packaged map files without mutating them."""

from __future__ import annotations

import argparse
import json
import math
import statistics
import subprocess
import zipfile
from collections import Counter
from pathlib import Path
from typing import Any
from uuid import uuid4


REPO_ROOT = Path(__file__).resolve().parents[3]
ELIN_MAP_DIR = Path(r"D:\Steam\steamapps\common\Elin\Package\_Elona\Map")
SKYREADER_MAP_DIR = Path(r"D:\Steam\steamapps\common\Elin\Package\SkyreaderGuild\Map")
REPORT_DIR = REPO_ROOT / "SkyreaderGuild" / "worklog" / "reports"
HELPER_PROJECT = REPO_ROOT / "SkyreaderGuild" / "worklog" / "tools" / "ElinExportDump" / "ElinExportDump.csproj"
HELPER_EXE = HELPER_PROJECT.parent / "bin" / "Release" / "ElinExportDump.exe"

IMPASSABLE_FLAG = 1 << 3
PLACE_STATE_INSTALLED = 2


def split_md_row(line: str) -> list[str]:
    return [cell.strip() for cell in line.strip().strip("|").split("|")]


def read_markdown_table(path: Path) -> tuple[list[str], list[dict[str, str]]]:
    lines = path.read_text(encoding="utf-8").splitlines()
    header_index = next(i for i, line in enumerate(lines) if line.startswith("| id |"))
    headers = split_md_row(lines[header_index])
    rows: list[dict[str, str]] = []

    for line in lines[header_index + 2 :]:
        if not line.startswith("|"):
            break
        cells = split_md_row(line)
        if len(cells) < len(headers):
            cells += [""] * (len(headers) - len(cells))
        rows.append(dict(zip(headers, cells)))

    return headers, rows


def load_source_lookup() -> dict[str, dict[Any, dict[str, Any]]]:
    _, thing_rows = read_markdown_table(REPO_ROOT / "elin_readable_game_data" / "SourceCard_Thing.md")
    _, block_rows = read_markdown_table(REPO_ROOT / "elin_readable_game_data" / "SourceBlock_Block.md")
    _, floor_rows = read_markdown_table(REPO_ROOT / "elin_readable_game_data" / "SourceBlock_Floor.md")
    _, chara_rows = read_markdown_table(REPO_ROOT / "elin_readable_game_data" / "SourceChara_Chara.md")
    _, obj_rows = read_markdown_table(REPO_ROOT / "elin_readable_game_data" / "SourceBlock_Obj.md")

    things: dict[str, dict[str, Any]] = {}
    for row in thing_rows:
        thing_id = row.get("id", "")
        if not thing_id:
            continue
        category = row.get("category", "")
        sort = row.get("sort", "")
        tile_type = row.get("_tileType", "")
        render_data = row.get("_idRenderData", "")
        trait = row.get("trait", "")
        light_data = row.get("lightData", "")
        things[thing_id] = {
            "id": thing_id,
            "name": row.get("name", ""),
            "category": category,
            "sort": sort,
            "tile_type": tile_type,
            "render_data": render_data,
            "trait": trait,
            "light_data": light_data,
            "wall_mounted": (
                category == "mount"
                or sort == "furniture_mount"
                or "WallHang" in tile_type
                or "hang" in render_data
            ),
            "light": category == "light" or sort == "light" or bool(light_data) or "Light" in trait,
        }

    charas: dict[str, dict[str, Any]] = {}
    for row in chara_rows:
        chara_id = row.get("id", "")
        if not chara_id:
            continue
        charas[chara_id] = {
            "id": chara_id,
            "name": row.get("name", ""),
            "category": row.get("job", "") or row.get("race", "") or "chara",
        }

    objs: dict[str, dict[str, Any]] = {}
    for row in obj_rows:
        obj_id = row.get("id", "")
        if not obj_id.isdigit():
            continue
        objs[obj_id] = {
            "id": obj_id,
            "name": row.get("name", "") or row.get("alias", ""),
            "category": "obj",
        }

    return {
        "things": things,
        "charas": charas,
        "objs": objs,
        "objs_by_int": {int(k): v for k, v in objs.items()},
        "blocks": {int(row["id"]): row for row in block_rows if row.get("id", "").isdigit()},
        "floors": {int(row["id"]): row for row in floor_rows if row.get("id", "").isdigit()},
    }


def helper_command() -> list[str]:
    if HELPER_EXE.exists():
        return [str(HELPER_EXE)]

    subprocess.run(
        ["dotnet", "build", str(HELPER_PROJECT), "-c", "Release", "-v", "minimal"],
        cwd=str(REPO_ROOT),
        check=True,
    )
    return [str(HELPER_EXE)]


def decode_export(entry: bytes) -> tuple[dict[str, Any] | None, str | None]:
    stripped = entry.lstrip()
    if stripped.startswith(b"{") or stripped.startswith(b"["):
        try:
            return json.loads(entry.decode("utf-8", errors="replace")), None
        except json.JSONDecodeError as ex:
            return None, f"plain export is not JSON: {ex}"

    try:
        command = helper_command()
    except Exception as ex:
        return None, f"helper build failed: {ex}"

    scratch_root = REPORT_DIR / ".tmp"
    scratch_root.mkdir(parents=True, exist_ok=True)
    export_path = scratch_root / f"export_{uuid4().hex}.bin"
    try:
        export_path.write_bytes(entry)
        result = subprocess.run(
            command + [str(export_path)],
            cwd=str(REPO_ROOT),
            text=True,
            encoding="utf-8",
            errors="replace",
            capture_output=True,
        )
        if result.returncode != 0:
            return None, result.stderr.strip() or f"helper exited {result.returncode}"
        try:
            return json.loads(result.stdout), None
        except json.JSONDecodeError as ex:
            return None, f"decoded export is not JSON: {ex}"
    finally:
        try:
            export_path.unlink(missing_ok=True)
        except OSError:
            pass


def top_counter(counter: Counter[int], source: dict[int, dict[str, str]], limit: int | None = None) -> list[dict[str, Any]]:
    rows: list[dict[str, Any]] = []
    items = counter.most_common(limit) if limit is not None else counter.most_common()
    for value, count in items:
        alias = source.get(value, {}).get("alias", "")
        name = source.get(value, {}).get("name", "")
        rows.append({"id": value, "alias": alias, "name": name, "count": count})
    return rows


def analyze_cards(export_data: dict[str, Any] | None, source: dict[str, dict[Any, dict[str, Any]]]) -> dict[str, Any]:
    cards = []
    if export_data:
        cards = export_data.get("serializedCards", {}).get("cards", []) or []

    thing_ids = source.get("things", {})
    chara_ids = source.get("charas", {})
    obj_ids = source.get("objs", {})

    thing_count = 0
    chara_count = 0
    obj_count = 0
    unknown_count = 0

    installed_count = 0
    wall_mounted_count = 0
    light_count = 0
    by_id: Counter[str] = Counter()

    for card in cards:
        strs = card.get("strs") or []
        ints = card.get("ints") or []
        card_id = strs[0] if strs else ""
        if not card_id:
            continue

        by_id[card_id] += 1
        meta = thing_ids.get(card_id)

        if meta is not None:
            thing_count += 1
            if len(ints) > 8 and ints[8] == PLACE_STATE_INSTALLED:
                installed_count += 1
            if meta["wall_mounted"]:
                wall_mounted_count += 1
            if meta["light"]:
                light_count += 1
        elif card_id in chara_ids:
            chara_count += 1
        elif card_id in obj_ids:
            obj_count += 1
        else:
            unknown_count += 1

    top_cards = []
    for card_id, count in by_id.most_common():
        if card_id in thing_ids:
            name = thing_ids[card_id].get("name", "")
            cat = thing_ids[card_id].get("category", "")
        elif card_id in chara_ids:
            name = chara_ids[card_id].get("name", "")
            cat = f"chara/{chara_ids[card_id].get('category', '')}"
        elif card_id in obj_ids:
            name = obj_ids[card_id].get("name", "")
            cat = "obj"
        else:
            name = ""
            cat = "unknown"

        top_cards.append({
            "id": card_id,
            "count": count,
            "name": name,
            "category": cat,
        })

    return {
        "serialized_cards": len(cards),
        "things": thing_count,
        "charas": chara_count,
        "objs": obj_count,
        "unknowns": unknown_count,
        "installed_things": installed_count,
        "wall_mounted_things": wall_mounted_count,
        "light_things": light_count,
        "top_cards": top_cards,
    }


def analyze_map(path: Path, source: dict[str, dict[Any, dict[str, Any]]]) -> dict[str, Any]:
    result: dict[str, Any] = {
        "name": path.name,
        "path": str(path),
        "exists": path.exists(),
    }
    if not path.exists():
        result["missing_reason"] = "file not found"
        return result

    with zipfile.ZipFile(path) as archive:
        names = set(archive.namelist())
        size = 0
        try:
            if "map" in names:
                map_str = archive.read("map").decode("utf-8-sig")
                map_data = json.loads(map_str)
                size = int(map_data.get("Size") or map_data.get("maxX") or 0)
        except Exception:
            pass
        
        layers: dict[str, bytes] = {}
        for layer in ["floors", "floorMats", "blocks", "blockMats", "objs", "objMats", "flags", "dirs"]:
            layers[layer] = archive.read(layer) if layer in names else b""

        export_data = None
        export_error = None
        if "export" in names:
            export_data, export_error = decode_export(archive.read("export"))
        else:
            export_error = "export entry missing"

    if size <= 0:
        candidates = [len(data) for data in layers.values() if data]
        size = int(math.sqrt(candidates[0])) if candidates else 0

    cell_count = size * size
    floors = layers.get("floors", b"")
    blocks = layers.get("blocks", b"")
    objs = layers.get("objs", b"")
    flags = layers.get("flags", b"")

    open_cells = 0
    impassable_cells = 0
    blocked_cells = 0
    floor_cells = 0
    obj_cells = 0

    for i in range(min(cell_count, len(floors))):
        is_impassable = i < len(flags) and (flags[i] & IMPASSABLE_FLAG) != 0
        has_block = i < len(blocks) and blocks[i] != 0
        has_floor = floors[i] != 0
        has_obj = i < len(objs) and objs[i] != 0
        if is_impassable:
            impassable_cells += 1
        if has_block:
            blocked_cells += 1
        if has_floor:
            floor_cells += 1
        if has_obj:
            obj_cells += 1
        if has_floor and not is_impassable and not has_block:
            open_cells += 1

    door_indices = set()
    if export_data:
        for c in export_data.get("serializedCards", {}).get("cards", []):
            strs = c.get("strs") or []
            if strs:
                card_id = strs[0]
                meta = source.get("things", {}).get(card_id, {})
                if meta.get("category") == "door" or card_id.startswith("door"):
                    ints = c.get("ints") or []
                    if len(ints) > 6:
                        # Assuming x=ints[5], z=ints[6] mapped back to 1D
                        door_indices.add(ints[5] + ints[6] * size)
                        # Also add transpose mapping just in case
                        door_indices.add(ints[5] * size + ints[6])

    visited = set()
    rooms = []
    
    def is_walkable(idx: int) -> bool:
        if idx >= len(blocks): return False
        return blocks[idx] == 0 and (idx < len(floors) and floors[idx] != 0)
        
    for i in range(cell_count):
        if i in visited:
            continue
        if is_walkable(i) and i not in door_indices:
            room_cells = 0
            room_doors = set()
            queue = [i]
            visited.add(i)
            while queue:
                curr = queue.pop(0)
                room_cells += 1
                
                cx = curr % size
                cz = curr // size
                for dx, dz in [(-1,0), (1,0), (0,-1), (0,1)]:
                    nx, nz = cx + dx, cz + dz
                    if 0 <= nx < size and 0 <= nz < size:
                        n = nx + nz * size
                        if n in visited:
                            continue
                        
                        if n in door_indices:
                            room_doors.add(n)
                            visited.add(n) # Mark door as visited so we don't count it from the other side immediately
                        elif is_walkable(n):
                            visited.add(n)
                            queue.append(n)
                            
            if room_cells >= 4:
                rooms.append({"size": room_cells, "doors": len(room_doors)})
    
    rooms.sort(key=lambda r: r["size"], reverse=True)

    card_stats = analyze_cards(export_data, source)
    per_100_open = (card_stats["things"] * 100.0 / open_cells) if open_cells else 0.0
    wall_per_100_open = (card_stats["wall_mounted_things"] * 100.0 / open_cells) if open_cells else 0.0

    result.update(
        {
            "size": size,
            "cells": cell_count,
            "open_cells": open_cells,
            "impassable_cells": impassable_cells,
            "blocked_cells": blocked_cells,
            "floor_cells": floor_cells,
            "obj_cells": obj_cells,
            "things_per_100_open": round(per_100_open, 2),
            "wall_mounts_per_100_open": round(wall_per_100_open, 2),
            "rooms": rooms,
            "export_error": export_error,
            "cards": card_stats,
            "top_floors": top_counter(Counter(floors), source["floors"], limit=None),
            "top_blocks": top_counter(Counter(blocks), source["blocks"], limit=None),
            "top_objs": top_counter(Counter(objs), source["objs_by_int"], limit=None),
        }
    )
    return result


def default_map_paths(map_dir: Path) -> list[Path]:
    names = ["derphy.z", "lumiest.z", "kapul.z", "little_garden.z"]
    names.extend(path.name for path in sorted(map_dir.glob("guild_*.z")))
    paths = [map_dir / name for name in dict.fromkeys(names)]
    paths.append(SKYREADER_MAP_DIR / "srg_guild_hq.z")
    return paths


def median(values: list[float]) -> float:
    return round(statistics.median(values), 2) if values else 0.0


def write_report(results: list[dict[str, Any]], markdown_path: Path, json_path: Path) -> None:
    existing_stock = [
        result
        for result in results
        if result.get("exists") and not result["name"].startswith("srg_")
    ]
    stock_things = [float(result["things_per_100_open"]) for result in existing_stock]
    stock_wall = [float(result["wall_mounts_per_100_open"]) for result in existing_stock]
    thing_threshold = round(median(stock_things) * 0.6, 2)
    wall_threshold = round(median(stock_wall) * 0.6, 2)

    payload = {
        "stock_median_things_per_100_open": median(stock_things),
        "stock_median_wall_mounts_per_100_open": median(stock_wall),
        "observatory_thresholds": {
            "things_per_100_open": thing_threshold,
            "wall_mounts_per_100_open": wall_threshold,
        },
        "maps": results,
    }

    json_path.parent.mkdir(parents=True, exist_ok=True)
    json_path.write_text(json.dumps(payload, indent=2), encoding="utf-8")

    lines = [
        "# Elin Map Density Baseline",
        "",
        "Generated by `SkyreaderGuild/worklog/scripts/inspect_elin_maps.py`.",
        "",
        f"- Stock median Things / 100 open cells: {payload['stock_median_things_per_100_open']}",
        f"- Stock median wall mounts / 100 open cells: {payload['stock_median_wall_mounts_per_100_open']}",
        f"- Observatory warning threshold: < {thing_threshold} Things / 100 open cells or < {wall_threshold} wall mounts / 100 open cells",
        "",
        "| Map | Size | Open Cells | Things | Charas | Objs (cards) | Unknown | Wall Mounts | Lights | Things/100 Open | Wall Mounts/100 Open | Export |",
        "| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | --- |",
    ]

    for result in results:
        if not result.get("exists"):
            lines.append(f"| {result['name']} | missing |  |  |  |  |  |  |  |  |  | {result.get('missing_reason', '')} |")
            continue
        cards = result["cards"]
        export_status = "ok" if not result.get("export_error") else result["export_error"].splitlines()[0]
        lines.append(
            "| {name} | {size} | {open_cells} | {things} | {charas} | {objs} | {unknowns} | {wall} | {lights} | {thing_density} | {wall_density} | {export} |".format(
                name=result["name"],
                size=result["size"],
                open_cells=result["open_cells"],
                things=cards["things"],
                charas=cards["charas"],
                objs=cards["objs"],
                unknowns=cards["unknowns"],
                wall=cards["wall_mounted_things"],
                lights=cards["light_things"],
                thing_density=result["things_per_100_open"],
                wall_density=result["wall_mounts_per_100_open"],
                export=export_status.replace("|", "/"),
            )
        )

    lines.extend(["", "## Top Terrain"])
    for result in results:
        if not result.get("exists"):
            continue
        floors = ", ".join(f"{row['alias'] or row['id']}={row['count']}" for row in result["top_floors"][:5])
        blocks = ", ".join(f"{row['alias'] or row['id']}={row['count']}" for row in result["top_blocks"][:5])
        objs = ", ".join(f"{row['name'] or row['alias'] or row['id']}={row['count']}" for row in result["top_objs"][:8])
        lines.append(f"- `{result['name']}` floors: {floors}; blocks: {blocks}; objs: {objs}")

    markdown_path.parent.mkdir(parents=True, exist_ok=True)
    markdown_path.write_text("\n".join(lines) + "\n", encoding="utf-8")


def write_map_dumps(results: list[dict[str, Any]], dump_dir: Path) -> None:
    dump_dir.mkdir(parents=True, exist_ok=True)
    for result in results:
        if not result.get("exists"):
            continue
        
        name = result["name"]
        lines = [
            f"# Map Dump: {name}",
            "",
            "## Stats",
            f"- Size: {result.get('size')}",
            f"- Open Cells: {result.get('open_cells')}",
            f"- Blocked Cells: {result.get('blocked_cells')}",
            f"- Impassable Cells: {result.get('impassable_cells')}",
            f"- Things / 100 Open: {result.get('things_per_100_open')}",
            "",
        ]

        lines.extend(["## Terrain", "### Floors"])
        rooms = result.get("rooms", [])
        if rooms:
            avg_room_size = sum(r["size"] for r in rooms) / len(rooms)
            lines.extend([
                "", "### Room Topology",
                f"- **Total Distinct Rooms**: {len(rooms)}",
                f"- **Avg Room Size**: {avg_room_size:.1f} cells",
                "- **Top 10 Largest Rooms:**",
            ])
            for i, r in enumerate(rooms[:10]):
                lines.append(f"  - Room {i+1}: Size {r['size']} cells, {r['doors']} bounding doors")
            lines.append("")
            
        for row in result.get("top_floors", []):
            label = row['alias'] or row['id']
            lines.append(f"- {label}: {row['count']}")
        
        lines.extend(["", "### Blocks"])
        for row in result.get("top_blocks", []):
            label = row['alias'] or row['id']
            lines.append(f"- {label}: {row['count']}")
            
        lines.extend(["", "### Objects (objs layer)"])
        for row in result.get("top_objs", []):
            label = row['name'] or row['alias'] or row['id']
            lines.append(f"- {label}: {row['count']}")

        cards = result.get("cards", {}).get("top_cards", [])
        
        lines.extend(["", "## Population (Charas)"])
        for card in [c for c in cards if str(c.get("category", "")).startswith("chara")]:
            lines.append(f"- {card['id']} ({card['name']}): {card['count']} [{card['category']}]")

        lines.extend(["", "## Objects (card layer)"])
        for card in [c for c in cards if c.get("category") == "obj"]:
            lines.append(f"- {card['id']} ({card['name']}): {card['count']}")

        lines.extend(["", "## Features & Furniture (Things)"])
        for card in [c for c in cards if c.get("category") not in ["obj", "unknown"] and not str(c.get("category", "")).startswith("chara")]:
            lines.append(f"- {card['id']} ({card['name']}): {card['count']} [{card['category']}]")

        lines.extend(["", "## Unknown Cards"])
        for card in [c for c in cards if c.get("category") == "unknown"]:
            lines.append(f"- {card['id']}: {card['count']}")

        dump_path = dump_dir / f"{name}_dump.md"
        dump_path.write_text("\n".join(lines) + "\n", encoding="utf-8")


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--map-dir", type=Path, default=ELIN_MAP_DIR)
    parser.add_argument("--all", action="store_true", help="Inspect all .z maps in the map-dir")
    parser.add_argument("--map", dest="maps", action="append", type=Path, help="Additional .z file to inspect.")
    parser.add_argument("--output", type=Path, default=REPORT_DIR / "map_density_baseline.md")
    parser.add_argument("--json-output", type=Path, default=REPORT_DIR / "map_density_baseline.json")
    args = parser.parse_args()

    source = load_source_lookup()
    if args.all:
        paths = list(args.map_dir.glob("*.z"))
        paths.append(SKYREADER_MAP_DIR / "srg_guild_hq.z")
    else:
        paths = default_map_paths(args.map_dir)
    
    if args.maps:
        paths.extend(args.maps)

    seen: set[Path] = set()
    unique_paths: list[Path] = []
    for path in paths:
        full = path if path.is_absolute() else args.map_dir / path
        if full not in seen:
            seen.add(full)
            unique_paths.append(full)

    results = [analyze_map(path, source) for path in unique_paths]
    write_report(results, args.output, args.json_output)
    write_map_dumps(results, REPORT_DIR / "dumps")
    print(f"Wrote {args.output}")
    print(f"Wrote {args.json_output}")
    print(f"Wrote map dumps to {REPORT_DIR / 'dumps'}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
