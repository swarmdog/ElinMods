# SkyreaderGuild Async Multiplayer – Focused Feature Design


Key constraints:

- **Purely asynchronous, HTTP-based backend**  thin Elin client, stateless REST API, small database
- **Authentication is entirely invisible to the player.** The mod auto-registers and auto-authenticates in the background; there are no login prompts or credentials.
- **Low server load:** clients poll infrequently (primarily once per in-game week for seasonal data) and batch writes where possible.[^1]
- **Lore-first UX:** all networked features are surfaced through Skyreader Guild HQ furniture and dialogs rather than generic menus.


## 2. Invisible Authentication and Account Model

### 2.1 Guild account lifecycle


**Data model (server):**

- `GuildAccount`
  - `id`: UUID primary key.
  - `installKey`: string, opaque, unique per installation (see below).
  - `displayName`: nullable string; optional if you later want cosmetic names.
  - `highestRank`: int (numeric encoding of Skyreader guild rank).
  - `createdAt`, `lastSeenAt`: timestamps.

**Client-side identity:**

- On first load of a save where the player joins the Skyreader Guild, the plugin checks for an existing `installKey` in a small config file under the mod folder.
- If none exists, it generates a random GUID and stores it locally as `installKey`.
- The client calls `POST /guild/register-anon` with `{ installKey, gameVersion, modVersion }` once; server returns `{ authToken, accountId }`.
- `authToken` is saved in the same local config and attached as a header (e.g., `Authorization: Bearer <token>`) on all subsequent requests.
- must be resilent to server outage, change, and reset (server may reset database)
### 2.2 Authentication endpoints

- `POST /guild/register-anon`
  - Request: `{ installKey, gameVersion, modVersion }`.
  - Behavior: if `installKey` exists, return existing `GuildAccount` and new `authToken`; else create and return a new account.

- `POST /guild/refresh-token`
  - Used rarely if the server invalidates tokens; the client can supply its `installKey` and receive a fresh `authToken` without player interaction.

The plugin wraps these flows in a small `SkyreaderAuthManager` class; if any request returns `401 Unauthorized`, it silently calls `refresh-token` and retries the original request once.


## 3. Shared Data Model Overview

The following shared entities support all chosen features:

- `GuildAccount` – identity and highest rank attained (Section 2).
- `AstralContribution` – normalized "starlight" contributions used to build ladders and constellation progress.
- `Constellation` and `ConstellationProgress` – seasonal allegiance system and cooperative goals.
- `SkySeason` – global seasonal sky phenomena and modifiers.
- `GeometrySample` – observations of Astral Rift geometry.
- `CometHeatBucket` – coarse heatmap representation of comet‑touched activity across the world.
- `ResearchNote` – short player-authored lore snippets for the guild library.

Each section below details how individual features map onto these entities.


## 4. Feature A – Global Skyreader Ladder (Refined)

### 4.1 Concept

The ladder mirrors the Underworld mod’s global reputation tracking but uses a single normalized "Starlight Score" derived from guild points, rift clears, meteor core harvests, and related actions. The ladder is non-intrusive and purely comparative; it does not gate content.[^1]

### 4.2 Data model

- `AstralContribution`
  - `id`: UUID.
  - `playerId`: foreign key to `GuildAccount`.
  - `type`: enum (`Extraction`, `RiftClear`, `BossKill`, `MeteorCoreHarvest`).
  - `amount`: int (server interprets this as starlight units; client simply sends raw guild point equivalents).
  - `createdAt`: timestamp.

- `LadderSnapshot` (materialized view or periodically updated table)
  - `playerId`, `totalScore`, `lastUpdatedAt`.

### 4.3 API endpoints

- `POST /contributions/batch`
  - Body: `{ contributions: [{ type, amount, localEventId, timestamp }] }`.
  - Auth via header.
  - Server validates ranges (e.g., clamps any `amount` above a configured ceiling) and inserts records.

- `GET /ladder/global?limit=50`
  - Returns top N players (`displayName` or anonymized ID plus `totalScore`).

- `GET /ladder/self`
  - Returns the caller’s rank, total score, and percentile.

### 4.4 Elin-side integration

- **Buffering:** the plugin maintains a small in-memory queue of `AstralContribution` entries and periodically flushes them:
  - Flush trigger: player rests at an inn or at the Guild HQ; or every in-game week if the player never rests.
