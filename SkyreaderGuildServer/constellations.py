from __future__ import annotations

import json
import uuid
from datetime import datetime, timezone

from fastapi import APIRouter, Depends, HTTPException, status
from pydantic import BaseModel, Field

from auth import get_current_account
from database import connect


router = APIRouter(prefix="/constellations", tags=["constellations"])

_AGGREGATION_STALE_SECONDS = 600  # 10 minutes


class JoinRequest(BaseModel):
    constellation_id: str = Field(min_length=1, max_length=128)


class ConstellationEntry(BaseModel):
    id: str
    name: str
    description: str
    lore_domain: str
    goals: dict[str, int]
    progress: dict[str, int]
    member_count: int
    goals_met: bool


class ConstellationsResponse(BaseModel):
    season_id: str | None
    season_name: str | None
    player_constellation_id: str | None
    constellations: list[ConstellationEntry]


class JoinResponse(BaseModel):
    joined: bool
    constellation_id: str


def _now_iso() -> str:
    return datetime.now(timezone.utc).isoformat(timespec="seconds").replace("+00:00", "Z")


def _get_current_season(db):
    now = _now_iso()
    return db.execute(
        "SELECT * FROM sky_seasons WHERE starts_at <= ? AND ends_at > ? LIMIT 1",
        (now, now),
    ).fetchone()


def _aggregate_if_stale(db, constellation_id: str, metric_type: str, season_starts_at: str) -> int:
    """Return the current aggregate for a constellation+metric, recomputing if stale."""
    row = db.execute(
        "SELECT current_amount, last_aggregated_at FROM constellation_progress WHERE constellation_id = ? AND metric_type = ?",
        (constellation_id, metric_type),
    ).fetchone()

    now = datetime.now(timezone.utc)
    if row is not None:
        last_agg = datetime.fromisoformat(row["last_aggregated_at"].replace("Z", "+00:00"))
        if (now - last_agg).total_seconds() < _AGGREGATION_STALE_SECONDS:
            return row["current_amount"]

    # Recompute: sum contributions from all members of this constellation since season start.
    result = db.execute(
        """
        SELECT COALESCE(SUM(ac.amount), 0) AS total
        FROM astral_contributions ac
        JOIN constellation_memberships cm ON cm.player_id = ac.player_id
        WHERE cm.constellation_id = ? AND ac.type = ? AND ac.created_at >= ?
        """,
        (constellation_id, metric_type, season_starts_at),
    ).fetchone()
    total = result["total"]

    now_str = _now_iso()
    db.execute(
        """
        INSERT INTO constellation_progress(constellation_id, metric_type, current_amount, last_aggregated_at)
        VALUES (?, ?, ?, ?)
        ON CONFLICT(constellation_id, metric_type) DO UPDATE SET
            current_amount = excluded.current_amount,
            last_aggregated_at = excluded.last_aggregated_at
        """,
        (constellation_id, metric_type, total, now_str),
    )
    return total


@router.get("/current", response_model=ConstellationsResponse)
def get_constellations(
    account: dict = Depends(get_current_account),
) -> ConstellationsResponse:
    account_id = account["id"]

    with connect() as db:
        season = _get_current_season(db)
        if season is None:
            return ConstellationsResponse(
                season_id=None,
                season_name=None,
                player_constellation_id=None,
                constellations=[],
            )

        season_id = season["id"]
        season_name = season["name"]
        season_starts = season["starts_at"]

        rows = db.execute(
            "SELECT * FROM constellations WHERE season_id = ?",
            (season_id,),
        ).fetchall()

        # Find player's membership for this season.
        player_membership = db.execute(
            """
            SELECT cm.constellation_id FROM constellation_memberships cm
            JOIN constellations c ON c.id = cm.constellation_id
            WHERE cm.player_id = ? AND c.season_id = ?
            """,
            (account_id, season_id),
        ).fetchone()
        player_constellation_id = player_membership["constellation_id"] if player_membership else None

        entries = []
        for row in rows:
            goals = json.loads(row["goal_config"])
            progress = {}
            all_met = True
            for metric, target in goals.items():
                current = _aggregate_if_stale(db, row["id"], metric, season_starts)
                progress[metric] = current
                if current < target:
                    all_met = False
            if not goals:
                all_met = False

            member_count = db.execute(
                "SELECT COUNT(*) AS n FROM constellation_memberships WHERE constellation_id = ?",
                (row["id"],),
            ).fetchone()["n"]

            entries.append(ConstellationEntry(
                id=row["id"],
                name=row["name"],
                description=row["description"],
                lore_domain=row["lore_domain"],
                goals=goals,
                progress=progress,
                member_count=member_count,
                goals_met=all_met,
            ))

    return ConstellationsResponse(
        season_id=season_id,
        season_name=season_name,
        player_constellation_id=player_constellation_id,
        constellations=entries,
    )


@router.post("/join", response_model=JoinResponse)
def join_constellation(
    request: JoinRequest,
    account: dict = Depends(get_current_account),
) -> JoinResponse:
    account_id = account["id"]

    with connect() as db:
        season = _get_current_season(db)
        if season is None:
            raise HTTPException(
                status_code=status.HTTP_400_BAD_REQUEST,
                detail="No active season.",
            )

        season_id = season["id"]

        # Verify the constellation exists and belongs to the current season.
        constellation = db.execute(
            "SELECT id FROM constellations WHERE id = ? AND season_id = ?",
            (request.constellation_id, season_id),
        ).fetchone()
        if constellation is None:
            raise HTTPException(
                status_code=status.HTTP_404_NOT_FOUND,
                detail="Constellation not found in the current season.",
            )

        # Check if the player already joined any constellation this season.
        existing = db.execute(
            """
            SELECT cm.constellation_id FROM constellation_memberships cm
            JOIN constellations c ON c.id = cm.constellation_id
            WHERE cm.player_id = ? AND c.season_id = ?
            """,
            (account_id, season_id),
        ).fetchone()
        if existing is not None:
            raise HTTPException(
                status_code=status.HTTP_409_CONFLICT,
                detail=f"Already joined constellation '{existing['constellation_id']}' this season.",
            )

        db.execute(
            "INSERT INTO constellation_memberships(player_id, constellation_id, joined_at) VALUES (?, ?, ?)",
            (account_id, request.constellation_id, _now_iso()),
        )

    return JoinResponse(joined=True, constellation_id=request.constellation_id)
