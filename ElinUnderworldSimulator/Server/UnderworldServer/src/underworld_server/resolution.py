from __future__ import annotations

from . import config


def calculate_satisfaction(order, quantity: int, avg_potency: int, avg_toxicity: int) -> float:
    potency_factor = avg_potency / max(1, order["min_potency"])
    potency_score = min(2.0, potency_factor)
    toxicity_penalty = 0.0
    if order["max_toxicity"] > 0 and avg_toxicity > order["max_toxicity"]:
        toxicity_penalty = (avg_toxicity - order["max_toxicity"]) / 100.0
    volume_factor = min(1.5, quantity / max(1, order["min_quantity"]))
    satisfaction = potency_score * 0.5 + volume_factor * 0.3 + max(0.0, 1.0 - toxicity_penalty) * 0.2
    return max(0.0, min(2.0, round(satisfaction, 4)))


def calculate_final_payout(order, satisfaction: float, rank_tier: int, territory_controlled: bool, coordination_bonus: bool) -> int:
    payout = order["base_payout"] * satisfaction * config.rank_payout_multiplier(rank_tier)
    if territory_controlled:
        payout *= 1.0 + config.FACTION_CONTROL_PAYOUT_BONUS_PCT / 100.0
    if coordination_bonus:
        payout *= 1.0 + config.FACTION_COORDINATION_BONUS_PCT / 100.0
    return max(0, int(round(payout)))


def calculate_rep_delta(order, satisfaction: float, outcome: str) -> int:
    base_rep = order["min_quantity"]
    if outcome == "failed":
        return -max(1, int(base_rep * 1.5 * config.REP_MULTIPLIERS[order["client_type"]]))

    potency_bonus = max(0.0, satisfaction - 1.0) * order["min_quantity"]
    delta = (base_rep + potency_bonus) * config.REP_MULTIPLIERS[order["client_type"]]
    if satisfaction < 0.7:
        delta *= 0.5
    elif satisfaction > 1.5:
        delta *= 1.4
    elif satisfaction > 1.0:
        delta *= 1.15
    return max(1, int(round(delta)))


def calculate_heat_delta(client_type: str, avg_traceability: int, bust_triggered: bool, surveillance_triggered: bool) -> int:
    client = config.CLIENTS[client_type]
    delta = client.heat_cost + int(round(avg_traceability * 0.05))
    if surveillance_triggered:
        delta += 15
    if bust_triggered:
        delta += 10
    return max(1, delta)


def enforcement_profile(current_heat: int, heat_capacity: int, surveillance_active: bool) -> dict[str, float]:
    heat_percent = config.heat_percent(current_heat, heat_capacity)
    inspect = 0.0
    bust = 0.0
    surveillance = 0.0

    if heat_percent > config.HEAT_THRESHOLD_LOCKDOWN:
        inspect = config.ENFORCEMENT_INSPECT_CHANCE_LOCKDOWN
        bust = config.ENFORCEMENT_BUST_CHANCE_LOCKDOWN
        surveillance = config.ENFORCEMENT_SURVEILLANCE_CHANCE_LOCKDOWN
    elif heat_percent > config.HEAT_THRESHOLD_CRITICAL:
        inspect = config.ENFORCEMENT_INSPECT_CHANCE_CRITICAL
        bust = config.ENFORCEMENT_BUST_CHANCE_CRITICAL
        surveillance = config.ENFORCEMENT_SURVEILLANCE_CHANCE_CRITICAL
    elif heat_percent > config.HEAT_THRESHOLD_HIGH:
        inspect = config.ENFORCEMENT_INSPECT_CHANCE_HIGH
        bust = config.ENFORCEMENT_BUST_CHANCE_HIGH
    elif heat_percent > config.HEAT_THRESHOLD_ELEVATED:
        inspect = config.ENFORCEMENT_INSPECT_CHANCE_ELEVATED

    if surveillance_active:
        inspect = min(1.0, inspect * 2.0 if inspect > 0 else 0.10)

    return {"inspection": inspect, "bust": bust, "surveillance": surveillance}


def resolve_enforcement(rng, current_heat: int, heat_capacity: int, quantity: int, player_gold: int, surveillance_active: bool) -> dict:
    profile = enforcement_profile(current_heat, heat_capacity, surveillance_active)
    if profile["bust"] > 0 and rng.random() < profile["bust"]:
        gold_penalty = max(2500, int(round(player_gold * 0.05)))
        return {
            "effective_quantity": 0,
            "bust": True,
            "surveillance": False,
            "event": {
                "type": "bust",
                "gold_penalty": gold_penalty,
                "investigation_days": 3,
                "message": "The authorities seized the entire shipment!",
            },
        }

    effective_quantity = quantity
    event = None
    if profile["inspection"] > 0 and rng.random() < profile["inspection"]:
        confiscated_quantity = max(1, int(round(quantity * rng.uniform(0.10, 0.30))))
        effective_quantity = max(0, quantity - confiscated_quantity)
        event = {
            "type": "inspection",
            "confiscated_quantity": confiscated_quantity,
            "message": "An inspection intercepted part of the shipment.",
        }

    surveillance_triggered = profile["surveillance"] > 0 and rng.random() < profile["surveillance"]
    return {
        "effective_quantity": effective_quantity,
        "bust": False,
        "surveillance": surveillance_triggered,
        "event": event,
    }
