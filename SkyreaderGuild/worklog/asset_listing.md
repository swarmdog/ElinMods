# Skyreader Guild Asset Listing

This document outlines the custom items, objects, and characters added by the Skyreader Guild mod that currently rely on stock *Elin* tiles. It also describes the technical specs required for custom replacement assets.

## How Custom Art Works in Elin
To replace a vanilla tile reference with a custom sprite, you typically provide a `.png` file named exactly after the object's `id` (e.g., `srg_astral_extractor.png`) and place it inside your mod's `Texture/Item/` or `Texture/Chara/` directories. 
- **`obj_S` / `obj_S flat`**: Generally uses a 32x32px sprite sheet format.
- **`obj` / `obj tall`**: Generally uses a 64x64px sprite sheet format.
- **`chara`**: Standard character sprites (typically 32x48px per frame if using standard RPG Maker-like sets, or 48x48px).
- **`chara_L`**: Large character sprites (usually double standard size, e.g. 64x96px or larger).

---

## Items and Objects

### 1. Astral Extractor (`srg_astral_extractor`)
* **Category:** Tool
* **Current Vanilla Tile:** `1611` (Teleporter pad)
* **Render Data:** `obj_S flat`
* **Asset Needed:** A 32x32px `.png` depicting a handheld astronomical apparatus or golden sextant that sits flat on the ground.

### 2. Meteorite Fragment (`srg_meteorite_fragment`)
* **Category:** Crafting Material (rubble/ore)
* **Current Vanilla Tile:** `1208` (Generic unrefined stone/gem)
* **Render Data:** `obj_S flat`
* **Asset Needed:** A 32x32px `.png` of a glowing, cracked piece of cosmic stone.

### 3. Meteorite Source (`srg_meteorite_source`)
* **Category:** Rare Resource (refined)
* **Current Vanilla Tile:** `503` (Crystal/Shard)
* **Render Data:** `obj_S`
* **Asset Needed:** A 32x32px `.png` of a purified, crystalline star fragment. Given `obj_S` (not flat), it renders slightly raised/embedded on the cell.

### 4. Astral Thread (`srg_astral_thread`)
* **Category:** High-Level Material
* **Current Vanilla Tile:** `503` (Dust/Fiber/Crystal)
* **Render Data:** `obj_S`
* **Asset Needed:** A 32x32px `.png` of a glowing, silvery strand or spool of cosmic silk.

### 5. Astrological Codex (`srg_astrological_codex`)
* **Category:** Crafting Station / Furniture
* **Current Vanilla Tile:** `504` (Ancient heavy book/stand)
* **Render Data:** `obj_S flat`
* **Asset Needed:** A 32x32px or 64x64px depending on scaling, but since it's `obj_S flat`, 32x32px of a large, opened tome brimming with shifting star maps.

### 6. Archivist's Scroll / Boss Summon Scroll (`srg_archivist_scroll` / `srg_boss_scroll`)
* **Category:** Consumable Magic Scroll
* **Current Vanilla Tile:** `530` (Standard rolled scroll)
* **Render Data:** `obj_S flat`
* **Asset Needed:** A 32x32px `.png` of a distinctly colored or glowing scroll (e.g., deep blue with silver trim) to differentiate it from basic fast-travel scrolls.

### 7. Yith Starchart (`srg_starchart`)
* **Category:** Quest / Map Item
* **Current Vanilla Tile:** `1552` (Standard treasure map)
* **Render Data:** `obj`
* **Asset Needed:** A 64x64px `.png` of a large, intricately drawn map outlining constellations and glowing impact zones.

### 8. Astral Portal (`srg_astral_portal`)
* **Category:** Interactive Furniture / Portal
* **Current Vanilla Tile:** `751` (Demonic Voidgate / red portal)
* **Render Data:** `obj tall`
* **Asset Needed:** A 64x64px (or taller) `.png` sprite of a torn rift in reality or a swirling blue/silver galactic gateway.

---

## Characters and Monsters

### 9. Arkyn (`srg_arkyn`)
* **Category:** Unique NPC
* **Current Vanilla Tile:** `806` (Alien / Yithian-adjacent)
* **Render Data:** (Inherited from `CharaGen`)
* **Asset Needed:** A standard `chara` format sprite sheet (e.g. 32x48) depicting an eccentric, star-crazed scholar with specialized garb.

