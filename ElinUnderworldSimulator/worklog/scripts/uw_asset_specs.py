from dataclasses import dataclass
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
REPO_ROOT = ROOT.parents[0]
TEMPLATE_SOURCE_CARD = REPO_ROOT / "elin_readable_game_data" / "xlsx format" / "SourceCard.xlsx"
SOURCE_CARD_OUT = ROOT / "LangMod" / "EN" / "SourceCard.xlsx"
TEXTURE_DIR = ROOT / "Texture"
PREVIEW_OUT = ROOT / "preview.jpg"

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

THINGS = [
    {"id": "uw_mixing_table", "name_JP": "囁きの調合台", "name": "whisper mixing table", "category": "crafter", "_tileType": "ObjBig", "_idRenderData": "@obj", "tiles": 724, "recipeKey": "*", "factory": "machinebench", "components": "plank/6,glass/2,potion_empty/2", "defMat": "rosewood", "value": 3200, "LV": 12, "chance": 0, "HP": 9000, "weight": 18000, "trait": "UwMixingTable,alchemy", "tag": "noShop,noWish", "roomName_JP": "密室,作業場", "roomName": "Back Room,Workroom", "detail_JP": "低い灯りの下で静かに品を仕立てるための調合台。", "detail": "A low-lit table built for quiet chemistry and quicker exits."},
    {"id": "uw_contraband_chest", "name_JP": "隠匿箱", "name": "contraband chest", "category": "container", "_idRenderData": "@obj", "tiles": 1400, "defMat": "walnut", "value": 1800, "LV": 8, "chance": 0, "quality": 2, "HP": 5000, "weight": 12000, "trait": "UwContrabandChest", "tag": "noShop,noWish", "roomName_JP": "隠し部屋,倉庫", "roomName": "Hideout,Storeroom", "detail_JP": "見た目は地味だが、底板の下にもう一段ある。", "detail": "An ordinary-looking chest with a second bottom and a better story."},
    {"id": "uw_dealers_ledger", "name_JP": "売人台帳", "unit_JP": "冊", "name": "dealer's ledger", "category": "util", "_idRenderData": "@obj_S flat", "tiles": 1712, "defMat": "paper", "value": 2400, "LV": 8, "chance": 0, "weight": 180, "trait": "UwDealerLedger", "tag": "noShop,noWish", "detail_JP": "名前、癖、注文、危ない兆候まで書き留めた薄い帳面。", "detail": "A thin ledger of names, habits, orders, and warning signs."},
    {"id": "uw_sample_kit", "name_JP": "試し包みの小袋", "name": "sample kit", "category": "_item", "_idRenderData": "@obj_S flat", "tiles": 1708, "defMat": "paper", "value": 320, "LV": 5, "chance": 0, "weight": 80, "trait": "UwSampleKit", "tag": "noShop,noWish", "detail_JP": "少量を安全に切り分けるための小瓶と包み紙が入っている。", "detail": "Small wraps and tiny bottles for floating careful samples."},
    {"id": "uw_antidote_vial", "name_JP": "鎮め薬の小瓶", "unit_JP": "本", "name": "antidote vial", "category": "_item", "_idRenderData": "@obj_S flat", "tiles": 1715, "defMat": "glass", "value": 480, "LV": 10, "chance": 0, "weight": 60, "trait": "UwAntidoteVial", "tag": "noShop", "detail_JP": "悪い巡りに入った客を現世へ引き戻すための応急薬。", "detail": "An emergency counteragent for customers slipping into a bad spiral."},
    {"id": "uw_whispervine", "name_JP": "囁き草", "unit_JP": "枚", "name": "whispervine", "category": "herb", "_idRenderData": "@obj_S flat", "tiles": 2033, "defMat": "grass", "value": 34, "LV": 4, "chance": 0, "weight": 18, "tag": "noShop", "detail_JP": "乾くと甘い匂いを放つ細葉の草。", "detail": "A fine-bladed herb that dries into a sweet, lingering scent."},
    {"id": "uw_dreamblossom", "name_JP": "夢見花", "unit_JP": "輪", "name": "dreamblossom", "category": "herb", "_idRenderData": "@obj_S flat", "tiles": 2033, "defMat": "flower", "value": 42, "LV": 6, "chance": 0, "weight": 20, "tag": "noShop", "detail_JP": "夜にだけ香り立つ柔らかな花弁。", "detail": "Soft petals that only really wake up after dark."},
    {"id": "uw_shadowcap", "name_JP": "影笠茸", "unit_JP": "本", "name": "shadowcap", "category": "mushroom", "_idRenderData": "@obj_S flat", "tiles": 725, "defMat": "soil", "value": 48, "LV": 8, "chance": 0, "weight": 26, "tag": "noShop", "detail_JP": "火にかけると青く汗をかくきのこ。", "detail": "A dark mushroom that beads blue when you put heat to it."},
    {"id": "uw_crude_moonite", "name_JP": "粗月鉱", "name": "crude moonite", "category": "ore", "_idRenderData": "@obj_S flat", "tiles": 530, "defMat": "mica", "value": 64, "LV": 10, "chance": 0, "weight": 240, "trait": "ResourceMain", "tag": "noShop", "detail_JP": "光を吸うように鈍く光る未精製の鉱石。", "detail": "A raw ore that seems to dim the light around it."},
    {"id": "uw_whisper_extract", "name_JP": "囁き抽出液", "unit_JP": "滴", "name": "whisper extract", "category": "_resource", "_idRenderData": "@obj_S flat", "tiles": 512, "recipeKey": "*", "factory": "uw_mixing_table", "components": "uw_whispervine/2,potion_empty/1", "defMat": "glass", "value": 84, "LV": 8, "chance": 0, "weight": 40, "tag": "noShop", "detail_JP": "舌に乗せると耳元で風が鳴る。", "detail": "Set a drop on the tongue and the air starts talking back."},
    {"id": "uw_dream_dust", "name_JP": "夢見の粉", "name": "dream dust", "category": "_resource", "_idRenderData": "@obj_S flat", "tiles": 504, "recipeKey": "*", "factory": "uw_mixing_table", "components": "uw_dreamblossom/2,ash/1", "defMat": "dust", "value": 96, "LV": 10, "chance": 0, "weight": 35, "tag": "noShop", "detail_JP": "乾いた花弁を灰で落ち着かせた眠りの粉。", "detail": "Sleepy powder cut from dried petals and just enough ash."},
    {"id": "uw_shadow_resin", "name_JP": "影脂", "name": "shadow resin", "category": "_resource", "_idRenderData": "@obj_S flat", "tiles": 503, "recipeKey": "*", "factory": "uw_mixing_table", "components": "uw_shadowcap/2,uw_crude_moonite/1", "defMat": "mud", "value": 112, "LV": 12, "chance": 0, "weight": 48, "tag": "noShop", "detail_JP": "熱で溶くと黒い鏡のようになる樹脂状の塊。", "detail": "A resinous lump that melts into something like a black mirror."},
    {"id": "uw_whisper_tonic", "name_JP": "囁きの煎液", "unit_JP": "本", "name": "whisper tonic", "category": "drug", "_idRenderData": "@obj_S flat", "tiles": 1715, "recipeKey": "*", "factory": "uw_mixing_table", "components": "uw_whisper_extract/1,potion_empty/1", "defMat": "glass", "value": 180, "LV": 10, "chance": 0, "weight": 55, "tag": "noShop", "detail_JP": "口当たりは軽いが、静かな高鳴りが長く残る。", "detail": "Easy on the swallow, with a hush that keeps ringing after."},
    {"id": "uw_dream_powder", "name_JP": "夢見の白粉", "name": "dream powder", "category": "drug", "_idRenderData": "@obj_S flat", "tiles": 504, "recipeKey": "*", "factory": "uw_mixing_table", "components": "uw_dream_dust/1,uw_whispervine/1", "defMat": "dust", "value": 260, "LV": 14, "chance": 0, "weight": 28, "tag": "noShop", "detail_JP": "吸い込むと世界の縁が少し柔らかくなる。", "detail": "One breath and the edges of the world turn a little soft."},
    {"id": "uw_shadow_elixir", "name_JP": "影歩きの秘薬", "unit_JP": "本", "name": "shadow elixir", "category": "drug", "_idRenderData": "@obj_S flat", "tiles": 1715, "recipeKey": "*", "factory": "uw_mixing_table", "components": "uw_shadow_resin/1,potion_empty/1", "defMat": "glass", "value": 420, "LV": 18, "chance": 0, "weight": 60, "tag": "noShop", "detail_JP": "強い。使う者にも、使われる者にも跡を残す。", "detail": "Strong enough to leave a mark on the hand that sells it too."},
]

