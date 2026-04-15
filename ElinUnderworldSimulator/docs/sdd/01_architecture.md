# 1 · Architecture

> Parent: [00_overview.md](./00_overview.md)

## 1.1 System Overview

Elin Underworld Simulator is a client-server system with two independently deployable components:

1. **Client Mod** — A BepInEx plugin (C# DLL) loaded into Elin via Harmony. Contains all game-side logic: startup scenario, crafting stations, UI panels, order management, and HTTP networking.
2. **Backend Server** — A Python FastAPI application serving a REST API. Manages the shared underworld economy: orders, shipments, territories, factions, and heat decay.

The client mod is fully functional offline (crafting, base building, NPC interaction). The server adds the multiplayer dimension — shared orders, competitive territory control, and cross-player faction warfare.

## 1.2 Component Diagram

```mermaid
graph TB
    subgraph "Elin Game Process"
        subgraph "BepInEx Plugin: ElinUnderworldSimulator"
            UP[UnderworldPlugin<br/>BaseUnityPlugin]
            HP[Harmony Patches<br/>UICharaMaker, Game,<br/>Zone.OnVisit]
            USB[UnderworldStartupBootstrap]
            
            subgraph "Crafting Module"
                TMT[TraitMixingTable<br/>: TraitFactory]
                TPV[TraitProcessingVat<br/>: TraitBrewery]
                TAL[TraitAdvancedLab<br/>: TraitFactory]
            end
            
            subgraph "Network Module"
                UAM[UnderworldAuthManager]
                UNC[UnderworldNetworkClient]
            end
            
            subgraph "Economy Module"
                OM[OrderManager]
                RT[ReputationTracker]
                HT[HeatTracker]
            end
            
            subgraph "UI Module"
                UUI[UnderworldUIManager]
                NP[NetworkPanel]
                MS[MarketScreen]
                TO[TerritoryOverlay]
            end
            
            TCC[TraitContrabandChest<br/>: TraitContainer]
            TFN[TraitFixerNPC<br/>: TraitChara]
        end
        
        EG[Elin Game Systems<br/>CraftUtil, RecipeSource,<br/>Zone, Guild, Quest]
    end
    
    subgraph "Backend Server"
        FA[FastAPI App]
        DB[(SQLite Database)]
        BJ[Background Jobs<br/>Heat decay, Warfare calc,<br/>Order expiration]
    end
    
    UP --> HP
    UP --> USB
    HP --> EG
    TMT --> EG
    TPV --> EG
    TCC --> UNC
    UNC --> FA
    UAM --> FA
    OM --> UNC
    FA --> DB
    FA --> BJ
    UUI --> OM
    UUI --> UNC
```

## 1.3 Technology Stack

### Client Mod

| Component | Technology | Source |
|-----------|-----------|--------|
| Runtime | .NET Framework 4.8 (`net48`) | [Directory.Build.props](file:///c:/Users/mcounts/Documents/ElinMods/Directory.Build.props) L12 |
| Mod Framework | BepInEx 5.x | `BepInEx.Core.dll`, `BepInEx.Unity.dll` |
| Patching | Harmony 2.x (`0Harmony.dll`) | Prefix/Postfix/Transpiler patches |
| Game API | `Elin.dll`, `Plugins.BaseCore.dll`, `Plugins.Modding.dll`, `Plugins.UI.dll` | [Directory.Build.targets](file:///c:/Users/mcounts/Documents/ElinMods/Directory.Build.targets) L37-L52 |
| HTTP | `System.Net.Http` | Standard .NET 4.8 |
| JSON | `Newtonsoft.Json` (bundled with Elin) | [Directory.Build.targets](file:///c:/Users/mcounts/Documents/ElinMods/Directory.Build.targets) L53-L56 |
| UI | `UnityEngine.UI.dll` | Unity UGUI |

### Backend Server

| Component | Technology |
|-----------|-----------|
| Framework | Python 3.11+, FastAPI |
| Database | SQLite via `aiosqlite` |
| Server | uvicorn |
| Auth | Token-based (custom, matching SkyreaderGuildServer pattern) |
| Testing | pytest with `conftest.py` routing to `worklog/pytest/test_tmp` |
| Background Jobs | `asyncio` scheduled tasks |

### Asset Pipeline

| Component | Technology |
|-----------|-----------|
| Sprite Generation | Google Gemini API (image generation) |
| Image Processing | Pillow (Python) |
| XLSX Management | openpyxl (Python) with NPOI shared-string normalization |
| Build | MSBuild via `dotnet build` |

## 1.4 Module Decomposition

### 1.4.1 `UnderworldPlugin : BaseUnityPlugin`

The BepInEx entry point. Responsibilities:
- Register Harmony patches on `Awake()`
- Initialize config bindings (server URL, polling interval, tuning values)
- Instantiate and wire up all module singletons
- Manage plugin lifecycle (`OnEnable`, `OnDisable`)

```csharp
[BepInPlugin("mrmeagle.elin.underworldsimulator", "Elin Underworld Simulator", "0.1.0")]
public class UnderworldPlugin : BaseUnityPlugin
{
    public static UnderworldPlugin Instance { get; private set; }
    internal static ManualLogSource Log;
    internal Harmony harmony;
    
    // Module singletons
    internal UnderworldAuthManager AuthManager;
    internal UnderworldNetworkClient NetworkClient;
    internal OrderManager Orders;
    internal ReputationTracker Reputation;
    internal HeatTracker Heat;
    internal UnderworldUIManager UI;
    
    // Config entries
    internal ConfigEntry<string> ServerUrl;
    internal ConfigEntry<int> PollIntervalSeconds;
    internal ConfigEntry<bool> OfflineMode;
    
    void Awake() { ... }
}
```

**Source pattern**: [SkyreaderGuild.cs](file:///c:/Users/mcounts/Documents/ElinMods/SkyreaderGuild/SkyreaderGuild.cs) — same BepInPlugin attribute, Harmony init, config binding approach.

### 1.4.2 `UnderworldStartupBootstrap`

Static class handling the one-time new-game setup when the player selects "Underworld Startup". Called from a `Game.StartNewGame` postfix patch.

```csharp
public static class UnderworldStartupBootstrap
{
    public static void Apply()
    {
        // 1. Claim starting zone
        // 2. Start QuestHome, advance to phase 2
        // 3. DO NOT advance QuestMain beyond phase 0
        // 4. Spawn Fixer NPC
        // 5. Grant starter items (mixing table, ingredients, tools, gold)
        // 6. Place mixing table in base
        // 7. Set dialog flags
    }
}
```

**Source pattern**: [FastStartBootstrap.Apply()](file:///c:/Users/mcounts/Documents/ElinMods/FastStart/Plugin.cs#L144-L189) — zone claiming, quest manipulation, item granting. The key difference: FastStart replays vanilla quests to completion; Underworld simply leaves `QuestMain` at phase 0.

### 1.4.3 Crafting Module

Three crafting station traits, each inheriting from Elin's existing crafting hierarchy:

| Class | Base Class | `idFactory` | Purpose |
|-------|-----------|-------------|---------|
| `TraitMixingTable` | `TraitFactory` | `"uw_mixing_table"` | Basic contraband crafting |
| `TraitProcessingVat` | `TraitBrewery` | N/A (uses decay model) | Time-delayed refinement |
| `TraitAdvancedLab` | `TraitFactory` | `"uw_advanced_lab"` | High-tier contraband crafting |

**Source patterns**:
- `TraitFactory` hierarchy: [TraitFactory.cs](file:///c:/Users/mcounts/Documents/ElinMods/Elin-Decompiled-main/Elin/TraitFactory.cs) → [TraitWorkbench.cs](file:///c:/Users/mcounts/Documents/ElinMods/Elin-Decompiled-main/Elin/TraitWorkbench.cs) → [TraitAlchemyBench.cs](file:///c:/Users/mcounts/Documents/ElinMods/Elin-Decompiled-main/Elin/TraitAlchemyBench.cs)
- Decay-to-product: [TraitBrewery.cs](file:///c:/Users/mcounts/Documents/ElinMods/Elin-Decompiled-main/Elin/TraitBrewery.cs)

### 1.4.4 Network Module

| Class | Responsibility |
|-------|---------------|
| `UnderworldAuthManager` | Token storage, registration, login. Polls `GetOrCreateAuth()` on first network call. |
| `UnderworldNetworkClient` | HTTP methods (GET/POST) with auth headers, timeout handling, offline fallback. All calls return `Task<T>` or `null` on failure. |

**Source patterns**:
- [SkyreaderAuthManager.cs](file:///c:/Users/mcounts/Documents/ElinMods/SkyreaderGuild/SkyreaderAuthManager.cs) — identical token flow
- [SkyreaderOnlineClient.cs](file:///c:/Users/mcounts/Documents/ElinMods/SkyreaderGuild/SkyreaderOnlineClient.cs) — `HttpClient` usage, error handling, polling

### 1.4.5 Economy Module

| Class | Responsibility |
|-------|---------------|
| `OrderManager` | Local cache of accepted orders. State machine: Available → Accepted → Fulfilling → Shipped → Resolved. Persisted in mod save data. |
| `ReputationTracker` | Per-territory local rep (0-1000) + global rank (enum). Read from server, cached locally. |
| `HeatTracker` | Per-territory heat (0-100). Fetched from server. Influences order availability and enforcement event probability. |

### 1.4.6 UI Module

| Class | Responsibility |
|-------|---------------|
| `UnderworldUIManager` | Coordinates panel creation/destruction. Entry point: Fixer NPC interaction or contraband chest. |
| `NetworkPanel` | Main hub — tabbed view of contracts, active orders, shipment results, territory overview. |
| `MarketScreen` | Browse/filter/accept available orders. |
| `TerritoryOverlay` | Territory status: name, heat, demand, controlling faction. |

**Source pattern**: [SkyreaderLadderDialog.cs](file:///c:/Users/mcounts/Documents/ElinMods/SkyreaderGuild/SkyreaderLadderDialog.cs) — `ELayer`-based custom dialog, Unity UI construction.

### 1.4.7 Custom Traits

| Class | Base | Purpose |
|-------|------|---------|
| `TraitContrabandChest` | `TraitContainer` | Shipping chest — place contraband, submit to server |
| `TraitFixerNPC` | (custom or `TraitUnique`) | Fixer NPC interaction — opens NetworkPanel |

**Source patterns**:
- [TraitShippingChest.cs](file:///c:/Users/mcounts/Documents/ElinMods/Elin-Decompiled-main/Elin/TraitShippingChest.cs) — container gating
- SkyreaderGuild trait patterns for custom NPC interaction

## 1.5 Dependency Hierarchy

```mermaid
graph TD
    UP[UnderworldPlugin] --> HP[Harmony Patches]
    UP --> USB[StartupBootstrap]
    UP --> CM[Crafting Module]
    UP --> NM[Network Module]
    UP --> EM[Economy Module]
    UP --> UM[UI Module]
    
    HP --> |patches| GS[Game Systems<br/>Elin.dll]
    USB --> GS
    CM --> GS
    
    NM --> |System.Net.Http| SERVER[Backend Server]
    EM --> NM
    UM --> EM
    UM --> NM
    
    CM --> |TraitFactory<br/>TraitBrewery| GS
    
    style GS fill:#2d3748,stroke:#4a5568,color:#e2e8f0
    style SERVER fill:#1a365d,stroke:#2b6cb0,color:#bee3f8
```

Key dependency rules:
- **Crafting Module** depends only on Elin game systems — no network calls during crafting
- **Economy Module** depends on Network Module for data fetching
- **UI Module** depends on Economy Module for data and Network Module for direct actions
- **Network Module** has no dependency on game systems — pure HTTP client
- **Bootstrap** runs once at game start and depends on game systems for zone/quest manipulation

## 1.6 Data Flow — Order Fulfillment

```mermaid
sequenceDiagram
    participant P as Player
    participant UI as NetworkPanel
    participant OM as OrderManager
    participant NC as NetworkClient
    participant SV as Server
    participant DB as Database
    
    P->>UI: Open Network Panel
    UI->>NC: GET /orders/available
    NC->>SV: HTTP GET
    SV->>DB: Query available orders
    DB-->>SV: Order list
    SV-->>NC: JSON response
    NC-->>UI: Display orders
    
    P->>UI: Accept order #42
    UI->>OM: AcceptOrder(42)
    OM->>NC: POST /orders/accept {order_id: 42}
    NC->>SV: HTTP POST
    SV->>DB: Mark claimed
    SV-->>NC: Confirmation
    OM-->>UI: Order added to active list
    
    Note over P: Player crafts contraband,<br/>places in chest
    
    P->>UI: Submit shipment
    UI->>OM: SubmitShipment(42, payload)
    OM->>NC: POST /shipments/submit
    NC->>SV: HTTP POST
    SV->>DB: Create shipment record
    SV-->>NC: Shipment ID
    
    Note over SV: Server resolves<br/>(quality check, heat calc,<br/>enforcement roll)
    
    P->>UI: Check results
    UI->>NC: GET /shipments/results
    NC->>SV: HTTP GET
    SV->>DB: Fetch resolved shipments
    SV-->>NC: Results (payout, heat delta, rep delta)
    NC-->>UI: Display outcome
    OM->>OM: Complete order, update local state
```

## 1.7 Testing & Verification

### Build Verification
- DLL compiles without errors against `net48` and all Elin references
- Plugin appears in Elin's Mod Viewer as `[Local] Elin Underworld Simulator`
- No errors in `BepInEx/LogOutput.log` on game startup
- No errors in `C:\Users\someuser\AppData\LocalLow\Lafrontier\Elin\Player.log`

### Architecture Integration Tests
- **Module isolation**: Verify crafting module works with no server running (offline mode)
- **Graceful degradation**: Start game with invalid `ServerUrl` config → confirm no crashes, warning logged, UI shows "offline mode"
- **Config system**: Change BepInEx config values → restart → verify all modules pick up new values

### Dependency Validation
```bash
# Verify all required assemblies resolve
dotnet build ElinUnderworldSimulator.csproj
# Expected: Build succeeded, 0 warnings about missing references
```
