from __future__ import annotations

import json
from datetime import datetime, timezone

from fastapi import APIRouter, Depends
from pydantic import BaseModel

from auth import get_current_account
from database import connect


router = APIRouter(prefix="/sky-season", tags=["seasons"])


class SeasonModifiers(BaseModel):
    meteorChanceMultiplier: float = 1.0
    skysignWeights: dict[str, float] = {}
    yithSpawnBonus: dict[str, float] = {}
    gpMultiplier: dict[str, float] = {}
    geometryBias: str = ""


class SeasonResponse(BaseModel):
    id: str
    name: str
    description: str
    starts_at: str | None
    ends_at: str | None
    duration_days: int
    modifiers: dict


_DEFAULT_SEASON = SeasonResponse(
    id="default",
    name="Season of Stars",
    description="The heavens hold steady. No particular phenomena dominate the sky.",
    starts_at=None,
    ends_at=None,
    duration_days=28,
    modifiers={},
)


@router.get("/current", response_model=SeasonResponse)
def get_current_season(
    account: dict = Depends(get_current_account),
) -> SeasonResponse:
    now = datetime.now(timezone.utc).isoformat(timespec="seconds").replace("+00:00", "Z")
    with connect() as db:
        row = db.execute(
            "SELECT * FROM sky_seasons WHERE starts_at <= ? AND ends_at > ? LIMIT 1",
            (now, now),
        ).fetchone()

    if row is None:
        return _DEFAULT_SEASON

    return SeasonResponse(
        id=row["id"],
        name=row["name"],
        description=row["description"],
        starts_at=row["starts_at"],
        ends_at=row["ends_at"],
        duration_days=row["duration_days"],
        modifiers=json.loads(row["modifiers"]),
    )
