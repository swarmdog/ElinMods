# Design Document: Elin Mod – Underworld Startup (Async Drug/TORN-Style Scenario)

## 1. Overview

This document describes a mod for **Elin** that adds a new starting option focused on crafting, base building, and an asynchronous, crime-economy gameplay loop inspired by **Drug Dealer Simulator** and **Torn**. Instead of following Elin’s standard main quest, players who choose this start build a black-market network from their homestead, trading contraband via an HTTP/HTTPS-backed async multiplayer service that coordinates player interactions and territory control.[^1][^2][^3][^4][^5][^6][^7][^8]

Key characteristics:

- New "Underworld Startup" character start with crafting‑oriented gear, land, and a basic base layout, skipping or suppressing the vanilla main quest line.[^6][^7][^9]
- Core loop revolves around producing, packaging, and shipping contraband, serving NPC/remote-player clients, and expanding influence over territories.
- Async multiplayer provided by a separate HTTP/HTTPS server; in-game mod acts as a thin client that polls/pushes state without real-time synchronous sessions.[^10]
- Thematic and mechanical influences from Drug Dealer Simulator’s client/addict/dealer systems and mixing table, and Torn’s crime, nerve, factions, and territorial warfare.[^2][^3][^4][^5]

## 2. Reference Games and Systems

### 2.1 Elin Core Systems Relevant to the Mod

Elin centers on sandbox roguelike progression with strong emphasis on housing, land management, residents, crafting, and nefia (dungeon) exploration. Housing revolves around acquiring land via **Land deeds**, then managing a **Hearthstone** that controls population, danger level, and unlockable features like build modes and network teleporters. Crafting uses recipes organized across simple crafting, workbenches, sawmills, and other stations to create building materials, furniture, tools, and processing devices such as shipping chests and sawmills.[^11][^12][^7][^8][^13][^14][^1][^6]

Base-building and automation are important: investing in land, residents, and policies can create passive income and resource production, while crafting skills (carpentry, blacksmithing, farming, etc.) are used for both economic power and convenience. Farming and related land fertility mechanics are a major pillar, producing food and crafting resources and tying into travel rations and broader progression.[^9][^15][^16][^17]

### 2.2 Drug Dealer Simulator: Core Mechanics to Adapt

Drug Dealer Simulator places the player as a street-level dealer who buys drugs from a cartel contact (Eddie), mixes them using a lab table with various substances, and sells them to clients in a growing territory while avoiding police and DEA. Key systems:[^4][^18][^19][^2]

- **Clients, addicts, and dealers**: clients place orders with expectations for quality, toxicity, and addiction thresholds; addicts consume more and tolerate weaker cuts; dealers place large orders and pay back later.[^18][^2]
- **Drugs and mixing**: drugs are crafted at a mixing table by combining active drugs and cutting agents; mixing affects profit, addictiveness, and overdose risk.[^2][^4][^18]
- **Territory and reputation**: delivering high-quality product grows reputation and expands client base and area of operations.[^4][^2]
- **Law enforcement pressure**: patrols, checkpoints, and DEA operations create risk of search, arrest, or confiscation, depending on carrying capacity, time of day, and behavior.[^18][^2][^4]

### 2.3 Torn: Core Mechanics to Adapt

Torn is a text-based crime MMO where players perform crimes using a **nerve bar**, gain crime experience (CE), and join factions to participate in organized crimes and territory warfare. Important systems:[^3][^20][^21][^22]

- **Crimes and CE**: each crime attempt consumes nerve and grants crime experience; failures, especially those leading to jail, remove a percentage of CE and can set back progression.[^20][^3]
- **Nerve bar**: acts as a resource limiting how often and which crimes a player can attempt; natural nerve bar increases in steps as CE thresholds are reached.[^3][^20]
- **Factions and organized crimes**: factions coordinate members for non-nerve-consuming organized crimes and large-scale activities.[^21][^22][^20]
- **PvP attacks and hospital/jail**: attacking other players uses energy and can hospitalize them; failed attacks can land the attacker in hospital.[^23]
- **Territories, travel, and drugs**: factions control territories, travel to other countries for smuggling, and use drugs heavily in both economy and stat progression; the game emphasizes asynchronous competition and politics.[^5][^24][^21]

