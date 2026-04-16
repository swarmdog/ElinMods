from __future__ import annotations

from dataclasses import dataclass
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
REPO_ROOT = ROOT.parents[0]
LANG_DIR = ROOT / "LangMod" / "EN"
TEXTURE_DIR = ROOT / "Texture"
PREVIEW_OUT = ROOT / "preview.jpg"

TEMPLATE_SOURCE_CARD = REPO_ROOT / "elin_readable_game_data" / "xlsx format" / "SourceCard.xlsx"
TEMPLATE_SOURCE_CHARA = REPO_ROOT / "elin_readable_game_data" / "xlsx format" / "SourceChara.xlsx"
TEMPLATE_SOURCE_BLOCK = REPO_ROOT / "elin_readable_game_data" / "xlsx format" / "SourceBlock.xlsx"
TEMPLATE_SOURCE_GAME = REPO_ROOT / "elin_readable_game_data" / "xlsx format" / "SourceGame.xlsx"

SOURCE_CARD_OUT = LANG_DIR / "SourceCard.xlsx"
SOURCE_BLOCK_OUT = LANG_DIR / "SourceBlock.xlsx"
SOURCE_GAME_OUT = LANG_DIR / "SourceGame.xlsx"

THING_COLUMNS = [
    "id", "name_JP", "unknown_JP", "unit_JP", "naming", "name", "unit", "unknown",
    "category", "sort_text", "sort_value", "_tileType", "_idRenderData", "tiles", "altTiles",
    "anime", "skins", "size", "colorMod", "colorType", "recipeKey", "factory", "components",
    "disassemble", "defMat", "tierGroup", "value", "LV", "chance", "quality", "HP", "weight",
    "electricity", "trait", "elements", "range", "attackType", "offense", "substats", "defense",
    "lightData", "idExtra", "idToggleExtra", "idActorEx", "idSound", "tag", "workTag", "filter",
    "roomName_JP", "roomName", "detail_JP", "detail",
]

CHARA_COLUMNS = [
    "id", "_id", "name_JP", "name", "aka_JP", "aka", "idActor", "sort", "size",
    "_idRenderData", "tiles", "tiles_snow", "colorMod", "components", "defMat", "LV", "chance",
    "quality", "hostility", "biome", "tag", "trait", "race", "job", "tactics", "aiIdle", "aiParam",
    "actCombat", "mainElement", "elements", "equip", "loot", "category", "filter", "gachaFilter",
    "tone", "actIdle", "lightData", "idExtra", "bio", "faith", "works", "hobbies", "idText",
    "moveAnime", "factory", "components_2", "recruitItems", "detail_JP", "detail",
]

OBJ_COLUMNS = [
    "id", "alias", "name_JP", "name", "_growth", "costSoil", "objType", "vals", "tag", "sort",
    "reqHarvest", "hp", "_tileType", "valType", "_idRenderData", "tiles", "snowTile", "colorMod",
    "colorType", "value", "LV", "chance", "recipeKey", "factory", "components", "defMat",
    "matCategory", "category", "idRoof", "detail_JP", "detail",
]

ELEMENT_COLUMNS = [
    "id", "alias", "name_JP", "name", "altname_JP", "altname", "aliasParent", "aliasRef",
    "aliasMtp", "parentFactor", "lvFactor", "encFactor", "encSlot", "mtp", "LV", "chance",
    "value", "cost", "geneSlot", "sort", "target", "proc", "type", "group", "category",
    "categorySub", "abilityType", "tag", "thing", "eleP", "cooldown", "charge", "radius", "max",
    "req", "idTrainer", "partySkill", "tagTrainer", "levelBonus_JP", "levelBonus", "foodEffect",
    "note", "langAct", "detail_JP", "detail", "textPhase_JP", "textPhase", "textExtra_JP",
    "textExtra", "textInc_JP", "textInc", "textDec_JP", "textDec", "textAlt_JP", "textAlt",
    "adjective_JP", "adjective",
]

STAT_COLUMNS = [
    "id", "alias", "name_JP", "name", "type", "group", "curse", "duration", "hexPower", "negate",
    "defenseAttb", "resistance", "gainRes", "elements", "nullify", "tag", "phase", "colors",
    "element", "effect", "strPhase_JP", "strPhase", "textPhase_JP", "textPhase", "textEnd_JP",
    "textEnd", "textPhase2_JP", "textPhase2", "gradient", "invert", "detail_JP", "detail",
]

NUMERIC_FIELDS = {
    "Thing": {"tiles", "sort_value", "value", "LV", "chance", "quality", "HP", "weight"},
    "Chara": {"_id", "tiles", "LV", "chance", "quality", "sort", "size"},
    "Obj": {"id", "costSoil", "vals", "sort", "hp", "value", "LV", "chance"},
    "Element": {"id", "parentFactor", "lvFactor", "encFactor", "mtp", "LV", "chance", "value", "sort", "eleP", "cooldown", "charge", "radius", "max", "partySkill"},
    "Stat": {"id", "hexPower"},
}

QUALITY_ALIASES = {
    "potency": 90020,
    "dream": 90021,
    "void": 90022,
    "toxicity": 90023,
    "traceability": 90024,
    "vat_started_raw": 90025,
}

UNDERWORLD_BOTTLED_CATEGORY = "_drink"
UNDERWORLD_BOTTLED_SORT = "drink"


def quality_elements(potency: int, toxicity: int, traceability: int, *extra: str) -> str:
    parts = [
        f"{QUALITY_ALIASES['potency']}/{potency}",
        f"{QUALITY_ALIASES['toxicity']}/{toxicity}",
        f"{QUALITY_ALIASES['traceability']}/{traceability}",
    ]
    parts.extend(extra)
    return ",".join(parts)


def thing_base(item_id: str, name_jp: str, name: str, category: str, *, unit_jp: str | None = None,
               sort_text: str | None = None, render: str = "@obj_S flat", tiles: int = 503,
               def_mat: str = "paper", value: int = 100, lv: int = 1, weight: int = 100,
               trait: str | None = None, factory: str | None = None, components: str | None = None,
               recipe_key: str | None = "*", tag: str | None = "noShop,noWish",
               detail_jp: str | None = None, detail: str | None = None, elements: str | None = None,
               tile_type: str | None = None, hp: int | None = None, quality: int | None = None,
               room_name_jp: str | None = None, room_name: str | None = None,
               lightData: str | None = None) -> dict[str, object]:
    return {
        "id": item_id,
        "name_JP": name_jp,
        "unit_JP": unit_jp,
        "name": name,
        "category": category,
        "sort_text": sort_text,
        "_tileType": tile_type,
        "_idRenderData": render,
        "tiles": tiles,
        "recipeKey": recipe_key,
        "factory": factory,
        "components": components,
        "defMat": def_mat,
        "value": value,
        "LV": lv,
        "chance": 0,
        "quality": quality,
        "HP": hp,
        "weight": weight,
        "trait": trait,
        "elements": elements,
        "tag": tag,
        "roomName_JP": room_name_jp,
        "roomName": room_name,
        "lightData": lightData,
        "detail_JP": detail_jp,
        "detail": detail,
    }


