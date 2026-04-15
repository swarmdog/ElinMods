"""Build importer-safe source sheets and final assets for ElinUnderworldSimulator.

Commands:
    python uw_asset_pipeline.py generate [--only ids...] [--model nano-banana-pro-preview]
    python uw_asset_pipeline.py integrate [--only ids...]
    python uw_asset_pipeline.py verify
    python uw_asset_pipeline.py all [--only ids...] [--model nano-banana-pro-preview]
"""

from __future__ import annotations

import argparse
from copy import copy
import os
import shutil
import tempfile
import zipfile
import xml.etree.ElementTree as ET

from openpyxl import Workbook, load_workbook
from PIL import Image, ImageDraw

from generate_assets import DEFAULT_MODEL, run_generation
from integrate_assets import integrate_assets
from uw_asset_specs import (
    CHARA_COLUMNS,
    CHARAS,
    PREVIEW_OUT,
    REPO_ROOT,
    SOURCE_CARD_OUT,
    TEMPLATE_SOURCE_CARD,
    TEXTURE_DIR,
    THING_COLUMNS,
    THINGS,
)

TEMPLATE_SOURCE_CHARA = REPO_ROOT / "elin_readable_game_data" / "xlsx format" / "SourceChara.xlsx"

NS_MAIN = "http://schemas.openxmlformats.org/spreadsheetml/2006/main"
NS_REL = "http://schemas.openxmlformats.org/package/2006/relationships"
NS_CT = "http://schemas.openxmlformats.org/package/2006/content-types"
NS_DOC_REL = "http://schemas.openxmlformats.org/officeDocument/2006/relationships"

ET.register_namespace("", NS_MAIN)
ET.register_namespace("r", NS_DOC_REL)


def ensure_dirs() -> None:
    SOURCE_CARD_OUT.parent.mkdir(parents=True, exist_ok=True)
    TEXTURE_DIR.mkdir(parents=True, exist_ok=True)


def copy_template_rows(src_sheet, dst_sheet, row_count: int, col_count: int) -> None:
    for row_idx in range(1, row_count + 1):
        dst_sheet.row_dimensions[row_idx].height = src_sheet.row_dimensions[row_idx].height
        for col_idx in range(1, col_count + 1):
            src_cell = src_sheet.cell(row=row_idx, column=col_idx)
            dst_cell = dst_sheet.cell(row=row_idx, column=col_idx)
            dst_cell.value = src_cell.value
            if src_cell.has_style:
                dst_cell._style = copy(src_cell._style)
            if src_cell.number_format:
                dst_cell.number_format = src_cell.number_format
            if src_cell.font:
                dst_cell.font = copy(src_cell.font)
            if src_cell.fill:
                dst_cell.fill = copy(src_cell.fill)
            if src_cell.border:
                dst_cell.border = copy(src_cell.border)
            if src_cell.alignment:
                dst_cell.alignment = copy(src_cell.alignment)
            if src_cell.protection:
                dst_cell.protection = copy(src_cell.protection)

    for col_letter, dim in src_sheet.column_dimensions.items():
        dst_sheet.column_dimensions[col_letter].width = dim.width
        dst_sheet.column_dimensions[col_letter].hidden = dim.hidden

    for merged in src_sheet.merged_cells.ranges:
        dst_sheet.merge_cells(str(merged))

    dst_sheet.freeze_panes = src_sheet.freeze_panes
    dst_sheet.sheet_view.zoomScale = src_sheet.sheet_view.zoomScale


def write_rows(sheet, columns: list[str], rows: list[dict[str, object]]) -> None:
    for row_offset, data in enumerate(rows, start=4):
        for col_idx, column_name in enumerate(columns, start=1):
            if column_name not in data:
                continue
            value = data[column_name]
            if value is None or value == "":
                continue
            sheet.cell(row=row_offset, column=col_idx).value = value


