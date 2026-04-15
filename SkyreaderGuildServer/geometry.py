from __future__ import annotations

import uuid
from datetime import datetime, timezone
from typing import Literal

from fastapi import APIRouter, Depends, HTTPException, status
from pydantic import BaseModel, Field

from auth import get_current_account
from database import connect


router = APIRouter(prefix="/geometry", tags=["geometry"])

VALID_SHAPES = {"Circle", "Ellipse", "Diamond", "Crescent", "Cross", "Star", "LShape", "Irregular"}

_AGGREGATION_STALE_SECONDS = 600  # 10 minutes

SHAPE_FLAVORS = {
    "Circle": "Circular rifts suggest cosmic harmony. The spheres are in alignment.",
    "Ellipse": "Elliptical rifts stretch across the void, traces of orbital decay.",
    "Diamond": "Diamond-cut rifts gleam with crystalline precision.",
    "Crescent": "Crescent rifts wax and wane with the astral tides.",
    "Cross": "Cross-shaped rifts mark the intersection of dimensional fault lines.",
    "Star": "Star-shaped rifts blaze across the firmament this season.",
    "LShape": "L-shaped rifts suggest broken symmetry. Something is off-balance.",
    "Irregular": "The rifts defy classification. Chaos reigns in the geometry.",
}


class GeometrySampleIn(BaseModel):
    danger_band: int = Field(ge=1, le=10)
    shape_type: str = Field(min_length=1, max_length=20)
    room_count: int = Field(ge=1, le=100)


class GeometrySampleResult(BaseModel):
    accepted: bool
    sample_id: str


class BandDistribution(BaseModel):
    shapes: dict[str, float]


class GeometrySummary(BaseModel):
    season_name: str | None
    total_samples: int
    bands: dict[str, dict[str, float]]
    dominant_shape: str | None
    dominant_flavor: str
    geometry_bias: str


def _now_iso() -> str:
    return datetime.now(timezone.utc).isoformat(timespec="seconds").replace("+00:00", "Z")


def _get_current_season(db):
    now = _now_iso()
    return db.execute(
        "SELECT * FROM sky_seasons WHERE starts_at <= ? AND ends_at > ? LIMIT 1",
        (now, now),
    ).fetchone()


@router.post("/sample", response_model=GeometrySampleResult)
def submit_sample(
    request: GeometrySampleIn,
    account: dict = Depends(get_current_account),
) -> GeometrySampleResult:
    if request.shape_type not in VALID_SHAPES:
        raise HTTPException(
            status_code=status.HTTP_422_UNPROCESSABLE_ENTITY,
            detail=f"Invalid shape_type '{request.shape_type}'. Must be one of: {sorted(VALID_SHAPES)}",
        )

    sample_id = str(uuid.uuid4())
    with connect() as db:
        db.execute(
            """
            INSERT INTO geometry_samples(id, player_id, danger_band, shape_type, room_count, sampled_at)
            VALUES (?, ?, ?, ?, ?, ?)
            """,
            (
                sample_id,
                account["id"],
                request.danger_band,
                request.shape_type,
                request.room_count,
                _now_iso(),
            ),
        )

    return GeometrySampleResult(accepted=True, sample_id=sample_id)


@router.get("/summary", response_model=GeometrySummary)
def get_summary(
    account: dict = Depends(get_current_account),
) -> GeometrySummary:
    import json

    with connect() as db:
        season = _get_current_season(db)
        if season is None:
            return GeometrySummary(
                season_name=None,
                total_samples=0,
                bands={},
                dominant_shape=None,
                dominant_flavor="No active season.",
                geometry_bias="",
            )

        season_id = season["id"]
        season_name = season["name"]
        season_starts = season["starts_at"]
        modifiers = json.loads(season["modifiers"])
        geometry_bias = modifiers.get("geometryBias", "")

        # Check if aggregation is stale.
        agg_row = db.execute(
            "SELECT MIN(last_aggregated_at) AS oldest FROM geometry_aggregates WHERE season_id = ?",
            (season_id,),
        ).fetchone()

        needs_reagg = True
        now = datetime.now(timezone.utc)
        if agg_row and agg_row["oldest"]:
            oldest = datetime.fromisoformat(agg_row["oldest"].replace("Z", "+00:00"))
            if (now - oldest).total_seconds() < _AGGREGATION_STALE_SECONDS:
                needs_reagg = False

        now_str = _now_iso()

        if needs_reagg:
            # Recompute from raw samples.
            db.execute(
                "DELETE FROM geometry_aggregates WHERE season_id = ?",
                (season_id,),
            )
            db.execute(
                """
                INSERT INTO geometry_aggregates(season_id, danger_band, shape_type, count, last_aggregated_at)
                SELECT ?, danger_band, shape_type, COUNT(*), ?
                FROM geometry_samples
                WHERE sampled_at >= ?
                GROUP BY danger_band, shape_type
                """,
                (season_id, now_str, season_starts),
            )

        # Read aggregates.
        rows = db.execute(
            "SELECT danger_band, shape_type, count FROM geometry_aggregates WHERE season_id = ?",
            (season_id,),
        ).fetchall()

    # Build response.
    band_totals: dict[int, dict[str, int]] = {}
    global_shape_counts: dict[str, int] = {}
    total_samples = 0

    for row in rows:
        band = row["danger_band"]
        shape = row["shape_type"]
        count = row["count"]
        total_samples += count

        if band not in band_totals:
            band_totals[band] = {}
        band_totals[band][shape] = count
        global_shape_counts[shape] = global_shape_counts.get(shape, 0) + count

    # Convert to percentages.
    bands: dict[str, dict[str, float]] = {}
    for band, shapes in sorted(band_totals.items()):
        band_total = sum(shapes.values())
        if band_total > 0:
            bands[str(band)] = {s: round(c / band_total, 4) for s, c in sorted(shapes.items())}
        else:
            bands[str(band)] = {}

    dominant_shape = max(global_shape_counts, key=global_shape_counts.get) if global_shape_counts else None
    dominant_flavor = SHAPE_FLAVORS.get(dominant_shape, "The geometry is unclear.") if dominant_shape else "No data collected yet."

    return GeometrySummary(
        season_name=season_name,
        total_samples=total_samples,
        bands=bands,
        dominant_shape=dominant_shape,
        dominant_flavor=dominant_flavor,
        geometry_bias=geometry_bias,
    )