THING_ROWS = [
    thing_base(
        "uw_mixing_table", "調合机", "alchemist's vice", "crafter",
        sort_text="furniture", render="@obj", tile_type="ObjBig", tiles=724, def_mat="oak",
        value=3000, lv=15, weight=15000, hp=7000, trait="MixingTable,handicraft",
        factory="workbench", components="log/4,ingot/4,glass/2", room_name_jp="密室,作業場",
        room_name="Back Room,Workroom",
        detail_jp="密造のための蒸留器と薬瓶を備えた作業台。",
        detail="A clandestine workstation fitted with burners, vials, and improvised distillation gear.",
    ),
    thing_base(
        "uw_processing_vat", "熟成樽", "fermentation cask", "crafter",
        sort_text="furniture", render="@obj", tile_type="ObjBig", tiles=724, def_mat="oak",
        value=5000, lv=20, weight=20000, hp=9000, trait="ProcessingVat",
        factory="uw_mixing_table", components="uw_mineral_crude/3,plank/6,ingot/2",
        room_name_jp="密室,作業場", room_name="Back Room,Workroom",
        detail_jp="密閉された樽。時間をかけて危ない品を熟成させる。",
        detail="A sealed barrel with copper fittings for patient, dangerous maturation.",
    ),
    thing_base(
        "uw_advanced_lab", "影の研究室", "shadow laboratory", "crafter",
        sort_text="furniture", render="@obj", tile_type="ObjBig", tiles=724, def_mat="glass",
        value=12000, lv=28, weight=25000, hp=12000, trait="AdvancedLab,handicraft",
        factory="uw_mixing_table", components="uw_crystal_void/2,glass/8,ingot/6,plank/4",
        room_name_jp="密室,研究室", room_name="Back Room,Lab",
        detail_jp="黒いガラスと金具で組まれた高度な錬金装置。",
        detail="A sophisticated alchemical apparatus of dark glass and polished metal.",
    ),
    thing_base(
        "uw_contraband_chest", "隠し木箱", "dead drop crate", "container",
        sort_text="container", render="@obj", tiles=1400, def_mat="oak",
        value=800, lv=8, weight=8000, hp=5000, trait="ContrabandChest,3,3,crate",
        factory="uw_mixing_table", components="plank/4,bolt/2",
        detail_jp="底板の下にもう一段の空間がある木箱。",
        detail="A nondescript wooden crate with a false bottom.",
    ),
    thing_base(
        "uw_dealers_ledger", "売人台帳", "dealer's ledger", "book",
        unit_jp="冊", sort_text="book", tiles=1712, def_mat="paper",
        value=200, lv=3, weight=300, trait="DealerLedger", recipe_key=None, tag="noShop,noWish",
        detail_jp="注文、顧客、危険信号を記した暗号化ノート。",
        detail="A battered notebook filled with coded names, quantities, and delivery schedules.",
    ),
    thing_base(
        "uw_sample_kit", "見本キット", "sample kit", "container",
        sort_text="container", tiles=1708, def_mat="hide",
        value=150, lv=5, weight=500, trait="SampleKit,1,3,pouch",
        factory="uw_mixing_table", components="skin/2,bolt/1",
        detail_jp="小袋と隠し区画付きの携行キット。",
        detail="A concealed pouch with hidden compartments for discreet samples.",
    ),
    thing_base(
        "uw_antidote_vial", "錬金術師の猶予", "alchemist's reprieve", UNDERWORLD_BOTTLED_CATEGORY,
        unit_jp="本", sort_text=UNDERWORLD_BOTTLED_SORT, tiles=1715, def_mat="glass",
        value=400, lv=15, weight=150, trait="AntidoteVial",
        factory="uw_mixing_table", components="uw_herb_whisper/2,uw_herb_shadow/1,potion_empty/1",
        detail_jp="悪い巡りを断ち切る緊急用の対抗薬。",
        detail="An emergency counteragent for someone slipping into a bad spiral.",
    ),
    thing_base("uw_herb_whisper", "囁き草", "whispervine", "herb", unit_jp="枚", sort_text="resource", tiles=2033, def_mat="grass", value=30, lv=4, weight=100, trait="ResourceMain", detail_jp="乾くと甘い香りを放つ細葉の草。", detail="A fine-bladed herb that dries into a sweet, lingering scent."),
    thing_base("uw_herb_dream", "夢見花", "dreamblossom", "herb", unit_jp="輪", sort_text="resource", tiles=2033, def_mat="grass", value=50, lv=6, weight=80, trait="ResourceMain", detail_jp="夜にだけ香り立つ柔らかな花。", detail="Soft petals that truly wake up after dark."),
    thing_base("uw_herb_shadow", "影笠茸", "shadowcap", "mushroom", unit_jp="本", sort_text="resource", tiles=725, def_mat="grass", value=40, lv=8, weight=120, trait="ResourceMain", detail_jp="熱を入れると青い汗をかく茸。", detail="A dark mushroom that beads blue when heat touches it."),
    thing_base("uw_herb_crimson", "深紅草", "crimsonwort", "herb", unit_jp="束", sort_text="resource", tiles=2033, def_mat="grass", value=60, lv=12, weight=90, trait="ResourceMain", detail_jp="熱い土地で育つ赤い薬草。", detail="An exotic herb with a fierce red sap."),
    thing_base("uw_herb_frostbloom", "霜花", "frostbloom", "herb", unit_jp="輪", sort_text="resource", tiles=2033, def_mat="grass", value=45, lv=12, weight=70, trait="ResourceMain", detail_jp="凍気を宿した白い花。", detail="A rare flower that carries a deep restorative chill."),
    thing_base("uw_herb_ashveil", "灰帳苔", "ashveil moss", "herb", unit_jp="束", sort_text="resource", tiles=2033, def_mat="grass", value=45, lv=12, weight=70, trait="ResourceMain", detail_jp="煙のようにほどける灰色の苔。", detail="A grey moss that burns into a revealing smoke."),
    thing_base("uw_mineral_crude", "粗月鉱", "crude moonite", "ore", sort_text="resource", tiles=530, def_mat="mica", value=20, lv=10, weight=500, trait="ResourceMain", detail_jp="光を吸うように鈍く光る未精製鉱石。", detail="A raw ore that seems to dim the light around it."),
    thing_base("uw_mineral_crystal", "虚石片", "voidstone shard", "ore", sort_text="resource", tiles=530, def_mat="obsidian", value=80, lv=18, weight=300, trait="ResourceMain", detail_jp="深層でしか見つからない虚ろな結晶片。", detail="A rare shard from the deepest and coldest places."),
    thing_base("uw_extract_whisper", "囁き抽出液", "whispervine extract", "_item", unit_jp="滴", sort_text="resource", tiles=512, def_mat="glass", value=120, lv=8, weight=150, factory="uw_mixing_table", components="uw_herb_whisper/3,potion_empty/1", elements=quality_elements(22, 8, 4), detail_jp="舌に乗せると耳元で風が鳴る。", detail="Set a drop on the tongue and the air starts talking back."),
    thing_base("uw_extract_dream", "夢見精", "dreamblossom essence", "_item", unit_jp="滴", sort_text="resource", tiles=512, def_mat="glass", value=200, lv=10, weight=150, factory="uw_mixing_table", components="uw_herb_dream/3,potion_empty/1", elements=quality_elements(28, 10, 5), detail_jp="甘い眠気をまとった花の精。", detail="An essence that leaves a pastel haze at the edge of thought."),
    thing_base("uw_extract_shadow", "影蒸留液", "shadowcap distillate", "_item", unit_jp="滴", sort_text="resource", tiles=512, def_mat="glass", value=160, lv=12, weight=150, factory="uw_mixing_table", components="uw_herb_shadow/4,potion_empty/1", elements=quality_elements(30, 12, 6), detail_jp="青い残光を引く危険な蒸留液。", detail="A distilled shadow that leaves a blue afterimage."),
    thing_base("uw_powder_moonite", "月鉱粉", "moonite powder", "_item", sort_text="resource", tiles=504, def_mat="mica", value=100, lv=10, weight=200, factory="uw_mixing_table", components="uw_mineral_crude/3", elements=quality_elements(10, 2, 1), detail_jp="月鉱を細かく砕いた活性粉。", detail="Finely milled moonite used to stabilize unstable blends."),
    thing_base("uw_crystal_void", "虚晶", "void crystal", "_item", sort_text="resource", tiles=503, def_mat="obsidian", value=300, lv=16, weight=250, factory="uw_mixing_table", components="uw_mineral_crystal/2,potion_empty/1", elements=quality_elements(18, 4, 2), detail_jp="深い闇を閉じ込めた結晶。", detail="A crystal that seems to hold a slice of silent dark."),
    thing_base("uw_tonic_whisper", "囁きの煎液", "whisper tonic", UNDERWORLD_BOTTLED_CATEGORY, unit_jp="本", sort_text=UNDERWORLD_BOTTLED_SORT, tiles=1715, def_mat="glass", value=500, lv=15, weight=200, trait="Drug,Buff,ConUWWhisperHigh", factory="uw_mixing_table", components="uw_extract_whisper/2,uw_powder_moonite/1", elements=quality_elements(40, 10, 10), detail_jp="静かな高鳴りが長く残る。", detail="Easy on the swallow, with a hush that keeps ringing after."),
    thing_base("uw_powder_dream", "夢見の白粉", "dream powder", "_item", sort_text="drug", tiles=504, def_mat="mica", value=800, lv=18, weight=150, trait="DreamPowder,Buff,ConUWDreamHigh", factory="uw_mixing_table", components="uw_extract_dream/2,uw_powder_moonite/1", elements=quality_elements(55, 14, 12), detail_jp="吸い込むと世界の縁が柔らかくなる。", detail="One breath and the edges of the world turn a little soft."),
    thing_base("uw_elixir_shadow", "影歩きの秘薬", "shadow elixir", UNDERWORLD_BOTTLED_CATEGORY, unit_jp="本", sort_text=UNDERWORLD_BOTTLED_SORT, tiles=1715, def_mat="glass", value=1200, lv=20, weight=200, trait="Drug,Buff,ConUWShadowRush", factory="uw_mixing_table", components="uw_extract_shadow/2,uw_crystal_void/1", elements=quality_elements(70, 20, 15), detail_jp="時間感覚を歪める濃い秘薬。", detail="A dense elixir that makes the world look painfully slow."),
    thing_base("uw_salts_void", "虚空塩", "void salts", "_item", sort_text="drug", tiles=504, def_mat="obsidian", value=2000, lv=24, weight=100, trait="Food", factory="uw_advanced_lab", components="uw_crystal_void/3,uw_extract_dream/1", elements=quality_elements(78, 24, 18, "90022/3"), detail_jp="激しい怒りと歪んだ快感を呼ぶ結晶塩。", detail="A jagged salt that burns fury into the blood."),
    thing_base("uw_elixir_crimson", "深紅の秘薬", "crimson elixir", UNDERWORLD_BOTTLED_CATEGORY, unit_jp="本", sort_text=UNDERWORLD_BOTTLED_SORT, tiles=1715, def_mat="glass", value=3000, lv=26, weight=250, trait="Drug,Buff,ConUWCrimsonSurge", factory="uw_advanced_lab", components="uw_herb_crimson/4,uw_extract_shadow/2,uw_crystal_void/1", elements=quality_elements(86, 28, 22), detail_jp="不自然な活力で血を熱くする。", detail="A crimson draught that swells the body with borrowed vitality."),
    thing_base("uw_roll_whisper", "囁き草巻き", "whispervine roll", "_item", unit_jp="本", sort_text="drug", tiles=1119, def_mat="paper", value=200, lv=12, weight=50, trait="ItemProc,Buff,ConUWWhisperCalm", factory="self", components="uw_herb_whisper/3,bark/1", elements=quality_elements(24, 6, 6), detail_jp="静かな煙で緊張を和らげる自家製巻草。", detail="A hand-rolled calm with a faint herbal sweetness."),
    thing_base("uw_roll_dream", "夢見草巻き", "dreamweed joint", "_item", unit_jp="本", sort_text="drug", tiles=1119, def_mat="paper", value=250, lv=12, weight=50, trait="ItemProc,Buff,ConUWDreamCalm", factory="self", components="uw_herb_dream/3,bark/1", elements=quality_elements(30, 7, 7), detail_jp="肩の力が抜ける甘い煙。", detail="A fragrant roll that turns every worry down a notch."),
    thing_base("uw_draught_berserker", "狂戦士の引き水", "berserker's draught", UNDERWORLD_BOTTLED_CATEGORY, unit_jp="本", sort_text=UNDERWORLD_BOTTLED_SORT, tiles=1715, def_mat="glass", value=4500, lv=28, weight=200, trait="Drug,Buff,ConUWBerserkerRage", factory="uw_advanced_lab", components="uw_salts_void/1,uw_elixir_crimson/1", elements=quality_elements(96, 36, 26), detail_jp="肉体に凶暴な勢いを叩き込む調合薬。", detail="A brutal fusion of rage, vitality, and very bad judgment."),
    thing_base("uw_elixir_rush", "疾影薬", "shadow rush", UNDERWORLD_BOTTLED_CATEGORY, unit_jp="本", sort_text=UNDERWORLD_BOTTLED_SORT, tiles=1715, def_mat="glass", value=3500, lv=28, weight=150, trait="Drug,Buff,ConUWShadowRushX", factory="uw_advanced_lab", components="uw_elixir_shadow/1,uw_salts_void/1", elements=quality_elements(94, 34, 25), detail_jp="ほんの短い間だけ世界を置き去りにする。", detail="For a few terrifying moments, everything else falls behind."),
    thing_base("uw_elixir_frost", "霜花の煎液", "frostbloom elixir", UNDERWORLD_BOTTLED_CATEGORY, unit_jp="本", sort_text=UNDERWORLD_BOTTLED_SORT, tiles=1715, def_mat="glass", value=1800, lv=20, weight=200, trait="Drug,Buff,ConUWFrostbloom", factory="uw_mixing_table", components="uw_herb_frostbloom/3,uw_extract_whisper/1,potion_empty/1", elements=quality_elements(68, 16, 14), detail_jp="冷たい活力が体を満たす長効型の薬。", detail="A long-acting elixir that fills the body with restorative cold."),
    thing_base("uw_incense_ash", "灰帳香", "ashveil incense", "_item", unit_jp="束", sort_text="drug", tiles=1119, def_mat="paper", value=2000, lv=20, weight=80, trait="AshveilIncense,Buff,ConUWAshveil", factory="uw_mixing_table", components="uw_herb_ashveil/3,uw_powder_moonite/1", elements=quality_elements(66, 15, 12), detail_jp="焚くと隠れたものを暴く灰色の煙になる。", detail="Smoke it or throw it; either way, hidden things stop staying hidden."),
    thing_base("uw_tonic_whisper_refined", "精製囁き煎液", "refined whisper tonic", UNDERWORLD_BOTTLED_CATEGORY, unit_jp="本", sort_text=UNDERWORLD_BOTTLED_SORT, tiles=1715, def_mat="glass", value=1000, lv=18, weight=200, trait="Drug,Buff,ConUWWhisperHigh", recipe_key=None, factory=None, components=None, elements=quality_elements(60, 8, 14), detail_jp="抽出段階から熟成させた上質な囁き薬。", detail="A vat-refined whisper tonic with a cleaner finish and stronger hush."),
    thing_base("uw_powder_dream_refined", "精製夢見粉", "refined dream powder", "_item", sort_text="drug", tiles=504, def_mat="mica", value=1800, lv=22, weight=150, trait="DreamPowder,Buff,ConUWDreamHigh", recipe_key=None, factory=None, components=None, elements=quality_elements(76, 11, 16), detail_jp="霧のように細かく整えられた夢見粉。", detail="A finer, cleaner dream powder refined through slow vat work."),
    thing_base("uw_elixir_shadow_refined", "精製影秘薬", "refined shadow elixir", UNDERWORLD_BOTTLED_CATEGORY, unit_jp="本", sort_text=UNDERWORLD_BOTTLED_SORT, tiles=1715, def_mat="glass", value=3600, lv=24, weight=200, trait="Drug,Buff,ConUWShadowRush", recipe_key=None, factory=None, components=None, elements=quality_elements(90, 16, 20), detail_jp="危ういが滑らかな高純度の影秘薬。", detail="A dangerously pure shadow elixir with an almost glassy finish."),
    thing_base("uw_tonic_whisper_aged", "熟成囁き煎液", "aged whisper tonic", UNDERWORLD_BOTTLED_CATEGORY, unit_jp="本", sort_text=UNDERWORLD_BOTTLED_SORT, tiles=1715, def_mat="glass", value=900, lv=18, weight=200, trait="Drug,Buff,ConUWWhisperHigh", recipe_key=None, factory=None, components=None, elements=quality_elements(56, 7, 12), detail_jp="樽で寝かせて丸みを出した囁き薬。", detail="An aged batch that trades some bite for a smoother, richer calm."),
    thing_base("uw_powder_dream_concentrated", "濃縮夢見粉", "concentrated dream powder", "_item", sort_text="drug", tiles=504, def_mat="mica", value=1760, lv=22, weight=150, trait="DreamPowder,Buff,ConUWDreamHigh", recipe_key=None, factory=None, components=None, elements=quality_elements(74, 10, 15), detail_jp="濃く圧縮された扱いの難しい夢見粉。", detail="A concentrated powder with a sharper rise and longer tail."),
    thing_base(
        "uw_territory_map", "地下組織の地図", "territory map", "furniture",
        sort_text="furniture", render="@obj", tile_type="ObjBig", tiles=724, def_mat="oak",
        value=2000, lv=12, weight=12000, hp=6000, trait="TerritoryMap",
        factory="uw_mixing_table", components="plank/4,glass/2",
        tag="noShop,noWish", room_name_jp="密室", room_name="Back Room",
        detail_jp="縄張りの勢力図と熱気圧を映す地図盤。",
        detail="A clandestine map board tracking territory control and enforcement heat.",
    ),
    thing_base(
        "uw_faction_desk", "組織運営机", "faction desk", "furniture",
        sort_text="furniture", render="@obj", tile_type="ObjBig", tiles=724, def_mat="oak",
        value=3000, lv=15, weight=14000, hp=7000, trait="FactionDesk",
        factory="uw_mixing_table", components="plank/6,ingot/2",
        tag="noShop,noWish", room_name_jp="密室", room_name="Back Room",
        detail_jp="密造組織の管理と連絡に使う重厚な机。",
        detail="A heavy desk for managing faction operations and coordinating with operatives.",
    ),
    thing_base(
        "uw_dead_drop_board", "取引掲示板", "dead drop board", "furniture",
        sort_text="furniture", render="@obj", tile_type="ObjBig", tiles=724, def_mat="oak",
        value=1500, lv=10, weight=8000, hp=5000, trait="DeadDropBoard",
        factory="uw_mixing_table", components="plank/3,bolt/2",
        tag="noShop,noWish", room_name_jp="密室", room_name="Back Room",
        detail_jp="匿名の注文が貼り出される裏取引の掲示板。",
        detail="A battered corkboard where anonymous contract postings appear and disappear.",
    ),
    thing_base(
        "uw_heat_monitor", "監視装置", "heat monitor", "furniture",
        sort_text="furniture", render="@obj", tile_type="ObjBig", tiles=724, def_mat="glass",
        value=2500, lv=14, weight=10000, hp=6000, trait="HeatMonitor",
        factory="uw_mixing_table", components="glass/3,ingot/2",
        tag="noShop,noWish", room_name_jp="密室", room_name="Back Room",
        lightData="11,1,0",
        detail_jp="縄張りごとの取締り脅威度を示す監視装置。",
        detail="A monitoring apparatus that displays real-time enforcement threat levels.",
    ),
]