### 10. The Archivist (`srg_archivist`)
* **Category:** Unique Guild NPC
* **Current Vanilla Tile:** `1502` (Scholarly/Mage NPC)
* **Render Data:** (Inherited)
* **Asset Needed:** A standard `chara` format sprite sheet depicting a mystical sage or celestial archivist holding a star map.

### 11. Yith Monster Entities (`srg_yith_hound` to `srg_yith_behemoth`)
* **Category:** Mob Progression Line
* **Current Vanilla Tiles:**
    * Hound (`srg_yith_hound`): `2823` (standard format)
    * Drone (`srg_yith_drone`): `1627` (standard format)
    * Weaver (`srg_yith_weaver`): `16` (`chara_L` format)
    * Ancient (`srg_yith_ancient`): `15` (`chara_L` format)
    * Behemoth (`srg_yith_behemoth`): `110` (`chara_L` format)
* **Asset Needed:** A cohesive set of custom sprite sheets tracking the evolution of a cosmic entity. Smaller variants use standard `chara` grids (32x48px equivalents), while the Weaver, Ancient, and Behemoth use `chara_L` (64x96px or scale equivalent) assets showing horrific, cosmic abominations.

# Skyreader Guild Asset Generation

**Inventory:** We have 11 assets (IDs and needed sprite sizes) to create. All item/object sprites are either **32×32px** or **64×64px** (for “flat” or “tall” tiles), and character sprites use RPG-Maker style grids (e.g. 32×48px per frame for standard characters, 64×96px for large). In summary:

- **Astral Extractor** (`srg_astral_extractor`): *Tool* – 32×32px icon (`obj_S flat`).
- **Meteorite Fragment** (`srg_meteorite_fragment`): *Material* – 32×32px icon.
- **Meteorite Source** (`srg_meteorite_source`): *Rare resource* – 32×32px icon (slightly raised).
- **Astral Thread** (`srg_astral_thread`): *High-level material* – 32×32px icon.
- **Astrological Codex** (`srg_astrological_codex`): *Furniture/Station* – 32×32px icon (open star-map book).
- **Archivist’s Scroll** (`srg_archivist_scroll`) and **Boss Summon Scroll** (`srg_boss_scroll`): *Consumables* – 32×32px icons (distinctly colored scrolls).
- **Yith Starchart** (`srg_starchart`): *Quest item* – 64×64px icon (large detailed map).
- **Astral Portal** (`srg_astral_portal`): *Portal* – 64×64px (or taller) icon (swirling rift/gateway).
- **Characters (Arkyn, Archivist)** (`srg_arkyn`, `srg_archivist`): *NPCs* – sprite sheets in `chara` format (each frame 32×48px).
- **Yith Monsters (pup to ancient)**: *Mob variants* – sprite sheets in `chara` (small forms, 32×48px per frame) or `chara_L` (large forms, ~64×96px per frame).