CHARAS = [
    {"id": "uw_fixer", "_id": 910201, "name_JP": "夜語りの世話人", "name": "Night-Fixer Sable", "_idRenderData": "@chara", "tiles": 806, "LV": 18, "chance": 0, "quality": 4, "hostility": "Friend", "tag": "neutral,noRandomProduct", "trait": "UniqueCharaNoJoin", "race": "norland", "job": "merchant", "works": "Brokerage", "hobbies": "Observe", "detail_JP": "声を荒げず、余計なことも聞かない。", "detail": "Never raises their voice and never asks the wrong question twice."},
]

ASSETS = {
    "uw_mixing_table": {"kind": "furniture", "palette": ("#233a32", "#78c9a7", "#d6b36c")},
    "uw_contraband_chest": {"kind": "chest", "palette": ("#3a2b22", "#89654d", "#d4c1a1")},
    "uw_dealers_ledger": {"kind": "book", "palette": ("#2d2b3c", "#8bc5a3", "#e6debd")},
    "uw_sample_kit": {"kind": "kit", "palette": ("#2c3340", "#7dc8ff", "#f4e8c1")},
    "uw_antidote_vial": {"kind": "vial", "palette": ("#20363b", "#6dd7d1", "#eafcf8")},
    "uw_whispervine": {"kind": "leaf", "palette": ("#24362c", "#7ad59b", "#d9f7df")},
    "uw_dreamblossom": {"kind": "flower", "palette": ("#462d4f", "#f6a5d7", "#fff2fb")},
    "uw_shadowcap": {"kind": "mushroom", "palette": ("#1c2330", "#7185d6", "#ccd4ff")},
    "uw_crude_moonite": {"kind": "ore", "palette": ("#252735", "#9ab1d9", "#f0f5ff")},
    "uw_whisper_extract": {"kind": "powder", "palette": ("#223a33", "#98f0b6", "#eaf9ef")},
    "uw_dream_dust": {"kind": "powder", "palette": ("#43304c", "#f1b3dc", "#fff1f9")},
    "uw_shadow_resin": {"kind": "ore", "palette": ("#151924", "#7d87c7", "#d2d8ff")},
    "uw_whisper_tonic": {"kind": "vial", "palette": ("#1e3a34", "#79dfad", "#f0fff8")},
    "uw_dream_powder": {"kind": "powder", "palette": ("#412a45", "#f3bad9", "#fff6fb")},
    "uw_shadow_elixir": {"kind": "vial", "palette": ("#141b2b", "#7f98f8", "#f7fbff")},
    "uw_fixer": {"kind": "portrait", "palette": ("#1f2a36", "#8fd1bb", "#f4dcc2")},
}

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
    return AssetScaleSpec(
        category="item",
        canvas_size=(32, 32),
        visible_size=visible_size,
        anchor=ANCHOR_CENTER,
        render_data=render_data,
    )