- **Sources:** whenever the player earns guild points for:
  - Using the Astral Extractor on meteor‑touched targets.
  - Clearing Astral Rifts.
  - Defeating astral bosses.
  the plugin enqueues a contribution with the matching type/amount.
- **UI:**
  - In the Guild HQ, add a "Skyreader Ladder" plaque or wall chart.
  - Interacting opens a simple list menu displaying:
    - Player’s current score and percentile.
    - Top 20 entries.
  - If network is unavailable, display "The stars are quiet today" and skip display logic.


## 5. Feature B – Constellation Allegiances and Seasons

### 5.1 Concept

Constellations act as light-weight, lore-friendly "teams" for each real-time season. Contributions already sent to the ladder are also grouped by the patron constellation of each account, driving cooperative goals like "The Hound constellation seeks 200,000 starlight in Rift clears this season."

### 5.2 Data model

- `Constellation`
  - `id`: UUID.
  - `seasonId`: foreign key to `SkySeason`.
  - `name`, `description`.
  - `goalConfig`: JSON, example:
    - `{ "Extraction": 150000, "RiftClear": 80000 }`.

- `ConstellationMembership`
  - `playerId`, `constellationId`, `joinedAt`.

- `ConstellationProgress`
  - `constellationId`, `metricType`, `currentAmount`.

### 5.3 API endpoints

- `GET /constellations/current`
  - Returns all constellations active in the current `SkySeason`, plus their goal config and current progress.

- `POST /constellations/join`
  - Body: `{ constellationId }`.
  - Server ensures the account either has no constellation for the current season or changing is disallowed after a grace period.

Server-side, a background job periodically aggregates contributions into `ConstellationProgress` based on each contributor’s current membership.  We should consider triggering this on demand as we may run this on google cloud run or another hosting platform where we prefer to calculate in a catch up rather than run background jobs.  Figure out the best design for our use case.

### 5.4 Elin-side integration

- **Unlock moment:** upon reaching rank `Seeker`, Arkyn fires a one-time dialog event presenting 3–5 patron constellations with short descriptions.
- **Choice UI:** choosing one calls `POST /constellations/join` and stores the selected `constellationId` locally as well.
- **Guild HQ UI:**
  - A "Constellation Board" in the Observatory displays:
    - The player’s chosen constellation, its symbol, and flavor text.
    - Seasonal goals and progress bars for that constellation.
    - Relative progress of other constellations as small bars or percentages (optional).
    - new furniture must be integrated properly - see our leaderboard and python asset generation and item scripts
- **Rewards:** when the server marks a constellation’s goals as met for the season, it exposes a flag in `GET /constellations/current`. The plugin checks this and, if the player is a member, grants small client-side rewards:
  - Cosmetic titles.
  - Decorative furniture recipes.
  - Valuable item reward such as a rare crafting item and meteorite source


## 6. Feature C – Astral Geometry Observation Network

### 6.1 Concept

Instead of a rift layout library, Astral Rift geometry is distilled into simple shape tags that contribute to a shared "geometry sky map". Globally, players push and pull the balance of shapes (Circles, Stars, Crescents, etc.), and the Guild HQ’s Observatory displays which geometries are currently ascendant.

### 6.2 Data model

- `GeometrySample`
  - `id`.
  - `playerId`.
  - `dangerBand`: small int bucket (e.g., 1 for Danger 1–10, 2 for 11–20, etc.).
  - `shapeType`: enum (`Circle`, `Ellipse`, `Diamond`, `Crescent`, `Cross`, `Star`).
  - `roomCount`: int.
  - `sampledAt`: timestamp.

- `GeometryAggregate`
  - `seasonId`.
  - `dangerBand`.
  - `shapeType`.
  - `count`: int.

A cron job periodically recomputes percentages per band and shape for the current `SkySeason`.  Same concern about a "cron job" as about background jobs.  We should prepare to run this without a background thread/cron job if possible.

### 6.3 API endpoints

- `POST /geometry/sample`
  - Body: `{ dangerBand, shapeType, roomCount }`.

- `GET /geometry/summary?seasonId=current`
  - Returns aggregate percentages, e.g.: `{ "1": { "Circle": 0.4, "Star": 0.2, ... }, ... }`.

### 6.4 Elin-side integration

- **Sampling:**
  - When generating an Astral Rift, the custom map generator already determines a primary shape (circle/star/diamond/etc.).
  - After generation, the plugin computes `dangerBand` and `roomCount`, then enqueues one `GeometrySample`.
  - Samples are batched and sent alongside contributions at rest or weekly.