After generation, name each PNG exactly as the `id` (e.g. `srg_astral_extractor.png`) and place it in the mod folder:
- **Texture/Item/** for items and objects (IDs 1–8 above)  
- **Texture/Chara/** for character sprites (IDs 9–11).  

## Image Generation Tools

The **best free generators** include:

- **Google Gemini (“Nano Banana”)** – Google’s latest text-to-image model. *Nano Banana 2* (Gemini 3.1 Flash Image) is accessible in Gemini/Google AI Studio and can generate high-quality art via an API. There is a free tier (up to ~20 images/day)【15†L296-L304】. The Gemini API is used via Google’s GenAI SDK or REST API【25†L300-L308】. This is ideal given your Google Ultra subscription.
- **Stable Diffusion** – Open-source and free. You can run it locally (no API needed) or use third-party APIs (Hugging Face Inference, Replicate, etc.). Stable Diffusion yields great results and has no built-in limits【33†L44-L50】, but requires a GPU or API setup.
- **DeepAI Image API** – A free online text-to-image API (no sign-up needed to explore; API keys available)【20†L131-L134】. It provides decent quality and is simple to integrate via REST.
- **Pollinations.ai** – A free multi-modal API (text, image, audio, video). Sign up at pollinations.ai to get an API key. Pollinations lets you call various models and is free to start.
- (Other options like DALL·E or Midjourney require paid plans; for *free usage* Google Gemini or Stable Diffusion are generally best.)

In practice, **Google’s Gemini (Nano Banana)** is recommended. It handles complex prompts well and can be scripted via the GenAI Python SDK or REST API. For example, using the GenAI SDK:  
```python
client = genai.Client()
response = client.models.generate_content(
    model="gemini-3.1-flash-image-preview",
    contents=["A 32×32 icon of a glowing golden sextant used for star navigation"]
)
```
【25†L320-L328】. The `response.parts` will include an inline image that you can save as a PNG.

DeepAI’s API can be used as a fallback (e.g. `requests.post("https://api.deepai.org/api/text2img", ...)`). Pollinations requires its own endpoints. Stable Diffusion can be used via the `diffusers` library or Hugging Face Inference (for example, with the `stabilityai/stable-diffusion-xl` model on HuggingFace, requiring an `HUGGINGFACE_API_KEY`).

## Automated Generation Script

Below is an example Python script that uses **Google’s GenAI SDK** to generate each asset from a prompt, resizes to the correct dimensions, and saves the file. Adjust the prompts as needed to match the “Asset Needed” descriptions. *(You will need to install the SDK: `pip install google-genai`.)*  

```python
from google import genai
from google.genai import types
from PIL import Image
import os

# **Set your Gemini API key in the environment** (or set GEMINI_API_KEY directly).
# os.environ["GEMINI_API_KEY"] = "YOUR_API_KEY_HERE"

client = genai.Client()

# List of items/objects: id, prompt description, target size, folder
assets = [
    {"id": "srg_astral_extractor",
     "prompt": "A handheld astronomical device or golden sextant lying flat, glowing softly",
     "size": (32, 32),
     "folder": "Texture/Item"},
    {"id": "srg_meteorite_fragment",
     "prompt": "A cracked, glowing piece of cosmic stone or meteorite fragment, 32x32 icon",
     "size": (32, 32),
     "folder": "Texture/Item"},
    {"id": "srg_meteorite_source",
     "prompt": "A purified crystalline star fragment, shining with inner light, 32x32 icon",
     "size": (32, 32),
     "folder": "Texture/Item"},
    {"id": "srg_astral_thread",
     "prompt": "A spool of glowing silvery cosmic thread or silk, 32x32 icon",
     "size": (32, 32),
     "folder": "Texture/Item"},
    {"id": "srg_astrological_codex",
     "prompt": "An open ancient tome brimming with shifting star maps, 32x32 icon",
     "size": (32, 32),
     "folder": "Texture/Item"},
    {"id": "srg_archivist_scroll",
     "prompt": "A magical scroll with deep blue paper and silver trim, 32x32 icon",
     "size": (32, 32),
     "folder": "Texture/Item"},
    {"id": "srg_boss_scroll",
     "prompt": "A glowing purple scroll inscribed with mystical symbols, 32x32 icon",
     "size": (32, 32),
     "folder": "Texture/Item"},
    {"id": "srg_starchart",
     "prompt": "A large treasure map showing constellations and glowing impact zones, 64x64 icon",
     "size": (64, 64),
     "folder": "Texture/Item"},
    {"id": "srg_astral_portal",
     "prompt": "A swirling blue-and-silver rift in reality, an astral portal, 64x64 icon",
     "size": (64, 64),
     "folder": "Texture/Item"}
]

# Generate item/object images (square icons). Use a small canvas (512px) and then downscale.
for asset in assets:
    config = types.GenerateContentConfig(
        image_config=types.ImageConfig(
            image_size="512",       # generate 512×512
            aspect_ratio="1:1"      # square
        )
    )
    response = client.models.generate_content(
        model="gemini-3.1-flash-image-preview",
        contents=[asset["prompt"]],
        config=config
    )
    # Save the image (resize to exact required size)
    for part in response.parts:
        if part.inline_data:
            img = part.as_image()  # PIL Image
            img = img.resize(asset["size"], Image.Resampling.LANCZOS)
            os.makedirs(asset["folder"], exist_ok=True)
            img.save(f"{asset['folder']}/{asset['id']}.png")

# Characters: prompts for Arkyn, Archivist, and Yith variants.
characters = [
    {"id": "srg_arkyn",
     "prompt": "An eccentric star-crazed scholar with robes and telescope, front-facing sprite",
     "size": (32, 48)},
    {"id": "srg_archivist",
     "prompt": "A mystical sage or archivist holding a celestial star map, front-facing sprite",
     "size": (32, 48)},
    {"id": "srg_yith_pup",
     "prompt": "A small cosmic hound creature, alien beast sprite",
     "size": (32, 48)},
    {"id": "srg_yith_grow",
     "prompt": "A larger evolving alien beast, mid-stage Yith monster,  front-facing sprite",
     "size": (48, 72)},
    {"id": "srg_yith_ancient",
     "prompt": "A massive horrific cosmic abomination, ancient Yithian monster",
     "size": (64, 96)}
]

# Generate character images (vertical sprites). Use aspect_ratio 2:3 for height > width.
for char in characters:
    config = types.GenerateContentConfig(
        image_config=types.ImageConfig(
            image_size="512",
            aspect_ratio="2:3"   # tall sprite (width:height = 2:3)
        )
    )
    response = client.models.generate_content(
        model="gemini-3.1-flash-image-preview",
        contents=[char["prompt"]],
        config=config
    )
    for part in response.parts:
        if part.inline_data:
            img = part.as_image()
            img = img.resize(char["size"], Image.Resampling.LANCZOS)
            os.makedirs("Texture/Chara", exist_ok=True)
            img.save(f"Texture/Chara/{char['id']}.png")
```

**Notes on the script:** You must install the Google GenAI SDK (`pip install google-genai`) and have your `GEMINI_API_KEY` set (see below). The script generates high-resolution images (512×512) and then downsamples to the specified sprite size for pixel-art consistency. Adjust the text `prompt` to get better style/art; you can tweak “Golden Sextant”, “Glowing Scroll”, etc. as needed.

## API Keys and Authentication

- **Google Gemini API:** Sign in to [Google AI Studio](https://aistudio.google.com/) and create a Gemini (Generative Language) API key under your project【31†L287-L295】. Then set it as an environment variable: e.g. `export GEMINI_API_KEY="YOUR_KEY"` (Linux/macOS)【31†L287-L295】. The above Python code uses `genai.Client()` which automatically reads this env var. No OAuth is needed for simple API key use.

- **Stable Diffusion (Hugging Face) [Optional]:** If you use Hugging Face’s inference API, generate an access token from your Hugging Face account settings. Set `HUGGINGFACE_API_KEY` in the environment or use it in the headers. Hugging Face’s free tier allows limited usage.

- **DeepAI API (Optional):** DeepAI offers API keys via signup on deepai.org. If used, include your key in request headers (`api-key: YOUR_DEEPAI_KEY`). DeepAI’s free tier has rate limits but is straightforward to call via REST (see [DeepAI docs](https://deepai.org/machine-learning-model/text2img)). Their site even advertises *“Free Online AI Image Generator”* you can try without signup【20†L131-L134】, but for scripting you’ll want the API key.

- **Pollinations API (Optional):** Register at pollinations.ai and find your API key in your dashboard. Use Pollinations’ REST endpoints for text-image generation (API docs at enter.pollinations.ai).

## Usage Instructions

1. **Set up credentials:** As above, ensure your Google Gemini API key is set (`GEMINI_API_KEY`). (No login needed each run.)  

2. **Run the script:** Execute the Python script. It will save PNG files in `Texture/Item/` and `Texture/Chara/`. Adjust prompts or model parameters if any image needs tweaking.

3. **Verify dimensions:** Each generated image is resized to the exact pixel dimensions required (32×32, 64×64, or character sprite sizes). Ensure the sprite’s style fits the 32×32 or 64×64 grid – complex scenes may not fully translate to tiny icons, so keep prompts simple and iconic.

4. **Place files in mod directories:**  
   - All **item/object** images (`srg_*.png` for IDs 1–8) go into your mod folder under `Texture/Item/`.  
   - All **character** images (`srg_*.png` for IDs 9–11) go under `Texture/Chara/`.  
   Name them exactly by ID (`srg_astral_extractor.png`, etc.) as per the mod’s requirements (the engine uses the filename to match the object ID). 

5. **Test in-game:** Start the mod and verify each custom sprite appears in place of the old Elin tile. You may need to clear caches or reload assets.

**Sources:** Google’s GenAI documentation provides code examples【25†L320-L328】 and explains the Nano Banana models【25†L300-L308】. DeepAI advertises its free generator【20†L131-L134】. The Gemini API key setup is documented by Google【31†L287-L295】. Adjustments and prompts are based on the asset descriptions given.