CHARA_ROWS = [
    {
        "id": "uw_fixer",
        "_id": 910201,
        "name_JP": "夜語りの世話人",
        "name": "Night-Fixer Sable",
        "_idRenderData": "@chara",
        "tiles": 806,
        "LV": 18,
        "chance": 0,
        "quality": 4,
        "hostility": "Friend",
        "tag": "neutral,noRandomProduct",
        "trait": "UnderworldFixer",
        "race": "norland",
        "job": "merchant",
        "detail_JP": "声を荒げず、余計なことも聞かない。",
        "detail": "Never raises their voice and never asks the wrong question twice.",
    },
]

OBJ_ROWS = [
    {"id": 90100, "alias": "uw_crop_whisper", "name_JP": "囁き草", "name": "Whispervine", "_growth": "Herb,530/531/532/533,534,uw_herb_whisper,3", "costSoil": 20, "objType": "crop", "vals": 50, "tag": "seed,scale", "reqHarvest": "gathering,1", "hp": 100, "_tileType": "Obj", "_idRenderData": "obj_S flat", "tiles": 530, "colorMod": 100, "value": 20, "LV": 10, "chance": 20, "components": "leaf", "defMat": "grass", "category": "obj", "detail_JP": "栽培用の囁き草。", "detail": "A cultivated whispervine patch."},
    {"id": 90101, "alias": "uw_crop_dream", "name_JP": "夢見花", "name": "Dreamblossom", "_growth": "Herb,540/541/542/543,544,uw_herb_dream,2", "costSoil": 20, "objType": "crop", "vals": 51, "tag": "seed,scale", "reqHarvest": "gathering,1", "hp": 100, "_tileType": "Obj", "_idRenderData": "obj_S flat", "tiles": 540, "colorMod": 100, "value": 20, "LV": 15, "chance": 12, "components": "leaf", "defMat": "grass", "category": "obj", "detail_JP": "栽培用の夢見花。", "detail": "A cultivated dreamblossom patch."},
    {"id": 90102, "alias": "uw_crop_shadow", "name_JP": "影笠茸", "name": "Shadowcap", "_growth": "Kinoko,550/551/552/553,554,uw_herb_shadow,3", "costSoil": 20, "objType": "crop", "vals": 52, "tag": "seed", "reqHarvest": "gathering,1", "hp": 100, "_tileType": "Obj", "_idRenderData": "obj_S flat", "tiles": 550, "colorMod": 100, "value": 20, "LV": 8, "chance": 18, "components": "grass", "defMat": "grass", "category": "obj", "detail_JP": "屋内でも育つ影笠茸。", "detail": "A cultivated shadowcap cluster that tolerates indoor growth."},
    {"id": 90103, "alias": "uw_crop_crimson", "name_JP": "深紅草", "name": "Crimsonwort", "_growth": "Herb,560/561/562/563,564,uw_herb_crimson,2", "costSoil": 20, "objType": "crop", "vals": 53, "tag": "seed,scale", "reqHarvest": "gathering,1", "hp": 100, "_tileType": "Obj", "_idRenderData": "obj_S flat", "tiles": 560, "colorMod": 100, "value": 20, "LV": 20, "chance": 10, "components": "leaf", "defMat": "grass", "category": "obj", "detail_JP": "熱を好む深紅草。", "detail": "A heat-loving crimsonwort patch."},
    {"id": 90104, "alias": "uw_crop_frostbloom", "name_JP": "霜花", "name": "Frostbloom", "_growth": "Herb,570/571/572/573,574,uw_herb_frostbloom,1", "costSoil": 20, "objType": "crop", "vals": 54, "tag": "seed,scale", "reqHarvest": "gathering,1", "hp": 100, "_tileType": "Obj", "_idRenderData": "obj_S flat", "tiles": 570, "colorMod": 100, "value": 20, "LV": 25, "chance": 8, "components": "leaf", "defMat": "grass", "category": "obj", "detail_JP": "冷地と冬に縁のある希少花。", "detail": "A rare cold-weather flower patch."},
    {"id": 90105, "alias": "uw_crop_ashveil", "name_JP": "灰帳苔", "name": "Ashveil Moss", "_growth": "Herb,580/581/582/583,584,uw_herb_ashveil,1", "costSoil": 20, "objType": "crop", "vals": 55, "tag": "seed,scale", "reqHarvest": "gathering,1", "hp": 100, "_tileType": "Obj", "_idRenderData": "obj_S flat", "tiles": 580, "colorMod": 100, "value": 20, "LV": 25, "chance": 8, "components": "leaf", "defMat": "grass", "category": "obj", "detail_JP": "灰の香りをまとった苔床。", "detail": "A moss bed that grows into aromatic ashveil clumps."},
]

