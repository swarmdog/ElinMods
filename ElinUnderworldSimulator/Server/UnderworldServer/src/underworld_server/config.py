from __future__ import annotations

from dataclasses import dataclass


@dataclass(frozen=True)
class RankSpec:
    tier: int
    name: str
    threshold: int
    payout_multiplier: float


@dataclass(frozen=True)
class ClientSpec:
    key: str
    display_name: str
    unlock_rank_tier: int
    min_quantity: int
    max_quantity: int
    min_potency: int
    payout_multiplier: float
    risk_multiplier: float
    heat_cost: int
    nerve_cost: int
    deadline_hours: int
    weight: int


@dataclass(frozen=True)
class TerritorySpec:
    id: str
    name: str
    region: str
    heat_capacity: int
    base_demand_volume: int
    base_demand_potency: int


RANKS = (
    RankSpec(0, "Novice", 0, 1.00),
    RankSpec(1, "Peddler", 500, 1.10),
    RankSpec(2, "Supplier", 2000, 1.20),
    RankSpec(3, "Kingpin", 5000, 1.35),
    RankSpec(4, "Overlord", 10000, 1.50),
)

CLIENTS = {
    "street_buyer": ClientSpec("street_buyer", "Alley Patron", 0, 1, 5, 20, 1.0, 0.5, 2, 5, 24, 30),
    "regular": ClientSpec("regular", "Returning Customer", 0, 5, 15, 40, 1.2, 1.0, 5, 10, 48, 35),
    "dependent": ClientSpec("dependent", "Desperate Soul", 1, 10, 30, 10, 0.8, 0.3, 2, 5, 48, 20),
    "broker": ClientSpec("broker", "Shadow Broker", 2, 20, 50, 60, 2.0, 2.0, 10, 20, 72, 10),
    "syndicate": ClientSpec("syndicate", "Guild Commissary", 3, 50, 100, 80, 3.5, 3.0, 20, 40, 120, 5),
}

TERRITORIES = {
    "derphy_underground": TerritorySpec("derphy_underground", "Derphy Underground", "Derphy", 200, 12, 30),
    "kapul_docks": TerritorySpec("kapul_docks", "Kapul Docks", "Port Kapul", 120, 9, 45),
    "yowyn_fields": TerritorySpec("yowyn_fields", "Yowyn Farmlands", "Yowyn", 80, 6, 25),
    "palmia_markets": TerritorySpec("palmia_markets", "Palmia Black Market", "Palmia", 60, 5, 70),
    "mysilia_backways": TerritorySpec("mysilia_backways", "Mysilia Backways", "Mysilia", 100, 8, 45),
    "lumiest_canal": TerritorySpec("lumiest_canal", "Lumiest Canals", "Lumiest", 90, 7, 60),
}

PRODUCT_IDS_BY_TYPE = {
    "tonic": ["uw_tonic_whisper", "uw_tonic_whisper_refined", "uw_tonic_whisper_aged"],
    "powder": ["uw_powder_dream", "uw_powder_dream_refined", "uw_powder_dream_concentrated"],
    "elixir": ["uw_elixir_shadow", "uw_elixir_shadow_refined", "uw_elixir_crimson", "uw_elixir_frost", "uw_elixir_rush"],
    "roll": ["uw_roll_whisper", "uw_roll_dream"],
    "incense": ["uw_incense_ash"],
    "draught": ["uw_draught_berserker"],
}

PRODUCT_TYPES_BY_CLIENT = {
    "street_buyer": ("roll", "powder", "tonic"),
    "regular": ("tonic", "powder", "roll"),
    "dependent": ("tonic", "powder", "roll"),
    "broker": ("elixir", "tonic", "powder", "incense"),
    "syndicate": ("elixir", "draught", "incense"),
}

REP_MULTIPLIERS = {
    "street_buyer": 1.0,
    "regular": 1.5,
    "dependent": 0.8,
    "broker": 2.5,
    "syndicate": 4.0,
}

HEAT_THRESHOLD_ELEVATED = 30
HEAT_THRESHOLD_HIGH = 50
HEAT_THRESHOLD_CRITICAL = 70
HEAT_THRESHOLD_LOCKDOWN = 85

HEAT_DECAY_PER_CYCLE = 2
HEAT_DECAY_INTERVAL_SECONDS = 3600
ORDER_GENERATION_INTERVAL_SECONDS = 21600
ORDER_EXPIRATION_INTERVAL_SECONDS = 900
WARFARE_RESOLUTION_INTERVAL_SECONDS = 86400

ENFORCEMENT_INSPECT_CHANCE_ELEVATED = 0.10
ENFORCEMENT_INSPECT_CHANCE_HIGH = 0.25
ENFORCEMENT_INSPECT_CHANCE_CRITICAL = 0.40
ENFORCEMENT_INSPECT_CHANCE_LOCKDOWN = 1.00
ENFORCEMENT_BUST_CHANCE_HIGH = 0.05
ENFORCEMENT_BUST_CHANCE_CRITICAL = 0.15
ENFORCEMENT_BUST_CHANCE_LOCKDOWN = 0.30
ENFORCEMENT_SURVEILLANCE_CHANCE_CRITICAL = 0.05
ENFORCEMENT_SURVEILLANCE_CHANCE_LOCKDOWN = 0.15

FACTION_MAX_MEMBERS_BASE = 10
FACTION_COORDINATION_THRESHOLD = 3
FACTION_COORDINATION_BONUS_PCT = 5
FACTION_CONTROL_PAYOUT_BONUS_PCT = 10
FACTION_CONTROL_HEAT_DECAY_BONUS = 1
FACTION_INFLUENCE_DECAY = 0.90
FACTION_CONTROL_LEAD_MULTIPLIER = 1.2

SURVEILLANCE_DURATION_HOURS = 24 * 5
INVESTIGATION_DURATION_HOURS = 24 * 3


def rank_for_total_rep(total_rep: int) -> int:
    tier = 0
    for spec in RANKS:
        if total_rep >= spec.threshold:
            tier = spec.tier
    return tier


def rank_name(tier: int) -> str:
    for spec in RANKS:
        if spec.tier == tier:
            return spec.name
    return RANKS[0].name


def rank_payout_multiplier(tier: int) -> float:
    for spec in RANKS:
        if spec.tier == tier:
            return spec.payout_multiplier
    return RANKS[0].payout_multiplier


def heat_percent(heat: int, heat_capacity: int = 100) -> float:
    return (max(0, heat) / max(1, heat_capacity)) * 100.0


def heat_level_name(heat: int, heat_capacity: int = 100) -> str:
    percent = heat_percent(heat, heat_capacity)
    if percent <= HEAT_THRESHOLD_ELEVATED:
        return "clear"
    if percent <= HEAT_THRESHOLD_HIGH:
        return "elevated"
    if percent <= HEAT_THRESHOLD_CRITICAL:
        return "high"
    if percent <= HEAT_THRESHOLD_LOCKDOWN:
        return "critical"
    return "lockdown"
