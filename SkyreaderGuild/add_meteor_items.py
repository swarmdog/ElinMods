"""Add meteor item rows to the canonical SourceCard.xlsx Thing sheet.

Elin's NPOI-based importer expects workbook strings through sharedStrings.xml.
openpyxl writes inlineStr cells by default, which Elin imports as empty values.
"""
import openpyxl
import sys
import io
import zipfile
import tempfile
import shutil
import xml.etree.ElementTree as ET
from pathlib import Path

sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8')

SOURCE_CARD = Path('LangMod/EN/SourceCard.xlsx')

NS_MAIN = 'http://schemas.openxmlformats.org/spreadsheetml/2006/main'
NS_REL = 'http://schemas.openxmlformats.org/package/2006/relationships'
NS_CT = 'http://schemas.openxmlformats.org/package/2006/content-types'
NS_DOC_REL = 'http://schemas.openxmlformats.org/officeDocument/2006/relationships'

ET.register_namespace('', NS_MAIN)
ET.register_namespace('r', NS_DOC_REL)


def normalize_shared_strings(path):
    """Convert inlineStr cells to shared strings for Elin's source importer."""
    shared = []
    shared_index = {}

    def intern(text):
        if text not in shared_index:
            shared_index[text] = len(shared)
            shared.append(text)
        return shared_index[text]

    with tempfile.NamedTemporaryFile(delete=False, suffix='.xlsx') as tmp:
        tmp_path = Path(tmp.name)

    try:
        with zipfile.ZipFile(path, 'r') as zin, zipfile.ZipFile(tmp_path, 'w', zipfile.ZIP_DEFLATED) as zout:
            names = set(zin.namelist())
            for info in zin.infolist():
                if info.filename == 'xl/sharedStrings.xml':
                    continue

                data = zin.read(info.filename)
                if info.filename.startswith('xl/worksheets/sheet') and info.filename.endswith('.xml'):
                    root = ET.fromstring(data)
                    for cell in root.iter(f'{{{NS_MAIN}}}c'):
                        if cell.get('t') != 'inlineStr':
                            continue
                        text_parts = []
                        inline = cell.find(f'{{{NS_MAIN}}}is')
                        if inline is not None:
                            for t in inline.iter(f'{{{NS_MAIN}}}t'):
                                text_parts.append(t.text or '')
                        for child in list(cell):
                            cell.remove(child)
                        cell.set('t', 's')
                        v = ET.SubElement(cell, f'{{{NS_MAIN}}}v')
                        v.text = str(intern(''.join(text_parts)))
                    data = ET.tostring(root, encoding='utf-8', xml_declaration=True)

                elif info.filename == '[Content_Types].xml':
                    root = ET.fromstring(data)
                    part_name = '/xl/sharedStrings.xml'
                    exists = any(
                        child.tag == f'{{{NS_CT}}}Override' and child.get('PartName') == part_name
                        for child in root
                    )
                    if not exists:
                        ET.SubElement(
                            root,
                            f'{{{NS_CT}}}Override',
                            {
                                'PartName': part_name,
                                'ContentType': 'application/vnd.openxmlformats-officedocument.spreadsheetml.sharedStrings+xml',
                            },
                        )
                    data = ET.tostring(root, encoding='utf-8', xml_declaration=True)

                elif info.filename == 'xl/_rels/workbook.xml.rels':
                    root = ET.fromstring(data)
                    exists = any(
                        child.tag == f'{{{NS_REL}}}Relationship'
                        and child.get('Type') == f'{NS_DOC_REL}/sharedStrings'
                        for child in root
                    )
                    if not exists:
                        used = {
                            int((child.get('Id') or 'rId0').replace('rId', ''))
                            for child in root
                            if (child.get('Id') or '').startswith('rId')
                        }
                        next_id = 1
                        while next_id in used:
                            next_id += 1
                        ET.SubElement(
                            root,
                            f'{{{NS_REL}}}Relationship',
                            {
                                'Id': f'rId{next_id}',
                                'Type': f'{NS_DOC_REL}/sharedStrings',
                                'Target': 'sharedStrings.xml',
                            },
                        )
                    data = ET.tostring(root, encoding='utf-8', xml_declaration=True)

                zout.writestr(info, data)

            sst = ET.Element(f'{{{NS_MAIN}}}sst', {'count': str(len(shared)), 'uniqueCount': str(len(shared))})
            for text in shared:
                si = ET.SubElement(sst, f'{{{NS_MAIN}}}si')
                t = ET.SubElement(si, f'{{{NS_MAIN}}}t')
                if text != text.strip():
                    t.set('{http://www.w3.org/XML/1998/namespace}space', 'preserve')
                t.text = text
            zout.writestr(
                'xl/sharedStrings.xml',
                ET.tostring(sst, encoding='utf-8', xml_declaration=True),
            )

        shutil.move(tmp_path, path)
    finally:
        if tmp_path.exists():
            tmp_path.unlink()