These systems demonstrate how an asynchronous crime economy with risk, resource gating (nerve), and faction politics can drive long-term progression, which the mod emulates via an external HTTP service instead of real-time synchronous play.

## 3. High-Level Feature Set

### 3.1 New Starting Scenario: "Underworld Startup"

- **Scenario selection**: in character creation, add a new start option "Underworld Startup" alongside existing starts; selecting it initializes the player in a custom homestead suited for crafting and smuggling.
- **Starting land/base**: player begins with a small land plot (Meadow-equivalent) featuring a Hearthstone at level 1, basic structures (grass walls/floor, storage), and space for expansion.[^7][^8][^6]
- **Starter items and stations**:
  - Land deed (already consumed for starting map), workbench, sawmill, shipping chest, basic storage, and minimal defenses.[^8][^13]
  - Resource bundles: logs, stone, fiber/grass for early carpentry recipes like walls, doors, and beds.[^14][^8]
  - Basic alchemy/herb processing tools and seeds to tie drugs thematically to herbs, alcohol, and concoctions using existing crafting categories.[^16][^8]
- **Adjusted skills/attributes**: the start biases toward crafting, stealth, and negotiation/social skills instead of pure combat, to support economic gameplay.
- **Main quest branching**: when this start is selected, vanilla main quest triggers are suppressed or delayed; instead the player receives initial contact from an in-world "Fixer" NPC who introduces the underworld network.

### 3.2 Core Gameplay Loop

The mod’s loop is a hybrid of Elin’s crafting/base-building and the crime-economy from Drug Dealer Simulator and Torn:[^6][^8][^2][^3]

1. **Harvest and craft**: gather natural resources, farm herbs, and mine ore around the base; use crafting stations to produce precursor goods, packaging, and contraband products.[^8][^9][^16]
2. **Process and mix**: convert precursors into contraband batches via custom recipes inspired by mixing tables, adjusting potency, volume, and toxicity.[^2][^4]
3. **Take orders**: via a new "Network" UI, fetch orders from NPC/remote clients with specified quantity, quality, delivery time, and risk/reward profile.[^18][^2]
4. **Package and ship**: fulfill orders by assigning inventory stacks into shipment manifests; the mod posts shipment data to the HTTP server, which simulates delivery, law enforcement checks, and client satisfaction.[^10][^4][^2]
5. **Resolve outcomes**: receive results (paid, partial loss, bust, heat increase, new clients) and adjust base state accordingly; gain gold, reputation, and influence, or suffer losses, temporary lockouts, and raids.
6. **Expand and specialize**: invest profits into crafting upgrades, base defenses, residents, and underworld infrastructure such as labs, safehouses, and logistics networks.[^17][^7][^6]

This loop keeps the player mostly in their homestead and nearby biomes, deeply engaging with Elin’s land and crafting systems while externalizing the city-scale crime simulation and other players to the HTTP service.

## 4. Systems Design

### 4.1 Contraband Item System

Contraband is represented as a family of new item categories that piggyback on existing Elin item types (herbs, potions, alcohol, processed goods) but flagged with custom tags used by the mod and HTTP backend.

- **Base categories**:
  - Herbal preparations (e.g., rolled herbs, tinctures) and alcohol-based products, building off existing crafting such as hand-rolled herbs and wine production.[^15][^8]
  - Processed contraband (powders, concentrates, pills) created at custom lab stations.
- **Attributes per batch**:
  - Potency: primary value driving price and client satisfaction, akin to Drug Dealer Simulator’s quality metric.[^4][^2]
  - Toxicity: risk factor influencing overdoses/addiction, mapped to client thresholds.[^2][^4]
  - Volume: total units crafted and shippable; influences risk and profit.
  - Traceability: how easily law enforcement can link a batch to a player (affected by cutting agents and laundering methods).
- **Crafting stations**:
  - Basic mixing table (unlocked early) using existing workbench/sawmill as a template for new crafting processors.[^25][^8]
  - Advanced lab (midgame), built with blacksmithing, alchemy, and rare materials, enabling high-value recipes with higher risk.[^17]

### 4.2 Orders, Clients, and Reputation

