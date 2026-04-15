# Elin Underworld Simulator — Software Design Document

## 0 · Overview

### 0.1 Project Vision

**Elin Underworld Simulator** is a standalone starting-scenario mod for [Elin](https://store.steampowered.com/app/2135150/Elin/) that replaces the vanilla Ylva main quest with a criminal-economy simulation. The player begins as a nobody in Ylva's seedy underbelly and builds a contraband empire — crafting illicit goods, fulfilling black-market orders, managing heat from the authorities, and competing with other players for territory control in an asynchronous multiplayer economy.

The design draws inspiration from three sources and fuses them into Elin's existing systems:

| Inspiration | What We Take |
|-------------|-------------|
| **Drug Dealer Simulator** | Crafting loop — acquire raw materials, process them at a mixing station, produce contraband with variable quality and potency |
| **Torn** | Async multiplayer economy — orders from a shared server, nerve/stamina action gating, faction-based territory warfare resolved on a schedule |
| **Elin (vanilla)** | Full RPG sandbox — base building, residents, crafting stations, item system, dungeon exploration, criminal reputation mechanics |

### 0.2 Gameplay Elevator Pitch

> You select "Underworld Startup" at character creation. You awaken on a patch of land on the outskirts of civilization with a crude mixing table and a handful of herbs. A mysterious figure — the Fixer — appears and offers you a simple proposition: there's a market for things the respectable merchants of Palmia won't touch. Cook something worth selling, pack it in a chest, and the Fixer's network will find buyers. Better product fetches better prices. Better prices attract bigger clients. Bigger clients draw the attention of the law — and of rival syndicates. How far into the underworld will you go?

### 0.3 Design Principles

1. **Elin-Native** — Every system hooks into Elin's existing architecture: `TraitFactory` for crafting stations, `TraitBrewery` for time-delayed processing, `TraitContainer` for shipping, `Zone` for custom locations, `RecipeSource` for recipes. The mod *extends*, never replaces.

2. **Async-First Multiplayer** — No real-time synchronous dependency between players. The server is a shared ledger: players submit shipments and poll results. Territory warfare resolves on a schedule. The game remains fully playable offline with graceful degradation.

3. **Graceful Mod Removal** — Disabling the mod causes custom items to become "alchemical ash" (Elin's default handling for unknown item IDs). No save corruption. Server data is external and survives mod toggling.

4. **Ylva-Lore Integration** — All items, NPCs, and locations feel native to Elin's world. Contraband uses fantasy alchemical language, not modern-world drug terminology. The Fixer speaks in Elin's dialog style. Underground operations reference Derphy, the Thieves' Guild, and Ylva's existing criminal ecosystem.

5. **Standalone Scenario** — This is NOT a midgame addon. The player selects "Underworld Startup" at character creation and enters a completely separate progression track. The vanilla main quest (`QuestMain`) is never advanced — it simply sits at phase 0 while the player pursues the underworld economy. The player can still explore the full Ylva world but the Ashland/Nymelle/exploration quest chain never activates.

### 0.4 Terminology Glossary

All in-game terms use Elin-flavored fantasy language.

| Term | Definition |
|------|-----------|
| **Contraband** | Illicit crafted goods with variable potency and toxicity. The core tradeable commodity. In Elin's item system these are custom Thing entries with specialized traits. |
| **Mixing Table** | A `TraitFactory` crafting station for combining raw ingredients into contraband. Analogous to the Alchemy Bench. |
| **Processing Vat** | A `TraitBrewery`-derived station that refines contraband over time. Items "ferment" into higher-value products. |
| **The Fixer** | Custom NPC who acts as the gateway to the underworld network. Placed in Derphy. Provides the network UI and introductory dialog. |
| **The Network** | The asynchronous multiplayer backend — a REST API that hosts orders, resolves shipments, tracks territory control, and manages factions. |
| **Order** | A contract from a buyer requesting specific contraband. Fetched from the server. Has requirements (type, quantity, minimum potency) and a payout. |
| **Shipment** | The player's fulfillment of an order — items packed into a contraband chest and submitted to the server for resolution. |
| **Heat** | Per-territory risk accumulation. High heat increases the chance of enforcement events (inspections, busts, raids). Decays over time. |
| **Nerve** | A stamina-like resource that limits how many high-risk operations the player can perform per time period. Regenerates passively. Named "Shadow Nerve" in-game. |
| **Territory** | An abstract market region mapped to Elin's world geography (e.g., "Derphy Underground", "Kapul Docks"). Each territory has its own demand, heat, and controlling faction. |
| **Faction** | A player-created syndicate. Members cooperate on shipments to influence territory control. Resolved server-side on a schedule. |
| **Underworld Rank** | Global player progression: Novice → Peddler → Supplier → Kingpin → Overlord. Unlocks higher-tier recipes, stations, and order access. |
| **Potency** | Primary quality attribute of contraband. Higher potency = better payouts but faster heat accumulation. Derived from ingredient quality and crafting skill. |
| **Toxicity** | Negative quality attribute. High toxicity reduces client satisfaction and may cause order failures. Result of poor ingredients or cutting agents. |
| **Shadow Guise** | Custom underworld skill (PER-based). Masks criminal status when entering lawful towns, duration scaling with level. |
| **Silver Tongue** | Custom underworld skill (CHA-based). Reduces karma loss, enables guard bribery, and improves street deal prices. |
| **Nerve Conditioning** | Custom underworld skill (WIL-based). Increases maximum Shadow Nerve and accelerates regeneration. |
| **Dealing** | Small-time sales of contraband directly to town NPCs. The early-game income loop — offer samples, build regulars, sell from inventory. |
| **Prospect/Regular/Devoted/Hooked** | NPC customer loyalty tiers. Higher tiers buy more, pay more, and generate passive rep. |
| **Sample Kit** | A concealment item that hides a small quantity of contraband from the `IsCriminal` check in lawful zones. |
| **Addiction** | Per-NPC element (`UW_ADDICTION`, 0-100) tracking how dependent a customer has become. Higher addiction = more volume/revenue but increasing OD risk. Five tiers: Clean, Casual, Dependent, Addicted, Severe. |
| **Tolerance** | Per-NPC element (`UW_TOLERANCE`, 0-50) tracking how much potency the customer needs. Increases with repeated purchases. Demands higher-potency product over time. |
| **Overdose** | Risk event triggered during deals when a customer's addiction ≥ 61. Three severities: mild (debuffs), severe (collapse + heat), fatal (customer death + major consequences). |
| **Withdrawal** | `ConUWWithdrawal` condition applied to Dependent+ customers who haven't been served within a visit threshold. Stat debuffs, vomiting, and desperate dialog. Cured by serving product. |
| **Alchemist's Reprieve** | Craftable recovery potion (`uw_antidote_vial`). Cures `ConUWOverdose` from mild/severe ODs, restoring 50% HP. Cannot prevent fatal ODs. |
| **Dealer's Ledger** | Book item (`uw_dealers_ledger`) that opens a custom `ELayer` panel tracking all customers across towns — status, loyalty, addiction, and pending orders. |

### 0.5 Scope Boundary

**In scope (this mod):**
- Standalone "Underworld Startup" starting scenario
- Custom crafting stations (Mixing Table, Processing Vat, Advanced Lab)
- 15-25 new items (raw ingredients, precursors, finished contraband, cutting agents)
- 2-3 custom NPCs (Fixer, suppliers, enforcers)
- Async multiplayer backend (FastAPI + SQLite)
- Order/shipment lifecycle with server-side resolution
- Heat and nerve systems
- Territory control with faction warfare
- Base integration (contraband chest, infrastructure progression)
- Custom UI panels (network screen, market, territory overview)
- Automated asset pipeline (sprite generation, XLSX management)
- 3 custom underworld skills (Shadow Guise, Silver Tongue, Nerve Conditioning)
- Small-time dealing system (NPC customer tracking, loyalty tiers, direct sales)
- NPC addiction and tolerance mechanics (progressive dependency)
- Overdose system with graduated consequences (mild/severe/fatal)
- Withdrawal conditions and customer management


**Out of scope:**
- Real-time PvP or synchronous multiplayer
- Modification of the vanilla main quest line (we simply don't advance it)
- New dungeon types or exploration content (player uses vanilla dungeons for ingredient gathering)
- Voice acting or elaborate cutscenes
- Mobile/console ports

### 0.6 Document Index

| # | Document | Contents |
|---|----------|----------|
| **00** | [00_overview.md](./00_overview.md) | This document — vision, glossary, scope, document index |
| **01** | [01_architecture.md](./01_architecture.md) | Component diagram, tech stack, module decomposition, class hierarchy |
| **02** | [02_game_integration.md](./02_game_integration.md) | Harmony patches, startup scenario, zone registration, NPC integration, criminal system hooks |
| **03** | [03_data_model.md](./03_data_model.md) | Item taxonomy, SourceCard XLSX specifications, element properties, NPOI compliance |
| **04** | [04_crafting_system.md](./04_crafting_system.md) | Mixing Table, Processing Vat, Advanced Lab, quality propagation, recipe registration |
| **05** | [05_orders_reputation.md](./05_orders_reputation.md) | Client archetypes, order lifecycle, reputation tracks, rank benefits, satisfaction algorithm |
| **06** | [06_risk_enforcement.md](./06_risk_enforcement.md) | Heat system, nerve resource, law enforcement events, raid implementation, recovery |
| **07** | [07_territory_factions.md](./07_territory_factions.md) | Territory map, faction system, warfare resolution, control rewards |
| **08** | [08_base_integration.md](./08_base_integration.md) | Starting base layout, infrastructure progression, resident roles, contraband chest |
| **09** | [09_server_api.md](./09_server_api.md) | FastAPI endpoints, database schema, auth flow, background jobs, client-side HTTP |
| **10** | [10_ui_ux.md](./10_ui_ux.md) | Network panel, market screen, shipment screen, territory overlay, Fixer dialog |
| **11** | [11_mod_packaging.md](./11_mod_packaging.md) | Package structure, build pipeline, asset generation, SourceCard automation, deployment |

### 0.7 Cross-Cutting Concerns

- **Testing**: Each document (01-11) includes its own **Testing & Verification** section describing unit tests, integration tests, and manual verification steps specific to that system. There is no standalone verification document.
- **Error Handling**: Network failures degrade gracefully to offline mode. Invalid crafting inputs produce harmless junk. Malformed server responses are caught and logged.
- **Configuration**: All tuneable values (heat decay rates, nerve regen, payout multipliers) are exposed via BepInEx config. Server-side values are configurable in `config.py`.
- **Logging**: All mod systems log to BepInEx's logger (`Logger.LogInfo`, `Logger.LogWarning`, `Logger.LogError`). Server logs via Python `logging` module.
