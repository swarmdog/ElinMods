from __future__ import annotations

import asyncio
import json
import random
from datetime import timedelta

from . import config
from .database import fetchall, fetchone, insert_player_event, iso, open_db, utc_now


async def start_background_jobs(app) -> list[asyncio.Task]:
    return [
        asyncio.create_task(_loop(app.state.db_path, config.HEAT_DECAY_INTERVAL_SECONDS, decay_all_territory_heat)),
        asyncio.create_task(_loop(app.state.db_path, config.ORDER_GENERATION_INTERVAL_SECONDS, generate_orders_for_all_territories)),
        asyncio.create_task(_loop(app.state.db_path, config.ORDER_EXPIRATION_INTERVAL_SECONDS, expire_overdue_orders)),
        asyncio.create_task(_loop(app.state.db_path, config.WARFARE_RESOLUTION_INTERVAL_SECONDS, resolve_territory_control)),
    ]


async def stop_background_jobs(tasks: list[asyncio.Task]) -> None:
    for task in tasks:
        task.cancel()
    for task in tasks:
        try:
            await task
        except asyncio.CancelledError:
            pass


async def _loop(db_path: str | None, interval_seconds: int, worker) -> None:
    while True:
        await asyncio.sleep(interval_seconds)
        await worker(db_path=db_path)


def _make_rng(seed_suffix: str | None = None) -> random.Random:
    seed = f"{utc_now().isoformat()}:{seed_suffix or ''}"
    return random.Random(seed)


async def ensure_initial_orders(db_path: str | None = None) -> None:
    async with open_db(db_path) as db:
        row = await fetchone(db, "SELECT COUNT(*) AS count FROM orders WHERE status = 'available'")
        if row is not None and row["count"] > 0:
            return
    await generate_orders_for_all_territories(db_path=db_path)