Orders abstract Drug Dealer Simulator’s clients/addicts/dealers into an async system:

- **Client types**:[^18][^2]
  - Casual clients: low volume, low risk, moderate quality expectations.
  - Addicts: higher frequency and volume, tolerate lower quality, but increase addiction and toxicity risk.
  - Dealers: bulk orders with delayed payment and higher risk of large busts.
- **Order attributes**:
  - Quantity (units), deadline (Elin in-game days), product type, minimum potency, maximum toxicity, and territory origin.
  - Base payout and bonus multipliers for early or over-spec deliveries.
- **Reputation tracks**:
  - Local reputation per territory: influences order size, frequency, and type in each region, similar to area reputation in Drug Dealer Simulator and territory respect in Torn.[^5][^21][^2]
  - Global underworld rank: unlocks new contraband tiers, lab upgrades, and faction connections.
- **Satisfaction outcomes**:
  - Over-delivery (better quality than requested) yields bonus pay, faster rep growth, and potential special offers.
  - Under-delivery or missed deadlines reduces rep, may trigger retaliation events or cut off orders from a client or territory.

### 4.3 Risk, Law Enforcement, and Nerve-Like Gating

The mod adapts Torn’s nerve/CE system and Drug Dealer Simulator’s police/DEA pressure into a heat and stamina model.[^20][^3][^5][^2]

- **Heat (per territory)**:
  - Increases after large or frequent shipments, especially when they exceed a region’s "safe throughput" or use risky routes.
  - Elevated heat increases probability of inspection or bust outcomes during shipment resolution.
  - Heat decays over time or can be reduced via specific side-actions (bribes, false flags, low-profile shipments).
- **Underworld stamina (nerve analogue)**:
  - Limits how many high-risk orders can be accepted in a given period, mirroring Torn’s nerve bar constraints.[^3][^20]
  - Regenerates over time or through consumables (e.g., stress-relief items, certain drugs) at the cost of side effects.
- **Law enforcement events**:
  - Bust: shipment lost, partial gold confiscated, temporary reputation loss, possible temporary "under investigation" state blocking high-risk orders.
  - Surveillance: increased heat, future shipments in a territory face higher risks until heat falls.
  - Raid (rare): Elin-side event instance such as a home invasion or ambush that the player must survive; outcome affects global reputation and heat.

### 4.4 Factions and Territory Control

Territory and factions borrow from Torn’s faction warfare and territorial control, but remain asynchronous.[^24][^21][^5]

- **Territory model**:
  - World map is partitioned into abstracted neighborhoods or routes (e.g., "North Dock", "Market District", "Hillfolk Trail") stored server-side.
  - Each territory has controlling faction, heat level, baseline demand, and a pool of potential orders.
- **Player affiliations**:
  - Players may join or found underworld factions via the HTTP service, similar to Torn factions.[^22][^21]
  - Factions can coordinate to push into new territories, hold high-value routes, and share bonuses.
- **Control mechanics**:
  - Territorial influence is gained by successfully fulfilling orders and occasionally taking special "push" contracts in contested areas.
  - Periodic server-side "war" calculations adjust control based on aggregated actions, rather than real-time battles.
  - Rewards for control include better base payouts, reduced heat sensitivity, and access to faction-only contracts.

### 4.5 Base Building and Residents Integration

The mod leverages Elin’s land, Hearthstone, residents, and policies to deepen the crime economy.[^7][^9][^6]

- **Infrastructure**:
  - Shipping area: placement of shipping chests and logistics furniture; shipping chest mechanics already exist as a way to send items from land and raise hearthstone level.[^6][^8]
  - Labs and workshops: interior rooms dedicated to contraband production using blacksmithing, alchemy, and carpentry stations.[^13][^8][^17]
- **Residents**:
  - Assign roles like growers, processors, guards, smugglers; resident traits can modify production output, risk, and heat.
  - Certain residents may open new UI actions (e.g., a money launderer or fixer) via custom actions.
- **Policies and hearthstone level**:
  - Home policies affect growth rate and danger; high hearthstone levels unlock more advanced infrastructure and network capacity, but also raise danger level, increasing raid likelihood.[^7][^6]

## 5. Async Multiplayer Architecture

### 5.1 Components