ELEMENT_ROWS = [
    {"id": 90010, "alias": "uw_customer_addiction", "name_JP": "顧客依存", "name": "Customer Addiction", "type": "Element", "group": "Underworld", "category": "underworld", "sort": 90010, "detail_JP": "顧客の依存度管理用内部値。", "detail": "Internal addiction tracker for underworld customers."},
    {"id": 90011, "alias": "uw_customer_tolerance", "name_JP": "顧客耐性", "name": "Customer Tolerance", "type": "Element", "group": "Underworld", "category": "underworld", "sort": 90011, "detail_JP": "顧客の耐性管理用内部値。", "detail": "Internal tolerance tracker for underworld customers."},
    {"id": 90012, "alias": "uw_customer_loyalty", "name_JP": "顧客忠誠", "name": "Customer Loyalty", "type": "Element", "group": "Underworld", "category": "underworld", "sort": 90012, "detail_JP": "顧客の忠誠度管理用内部値。", "detail": "Internal loyalty tracker for underworld customers."},
    {"id": 90013, "alias": "uw_customer_preferred_product", "name_JP": "嗜好品目", "name": "Preferred Product", "type": "Element", "group": "Underworld", "category": "underworld", "sort": 90013, "detail_JP": "顧客が好む品目。", "detail": "Internal preferred-product marker for customers."},
    {"id": 90014, "alias": "uw_customer_offer_cooldown", "name_JP": "提案待機", "name": "Offer Cooldown", "type": "Element", "group": "Underworld", "category": "underworld", "sort": 90014, "detail_JP": "顧客提案のクールダウン。", "detail": "Internal cooldown value for customer offers."},
    {"id": 90020, "alias": "uw_potency", "name_JP": "効力", "name": "Potency", "type": "Element", "group": "Underworld", "category": "underworld", "sort": 90020, "detail_JP": "商品の主要品質値。高いほど強い。", "detail": "Primary quality factor for contraband products."},
    {"id": 90021, "alias": "UW_DREAM_TRAIT", "name_JP": "夢見食特性", "name": "Dream Food Trait", "type": "Element", "group": "Underworld", "category": "food", "sort": 90021, "foodEffect": "uw_dream", "detail_JP": "食べると夢見系の効果を与える。", "detail": "Food trait that applies the Dream High effect."},
    {"id": 90022, "alias": "UW_VOID_TRAIT", "name_JP": "虚空食特性", "name": "Void Food Trait", "type": "Element", "group": "Underworld", "category": "food", "sort": 90022, "foodEffect": "uw_void", "detail_JP": "食べると虚空系の効果を与える。", "detail": "Food trait that applies the Void Rage effect."},
    {"id": 90023, "alias": "uw_toxicity", "name_JP": "毒性", "name": "Toxicity", "type": "Element", "group": "Underworld", "category": "underworld", "sort": 90023, "detail_JP": "商品の負品質。高いほど危険。", "detail": "Negative quality factor for underworld products."},
    {"id": 90024, "alias": "uw_traceability", "name_JP": "追跡性", "name": "Traceability", "type": "Element", "group": "Underworld", "category": "underworld", "sort": 90024, "detail_JP": "商品の足のつきやすさ。高いほど熱を呼ぶ。", "detail": "How easily a product can be traced back to the player."},
    {"id": 90025, "alias": "uw_vat_started_raw", "name_JP": "熟成開始時刻", "name": "Vat Start Raw", "type": "Element", "group": "Underworld", "category": "underworld", "sort": 90025, "tag": "hidden", "detail_JP": "熟成樽に入れた時刻を保持する内部値。", "detail": "Internal timestamp for underworld processing vat progress."},
]

