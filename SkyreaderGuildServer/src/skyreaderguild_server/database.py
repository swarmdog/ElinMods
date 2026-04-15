from __future__ import annotations

import json
import os
import sqlite3
from contextlib import contextmanager
from datetime import datetime, timedelta, timezone
from pathlib import Path
from typing import Iterator


_DATA_DIR = Path(__file__).resolve().parent / "data"


def database_path() -> Path:
    return Path(os.environ.get("SKYREADER_DB_PATH", "skyreader.db"))


@contextmanager
def connect() -> Iterator[sqlite3.Connection]:
    path = database_path()
    if path.parent != Path("."):
        path.parent.mkdir(parents=True, exist_ok=True)
    db = sqlite3.connect(path)
    db.row_factory = sqlite3.Row
    db.execute("PRAGMA foreign_keys = ON")
    try:
        yield db
        db.commit()
    except Exception:
        db.rollback()
        raise
    finally:
        db.close()


def init_db() -> None:
    with connect() as db:
        _ensure_comet_site_schema(db)
        db.executescript(
            """
            -- Auth & Ladder
            CREATE TABLE IF NOT EXISTS guild_accounts (
                id TEXT PRIMARY KEY,
                install_key TEXT NOT NULL UNIQUE,
                display_name TEXT NOT NULL,
                highest_rank INTEGER NOT NULL DEFAULT 0,
                game_version TEXT,
                mod_version TEXT,
                created_at TEXT NOT NULL,
                last_seen_at TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS auth_tokens (
                token_hash TEXT PRIMARY KEY,
                account_id TEXT NOT NULL REFERENCES guild_accounts(id) ON DELETE CASCADE,
                created_at TEXT NOT NULL,
                expires_at TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS astral_contributions (
                id TEXT PRIMARY KEY,
                player_id TEXT NOT NULL REFERENCES guild_accounts(id) ON DELETE CASCADE,
                type TEXT NOT NULL,
                amount INTEGER NOT NULL,
                local_event_id TEXT NOT NULL UNIQUE,
                client_timestamp TEXT,
                created_at TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS ladder_snapshot (
                player_id TEXT PRIMARY KEY REFERENCES guild_accounts(id) ON DELETE CASCADE,
                display_name TEXT NOT NULL,
                total_score INTEGER NOT NULL,
                last_updated_at TEXT NOT NULL
            );

            CREATE INDEX IF NOT EXISTS idx_ladder_score
                ON ladder_snapshot(total_score DESC, last_updated_at ASC);

            CREATE INDEX IF NOT EXISTS idx_contrib_player
                ON astral_contributions(player_id);

            -- Seasons
            CREATE TABLE IF NOT EXISTS sky_seasons (
                id TEXT PRIMARY KEY,
                name TEXT NOT NULL,
                description TEXT NOT NULL,
                starts_at TEXT NOT NULL,
                ends_at TEXT NOT NULL,
                duration_days INTEGER NOT NULL DEFAULT 28,
                modifiers TEXT NOT NULL DEFAULT '{}'
            );
            CREATE INDEX IF NOT EXISTS idx_season_range
                ON sky_seasons(starts_at, ends_at);

            -- Constellations
            CREATE TABLE IF NOT EXISTS constellations (
                id TEXT PRIMARY KEY,
                season_id TEXT NOT NULL REFERENCES sky_seasons(id),
                name TEXT NOT NULL,
                description TEXT NOT NULL,
                lore_domain TEXT NOT NULL DEFAULT '',
                goal_config TEXT NOT NULL DEFAULT '{}'
            );

            CREATE TABLE IF NOT EXISTS constellation_memberships (
                player_id TEXT NOT NULL REFERENCES guild_accounts(id) ON DELETE CASCADE,
                constellation_id TEXT NOT NULL REFERENCES constellations(id) ON DELETE CASCADE,
                joined_at TEXT NOT NULL,
                PRIMARY KEY (player_id, constellation_id)
            );

            CREATE TABLE IF NOT EXISTS constellation_progress (
                constellation_id TEXT NOT NULL REFERENCES constellations(id) ON DELETE CASCADE,
                metric_type TEXT NOT NULL,
                current_amount INTEGER NOT NULL DEFAULT 0,
                last_aggregated_at TEXT NOT NULL,
                PRIMARY KEY (constellation_id, metric_type)
            );

            -- Geometry
            CREATE TABLE IF NOT EXISTS geometry_samples (
                id TEXT PRIMARY KEY,
                player_id TEXT NOT NULL REFERENCES guild_accounts(id) ON DELETE CASCADE,
                danger_band INTEGER NOT NULL,
                shape_type TEXT NOT NULL,
                room_count INTEGER NOT NULL,
                sampled_at TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS idx_geom_sampled
                ON geometry_samples(sampled_at);

            CREATE TABLE IF NOT EXISTS geometry_aggregates (
                season_id TEXT NOT NULL,
                danger_band INTEGER NOT NULL,
                shape_type TEXT NOT NULL,
                count INTEGER NOT NULL DEFAULT 0,
                last_aggregated_at TEXT NOT NULL,
                PRIMARY KEY (season_id, danger_band, shape_type)
            );

            -- Comet Heatmap
            CREATE TABLE IF NOT EXISTS comet_site_catalog (
                id TEXT PRIMARY KEY,
                name TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS comet_sites (
                id TEXT PRIMARY KEY,
                name TEXT NOT NULL,
                world_x INTEGER NOT NULL,
                world_y INTEGER NOT NULL,
                last_reported_at TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS comet_heat_reports (
                id TEXT PRIMARY KEY,
                player_id TEXT NOT NULL REFERENCES guild_accounts(id) ON DELETE CASCADE,
                site_id TEXT NOT NULL REFERENCES comet_sites(id) ON DELETE CASCADE,
                touched_count INTEGER NOT NULL DEFAULT 0,
                cleansed_count INTEGER NOT NULL DEFAULT 0,
                reported_at TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS idx_comet_report_site
                ON comet_heat_reports(site_id, reported_at);

            CREATE TABLE IF NOT EXISTS comet_heat_buckets (
                season_id TEXT NOT NULL,
                site_id TEXT NOT NULL REFERENCES comet_sites(id) ON DELETE CASCADE,
                site_name TEXT NOT NULL,
                world_x INTEGER NOT NULL,
                world_y INTEGER NOT NULL,
                touched_reports INTEGER NOT NULL DEFAULT 0,
                cleansed_reports INTEGER NOT NULL DEFAULT 0,
                last_aggregated_at TEXT NOT NULL,
                PRIMARY KEY (season_id, site_id)
            );

            -- Star Papers
            CREATE TABLE IF NOT EXISTS research_notes (
                id TEXT PRIMARY KEY,
                player_id TEXT NOT NULL REFERENCES guild_accounts(id) ON DELETE CASCADE,
                title TEXT NOT NULL,
                body TEXT NOT NULL,
                created_at TEXT NOT NULL,
                rating INTEGER NOT NULL DEFAULT 0
            );
            CREATE INDEX IF NOT EXISTS idx_notes_rating
                ON research_notes(rating DESC);

            CREATE TABLE IF NOT EXISTS research_note_ratings (
                player_id TEXT NOT NULL REFERENCES guild_accounts(id) ON DELETE CASCADE,
                note_id TEXT NOT NULL REFERENCES research_notes(id) ON DELETE CASCADE,
                value INTEGER NOT NULL,
                rated_at TEXT NOT NULL,
                PRIMARY KEY (player_id, note_id)
            );

            CREATE TABLE IF NOT EXISTS research_note_pulls (
                player_id TEXT NOT NULL REFERENCES guild_accounts(id) ON DELETE CASCADE,
                note_id TEXT NOT NULL REFERENCES research_notes(id) ON DELETE CASCADE,
                pulled_at TEXT NOT NULL,
                PRIMARY KEY (player_id, note_id)
            );
            """
        )

        _seed_from_json(db)