def read_shared_strings(zin: zipfile.ZipFile) -> list[str]:
    if "xl/sharedStrings.xml" not in zin.namelist():
        return []
    root = ET.fromstring(zin.read("xl/sharedStrings.xml"))
    values: list[str] = []
    for si in root.findall(f"{{{NS_MAIN}}}si"):
        values.append("".join(text_node.text or "" for text_node in si.iter(f"{{{NS_MAIN}}}t")))
    return values


def normalize_shared_strings(path) -> None:
    shared: list[str] = []
    shared_index: dict[str, int] = {}

    def intern(text: str) -> int:
        if text not in shared_index:
            shared_index[text] = len(shared)
            shared.append(text)
        return shared_index[text]

    with tempfile.NamedTemporaryFile(delete=False, suffix=".xlsx") as tmp:
        tmp_path = tmp.name

    try:
        with zipfile.ZipFile(path, "r") as zin:
            old_shared = read_shared_strings(zin)
            with zipfile.ZipFile(tmp_path, "w", zipfile.ZIP_DEFLATED) as zout:
                for info in zin.infolist():
                    if info.filename == "xl/sharedStrings.xml":
                        continue

                    data = zin.read(info.filename)
                    if info.filename.startswith("xl/worksheets/sheet") and info.filename.endswith(".xml"):
                        root = ET.fromstring(data)
                        for cell in root.iter(f"{{{NS_MAIN}}}c"):
                            cell_type = cell.get("t")
                            if cell_type not in ("inlineStr", "s", "str"):
                                continue

                            text = ""
                            if cell_type == "inlineStr":
                                inline = cell.find(f"{{{NS_MAIN}}}is")
                                if inline is not None:
                                    text = "".join(node.text or "" for node in inline.iter(f"{{{NS_MAIN}}}t"))
                            else:
                                value_node = cell.find(f"{{{NS_MAIN}}}v")
                                if value_node is not None:
                                    if cell_type == "s":
                                        idx = int(value_node.text or "0")
                                        text = old_shared[idx] if 0 <= idx < len(old_shared) else ""
                                    else:
                                        text = value_node.text or ""

                            for child in list(cell):
                                cell.remove(child)
                            cell.set("t", "s")
                            value_node = ET.SubElement(cell, f"{{{NS_MAIN}}}v")
                            value_node.text = str(intern(text))
                        data = ET.tostring(root, encoding="utf-8", xml_declaration=True)

                    elif info.filename == "[Content_Types].xml":
                        root = ET.fromstring(data)
                        part_name = "/xl/sharedStrings.xml"
                        exists = any(
                            child.tag == f"{{{NS_CT}}}Override" and child.get("PartName") == part_name
                            for child in root
                        )
                        if not exists:
                            ET.SubElement(
                                root,
                                f"{{{NS_CT}}}Override",
                                {
                                    "PartName": part_name,
                                    "ContentType": (
                                        "application/vnd.openxmlformats-officedocument."
                                        "spreadsheetml.sharedStrings+xml"
                                    ),
                                },
                            )
                        data = ET.tostring(root, encoding="utf-8", xml_declaration=True)

                    elif info.filename == "xl/_rels/workbook.xml.rels":
                        root = ET.fromstring(data)
                        exists = any(
                            child.tag == f"{{{NS_REL}}}Relationship"
                            and child.get("Type") == f"{NS_DOC_REL}/sharedStrings"
                            for child in root
                        )
                        if not exists:
                            used_ids = {
                                int((child.get("Id") or "rId0").replace("rId", ""))
                                for child in root
                                if (child.get("Id") or "").startswith("rId")
                            }
                            next_id = 1
                            while next_id in used_ids:
                                next_id += 1
                            ET.SubElement(
                                root,
                                f"{{{NS_REL}}}Relationship",
                                {
                                    "Id": f"rId{next_id}",
                                    "Type": f"{NS_DOC_REL}/sharedStrings",
                                    "Target": "sharedStrings.xml",
                                },
                            )
                        data = ET.tostring(root, encoding="utf-8", xml_declaration=True)

                    zout.writestr(info, data)

                sst = ET.Element(
                    f"{{{NS_MAIN}}}sst",
                    {"count": str(len(shared)), "uniqueCount": str(len(shared))},
                )
                for text in shared:
                    si = ET.SubElement(sst, f"{{{NS_MAIN}}}si")
                    t = ET.SubElement(si, f"{{{NS_MAIN}}}t")
                    if text != text.strip():
                        t.set("{http://www.w3.org/XML/1998/namespace}space", "preserve")
                    t.text = text
                zout.writestr(
                    "xl/sharedStrings.xml",
                    ET.tostring(sst, encoding="utf-8", xml_declaration=True),
                )

        shutil.move(tmp_path, path)
    finally:
        if tmp_path and os.path.exists(tmp_path):
            os.unlink(tmp_path)