- **Observatory UI:**
  - Add a "Geometry Orrery" in the Observatory. (note: for this and all furniture/visual assets we must reference our existing asset generation scripts)
  - Interacting calls `GET /geometry/summary` (if cached data is older than one in-game week) and displays:
    - For each shape: a bar or value indicating global share this season.
    - Flavor text, e.g., "Star-shaped rifts blaze across the firmament this season" when `Star` exceeds a threshold.

- **Minor gameplay hooks (optional):**
  - If a shape’s global share crosses e.g. 50% in a danger band relevant to the player’s current rift, the plugin can slightly bias Skysign effects or Yith spawn mixes in that rift, framed as "geometry resonance".  Note: This sound cool if properly researched and detailed.


## 7. Feature D – Comet‑Touched Heatmap and Cleanup Indicator

### 7.1 Concept

Meteor/comet-touched entities already exist via the Astral Extractor system. The async layer aggregates how much comet‑touch remains globally by coarse region, and how much players are cleaning up. The Guild HQ displays a **heatmap** of comet activity; as the player and others cleanse more, the map cools.

### 7.2 Data model

- `CometRegion`
  - Static table defining coarse regions (e.g., `Northlands`, `Coastal Belt`, `Forest Ring`, etc.). Each region maps to a world-coordinate band and possibly a town archetype.

- `CometHeatBucket`
  - `seasonId`.
  - `regionId`.
  - `touchedReports`: int (how many comet‑touched entities were detected).
  - `cleansedReports`: int (how many were cleansed).

### 7.3 API endpoints

- `POST /comet/report`
  - Body: `{ regionId, touchedCount, cleansedCount }`.

- `GET /comet/heatmap?seasonId=current`
  - Returns heat ratios per region, e.g. `{ regionId: { touched: 1000, cleansed: 800 } }`.

Server logic may decay counts slowly over real time to keep the heatmap responsive and prevent saturation.

### 7.4 Elin-side integration

- **Tracking:**
  - The plugin determines `regionId` from the current overworld position or town archetype.
  - While in a region, it tracks two counters for the current session:
    - `touchedSeen`: counts newly spawned or discovered meteor‑touched NPCs/items.
    - `touchedCleansed`: counts successful Astral Extractor cleanses.
  - On leaving the region, resting, or after an in-game day, the plugin posts a `comet/report` with the delta values and resets local counters.

- **Heatmap visualization:**
  - In the Guild HQ Atrium, add a giant star chart or world map table.
  - Interacting calls `GET /comet/heatmap` if cached data is older than an in-game week.
  - The UI shades each region in 3–4 discrete levels (e.g., Calm, Stirring, Troubled, Overrun) based on the ratio `cleansed / max(touched, 1)` and/or total touched volume.
  - Player-local contributions can be slightly up-weighted so that cleaning efforts feel immediately reflected.

- **Feedback hooks:**
  - When entering a region considered "Calm" globally, Arkyn or a system message notes the player is walking in "well-tended skies".
  - Conversely, "Overrun" regions occasionally spawn extra meteor‑touched targets, giving players more opportunities to improve the map.

Plan must properly detail integration here.

## 8. Feature E – Star Papers and Guild HQ Library

### 8.1 Concept

Star Papers are short, player-authored research notes about phenomena encountered while playing (Skysign interactions, odd Yith behavior, rare meteor events). They are uploaded anonymously and re‑distributed as physical documents in the Guild HQ library, adding asynchronous lore flavor.

### 8.2 Data model

- `ResearchNote`
  - `id`.
  - `playerId`.
  - `title`: short string.
  - `body`: markdown or plain text, length-limited (e.g., 500–800 characters).
  - `createdAt`.
  - `rating`: aggregate of up/down votes.

### 8.3 API endpoints

- `POST /research-notes/create`
  - Body: `{ title, body }`.
  - Server performs profanity filtering and length validation.

- `GET /research-notes/pull?limit=K`
  - Returns K notes not previously pulled by this account, or rotates periodically.

- `POST /research-notes/rate`
  - Body: `{ noteId, value }` where `value` is `+1` or `-1`.

### 8.4 Elin-side integration

