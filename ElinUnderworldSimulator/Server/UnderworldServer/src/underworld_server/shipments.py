from __future__ import annotations

import json
import random
from datetime import timedelta

import aiosqlite
from fastapi import APIRouter, Depends, HTTPException, Request, status

from . import config, schemas
from .auth import get_current_player
from .database import fetchall, fetchone, get_db, insert_player_event, iso, parse_iso, utc_now
from .resolution import calculate_final_payout, calculate_heat_delta, calculate_rep_delta, calculate_satisfaction, resolve_enforcement


router = APIRouter(prefix="/api/shipments", tags=["shipments"])


@router.post("/submit", response_model=schemas.ShipmentSubmitResponse)
async def submit_shipment(
    request: schemas.ShipmentSubmitRequest,
    http_request: Request,
    player=Depends(get_current_player),
    db=Depends(get_db),
) -> schemas.ShipmentSubmitResponse:
    now_dt = utc_now()
    now_iso = iso(now_dt)

    order = await fetchone(db, "SELECT * FROM orders WHERE id = ? AND assigned_player_id = ?", (request.order_id, player["id"]))
    if order is None:
        raise HTTPException(status_code=status.HTTP_404_NOT_FOUND, detail="No accepted order found")
    if order["status"] != "accepted":
        raise HTTPException(status_code=status.HTTP_409_CONFLICT, detail="Order already resolved")
    order_expires_at = parse_iso(order["expires_at"])
    if order_expires_at is not None and order_expires_at <= now_dt:
        raise HTTPException(status_code=status.HTTP_409_CONFLICT, detail="Order expired")
    if request.quantity < order["min_quantity"] or request.quantity > order["max_quantity"]:
        raise HTTPException(
            status_code=status.HTTP_400_BAD_REQUEST,
            detail=f"Quantity must be between {order['min_quantity']} and {order['max_quantity']}",
        )

    lock_cursor = await db.execute(
        """
        UPDATE orders
        SET status = 'resolving'
        WHERE id = ? AND assigned_player_id = ? AND status = 'accepted' AND expires_at > ?
        """,
        (request.order_id, player["id"], now_iso),
    )
    lock_rows = lock_cursor.rowcount
    await lock_cursor.close()
    if lock_rows != 1:
        refreshed = await fetchone(db, "SELECT * FROM orders WHERE id = ? AND assigned_player_id = ?", (request.order_id, player["id"]))
        if refreshed is None:
            raise HTTPException(status_code=status.HTTP_404_NOT_FOUND, detail="No accepted order found")
        refreshed_expires_at = parse_iso(refreshed["expires_at"])
        if refreshed_expires_at is not None and refreshed_expires_at <= now_dt:
            raise HTTPException(status_code=status.HTTP_409_CONFLICT, detail="Order expired")
        raise HTTPException(status_code=status.HTTP_409_CONFLICT, detail="Order already resolved")

    territory = await fetchone(db, "SELECT * FROM territories WHERE id = ?", (order["territory_id"],))
    if territory is None:
        raise HTTPException(status_code=status.HTTP_500_INTERNAL_SERVER_ERROR, detail="Territory missing")

    surveillance_active = await fetchone(
        db,
        "SELECT 1 AS active FROM territory_effects WHERE territory_id = ? AND effect_type = 'surveillance'",
        (territory["id"],),
    )
    rng = getattr(http_request.app.state, "rng", None) or random.Random()

    enforcement = resolve_enforcement(
        rng,
        territory["heat"],
        territory["heat_capacity"],
        request.quantity,
        player["gold"],
        surveillance_active is not None,
    )
    effective_quantity = enforcement["effective_quantity"]
    bust_triggered = enforcement["bust"]
    surveillance_triggered = enforcement["surveillance"]

    satisfaction = 0.0 if bust_triggered else calculate_satisfaction(order, effective_quantity, request.avg_potency, request.avg_toxicity)
    outcome = "failed" if bust_triggered or satisfaction < 0.3 else "completed"

    coordination_bonus = False
    territory_controlled = territory["controlling_faction_id"] is not None and territory["controlling_faction_id"] == player["faction_id"]
    if player["faction_id"] is not None:
        recent_rows = await fetchall(
            db,
            """
            SELECT DISTINCT s.player_id
            FROM shipments s
            JOIN players p ON p.id = s.player_id
            WHERE p.faction_id = ? AND s.territory_id = ? AND s.submitted_at >= ?
            """,
            (player["faction_id"], territory["id"], iso(utc_now() - timedelta(hours=24))),
        )
        coordination_bonus = len(recent_rows) + 1 >= config.FACTION_COORDINATION_THRESHOLD

    final_payout = 0 if outcome == "failed" else calculate_final_payout(order, satisfaction, player["underworld_rank"], territory_controlled, coordination_bonus)
    rep_delta = calculate_rep_delta(order, satisfaction, outcome)
    heat_delta = calculate_heat_delta(order["client_type"], request.avg_traceability, bust_triggered, surveillance_triggered)

    gold_penalty = enforcement["event"]["gold_penalty"] if bust_triggered and enforcement["event"] is not None else 0
    player_total_rep = max(0, player["total_rep"] + rep_delta)
    new_rank = config.rank_for_total_rep(player_total_rep)
    player_gold = max(0, player["gold"] + final_payout - gold_penalty)
    new_heat = min(territory["heat_capacity"], territory["heat"] + heat_delta)

    submitted_at = now_iso
    enforcement_event_json = json.dumps(enforcement["event"]) if enforcement["event"] is not None else None
    try:
        cursor = await db.execute(
            """
            INSERT INTO shipments(
                order_id, player_id, territory_id, client_type, quantity, avg_potency, avg_toxicity, avg_traceability,
                item_ids_json, satisfaction_score, final_payout, outcome, heat_delta, rep_delta,
                enforcement_event_json, submitted_at, resolved_at
            )
            VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
            """,
            (
                order["id"],
                player["id"],
                territory["id"],
                order["client_type"],
                request.quantity,
                request.avg_potency,
                request.avg_toxicity,
                request.avg_traceability,
                json.dumps(request.item_ids),
                satisfaction,
                final_payout,
                outcome,
                heat_delta,
                rep_delta,
                enforcement_event_json,
                submitted_at,
                submitted_at,
            ),
        )
    except aiosqlite.IntegrityError as exc:
        raise HTTPException(status_code=status.HTTP_409_CONFLICT, detail="Order already resolved") from exc
    shipment_id = cursor.lastrowid
    await cursor.close()

    await db.execute(
        "UPDATE orders SET status = ?, resolved_at = ? WHERE id = ? AND status = 'resolving'",
        (outcome, submitted_at, order["id"]),
    )
    await db.execute(
        """
        UPDATE players
        SET total_rep = ?, underworld_rank = ?, gold = ?, under_investigation_until = ?
        WHERE id = ?
        """,
        (
            player_total_rep,
            new_rank,
            player_gold,
            iso(utc_now() + timedelta(hours=config.INVESTIGATION_DURATION_HOURS)) if bust_triggered else player["under_investigation_until"],
            player["id"],
        ),
    )
    await _upsert_reputation(db, player["id"], territory["id"], rep_delta)
    await db.execute("UPDATE territories SET heat = ? WHERE id = ?", (new_heat, territory["id"]))

    if surveillance_triggered:
        await db.execute(
            """
            INSERT INTO territory_effects(territory_id, effect_type, expires_at)
            VALUES (?, 'surveillance', ?)
            ON CONFLICT(territory_id, effect_type) DO UPDATE SET expires_at = excluded.expires_at
            """,
            (territory["id"], iso(utc_now() + timedelta(hours=config.SURVEILLANCE_DURATION_HOURS))),
        )

    if player["faction_id"] is not None and outcome == "completed" and final_payout > 0:
        scores = json.loads(territory["control_scores_json"] or "{}")
        faction_key = str(player["faction_id"])
        scores[faction_key] = scores.get(faction_key, 0) + int(final_payout * max(1.0, satisfaction))
        await db.execute("UPDATE territories SET control_scores_json = ? WHERE id = ?", (json.dumps(scores), territory["id"]))

    if new_rank != player["underworld_rank"]:
        await insert_player_event(
            db,
            player["id"],
            "rank_change",
            {
                "old_rank": player["underworld_rank"],
                "new_rank": new_rank,
                "old_rank_name": config.rank_name(player["underworld_rank"]),
                "new_rank_name": config.rank_name(new_rank),
                "message": f"Your standing has risen. You are now a {config.rank_name(new_rank)}.",
            },
        )

    return schemas.ShipmentSubmitResponse(
        shipment_id=shipment_id,
        outcome=outcome,
        satisfaction_score=round(satisfaction, 4),
        final_payout=final_payout,
        heat_delta=heat_delta,
        rep_delta=rep_delta,
        enforcement_event=schemas.EnforcementEvent.model_validate(enforcement["event"]) if enforcement["event"] is not None else None,
        territory_heat_after=new_heat,
    )