The multiplayer layer is intentionally simple and asynchronous:

- **Game client mod (Elin-side)**:
  - BepInEx-based plugin loaded by Elin, using Harmony to patch into relevant methods and adding new UI elements.[^26][^10][^2]
  - Handles local state, UI, and packaging of shipments.
  - Communicates with the backend over HTTPS using .NET HTTP APIs.
- **HTTP/HTTPS backend**:
  - Stateless or minimally stateful REST API front-end.
  - Database (e.g., relational or document) storing players, orders, territories, factions, and logs.
  - Periodic background jobs for territorial warfare, heat decay, and event resolution.

### 5.2 Data Model (High-Level)

- **Player**: unique ID, auth token, display name, underworld rank, faction ID, soft stats imported from Elin (e.g., select skill levels) for matchmaking.
- **Base**: references to Elin land ID and rough infrastructure tiers (labs level, shipping capacity) for backend balancing.
- **Order**: client type, territory, product type, quantity, quality requirements, payout, deadline, and status.
- **Shipment**: foreign key to order and player, payload summary (not full item data), risk profile, timestamp, and outcome.
- **Faction**: name, members, controlled territories, faction-level bonuses.
- **Territory**: baseline demand, controlling faction, heat, and historical shipment data.

### 5.3 API Endpoints (Examples)

- `POST /register`: create a new underworld account; may require Elin-provided one-time key to prevent abuse.
- `POST /login`: returns auth token for subsequent requests.
- `GET /orders/available`: returns list of candidate orders filtered by player rank, territories unlocked, and heat.
- `POST /orders/accept`: claim an order.
- `POST /shipments/submit`: submit shipment payload for an order; server resolves outcome based on payload and current heat.
- `GET /shipments/results`: retrieve pending results for display in-game.
- `GET /territories`: fetch territory overview for the map UI.
- `POST /factions/join` and related endpoints for faction management.

All endpoints use HTTPS by default, and the client mod caches responses and gracefully handles network failures.

### 5.4 Client Integration and UX Flow

- On selecting the Underworld Startup scenario and finishing character creation, the mod prompts the player to register or log in to the underworld network.
- A new **Network** UI tab in Elin’s interface shows:
  - Available contracts (orders) and their details.
  - Active orders, deadlines, and required product stats.
  - Territory map overlay with influence and heat.
  - Faction status and messages.
- When the player finalizes a shipment in a dedicated shipping screen, the mod:
  - Summarizes the contraband items into an abstract payload (e.g., potency histogram, volume, toxicity, route choice).
  - Sends the shipment as an HTTP request; the result is stored server-side and fetched later when ready.

## 6. Elin Modding Integration

### 6.1 Mod Packaging and Dependencies

Elin mods are distributed as packages under the game’s `Package` directory, using XML `package` descriptors and often relying on BepInEx for runtime code injection and Custom Whatever Loader (CWL) for data-driven extensions. The mod will be structured as:[^27][^28][^29][^26]

- **BepInEx plugin**: compiled .NET assembly providing core logic, HTTP client, Harmony patches, and new UI.
- **CWL sheets**: custom source sheets for new items, abilities, and possibly a new starting scenario using the Custom Whatever Loader framework.[^30][^27][^26]
- **Package XML**: defines the mod’s identity, dependencies (e.g., nightly CWL loader), and Workshop metadata if distributed via Steam.

The Elin Modding Wiki and Elin.Docs provide examples on editing Thing/Race/Chara sheets and adding custom actions, which are used as patterns for this mod.[^26]

### 6.2 Adding the Starting Option

Official documentation and community guides indicate that custom adventurers and starting configurations can be built using sourcecard sheets and CWL, with mods frequently subclassing existing races/classes or defining new ones. The mod will:[^30][^27][^26]

- Define a **new starting scenario** in the relevant CWL sheet, referencing:
  - Initial map (custom homestead template).
  - Starting items and crafting stations.
  - Initial skills/attributes.
- Add a **custom trait** or flag (e.g., `UnderworldStart`) to the player character used by the BepInEx plugin to:
  - Suppress or delay vanilla quest hooks during game initialization.
  - Inject the Fixer NPC and network introduction event.

### 6.3 Custom Actions and UI Hooks