def _seed_from_json(db: sqlite3.Connection) -> None:
    _seed_comet_site_catalog(db)
    _seed_seasons_and_constellations(db)


def _ensure_comet_site_schema(db: sqlite3.Connection) -> None:
    legacy = False
    if _table_columns(db, "comet_regions"):
        legacy = True

    report_cols = _table_columns(db, "comet_heat_reports")
    if report_cols and "site_id" not in report_cols:
        legacy = True

    bucket_cols = _table_columns(db, "comet_heat_buckets")
    if bucket_cols and "site_id" not in bucket_cols:
        legacy = True

    site_cols = _table_columns(db, "comet_sites")
    required_site_cols = {"id", "name", "world_x", "world_y", "last_reported_at"}
    if site_cols and not required_site_cols.issubset(site_cols):
        legacy = True

    if not legacy:
        return

    for table in ["comet_heat_buckets", "comet_heat_reports", "comet_sites", "comet_regions"]:
        db.execute(f"DROP TABLE IF EXISTS {table}")


def _table_columns(db: sqlite3.Connection, table_name: str) -> set[str]:
    rows = db.execute(
        "SELECT name FROM sqlite_master WHERE type = 'table' AND name = ?",
        (table_name,),
    ).fetchall()
    if not rows:
        return set()

    return {
        row["name"]
        for row in db.execute(f"PRAGMA table_info({table_name})").fetchall()
    }