def furniture(
    render_data: str = "@obj",
    canvas_size: tuple[int, int] = (48, 48),
    visible_size: tuple[int, int] | None = None,
) -> AssetScaleSpec:
    if visible_size is None:
        visible_size = (canvas_size[0] - 2, canvas_size[1] - 2)
    return AssetScaleSpec(
        category="item",
        canvas_size=canvas_size,
        visible_size=visible_size,
        anchor=ANCHOR_CENTER,
        render_data=render_data,
    )


def chara(visible_size: tuple[int, int], render_data: str = "@chara", ground_lift_px: int = 16) -> AssetScaleSpec:
    return AssetScaleSpec(
        category="chara",
        canvas_size=(128, 192),
        visible_size=visible_size,
        anchor=ANCHOR_BOTTOM_CENTER,
        render_data=render_data,
        ground_lift_px=ground_lift_px,
    )


ASSET_SCALE_SPECS: dict[str, AssetScaleSpec] = {
    "uw_mixing_table": furniture("@obj", (64, 64), (58, 58)),
    "uw_contraband_chest": furniture("@obj"),
    "uw_dealers_ledger": small_item("@obj_S flat"),
    "uw_sample_kit": small_item("@obj_S flat"),
    "uw_antidote_vial": small_item("@obj_S flat"),
    "uw_whispervine": small_item("@obj_S flat"),
    "uw_dreamblossom": small_item("@obj_S flat"),
    "uw_shadowcap": small_item("@obj_S flat"),
    "uw_crude_moonite": small_item("@obj_S flat"),
    "uw_whisper_extract": small_item("@obj_S flat"),
    "uw_dream_dust": small_item("@obj_S flat"),
    "uw_shadow_resin": small_item("@obj_S flat"),
    "uw_whisper_tonic": small_item("@obj_S flat"),
    "uw_dream_powder": small_item("@obj_S flat"),
    "uw_shadow_elixir": small_item("@obj_S flat"),
    "uw_fixer": chara((74, 110)),
}