The Elin.Docs include examples of adding custom actions and using Harmony transpilers to modify existing behavior (e.g., crime karma handling). This mod will:[^26]

- Add menu actions like "Open Network" and "Manage Shipments" bound to the player, Fixer NPC, or specific furniture.
- Patch relevant UI panels to insert network-related tabs or buttons.
- Provide localized strings for interface text.

### 6.4 Save Compatibility and Mod Disable Behavior

When mods are removed, items added by mods become "alchemical ash" in Elin, and custom races/classes are reassigned to existing ones. To avoid breaking saves:[^28]

- All new items must degrade gracefully into generic ash when the mod is absent.
- Core world progression (vanilla quest flags) should remain intact in case the player later disables the mod and wants to continue the character in standard content.
- Async network data is primarily server-side; client-side caches are optional and can be safely deleted.

## 7. Balancing and Progression

### 7.1 Early Game

The Underworld Startup start must feel powerful for crafting/base building, but not trivialize the wider game:

- Early access to land and basic crafting stations, but limited in scope and resources, requiring active gathering and skill leveling.[^13][^14][^8]
- Initial contracts are low volume and low risk; heat thresholds are generous to allow learning.
- Law enforcement penalties are mild early on, focusing more on warnings and reduced payouts than catastrophic losses.

### 7.2 Mid Game

As players upgrade their base and labs and expand to new territories:

- Contraband recipes yield higher profits but increase toxicity and heat risks.
- More complex orders require multi-stage production and inventory management.
- Faction and

---

## References