STAT_ROWS = [
    {"id": 90100, "alias": "ConUWWhisperHigh", "name_JP": "静寂の陶酔", "name": "Whisper High", "group": "Buff", "duration": "p/5", "elements": "PV/15,DV/10,SPD/-5", "colors": "buff", "effect": "buff", "strPhase_JP": "静寂の陶酔", "strPhase": "Whisper High", "textPhase_JP": "#1は静かな酩酊に包まれた。", "textPhase": "#1 feel/feels a soothing numbness spread through #his body.", "textEnd_JP": "#1の静かな酔いは消えた。", "textEnd": "The numbness fades. #1 feel/feels exposed again.", "detail_JP": "防御と回避を高めるが動きは鈍る。", "detail": "Protection and dodge are enhanced, but movement is sluggish."},
    {"id": 90101, "alias": "ConUWShadowRush", "name_JP": "影の奔流", "name": "Shadow Rush", "group": "Buff", "duration": "p/3", "elements": "SPD/15", "colors": "buff", "effect": "buff", "strPhase_JP": "影の奔流", "strPhase": "Shadow Rush", "textPhase_JP": "#1は時の流れが遅く感じた。", "textPhase": "#1 drinks the dark liquid. Time seems to slow to a crawl.", "textEnd_JP": "#1の加速感は途切れた。", "textEnd": "The rush ends and reality catches up with a vengeance.", "detail_JP": "非常に短い加速。3ターンごとに追加行動。", "detail": "Massively increased speed with extra actions every 3 turns."},
    {"id": 90102, "alias": "ConUWShadowCrash", "name_JP": "影の反動", "name": "Shadow Crash", "group": "Bad", "duration": "30", "elements": "SPD/-10", "colors": "debuff", "effect": "debuff", "strPhase_JP": "影の反動", "strPhase": "Shadow Crash", "textPhase_JP": "#1は大きくよろめいた。", "textPhase": "#1 staggers as the world snaps back to full speed.", "textEnd_JP": "#1は反動から回復した。", "textEnd": "#1 feels normal again.", "detail_JP": "速度低下と持久力消耗。", "detail": "Speed penalty and stamina drain."},
    {"id": 90103, "alias": "ConUWDreamHigh", "name_JP": "夢幻の啓示", "name": "Dream Lucidity", "group": "Buff", "duration": "p/5", "elements": "INT/3,PER/2", "colors": "buff", "effect": "buff", "strPhase_JP": "夢幻の啓示", "strPhase": "Dream Lucidity", "textPhase_JP": "#1の視界は冴えわたった。", "textPhase": "#1 inhales deeply. The world sharpens into crystalline focus.", "textEnd_JP": "#1の冴えは夢のように消えた。", "textEnd": "The clarity fades like a dream upon waking.", "detail_JP": "知力と感覚を高める。", "detail": "Enhanced intelligence and perception."},
    {"id": 90104, "alias": "ConUWVoidRage", "name_JP": "虚空の激昂", "name": "Void Rage", "group": "Buff", "duration": "p/5", "elements": "STR/3,WIL/-3", "colors": "buff", "effect": "buff", "strPhase_JP": "虚空の激昂", "strPhase": "Void Rage", "textPhase_JP": "#1の腹の底から怒りが込み上げた。", "textPhase": "#1 crunches the salts and a hot fury rises from within.", "textEnd_JP": "#1の怒りは引いていった。", "textEnd": "The rage subsides, leaving a trembling emptiness.", "detail_JP": "筋力を高め、意志を削る。", "detail": "Massive strength boost, but willpower is shattered."},
    {"id": 90105, "alias": "ConUWCrimsonSurge", "name_JP": "紅蓮の活力", "name": "Crimson Surge", "group": "Buff", "duration": "p/5", "elements": "STR/3,END/3", "colors": "buff", "effect": "buff", "strPhase_JP": "紅蓮の活力", "strPhase": "Crimson Surge", "textPhase_JP": "#1の血管は熱く脈打った。", "textPhase": "#1 drinks the crimson liquid. Veins pulse with unnatural vitality.", "textEnd_JP": "#1から活力が引いていく。", "textEnd": "The surge of vitality drains away. #1 feels fragile.", "detail_JP": "筋力と耐久を高め、一時的な生命力を与える。", "detail": "Strength, endurance, and health surge beyond natural limits."},
    {"id": 90106, "alias": "ConUWWhisperCalm", "name_JP": "静寂の安息", "name": "Whisper Calm", "group": "Buff", "duration": "p/4", "elements": "PV/10", "colors": "buff", "effect": "buff,cigar", "strPhase_JP": "静寂の安息", "strPhase": "Whisper Calm", "textPhase_JP": "#1は囁き草巻きをくゆらせはじめた。", "textPhase": "#1 lights a whispervine roll. A calming smoke rises.", "textEnd_JP": "#1は煙を吐き出して落ち着きを取り戻した。", "textEnd": "#1 stubs out the ember.", "detail_JP": "軽い防御上昇と眠気緩和。", "detail": "Mild protection boost that eases drowsiness."},
    {"id": 90107, "alias": "ConUWDreamCalm", "name_JP": "夢見の微笑", "name": "Dream Ease", "group": "Buff", "duration": "p/5", "elements": "CHA/3", "colors": "buff", "effect": "buff,cigar", "strPhase_JP": "夢見の微笑", "strPhase": "Dream Ease", "textPhase_JP": "#1はゆっくりと煙を吸い込んだ。", "textPhase": "#1 takes a slow drag. A lazy smile spreads.", "textEnd_JP": "#1の気安さは薄れていった。", "textEnd": "The easygoing warmth fades.", "detail_JP": "魅力と社交性を高める。", "detail": "Enhanced charisma and social ease."},
    {"id": 90108, "alias": "ConUWBerserkerRage", "name_JP": "狂戦の嵐", "name": "Berserker Fury", "group": "Buff", "duration": "p/4", "elements": "STR/5,END/3", "colors": "buff", "effect": "buff", "strPhase_JP": "狂戦の嵐", "strPhase": "Berserker Fury", "textPhase_JP": "#1の筋肉は危ういほど膨れ上がった。", "textPhase": "#1 gulps the ruby draught. Muscles swell with terrible power.", "textEnd_JP": "#1の力は急速に失われた。", "textEnd": "The power drains as quickly as it came.", "detail_JP": "極端な筋力と耐久の強化。", "detail": "Extreme strength and endurance. The cost is devastating."},
    {"id": 90109, "alias": "ConUWBerserkerCrash", "name_JP": "狂戦の反動", "name": "Berserker Crash", "group": "Bad", "duration": "15", "elements": "STR/-3,END/-2", "colors": "debuff", "effect": "debuff", "strPhase_JP": "狂戦の反動", "strPhase": "Berserker Crash", "textPhase_JP": "#1の筋肉は悲鳴を上げた。", "textPhase": "#1's muscles scream. The borrowed power takes its toll.", "textEnd_JP": "#1は反動から持ち直した。", "textEnd": "#1 recovers from the aftereffects.", "detail_JP": "筋力と耐久が低下し混乱を伴う。", "detail": "Strength and endurance penalty with confusion."},
    {"id": 90110, "alias": "ConUWShadowRushX", "name_JP": "疾影一閃", "name": "Shadow Blitz", "group": "Buff", "duration": "p/2", "elements": "SPD/20", "colors": "buff", "effect": "buff", "strPhase_JP": "疾影一閃", "strPhase": "Shadow Blitz", "textPhase_JP": "#1の視界は暗転し、すべてが加速した。", "textPhase": "#1 drinks. Everything goes dark, then impossibly fast.", "textEnd_JP": "#1に痛みと現実が戻ってきた。", "textEnd": "Light returns. Sound returns. Pain returns.", "detail_JP": "極端な速度上昇。2ターンごとに追加行動。", "detail": "Extreme speed and extra actions every 2 turns."},
    {"id": 90111, "alias": "ConUWRushCrash", "name_JP": "疾影の代償", "name": "Blitz Aftermath", "group": "Bad", "duration": "40", "elements": "SPD/-15", "colors": "debuff", "effect": "debuff", "strPhase_JP": "疾影の代償", "strPhase": "Blitz Aftermath", "textPhase_JP": "#1は膝をついた。", "textPhase": "#1 collapses. The world is too slow, too heavy, too real.", "textEnd_JP": "#1の身体はようやく動きを取り戻した。", "textEnd": "#1's body finally remembers how to work.", "detail_JP": "深刻な速度低下と消耗。", "detail": "Severe speed penalty, stamina drain, and dimness."},
    {"id": 90112, "alias": "ConUWFrostbloom", "name_JP": "霜花の抱擁", "name": "Frostbloom Embrace", "group": "Buff", "duration": "p/6", "elements": "END/4,resCold/50,resFire/-10", "colors": "buff", "effect": "buff", "strPhase_JP": "霜花の抱擁", "strPhase": "Frostbloom Embrace", "textPhase_JP": "#1の身体を深い冷気が満たした。", "textPhase": "#1 drinks. A deep, restorative cold fills #his body.", "textEnd_JP": "#1の身体に世界の熱が戻った。", "textEnd": "The warmth of the world returns.", "detail_JP": "耐久上昇、冷気への適応、継続回復。", "detail": "Endurance boost, cold resistance, and continuous healing."},
    {"id": 90113, "alias": "ConUWAshveil", "name_JP": "灰霞の啓示", "name": "Ashveil Clarity", "group": "Buff", "duration": "p/5", "elements": "PER/5,resFire/30", "colors": "buff", "effect": "buff,cigar", "strPhase_JP": "灰霞の啓示", "strPhase": "Ashveil Clarity", "textPhase_JP": "#1は灰の香りを吸い込んだ。", "textPhase": "#1 breathes the ash-scented smoke. Hidden things reveal themselves.", "textEnd_JP": "#1の前から再び帳が下りた。", "textEnd": "The smoke clears. The veil falls again.", "detail_JP": "感覚上昇。火への耐性と透明可視を付与。", "detail": "Enhanced perception, fire resistance, and see invisible."},
    {"id": 90114, "alias": "ConUWWithdrawal", "name_JP": "禁断症状", "name": "Withdrawal", "group": "Bad", "duration": "20", "elements": "STR/-2,END/-2,INT/-2,PER/-2,CHA/-2,WIL/-2", "colors": "debuff", "effect": "debuff", "strPhase_JP": "禁断症状", "strPhase": "Withdrawal", "textPhase_JP": "#1はひどく落ち着かない。", "textPhase": "#1 is wracked by underworld withdrawal.", "textEnd_JP": "#1はようやく落ち着きを取り戻した。", "textEnd": "#1 steadies as the withdrawal fades.", "detail_JP": "依存が切れた時に現れる全般的な弱体化。", "detail": "Broad stat penalties that appear when a dependent user goes without drugs."},
]