def build_workbook() -> None:
    ensure_dirs()
    thing_template_wb = load_workbook(TEMPLATE_SOURCE_CARD)
    chara_template_wb = load_workbook(TEMPLATE_SOURCE_CHARA)

    workbook = Workbook()
    workbook.remove(workbook.active)

    thing_sheet = workbook.create_sheet("Thing")
    chara_sheet = workbook.create_sheet("Chara")

    copy_template_rows(thing_template_wb["Thing"], thing_sheet, row_count=3, col_count=len(THING_COLUMNS))
    copy_template_rows(chara_template_wb["Chara"], chara_sheet, row_count=3, col_count=len(CHARA_COLUMNS))
    write_rows(thing_sheet, THING_COLUMNS, THINGS)
    write_rows(chara_sheet, CHARA_COLUMNS, CHARAS)

    workbook.save(SOURCE_CARD_OUT)
    normalize_shared_strings(SOURCE_CARD_OUT)


def build_preview() -> None:
    ensure_dirs()
    card_ids = [
        "uw_mixing_table",
        "uw_contraband_chest",
        "uw_dealers_ledger",
        "uw_antidote_vial",
        "uw_whispervine",
        "uw_dreamblossom",
        "uw_shadowcap",
        "uw_shadow_elixir",
        "uw_fixer",
    ]

    tiles = []
    for asset_id in card_ids:
        image = Image.open(TEXTURE_DIR / f"{asset_id}.png").convert("RGBA")
        tile = Image.new("RGBA", (180, 120), (18, 22, 28, 255))
        image.thumbnail((96, 96), Image.Resampling.NEAREST)
        tile.alpha_composite(image, ((tile.width - image.width) // 2, 8))
        draw = ImageDraw.Draw(tile)
        draw.rounded_rectangle((0, 0, tile.width - 1, tile.height - 1), radius=12, outline=(90, 190, 150, 255), width=2)
        draw.text((12, 97), asset_id.replace("uw_", "").replace("_", " "), fill=(234, 241, 228, 255))
        tiles.append(tile)

    preview = Image.new("RGB", (960, 640), (12, 16, 22))
    draw = ImageDraw.Draw(preview)
    draw.text((36, 28), "Elin Underworld Simulator", fill=(237, 242, 232))
    draw.text((36, 62), "Local-first dealing loop MVP", fill=(130, 208, 176))
    for idx, tile in enumerate(tiles):
        row = idx // 3
        col = idx % 3
        preview.paste(tile.convert("RGB"), (36 + col * 204, 118 + row * 150))
    preview.save(PREVIEW_OUT, quality=92)


def read_sheet_rows(path, sheet_name: str, rows: int = 3, cols: int | None = None) -> list[list[object]]:
    wb = load_workbook(path, data_only=False, read_only=True)
    ws = wb[sheet_name]
    cols = cols or ws.max_column
    return [
        [ws.cell(row=row_idx, column=col_idx).value for col_idx in range(1, cols + 1)]
        for row_idx in range(1, rows + 1)
    ]


def workbook_sheet_xml_entries(path) -> list[str]:
    with zipfile.ZipFile(path, "r") as archive:
        return sorted(
            name for name in archive.namelist()
            if name.startswith("xl/worksheets/") and name.endswith(".xml")
        )


def assert_no_inline_strings() -> None:
    with zipfile.ZipFile(SOURCE_CARD_OUT, "r") as archive:
        if "xl/sharedStrings.xml" not in archive.namelist():
            raise ValueError("Workbook is missing xl/sharedStrings.xml")
        for entry_name in workbook_sheet_xml_entries(SOURCE_CARD_OUT):
            text = archive.read(entry_name).decode("utf-8")
            if 't="inlineStr"' in text or "<is>" in text:
                raise ValueError(f"Workbook still contains inline strings in {entry_name}")


def assert_template_rows_preserved() -> None:
    generated_thing = read_sheet_rows(SOURCE_CARD_OUT, "Thing", rows=3, cols=len(THING_COLUMNS))
    template_thing = read_sheet_rows(TEMPLATE_SOURCE_CARD, "Thing", rows=3, cols=len(THING_COLUMNS))
    if generated_thing != template_thing:
        raise ValueError("Thing sheet rows 1-3 do not match the SourceCard template.")

    generated_chara = read_sheet_rows(SOURCE_CARD_OUT, "Chara", rows=3, cols=len(CHARA_COLUMNS))
    template_chara = read_sheet_rows(TEMPLATE_SOURCE_CHARA, "Chara", rows=3, cols=len(CHARA_COLUMNS))
    if generated_chara != template_chara:
        raise ValueError("Chara sheet rows 1-3 do not match the SourceChara template.")


def assert_thing_sort_numeric() -> None:
    wb = load_workbook(SOURCE_CARD_OUT, data_only=False, read_only=True)
    ws = wb["Thing"]
    sort_value_col = THING_COLUMNS.index("sort_value") + 1
    for row_idx in range(4, ws.max_row + 1):
        value = ws.cell(row=row_idx, column=sort_value_col).value
        if value in (None, ""):
            continue
        if not isinstance(value, (int, float)):
            raise ValueError(f"Thing row {row_idx} sort_value must be numeric or blank, got {value!r}")


def assert_id_texture_parity() -> None:
    expected = {row["id"] for row in THINGS} | {row["id"] for row in CHARAS}
    actual = {path.stem for path in TEXTURE_DIR.glob("*.png")}
    missing = sorted(expected - actual)
    extras = sorted(actual - expected)
    if missing:
        raise ValueError(f"Missing textures: {', '.join(missing)}")
    if extras:
        raise ValueError(f"Unexpected textures: {', '.join(extras)}")


def verify() -> None:
    if not SOURCE_CARD_OUT.exists():
        raise FileNotFoundError(f"Missing workbook: {SOURCE_CARD_OUT}")
    if not PREVIEW_OUT.exists():
        raise FileNotFoundError(f"Missing preview: {PREVIEW_OUT}")
    assert_no_inline_strings()
    assert_template_rows_preserved()
    assert_thing_sort_numeric()
    assert_id_texture_parity()


def run_integration(only_ids: list[str] | None = None) -> None:
    integrate_assets(only_ids=only_ids)
    build_workbook()
    build_preview()
    verify()


def main() -> None:
    parser = argparse.ArgumentParser(description="Underworld asset/source-sheet pipeline")
    parser.add_argument("command", choices=("generate", "integrate", "verify", "all"), nargs="?", default="all")
    parser.add_argument("--only", nargs="+", help="Limit to specific asset ids")
    parser.add_argument("--model", default=DEFAULT_MODEL, help=f"Gemini image model (default: {DEFAULT_MODEL})")
    args = parser.parse_args()

    if args.command == "generate":
        run_generation(only_ids=args.only, model=args.model)
    elif args.command == "integrate":
        run_integration(only_ids=args.only)
    elif args.command == "verify":
        verify()
    else:
        run_generation(only_ids=args.only, model=args.model)
        run_integration(only_ids=args.only)

    print("Underworld asset pipeline completed successfully.")


if __name__ == "__main__":
    main()