@router.get("/results", response_model=schemas.ShipmentResultsResponse)
async def shipment_results(player=Depends(get_current_player), db=Depends(get_db)) -> schemas.ShipmentResultsResponse:
    shipments = await fetchall(
        db,
        """
        SELECT id, order_id, outcome, final_payout, rep_delta, enforcement_event_json
        FROM shipments
        WHERE player_id = ? AND result_seen_at IS NULL
        ORDER BY resolved_at ASC, id ASC
        """,
        (player["id"],),
    )
    events = await fetchall(
        db,
        "SELECT * FROM player_events WHERE player_id = ? AND seen_at IS NULL ORDER BY created_at ASC, id ASC",
        (player["id"],),
    )

    now = iso()
    if shipments:
        shipment_ids = ",".join(str(row["id"]) for row in shipments)
        await db.execute(f"UPDATE shipments SET result_seen_at = ? WHERE id IN ({shipment_ids})", (now,))
    if events:
        event_ids = ",".join(str(row["id"]) for row in events)
        await db.execute(f"UPDATE player_events SET seen_at = ? WHERE id IN ({event_ids})", (now,))

    territory_changes: list[schemas.TerritoryChangeDto] = []
    rank_change = None
    for event in events:
        payload = json.loads(event["payload_json"])
        if event["event_type"] == "territory_change":
            territory_changes.append(schemas.TerritoryChangeDto.model_validate(payload))
        elif event["event_type"] == "rank_change":
            rank_change = schemas.RankChangeDto.model_validate(payload)

    return schemas.ShipmentResultsResponse(
        results=[
            schemas.ShipmentResultDto(
                shipment_id=row["id"],
                order_id=row["order_id"],
                outcome=row["outcome"],
                final_payout=row["final_payout"],
                rep_delta=row["rep_delta"],
                enforcement_event=schemas.EnforcementEvent.model_validate(json.loads(row["enforcement_event_json"])) if row["enforcement_event_json"] else None,
            )
            for row in shipments
        ],
        territory_changes=territory_changes,
        rank_change=rank_change,
    )


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