- **Writing papers:**
  - Unlock a "Submit Star Paper" option once the player reaches rank `Understander` and has observed at least N Skysign events.
  - Activating this option opens a simple text-entry UI limited to title and short body.
  - On submission, the plugin sends a `create` request; if successful, the player receives a small amount of guild points and an in-world `Star Paper (Copy)` item.
  - Note: this probably needs to be represented with an in game note that is awarded or crafted somehow.  The client can rate limit how often it is used, and we don't mind if its consumed even if the create request fails.  proper asset generation and deployment will be needed.

- **Library distribution:**
  - Periodically (e.g., once per in-game week when the player visits the HQ), the plugin calls `GET /research-notes/pull` and stores 3–5 notes locally.
  - It then spawns corresponding `Star Paper (Research)` items on library shelves; each paper, when read, displays the note’s content.

- **Rating:**
  - After reading a paper, the player can choose "Mark as Insightful" or "Discard"; these map to a `rate` call with `+1` or `0` (only positive feedback is sent if you want to keep the system simple).
  - The server can eventually prefer higher-rated notes when choosing which to distribute.  Note: We have plenty of time - don't leave any features incomplete.


## 9. Feature F – Seasonal Sky Phenomena with Weekly Polling

### 9.1 Concept

The world operates under a global `SkySeason` (e.g., "Season of Crimson Showers"), each with its own modifiers and flavor. Seasons last for a real-time duration (e.g., weeks or months) and are evaluated entirely server-side. Clients only need to check in occasionally, roughly once per in-game week, to synchronize the active season and its modifiers.

### 9.2 Data model

- `SkySeason`
  - `id`.
  - `name`.
  - `description`.
  - `startsAt`, `endsAt` (UTC timestamps).
  - `modifiers`: JSON, such as:
    - `{ "meteorChanceMultiplier": 1.3, "skysignDimensionalGatewayWeight": 1.5, "yithSpawnBonus": { "Weaver": 0.2 } }`.

Server jobs ensure exactly one active season at any given time.  Note: another job/background concern.  Should be ready to do this statelessly in our plan.  Elin also may have some seasonal rotation we can leverage.

### 9.3 API endpoint

- `GET /sky-season/current`
  - Returns the active `SkySeason` object (or a default season if none configured).

### 9.4 Elin-side integration and polling cadence

- **Polling cadence:**
  - The plugin caches the active `SkySeason` plus its `endsAt` field locally.
  - It only calls `GET /sky-season/current` when:
    - No cached season exists, or  
    - In-game time has advanced by at least one in-game week since the last successful fetch, or
    - The cached `endsAt` time has passed in real time.
  - This reduces server load while ensuring that long-running saves still adapt to new seasons.
  - Note: our caching system should be resilent enough to handle swapping or fresh servers just like auth.

- **Applying modifiers:**
  - At the start of each in-game day, the plugin reads the cached season modifiers and applies them to local systems:
    - Multiply the base meteor shower daily chance by `meteorChanceMultiplier` (capped to avoid absurd values).
    - Adjust Skysign RNG weighting according to configured weights.
    - Slightly adjust Yith spawn composition in Astral Rifts using `yithSpawnBonus` percentages.
  - These changes require only local tuning of existing probability tables.

- **Visual and narrative cues:**
  - Upon first entering the Guild HQ under a new season, fire a short Arkyn dialog summarizing the current sky condition.  Arkyn's place holder quest dialog should also be upgraded to be a thematic state of the current season.
  - Toggle decorations (banners, crystals) in HQ based on `SkySeason.id` if you wish to give a visual indicator.


## 10. Network and Implementation Notes

### 10.1 Client architecture

The Elin mod follows the same architecture as the Underworld/Drug Empire mod:

- Implemented as a BepInEx plugin compiled against Elin’s assemblies and 0Harmony, with CWL sheets for data-driven content such as items and furniture.

- If any request fails, the plugin should:
  - Cache contributions locally (in memory or a small save fragment) and retry on the next trigger. note: this is wrong - we don't want any caching.  It's just lost if the player isn't online.
  - Display flavor text instead of hard errors for read operations ("The stars aren’t speaking right now").
- No critical gameplay should depend on server responses; everything degrades gracefully to a pure single-player experience.

### 10.3 Rate limiting and payload size

- All write operations (`/contributions/batch`, `/geometry/sample`, `/comet/report`, `/research-notes/create`) are batched and infrequent, usually tied to rest events or in-game week boundaries.
- Read operations (`/ladder/global`, `/constellations/current`, `/geometry/summary`, `/comet/heatmap`, `/research-notes/pull`, `/sky-season/current`) use cached results and only refresh when local time thresholds expire.

