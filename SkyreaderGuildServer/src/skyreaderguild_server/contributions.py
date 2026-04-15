from __future__ import annotations

import uuid
from typing import Literal

from fastapi import APIRouter, Depends
from pydantic import BaseModel, Field

from .auth import get_current_account, iso, now_utc
from .database import connect


ContributionType = Literal["Extraction", "RiftClear", "BossKill", "MeteorCoreHarvest"]

router = APIRouter(prefix="/contributions", tags=["contributions"])


class ContributionIn(BaseModel):
    type: ContributionType
    amount: int = Field(ge=1, le=10000)
    local_event_id: str = Field(min_length=8, max_length=128)
    timestamp: str | None = Field(default=None, max_length=64)


class ContributionBatch(BaseModel):
    display_name: str = Field(min_length=1, max_length=80)
    highest_rank: int = Field(default=0, ge=0, le=10000)
    contributions: list[ContributionIn] = Field(min_length=1, max_length=100)


class ContributionBatchResult(BaseModel):
    accepted: int
    duplicates: int
    total_score: int


@router.post("/batch", response_model=ContributionBatchResult)
def submit_batch(
    request: ContributionBatch,
    account: dict = Depends(get_current_account),
) -> ContributionBatchResult:
    account_id = account["id"]
    accepted = 0
    duplicates = 0
    stamp = iso(now_utc())

    with connect() as db:
        db.execute(
            """
            UPDATE guild_accounts
            SET display_name = ?, highest_rank = MAX(highest_rank, ?), last_seen_at = ?
            WHERE id = ?
            """,
            (request.display_name, request.highest_rank, stamp, account_id),
        )

        for contribution in request.contributions:
            cursor = db.execute(
                """
                INSERT OR IGNORE INTO astral_contributions(
                    id, player_id, type, amount, local_event_id, client_timestamp, created_at
                )
                VALUES (?, ?, ?, ?, ?, ?, ?)
                """,
                (
                    str(uuid.uuid4()),
                    account_id,
                    contribution.type,
                    contribution.amount,
                    contribution.local_event_id,
                    contribution.timestamp,
                    stamp,
                ),
            )
            if cursor.rowcount == 1:
                accepted += 1
            else:
                duplicates += 1

        total_score = db.execute(
            "SELECT COALESCE(SUM(amount), 0) AS total FROM astral_contributions WHERE player_id = ?",
            (account_id,),
        ).fetchone()["total"]

        db.execute(
            """
            INSERT INTO ladder_snapshot(player_id, display_name, total_score, last_updated_at)
            VALUES (?, ?, ?, ?)
            ON CONFLICT(player_id) DO UPDATE SET
                display_name = excluded.display_name,
                total_score = excluded.total_score,
                last_updated_at = excluded.last_updated_at
            """,
            (account_id, request.display_name, total_score, stamp),
        )

    return ContributionBatchResult(accepted=accepted, duplicates=duplicates, total_score=total_score)