async def generate_orders_for_all_territories(db_path: str | None = None, rng: random.Random | None = None) -> None:
    rng = rng or _make_rng("orders")
    async with open_db(db_path) as db:
        territories = await fetchall(db, "SELECT * FROM territories ORDER BY id")
        now = utc_now()
        for territory in territories:
            backlog = await fetchone(
                db,
                "SELECT COUNT(*) AS count FROM orders WHERE territory_id = ? AND status = 'available'",
                (territory["id"],),
            )
            backlog_count = backlog["count"] if backlog is not None else 0
            target_count = rng.randint(max(1, territory["base_demand_volume"] // 2), territory["base_demand_volume"])
            if backlog_count >= target_count:
                continue

            for _ in range(target_count - backlog_count):
                client = _choose_client_for_territory(territory["id"], rng)
                min_quantity = rng.randint(client.min_quantity, client.max_quantity)
                max_quantity = max(min_quantity, min(client.max_quantity, min_quantity + rng.randint(0, max(0, client.max_quantity - min_quantity))))
                min_potency = max(client.min_potency, territory["base_demand_potency"] + rng.randint(-10, 10))
                max_toxicity = rng.randint(40, 80)
                product_type = rng.choice(config.PRODUCT_TYPES_BY_CLIENT[client.key])
                product_id = rng.choice(config.PRODUCT_IDS_BY_TYPE.get(product_type, [])) if rng.random() < 0.5 else None
                base_payout = int(round((min_quantity * 120 + min_potency * 12) * client.payout_multiplier))
                created_at = iso(now)
                expires_at = iso(now + timedelta(hours=client.deadline_hours))

                await db.execute(
                    """
                    INSERT INTO orders(
                        territory_id, client_type, product_type, product_id, min_quantity, max_quantity,
                        min_potency, max_toxicity, base_payout, deadline_hours, required_rank_tier,
                        status, created_at, expires_at
                    )
                    VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, 'available', ?, ?)
                    """,
                    (
                        territory["id"],
                        client.key,
                        product_type,
                        product_id,
                        min_quantity,
                        max_quantity,
                        min_potency,
                        max_toxicity,
                        base_payout,
                        client.deadline_hours,
                        client.unlock_rank_tier,
                        created_at,
                        expires_at,
                    ),
                )


def _choose_client_for_territory(territory_id: str, rng: random.Random) -> config.ClientSpec:
    territory = config.TERRITORIES[territory_id]
    weighted = []
    for client in config.CLIENTS.values():
        weight = client.weight
        if territory.id == "palmia_markets" and client.key in {"broker", "syndicate"}:
            weight += 5
        if territory.id == "derphy_underground" and client.key in {"street_buyer", "regular", "dependent"}:
            weight += 5
        weighted.append((client, weight))

    total = sum(weight for _, weight in weighted)
    roll = rng.uniform(0, total)
    upto = 0.0
    for client, weight in weighted:
        upto += weight
        if roll <= upto:
            return client
    return weighted[-1][0]


async def decay_all_territory_heat(db_path: str | None = None) -> None:
    async with open_db(db_path) as db:
        now = iso()
        territories = await fetchall(db, "SELECT * FROM territories")
        for territory in territories:
            decay = config.HEAT_DECAY_PER_CYCLE
            if territory["controlling_faction_id"] is not None:
                decay += config.FACTION_CONTROL_HEAT_DECAY_BONUS
            new_heat = max(0, territory["heat"] - decay)
            await db.execute("UPDATE territories SET heat = ? WHERE id = ?", (new_heat, territory["id"]))
        await db.execute("DELETE FROM territory_effects WHERE expires_at <= ?", (now,))


async def expire_overdue_orders(db_path: str | None = None) -> None:
    async with open_db(db_path) as db:
        now = iso()
        expired = await fetchall(
            db,
            """
            SELECT o.*, p.total_rep, p.underworld_rank
            FROM orders o
            LEFT JOIN players p ON p.id = o.assigned_player_id
            WHERE o.status IN ('available', 'accepted') AND o.expires_at <= ?
            """,
            (now,),
        )
        for order in expired:
            if order["assigned_player_id"] is not None:
                penalty = max(1, int(round(order["min_quantity"] * 0.5)))
                player = await fetchone(db, "SELECT * FROM players WHERE id = ?", (order["assigned_player_id"],))
                if player is not None:
                    old_rank = player["underworld_rank"]
                    total_rep = max(0, player["total_rep"] - penalty)
                    new_rank = config.rank_for_total_rep(total_rep)
                    await db.execute("UPDATE players SET total_rep = ?, underworld_rank = ? WHERE id = ?", (total_rep, new_rank, player["id"]))
                    await _upsert_reputation(db, player["id"], order["territory_id"], -penalty)
                    if new_rank != old_rank:
                        await insert_player_event(
                            db,
                            player["id"],
                            "rank_change",
                            {
                                "old_rank": old_rank,
                                "new_rank": new_rank,
                                "old_rank_name": config.rank_name(old_rank),
                                "new_rank_name": config.rank_name(new_rank),
                                "message": f"Your standing slipped to {config.rank_name(new_rank)}.",
                            },
                        )
            await db.execute("UPDATE orders SET status = 'expired', resolved_at = ? WHERE id = ?", (now, order["id"]))


async def resolve_territory_control(db_path: str | None = None) -> None:
    async with open_db(db_path) as db:
        territories = await fetchall(db, "SELECT * FROM territories ORDER BY id")
        for territory in territories:
            scores = json.loads(territory["control_scores_json"] or "{}")
            updated_scores = {
                faction_id: int(score * config.FACTION_INFLUENCE_DECAY)
                for faction_id, score in scores.items()
                if int(score * config.FACTION_INFLUENCE_DECAY) > 0
            }

            old_faction_id = territory["controlling_faction_id"]
            new_faction_id = None
            if updated_scores:
                ranked = sorted(updated_scores.items(), key=lambda item: item[1], reverse=True)
                top_faction_id, top_score = ranked[0]
                runner_up = ranked[1][1] if len(ranked) > 1 else 0
                if top_score > runner_up * config.FACTION_CONTROL_LEAD_MULTIPLIER:
                    new_faction_id = int(top_faction_id)

            if old_faction_id != new_faction_id:
                await db.execute(
                    "UPDATE territories SET controlling_faction_id = ?, control_scores_json = ? WHERE id = ?",
                    (new_faction_id, json.dumps(updated_scores), territory["id"]),
                )
                await _notify_territory_change(db, territory["id"], old_faction_id, new_faction_id)
            else:
                await db.execute("UPDATE territories SET control_scores_json = ? WHERE id = ?", (json.dumps(updated_scores), territory["id"]))


async def _notify_territory_change(db, territory_id: str, old_faction_id: int | None, new_faction_id: int | None) -> None:
    territory_name = config.TERRITORIES[territory_id].name
    old_name = None
    new_name = None
    if old_faction_id is not None:
        row = await fetchone(db, "SELECT name FROM factions WHERE id = ?", (old_faction_id,))
        old_name = row["name"] if row is not None else None
    if new_faction_id is not None:
        row = await fetchone(db, "SELECT name FROM factions WHERE id = ?", (new_faction_id,))
        new_name = row["name"] if row is not None else None

    players_to_notify: set[int] = set()
    for faction_id in {old_faction_id, new_faction_id}:
        if faction_id is None:
            continue
        members = await fetchall(db, "SELECT player_id FROM faction_members WHERE faction_id = ?", (faction_id,))
        players_to_notify.update(member["player_id"] for member in members)

    if not players_to_notify:
        players = await fetchall(db, "SELECT id FROM players")
        players_to_notify.update(player["id"] for player in players)

    if new_name and old_name:
        message = f"{territory_name} has fallen to {new_name}."
    elif new_name:
        message = f"{new_name} now controls {territory_name}."
    else:
        message = f"{territory_name} is now contested."

    payload = {
        "territory_id": territory_id,
        "old_faction": old_name,
        "new_faction": new_name,
        "message": message,
    }
    for player_id in players_to_notify:
        await insert_player_event(db, player_id, "territory_change", payload)


async def _upsert_reputation(db, player_id: int, territory_id: str, delta: int) -> None:
    row = await fetchone(db, "SELECT local_rep FROM reputation WHERE player_id = ? AND territory_id = ?", (player_id, territory_id))
    current = row["local_rep"] if row is not None else 0
    new_value = max(0, current + delta)
    await db.execute(
        """
        INSERT INTO reputation(player_id, territory_id, local_rep)
        VALUES (?, ?, ?)
        ON CONFLICT(player_id, territory_id) DO UPDATE SET local_rep = excluded.local_rep
        """,
        (player_id, territory_id, new_value),
    )