TEXTURE_SOURCE_MAP = {
    "uw_mixing_table": "uw_mixing_table",
    "uw_processing_vat": "uw_processing_vat",
    "uw_advanced_lab": "uw_advanced_lab",
    "uw_contraband_chest": "uw_contraband_chest",
    "uw_dealers_ledger": "uw_dealers_ledger",
    "uw_sample_kit": "uw_sample_kit",
    "uw_antidote_vial": "uw_antidote_vial",
    "uw_herb_whisper": "uw_herb_whisper",
    "uw_herb_dream": "uw_herb_dream",
    "uw_herb_shadow": "uw_herb_shadow",
    "uw_herb_crimson": "uw_herb_crimson",
    "uw_herb_frostbloom": "uw_herb_frostbloom",
    "uw_herb_ashveil": "uw_herb_ashveil",
    "uw_mineral_crude": "uw_mineral_crude",
    "uw_mineral_crystal": "uw_mineral_crystal",
    "uw_extract_whisper": "uw_extract_whisper",
    "uw_extract_dream": "uw_extract_dream",
    "uw_extract_shadow": "uw_extract_shadow",
    "uw_powder_moonite": "uw_powder_moonite",
    "uw_crystal_void": "uw_crystal_void",
    "uw_tonic_whisper": "uw_tonic_whisper",
    "uw_powder_dream": "uw_powder_dream",
    "uw_elixir_shadow": "uw_elixir_shadow",
    "uw_salts_void": "uw_salts_void",
    "uw_elixir_crimson": "uw_elixir_crimson",
    "uw_roll_whisper": "uw_roll_whisper",
    "uw_roll_dream": "uw_roll_dream",
    "uw_draught_berserker": "uw_draught_berserker",
    "uw_elixir_rush": "uw_elixir_rush",
    "uw_elixir_frost": "uw_elixir_frost",
    "uw_incense_ash": "uw_incense_ash",
    "uw_tonic_whisper_refined": "uw_tonic_whisper_refined",
    "uw_powder_dream_refined": "uw_powder_dream_refined",
    "uw_elixir_shadow_refined": "uw_elixir_shadow_refined",
    "uw_tonic_whisper_aged": "uw_tonic_whisper_aged",
    "uw_powder_dream_concentrated": "uw_powder_dream_concentrated",
    "uw_fixer": "uw_fixer",
    "uw_territory_map": "uw_territory_map",
    "uw_faction_desk": "uw_faction_desk",
    "uw_dead_drop_board": "uw_dead_drop_board",
    "uw_heat_monitor": "uw_heat_monitor",
}