1. [Elin | Guide for Complete Beginners | Episode 1 - YouTube](https://www.youtube.com/watch?v=JpHkWEWjGKU) - ... Ylva? In this Complete Beginner's Guide to Elin, I start a brand new game so you can play along ...

2. [Gameplay | Drug Dealer Simulator Wiki | Fandom](https://drug-dealer-simulator.fandom.com/wiki/Gameplay) - The main feature of the game. The basics of it is that you buy drugs from the cartel, distribute tho...

3. [Torn City Guides - Crimes - Torn Stats](https://tornstats.com/guides/show/54) - You cannot gain drug crime merits without doing transport drug crimes, which mean you will get jaile...

4. [Drug Dealer Simulator - Wikipedia](https://en.wikipedia.org/wiki/Drug_Dealer_Simulator) - The player assumes the role of a drug dealer that must create and sell drugs, slowly creating a drug...

5. [The Most In-Depth Crime Game Made | Torn City - YouTube](https://www.youtube.com/watch?v=vKAzKuPQV5Q) - This a is a browser RPG crime game called crime city and also some clips from Zed city. It's a fun g...

6. [Housing - Ylvapedia](https://ylvapedia.wiki/wiki/Elin:Housing) - A key part of Elin is managing My Home, a location the player sets up as a home base for housing. Ho...

7. [ハウジング - Ylvapedia](https://ylvapedia.wiki/index.php?title=Elin%3A%E3%83%8F%E3%82%A6%E3%82%B8%E3%83%B3%E3%82%B0)

8. [Creating - Ylvapedia](https://ylvapedia.wiki/wiki/Elin:Creating) - Craft recipes in Elin. See items for craft specifications, for building material recipes, see Creati...

9. [Can I ignore the whole housing/land proccess and just go out exploring?](https://www.reddit.com/r/ElinsInn/comments/1gun2f7/can_i_ignore_the_whole_housingland_proccess_and/) - Can I ignore the whole housing/land proccess and just go out exploring?

10. [Running games on Steam - BepInEx Docs](https://docs.bepinex.dev/master/articles/advanced/steam_interop.html) - 1. Download and install BepInEx · 2. Set up permissions · 3. Configure Steam to run the script · 4. ...

11. [Elin Beginner Guide: 10 Tips That Make the Early Game Easier](https://www.youtube.com/watch?v=hC_MKvM_7Wo) - ... players understand the game's core mechanics and early progression. I cover essential survival s...

12. [Somewhat-Comprehensive New Player's Guide - Steam Community](https://steamcommunity.com/sharedfiles/filedetails/?id=3358083554) - A major mechanic in the game is stamina. ... And please help share this guide so other Elin players ...

13. [Elin Guide: Leveling Crafting Skills - YouTube](https://www.youtube.com/watch?v=39cDRQzXG7c) - Guide on what is best to level the crafting skills we all use in our playthroughs. -Please subscribe...

14. [Tips/Guide for crafting, start to early game, and what materials to ...](https://www.reddit.com/r/ElinsInn/comments/1ll4fyt/tipsguide_for_crafting_start_to_early_game_and/) - Regarding Hardness, you got it. To craft tools with harder materials, you need to increase your mini...

15. [What crafting skills do people level to make money? : r/ElinsInn](https://www.reddit.com/r/ElinsInn/comments/1rsvfxc/what_crafting_skills_do_people_level_to_make_money/) - Are there certain crafting skills that are easier or more profitable to level for income? Any tips o...

16. [How To Farm In Elin - TheGamer](https://www.thegamer.com/elin-farming-complete-guide/) - This guide will cover how the different mechanics such as fertility and crop levels work, alongside ...

17. [What are the best/most implemented skills so far? - Steam Community](https://steamcommunity.com/app/2135150/discussions/0/4629231414252931321/) - Blacksmithing has alot of uses for your base needs, you need it for the sun lamps that will alow you...

18. [Drug Dealer Simulator #Xbox How To Play - Basic Controls/Tips](https://www.youtube.com/watch?v=IlcUj2pi8HI) - Drug Dealer Simulator #Xbox How To Play - Basic Controls/Tips 

Voiced by Billz
Gamertag: Billzumana...

19. [Drug Dealer Simulator - Getting Started (Episode 1) - YouTube](https://www.youtube.com/watch?v=8bypjzdrMzU) - Drug Dealing Simulator Gameplay 2023 Have you ever thought about expanding your own crime empire, wi...

20. [Another New Player Guide : r/torncity - Reddit](https://www.reddit.com/r/torncity/comments/fzjpel/another_new_player_guide/) - OC's are organised crimes. These do not consume nerve and are created by someone in the faction. You...

21. [Viewing pages for torncity](https://www.reddit.com/r/torncity/wiki/pages/) - From Torn.com: Torn City is an exciting, gritty, real-life text based crime RPG. Online RPG games ar...

22. [Torn City: The Ultimate Guide for Getting Started](https://www.youtube.com/watch?v=kPW6fMLaaIM) - I developed this guide and video series for beginners in Torn City.  This is the ultimate guide for ...

23. [Attacking | Torn City - Fandom](https://torncity.fandom.com/wiki/Attacking) - Attacking another player uses 25 energy every time the option is chosen. To attack, go to someone's ...

24. [The Ultimate Travel Guide for Torn City](https://www.youtube.com/watch?v=Y2nlP4m1Ajo) - After you have reached level 15 it is time to start flying.  This video contains a concise but compl...

25. [Elin - Crafting Stations & Recipes | Rise of the Industry™ - YouTube](https://www.youtube.com/watch?v=_2On6ZCJvt4) - Elin eternal league of nefia - Guide on Crafting Stations & Recipes. EA 23.46 version of the game. 0...

26. [Elin Modding Wiki](https://elin-modding-resources.github.io/Elin.Docs/) - A short Tutorial for creating basic Elin Source Sheet Based Mods. ... Creating Custom Actions. VHS. ...

27. [Steam Workshop::(Nightly) Custom Whatelse Loader](https://steamcommunity.com/sharedfiles/filedetails/?id=3370512305&searchtext=) - Allows the game to automatically load modders' custom resources from the mod directory, simplifying ...

28. [Elin:Modding - Ylvapedia](https://ylvapedia.wiki/wiki/Elin:Modding) - Elin has a variety of user-created mods available for download. However, please remember that it is ...

29. [How to mod? :: Elin General Discussions - Steam Community](https://steamcommunity.com/app/2135150/discussions/0/4629231220481348522/) - To start with a mod you need to create a file in Elin>Package and in that folder you need an xml fil...

30. [Creating custom adventurers? : r/ElinsInn - Reddit](https://www.reddit.com/r/ElinsInn/comments/1q7g82k/creating_custom_adventurers/) - if you want custom traits or custom abilities that aren't already ingame, then you need to actually ...

