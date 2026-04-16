from __future__ import annotations

from fastapi import APIRouter, Depends

from . import config, schemas
from .auth import get_current_player
from .database import fetchall, fetchone, get_db


router = APIRouter(prefix="/api/player", tags=["player"])


@router.get("/status", response_model=schemas.PlayerStatusResponse)
async def player_status(player=Depends(get_current_player), db=Depends(get_db)) -> schemas.PlayerStatusResponse:
    faction = None
    if player["faction_id"] is not None:
        faction_row = await fetchone(
            db,
            """
            SELECT f.id, f.name, fm.role
            FROM factions f
            JOIN faction_members fm ON fm.faction_id = f.id AND fm.player_id = ?
            WHERE f.id = ?
            """,
            (player["id"], player["faction_id"]),
        )
        if faction_row is not None:
            faction = schemas.PlayerFactionDto(id=faction_row["id"], name=faction_row["name"], role=faction_row["role"])

    rep_rows = await fetchall(db, "SELECT territory_id, local_rep FROM reputation WHERE player_id = ?", (player["id"],))
    active_orders = await fetchone(db, "SELECT COUNT(*) AS count FROM orders WHERE assigned_player_id = ? AND status = 'accepted'", (player["id"],))
    pending_shipments = await fetchone(
        db,
        "SELECT COUNT(*) AS count FROM shipments WHERE player_id = ? AND result_seen_at IS NULL",
        (player["id"],),
    )

    return schemas.PlayerStatusResponse(
        player_id=player["id"],
        display_name=player["display_name"],
        underworld_rank=player["underworld_rank"],
        rank_name=config.rank_name(player["underworld_rank"]),
        total_rep=player["total_rep"],
        gold=player["gold"],
        faction=faction,
        reputation_by_territory={row["territory_id"]: row["local_rep"] for row in rep_rows},
        active_orders_count=active_orders["count"] if active_orders is not None else 0,
        pending_shipments_count=pending_shipments["count"] if pending_shipments is not None else 0,
    )
