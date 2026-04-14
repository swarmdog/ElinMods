from __future__ import annotations

from fastapi import APIRouter, Depends, Query
from pydantic import BaseModel

from auth import get_current_account
from database import connect


router = APIRouter(prefix="/ladder", tags=["ladder"])


class LadderEntry(BaseModel):
    rank: int
    display_name: str
    total_score: int


class GlobalLadderResponse(BaseModel):
    entries: list[LadderEntry]


class SelfRankResponse(BaseModel):
    display_name: str
    rank: int | None
    total_score: int
    percentile: float | None
    total_players: int


@router.get("/global", response_model=GlobalLadderResponse)
def global_ladder(limit: int = Query(default=20, ge=1, le=100)) -> GlobalLadderResponse:
    with connect() as db:
        rows = db.execute(
            """
            SELECT display_name, total_score,
                   RANK() OVER (ORDER BY total_score DESC) AS rank
            FROM ladder_snapshot
            ORDER BY total_score DESC, last_updated_at ASC
            LIMIT ?
            """,
            (limit,),
        ).fetchall()

    return GlobalLadderResponse(
        entries=[
            LadderEntry(rank=row["rank"], display_name=row["display_name"], total_score=row["total_score"])
            for row in rows
        ]
    )


@router.get("/self", response_model=SelfRankResponse)
def self_rank(account: dict = Depends(get_current_account)) -> SelfRankResponse:
    account_id = account["id"]
    with connect() as db:
        total_players = db.execute("SELECT COUNT(*) AS count FROM ladder_snapshot").fetchone()["count"]
        row = db.execute(
            """
            WITH ranked AS (
                SELECT player_id, display_name, total_score,
                       RANK() OVER (ORDER BY total_score DESC) AS rank
                FROM ladder_snapshot
            )
            SELECT display_name, total_score, rank
            FROM ranked
            WHERE player_id = ?
            """,
            (account_id,),
        ).fetchone()

    if row is None:
        return SelfRankResponse(
            display_name=account["display_name"],
            rank=None,
            total_score=0,
            percentile=None,
            total_players=total_players,
        )

    rank = row["rank"]
    percentile = 100.0 if total_players <= 1 else round(100.0 * (total_players - rank + 1) / total_players, 2)
    return SelfRankResponse(
        display_name=row["display_name"],
        rank=rank,
        total_score=row["total_score"],
        percentile=percentile,
        total_players=total_players,
    )
