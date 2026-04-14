# The Skyreader's Guild - Feature Documentation
*An overview of the systems, items, and mechanics implemented in the SkyreaderGuild mod for Elin.*

## 1. Meteor Mechanics and Impact Sites
The core hook of the mod revolves around falling meteors and the cosmic energy they leave behind.
- **Meteor Showers**: Every day, there is a base chance (15%, +5% at Researcher rank) for a meteor to fall on the overworld map. 
- **Meteor Impact Sites**: Temporary zones (`srg_meteor`) that spawn near the player as a result of meteor showers.
  - They feature a large crater containing a central **Meteor Core** (`srg_meteor_core`), scattered debris, junk loot, and raw ores.
  - The impact site expires shortly after the player harvests the central Meteor Core.

## 2. The Skyreader's Guild Quest
Players can join the ranks of an ancient order dedicated to the study of the stars.
- **Initiation**: High-level Nefias (Danger Lv 15+) have a chance to spawn a **Yith Growth**. Defeating it yields a **Star Chart**. Reading this chart initiates the questline and summons **Arkyn, Keeper of Stars**.
- **Guild Progression**: Players earn **Guild Points** by extracting starlight from the world and defeating astral entities.
  - **Ranks**: Wanderer ➔ Seeker ➔ Researcher ➔ Cosmos Addled ➔ Cosmos Applied ➔ Understander ➔ Principal Starseeker.
  - Arkyn acts as a wandering adventurer early on but eventually settles in the Guild HQ.

## 3. Meteor-Touched Entities and the Astral Extractor
Starlight radiation clings to characters and objects in the world.
- **Meteor-Touched Tagging**: When visiting civilized towns, there is a chance (30%) for up to three NPCs or items to become secretly "meteor-touched."
- **Astral Sensing**: If the player is holding an **Astral Extractor**, they receive proximity alerts ("A whisper of starlight brushes your awareness...") when near touched targets.
- **Extraction**: Using the Astral Extractor on a touched target cleanses it, granting the player Guild Points and dropping **Meteorite Sources**.
- **Skysign Effects (RNG)**: Cleansing a target triggers a random cosmic phenomenon:
  - *Dimensional Gateway*: Tears open a stabilized portal to an Astral Rift.
  - *Astral Exposure*: Transmutes nearby random objects/inventory items into higher-tier materials.
  - *Cosmic Alignment*: Grants the player a temporary buff to their core stats.
  - *Cosmic Attunement*: Permanently boosts the elemental resistances of the cleansed NPC.
  - *Medical Success*: Hugely boosts the cleansed NPC's affinity/relationship toward the player.

## 4. Astral Rifts (Nefia Variants)
Accessed via portals created by the Extractor, Astral Rifts (`srg_astral_rift`) are dimensional dungeons.
- **Thematic Map Generation**: Unlike standard rectangular Elin dungeons, Astral Rift rooms are algorithmically reshaped into cosmic geometry: Circles, Ellipses, Diamonds, Crescents, Crosses, and Stars using custom Cellular Automata.
- **Yith Invasions**: Rifts bypass normal spawning to guarantee packs of alien Yith monsters.
- **Yith Monster Tiers**: The entities encountered scale with the rift's Danger Level:
  - *Yith Hound* (Base)
  - *Yith Drone* (Lv 15+)
  - *Yith Weaver* (Lv 30+)
  - *Yith Ancient* (Lv 50+)
  - *Yith Behemoth* (Lv 75+)
- Extra meteorite sources are found scattered across the layout.

## 5. The Skyreader Observatory (Guild HQ)
A dynamically expanding, hand-built guild hall (`srg_guild_hq`) located in the world.
- **Dynamic Architecture**: As the player's Guild Rank increases, magical stone barriers dissipate, physically opening new rooms within the compound:
  - **Atrium** (Wanderer): Houses the central Nexus Core, Telescopes, and Celestial Globes.
  - **Study Hall** (Seeker): Contains Starfall Tables and Lunar Armchairs.
  - **Observatory** (Researcher): Contains the Zodiac Dresser, Planisphere Cabinets, and Cosmic Mirrors.
  - **Astral Forge** (Cosmos Addled): Features the Astral Chandelier, Stardust Bed, and Meteorite Statues.
  - **Starseeker Sanctum** (Cosmos Applied): The most secluded chamber containing the Eclipse Hearth.
- **NPC Expansion**: At the rank of Understander, the **Archivist** moves into the Observatory, offering a 20% discount on identifying items.

## 6. Astral Bosses
Players can obtain unique summoning scrolls (Scroll of Twilight, Radiance, the Abyss, or the Nova) that initiate multi-stage summoning events.
- **Boss Entities**: The scrolls summon Umbryon, Solaris, Erevor, and Quasarix respectively.
- **Rewards**: Defeating these massive celestial entities yields large sums of Guild Points and scatterings of Meteorite raw materials.