ITEM_ASSETS = [
    {
        "id": "uw_mixing_table",
        "preview_size": (64, 64),
        "prompt": (
            "2D isometric RPG game furniture sprite, three-quarter top-down view angled from upper-right, "
            "a clandestine alchemical mixing table made from dark stained wood, cluttered with brass scales, "
            "glass flasks, wrapped herbs, handwritten recipe scraps, and a faint green lamp glow, "
            "showing the tabletop and front-left legs, pixel art style, deep walnut, oxidized brass, and teal glass palette, "
            "uniform flat fuchsia/magenta screen background, centered, clean edges, no text, no frame, game asset"
        ),
    },
    {
        "id": "uw_contraband_chest",
        "preview_size": (48, 48),
        "prompt": (
            "2D isometric RPG game furniture sprite, three-quarter top-down view angled from upper-right, "
            "an ordinary walnut chest with scuffed corners, iron banding, a discreet brass latch, and the suggestion of a false bottom, "
            "humble enough to avoid suspicion, showing top and front-left faces, pixel art style, brown wood and worn brass palette, "
            "uniform flat fuchsia/magenta screen background, centered, clean edges, no text, no frame, game asset"
        ),
    },
    {
        "id": "uw_dealers_ledger",
        "preview_size": (48, 48),
        "prompt": (
            "2D top-down RPG game item icon, a slim leather ledger with a cloth ribbon, dog-eared pages, wax-pencil tabs, "
            "and tiny coded annotations tucked inside, pixel art style, charcoal leather, faded parchment, and muted jade palette, "
            "uniform flat fuchsia/magenta screen background, centered, clean edges, no text, no frame, game asset"
        ),
    },
    {
        "id": "uw_sample_kit",
        "preview_size": (48, 48),
        "prompt": (
            "2D top-down RPG game item icon, a sample kit of folded paper wraps, miniature corked bottles, sealing wax, and a tiny metal scoop, "
            "laid out as one tidy travel bundle, pixel art style, smoke blue, cream paper, and pale brass palette, "
            "uniform flat fuchsia/magenta screen background, centered, clean edges, no text, no frame, game asset"
        ),
    },
    {
        "id": "uw_antidote_vial",
        "preview_size": (48, 48),
        "prompt": (
            "2D top-down RPG game consumable icon, a slim emergency antidote vial with translucent teal liquid, a wax-sealed stopper, "
            "and silver wire wrap, pixel art style, sea-glass teal, dark glass, and pale silver palette, "
            "uniform flat fuchsia/magenta screen background, centered, clean edges, no text, no frame, game asset"
        ),
    },
    {
        "id": "uw_whispervine",
        "preview_size": (48, 48),
        "prompt": (
            "2D top-down RPG game herb icon, a tied bundle of long whispervine leaves, delicate and aromatic, "
            "with pale veins and a few loose cuttings, pixel art style, deep green and mint palette, "
            "uniform flat fuchsia/magenta screen background, centered, clean edges, no text, no frame, game asset"
        ),
    },
    {
        "id": "uw_dreamblossom",
        "preview_size": (48, 48),
        "prompt": (
            "2D top-down RPG game herb icon, a soft nocturnal blossom with layered petals and a faint sleepy glow, "
            "gathered as a single clipped flower head, pixel art style, blush pink, plum, and moonlit ivory palette, "
            "uniform flat fuchsia/magenta screen background, centered, clean edges, no text, no frame, game asset"
        ),
    },
    {
        "id": "uw_shadowcap",
        "preview_size": (48, 48),
        "prompt": (
            "2D top-down RPG game mushroom icon, a dark shadowcap mushroom with a rounded cap, blue bioluminescent sheen, "
            "and pale stem, slightly toxic-looking but valuable, pixel art style, midnight blue and cold lilac palette, "
            "uniform flat fuchsia/magenta screen background, centered, clean edges, no text, no frame, game asset"
        ),
    },
    {
        "id": "uw_crude_moonite",
        "preview_size": (48, 48),
        "prompt": (
            "2D top-down RPG game ore icon, a raw chunk of moonite stone with fractured faces that absorb light and glow at the edges, "
            "irregular and unrefined, pixel art style, slate blue, silver, and pale lunar white palette, "
            "uniform flat fuchsia/magenta screen background, centered, clean edges, no text, no frame, game asset"
        ),
    },
    {
        "id": "uw_whisper_extract",
        "preview_size": (48, 48),
        "prompt": (
            "2D top-down RPG game alchemical reagent icon, a tiny stoppered vial of whisper extract with translucent green liquid and herbal sediment, "
            "pixel art style, teal-green, smoked glass, and ivory palette, uniform flat fuchsia/magenta screen background, centered, clean edges, no text, no frame, game asset"
        ),
    },
    {
        "id": "uw_dream_dust",
        "preview_size": (48, 48),
        "prompt": (
            "2D top-down RPG game alchemical powder icon, a neat crescent pile of dream dust with soft sparkling grains and sleepy pastel tone, "
            "pixel art style, pale rose, lavender ash, and pearl palette, uniform flat fuchsia/magenta screen background, centered, clean edges, no text, no frame, game asset"
        ),
    },
    {
        "id": "uw_shadow_resin",
        "preview_size": (48, 48),
        "prompt": (
            "2D top-down RPG game alchemical reagent icon, a glossy lump of shadow resin like hardened black tar with blue reflections and cracked surface highlights, "
            "pixel art style, black indigo and icy blue palette, uniform flat fuchsia/magenta screen background, centered, clean edges, no text, no frame, game asset"
        ),
    },
    {
        "id": "uw_whisper_tonic",
        "preview_size": (48, 48),
        "prompt": (
            "2D top-down RPG game potion icon, a corked tonic bottle of whisper tonic filled with muted green liquid and a soft inner shimmer, "
            "pixel art style, moss green, clear glass, and parchment palette, uniform flat fuchsia/magenta screen background, centered, clean edges, no text, no frame, game asset"
        ),
    },
    {
        "id": "uw_dream_powder",
        "preview_size": (48, 48),
        "prompt": (
            "2D top-down RPG game contraband icon, a folded paper pouch spilled open with a clean line of dream powder, refined and valuable, "
            "pixel art style, powder pink, ivory paper, and muted violet palette, uniform flat fuchsia/magenta screen background, centered, clean edges, no text, no frame, game asset"
        ),
    },
    {
        "id": "uw_shadow_elixir",
        "preview_size": (48, 48),
        "prompt": (
            "2D top-down RPG game potion icon, a dark glass elixir bottle holding shadow elixir with deep blue glow and dangerous intensity, "
            "pixel art style, black glass, electric blue, and cold white palette, uniform flat fuchsia/magenta screen background, centered, clean edges, no text, no frame, game asset"
        ),
    },
]


CHARA_ASSETS = [
    {
        "id": "uw_fixer",
        "preview_size": (96, 144),
        "prompt": (
            "2D front-facing RPG humanoid sprite, a composed fantasy underworld fixer in a layered dark coat with quiet luxury details, "
            "slender silhouette, gloved hands, discreet satchel, calm unreadable expression, no weapon drawn, full body, feet visible, complete silhouette, "
            "pixel art style, charcoal, muted jade, and warm parchment palette, uniform flat fuchsia/magenta screen background, centered, clean edges, no text, no frame, game sprite"
        ),
    },
]


def get_asset_spec(asset_id: str) -> AssetScaleSpec:
    try:
        return ASSET_SCALE_SPECS[asset_id]
    except KeyError as exc:
        raise KeyError(f"No deployment scale spec defined for {asset_id}") from exc