def _seed_comet_site_catalog(db: sqlite3.Connection) -> None:
    path = _DATA_DIR / "SourceGame_Zone.md"
    if not path.exists():
        return

    for row in _read_markdown_table(path):
        zone_type = row.get("type", "")
        zone_id = row.get("id", "")
        zone_name = row.get("name", "") or zone_id
        if not zone_id or not zone_type.startswith("Zone_"):
            continue

        db.execute(
            "INSERT OR IGNORE INTO comet_site_catalog(id, name) VALUES (?, ?)",
            (zone_id, zone_name),
        )


def _read_markdown_table(path: Path) -> list[dict[str, str]]:
    lines = path.read_text(encoding="utf-8").splitlines()
    header_index = next(i for i, line in enumerate(lines) if line.startswith("| id |"))
    headers = [cell.strip() for cell in lines[header_index].strip().strip("|").split("|")]
    rows: list[dict[str, str]] = []

    for line in lines[header_index + 2 :]:
        if not line.startswith("|"):
            break
        cells = [cell.strip() for cell in line.strip().strip("|").split("|")]
        if len(cells) < len(headers):
            cells += [""] * (len(headers) - len(cells))
        rows.append(dict(zip(headers, cells)))

    return rows


def _seed_seasons_and_constellations(db: sqlite3.Connection) -> None:
    count = db.execute("SELECT COUNT(*) AS n FROM sky_seasons").fetchone()["n"]
    if count > 0:
        return

    seasons_path = _DATA_DIR / "seasons.json"
    constellations_path = _DATA_DIR / "constellations.json"
    if not seasons_path.exists():
        return

    seasons = json.loads(seasons_path.read_text(encoding="utf-8"))
    constellations_by_season: dict = {}
    if constellations_path.exists():
        constellations_by_season = json.loads(
            constellations_path.read_text(encoding="utf-8")
        )

    cursor_dt = datetime.now(timezone.utc).replace(
        hour=0, minute=0, second=0, microsecond=0
    )

    for season in seasons:
        duration = season.get("duration_days", 28)
        starts_at = cursor_dt.isoformat(timespec="seconds").replace("+00:00", "Z")
        ends_at = (cursor_dt + timedelta(days=duration)).isoformat(
            timespec="seconds"
        ).replace("+00:00", "Z")

        db.execute(
            """
            INSERT INTO sky_seasons(id, name, description, starts_at, ends_at, duration_days, modifiers)
            VALUES (?, ?, ?, ?, ?, ?, ?)
            """,
            (
                season["id"],
                season["name"],
                season["description"],
                starts_at,
                ends_at,
                duration,
                json.dumps(season.get("modifiers", {})),
            ),
        )

        for constellation in constellations_by_season.get(season["id"], []):
            db.execute(
                """
                INSERT INTO constellations(id, season_id, name, description, lore_domain, goal_config)
                VALUES (?, ?, ?, ?, ?, ?)
                """,
                (
                    constellation["id"],
                    season["id"],
                    constellation["name"],
                    constellation["description"],
                    constellation.get("lore_domain", ""),
                    json.dumps(constellation.get("goal_config", {})),
                ),
            )

        cursor_dt += timedelta(days=duration)