wb = openpyxl.load_workbook(SOURCE_CARD)
ws = wb['Thing']

# Check existing ids
existing_ids = set()
for row in ws.iter_rows(min_row=2, values_only=False):
    cell_val = row[0].value
    if cell_val:
        existing_ids.add(str(cell_val))

srg_ids = [x for x in existing_ids if x.startswith("srg_")]
print("Existing srg_ ids:", srg_ids)

# Column mapping (1-indexed for openpyxl)
col_map = {
    'id': 1, 'name_JP': 2, 'name': 6, 'category': 9, 'sort': 10,
    '_idRenderData': 13, 'tiles': 14, 'defMat': 25, 'value': 27,
    'LV': 28, 'weight': 32, 'trait': 34, 'detail': 52,
}

sort_value_col = 11

new_items = [
    {
        'id': 'srg_meteor_core',
        'name_JP': 'meteor core',
        'name': 'meteor core',
        'category': 'junk',
        'sort': 'junk',
        '_idRenderData': 'obj_S',
        'tiles': 503,
        'defMat': 'granite',
        'value': 500,
        'LV': 1,
        'weight': 5000,
        'trait': 'MeteorCore',
        'detail': 'A pulsing core of extraterrestrial origin. It thrums with energy.',
    },
    {
        'id': 'srg_meteorite_source',
        'name_JP': 'meteorite source',
        'name': 'meteorite source',
        'category': 'ore',
        'sort': 'resource_ore',
        '_idRenderData': 'obj_S flat',
        'tiles': 530,
        'defMat': 'gold',
        'value': 200,
        'LV': 1,
        'weight': 500,
        'trait': 'ResourceMain',
        'detail': 'A fragment of fallen star, rich with rare minerals.',
    },
    {
        'id': 'srg_debris',
        'name_JP': 'impact debris',
        'name': 'impact debris',
        'category': 'junk',
        'sort': 'junk',
        '_idRenderData': 'obj_S',
        'tiles': 503,
        'defMat': 'granite',
        'value': 10,
        'LV': 1,
        'weight': 2000,
        'detail': 'Scorched fragments from a meteor impact.',
    },
]

added = 0
for item in new_items:
    if item['id'] in existing_ids:
        print("  SKIP:", item['id'], "already exists")
        continue

    new_row = ws.max_row + 1
    for key, col in col_map.items():
        val = item.get(key)
        if val is not None and val != '':
            ws.cell(row=new_row, column=col, value=val)
    added += 1
    print("  ADDED:", item['id'], "at row", new_row)

cleaned = 0
for row in ws.iter_rows(min_row=4, values_only=False):
    id_value = row[0].value
    if not isinstance(id_value, str) or not id_value.startswith('srg_'):
        continue
    sort_value = row[sort_value_col - 1].value
    if sort_value is not None and not isinstance(sort_value, int):
        row[sort_value_col - 1].value = None
        cleaned += 1
        print("  CLEAN:", id_value, "cleared non-numeric sort value", repr(sort_value))

if added > 0:
    wb.save(SOURCE_CARD)
    print("Saved", added, "new items to", SOURCE_CARD)
else:
    if cleaned > 0:
        wb.save(SOURCE_CARD)
        print("Saved cleanup to", SOURCE_CARD)
    print("No items to add")

wb.close()
normalize_shared_strings(SOURCE_CARD)
print("Normalized shared strings in", SOURCE_CARD)