EXPECTED_TEXTURE_IDS = set(TEXTURE_SOURCE_MAP)

ANCHOR_CENTER = "center"
ANCHOR_BOTTOM_CENTER = "bottom_center"


@dataclass(frozen=True)
class AssetScaleSpec:
    category: str
    canvas_size: tuple[int, int]
    visible_size: tuple[int, int]
    anchor: str
    render_data: str
    ground_lift_px: int = 0

    def to_log_fields(self) -> dict[str, object]:
        return {
            "canvas_size": list(self.canvas_size),
            "visible_size": list(self.visible_size),
            "anchor": self.anchor,
            "render_data": self.render_data,
            "ground_lift_px": self.ground_lift_px,
        }


def small_item(render_data: str = "@obj_S flat", visible_size: tuple[int, int] = (30, 30)) -> AssetScaleSpec:
    return AssetScaleSpec("item", (32, 32), visible_size, ANCHOR_CENTER, render_data)


def furniture(render_data: str = "@obj", canvas_size: tuple[int, int] = (48, 48),
              visible_size: tuple[int, int] | None = None) -> AssetScaleSpec:
    visible = visible_size or (canvas_size[0] - 2, canvas_size[1] - 2)
    return AssetScaleSpec("item", canvas_size, visible, ANCHOR_CENTER, render_data)


