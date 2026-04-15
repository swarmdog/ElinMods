from __future__ import annotations

import uuid
from datetime import datetime, timezone

from fastapi import APIRouter, Depends, HTTPException, status
from pydantic import BaseModel, Field

from .auth import get_current_account
from .database import connect


router = APIRouter(prefix="/comet", tags=["comet"])

_AGGREGATION_STALE_SECONDS = 600  # 10 minutes


class CometReportIn(BaseModel):
    site_id: str = Field(min_length=1, max_length=80)
    site_name: str = Field(min_length=1, max_length=120)
    world_x: int = Field(ge=-1024, le=1024)
    world_y: int = Field(ge=-1024, le=1024)
    touched_count: int = Field(ge=0, le=10000)
    cleansed_count: int = Field(ge=0, le=10000)


class CometReportResult(BaseModel):
    accepted: bool
    report_id: str


class SiteHeat(BaseModel):
    site_id: str
    site_name: str
    world_x: int
    world_y: int
    touched: int
    cleansed: int
    ratio: float
    status: str


class HeatmapResponse(BaseModel):
    season_name: str | None
    sites: list[SiteHeat]


def _now_iso() -> str:
    return datetime.now(timezone.utc).isoformat(timespec="seconds").replace("+00:00", "Z")


def _get_current_season(db):
    now = _now_iso()
    return db.execute(
        "SELECT * FROM sky_seasons WHERE starts_at <= ? AND ends_at > ? LIMIT 1",
        (now, now),
    ).fetchone()


def _status_tier(ratio: float) -> str:
    if ratio >= 0.8:
        return "Calm"
    if ratio >= 0.5:
        return "Stirring"
    if ratio >= 0.2:
        return "Troubled"
    return "Overrun"


def _site_is_valid(db, site_id: str) -> bool:
    if site_id.startswith("srg_"):
        return True

    existing = db.execute(
        "SELECT 1 FROM comet_sites WHERE id = ? LIMIT 1",
        (site_id,),
    ).fetchone()
    if existing is not None:
        return True

    catalog = db.execute(
        "SELECT 1 FROM comet_site_catalog WHERE id = ? LIMIT 1",
        (site_id,),
    ).fetchone()
    return catalog is not None


@router.post("/report", response_model=CometReportResult)
def submit_report(
    request: CometReportIn,
    account: dict = Depends(get_current_account),
) -> CometReportResult:
    report_id = str(uuid.uuid4())
    now = _now_iso()

    with connect() as db:
        if not _site_is_valid(db, request.site_id):
            raise HTTPException(
                status_code=status.HTTP_422_UNPROCESSABLE_ENTITY,
                detail=f"Unknown site_id '{request.site_id}'.",
            )

        db.execute(
            """
            INSERT INTO comet_sites(id, name, world_x, world_y, last_reported_at)
            VALUES (?, ?, ?, ?, ?)
            ON CONFLICT(id) DO UPDATE SET
                name = excluded.name,
                world_x = excluded.world_x,
                world_y = excluded.world_y,
                last_reported_at = excluded.last_reported_at
            """,
            (request.site_id, request.site_name, request.world_x, request.world_y, now),
        )

        db.execute(
            """
            INSERT INTO comet_heat_reports(id, player_id, site_id, touched_count, cleansed_count, reported_at)
            VALUES (?, ?, ?, ?, ?, ?)
            """,
            (
                report_id,
                account["id"],
                request.site_id,
                request.touched_count,
                request.cleansed_count,
                now,
            ),
        )

    return CometReportResult(accepted=True, report_id=report_id)


@router.get("/heatmap", response_model=HeatmapResponse)
def get_heatmap(
    account: dict = Depends(get_current_account),
) -> HeatmapResponse:
    with connect() as db:
        season = _get_current_season(db)
        if season is None:
            tracked_sites = db.execute(
                "SELECT id, name, world_x, world_y FROM comet_sites ORDER BY name ASC"
            ).fetchall()
            return HeatmapResponse(
                season_name=None,
                sites=[
                    SiteHeat(
                        site_id=row["id"],
                        site_name=row["name"],
                        world_x=row["world_x"],
                        world_y=row["world_y"],
                        touched=0,
                        cleansed=0,
                        ratio=1.0,
                        status="Calm",
                    )
                    for row in tracked_sites
                ],
            )

        season_id = season["id"]
        season_name = season["name"]
        season_starts = season["starts_at"]

        agg_row = db.execute(
            "SELECT MIN(last_aggregated_at) AS oldest FROM comet_heat_buckets WHERE season_id = ?",
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
            db.execute(
                "DELETE FROM comet_heat_buckets WHERE season_id = ?",
                (season_id,),
            )
            db.execute(
                """
                INSERT INTO comet_heat_buckets(
                    season_id,
                    site_id,
                    site_name,
                    world_x,
                    world_y,
                    touched_reports,
                    cleansed_reports,
                    last_aggregated_at
                )
                SELECT
                    ?,
                    s.id,
                    s.name,
                    s.world_x,
                    s.world_y,
                    COALESCE(SUM(r.touched_count), 0),
                    COALESCE(SUM(r.cleansed_count), 0),
                    ?
                FROM comet_sites s
                LEFT JOIN comet_heat_reports r
                    ON r.site_id = s.id
                   AND r.reported_at >= ?
                GROUP BY s.id, s.name, s.world_x, s.world_y
                """,
                (season_id, now_str, season_starts),
            )

        bucket_rows = db.execute(
            """
            SELECT site_id, site_name, world_x, world_y, touched_reports, cleansed_reports
            FROM comet_heat_buckets
            WHERE season_id = ?
            ORDER BY touched_reports DESC, site_name ASC
            """,
            (season_id,),
        ).fetchall()

    sites = []
    for row in bucket_rows:
        touched = row["touched_reports"]
        cleansed = row["cleansed_reports"]
        ratio = cleansed / max(touched, 1) if touched > 0 else 1.0
        sites.append(
            SiteHeat(
                site_id=row["site_id"],
                site_name=row["site_name"],
                world_x=row["world_x"],
                world_y=row["world_y"],
                touched=touched,
                cleansed=cleansed,
                ratio=round(ratio, 4),
                status=_status_tier(ratio),
            )
        )

    return HeatmapResponse(season_name=season_name, sites=sites)