def chara(visible_size: tuple[int, int], render_data: str = "@chara", ground_lift_px: int = 16) -> AssetScaleSpec:
    return AssetScaleSpec("chara", (128, 192), visible_size, ANCHOR_BOTTOM_CENTER, render_data, ground_lift_px)


ASSET_SCALE_SPECS: dict[str, AssetScaleSpec] = {
    "uw_mixing_table": furniture("@obj", (64, 64), (58, 58)),
    "uw_processing_vat": furniture("@obj", (64, 64), (58, 58)),
    "uw_advanced_lab": furniture("@obj", (64, 64), (58, 58)),
    "uw_contraband_chest": furniture("@obj"),
    "uw_fixer": chara((74, 110)),
    "uw_territory_map": furniture("@obj", (64, 64), (58, 58)),
    "uw_faction_desk": furniture("@obj", (64, 64), (58, 58)),
    "uw_dead_drop_board": furniture("@obj", (64, 64), (58, 58)),
    "uw_heat_monitor": furniture("@obj", (64, 64), (58, 58)),
}

for asset_id in EXPECTED_TEXTURE_IDS:
    ASSET_SCALE_SPECS.setdefault(asset_id, small_item("@obj_S flat"))


def get_asset_spec(asset_id: str) -> AssetScaleSpec:
    return ASSET_SCALE_SPECS[asset_id]


def build_prompt(asset_id: str) -> str:
    if asset_id == "uw_fixer":
        return (
            "2D front-facing RPG humanoid sprite, a composed fantasy underworld fixer in a layered dark coat, "
            "full body, complete silhouette, pixel art style, no text, no frame, game sprite"
        )
    if asset_id == "uw_mixing_table":
        return (
            "2D isometric RPG game furniture sprite, three-quarter top-down view angled from upper-right, "
            "a clandestine alchemical mixing table made from dark stained wood, cluttered with brass scales, "
            "glass flasks, wrapped herbs, handwritten recipe scraps, and a faint green lamp glow, showing the "
            "tabletop and front-left legs, pixel art style, deep walnut, oxidized brass, and teal glass palette, "
            "no text, no frame, game asset"
        )
    if asset_id == "uw_processing_vat":
        return (
            "2D isometric RPG game furniture sprite, three-quarter top-down view angled from upper-right, "
            "a heavy sealed fermentation cask with dark oak staves, copper hoops, pressure valves, a sampling tap, "
            "coiled tubing, and a small tray of aging jars, showing the barrel body and front-left base, "
            "pixel art style, smoked oak, verdigris copper, and moss-glass palette, no text, no frame, game asset"
        )
    if asset_id == "uw_advanced_lab":
        return (
            "2D isometric RPG game furniture sprite, three-quarter top-down view angled from upper-right, "
            "a shadow laboratory workstation of black glass retorts, polished metal frames, condenser coils, "
            "crystal vials, and a luminous analysis basin on a reinforced bench, showing the apparatus and "
            "front-left supports, pixel art style, obsidian glass, cold steel, and electric cyan highlights, "
            "no text, no frame, game asset"
        )
    if asset_id == "uw_territory_map":
        return (
            "2D isometric RPG game furniture sprite, three-quarter top-down view angled from upper-right, "
            "a dark-wood wall-mounted territory map board covered with pins, string routes, and cryptic markers, "
            "dim lamp light casting shadows across the surface, pixel art style, "
            "dark walnut, faded parchment, and brass pin palette, no text, no frame, game asset"
        )
    if asset_id == "uw_faction_desk":
        return (
            "2D isometric RPG game furniture sprite, three-quarter top-down view angled from upper-right, "
            "a heavy operations desk with stacked ledgers, sealed envelopes, an ink well, a faction seal stamp, "
            "and a brass lamp, dark wood with leather inlay, pixel art style, "
            "mahogany, aged leather, and brass palette, no text, no frame, game asset"
        )
    if asset_id == "uw_dead_drop_board":
        return (
            "2D isometric RPG game furniture sprite, three-quarter top-down view angled from upper-right, "
            "a battered wooden corkboard on a stand with pinned contract notices, torn paper scraps, "
            "red string connections, and a few coins left as payment markers, pixel art style, "
            "weathered cork, parchment, and rusty pin palette, no text, no frame, game asset"
        )
    if asset_id == "uw_heat_monitor":
        return (
            "2D isometric RPG game furniture sprite, three-quarter top-down view angled from upper-right, "
            "a fantasy enforcement monitoring apparatus with glowing crystal displays, copper wiring, "
            "pulsing red and green indicator lights, and etched glass panels on a metal frame, "
            "pixel art style, dark iron, crystal cyan, and warning red palette, no text, no frame, game asset"
        )
    readable = asset_id.replace("uw_", "").replace("_", " ")
    return (
        f"2D top-down RPG game asset icon for {readable}, isolated, pixel art style, "
        "no text, no frame, game asset"
    )


ITEM_ASSETS = [
    {
        "id": asset_id,
        "preview_size": (64, 64) if get_asset_spec(asset_id).canvas_size[0] > 32 else (48, 48),
        "prompt": build_prompt(asset_id),
    }
    for asset_id in sorted(EXPECTED_TEXTURE_IDS)
    if asset_id != "uw_fixer"
]

CHARA_ASSETS = [
    {"id": "uw_fixer", "preview_size": (96, 144), "prompt": build_prompt("uw_fixer")},
]
