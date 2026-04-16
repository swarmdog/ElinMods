from __future__ import annotations

import hashlib
import json
import os
from contextlib import asynccontextmanager
from datetime import datetime, timezone
from pathlib import Path
from typing import AsyncIterator

import aiosqlite
from fastapi import Request

from . import config


SCHEMA = """
CREATE TABLE IF NOT EXISTS players (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    install_key TEXT NOT NULL UNIQUE,
    auth_token_hash TEXT UNIQUE,
    display_name TEXT NOT NULL UNIQUE COLLATE NOCASE,
    underworld_rank INTEGER NOT NULL DEFAULT 0,
    total_rep INTEGER NOT NULL DEFAULT 0,
    gold INTEGER NOT NULL DEFAULT 0,
    faction_id INTEGER REFERENCES factions(id),
    game_version TEXT,
    mod_version TEXT,
    under_investigation_until TEXT,
    created_at TEXT NOT NULL,
    last_seen_at TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS factions (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    name TEXT NOT NULL UNIQUE COLLATE NOCASE,
    leader_player_id INTEGER NOT NULL REFERENCES players(id),
    max_members INTEGER NOT NULL DEFAULT 10,
    created_at TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS faction_members (
    faction_id INTEGER NOT NULL REFERENCES factions(id) ON DELETE CASCADE,
    player_id INTEGER NOT NULL REFERENCES players(id) ON DELETE CASCADE,
    role TEXT NOT NULL DEFAULT 'member',
    joined_at TEXT NOT NULL,
    PRIMARY KEY (faction_id, player_id)
);

CREATE TABLE IF NOT EXISTS territories (
    id TEXT PRIMARY KEY,
    name TEXT NOT NULL,
    heat INTEGER NOT NULL DEFAULT 0,
    heat_capacity INTEGER NOT NULL,
    base_demand_volume INTEGER NOT NULL,
    base_demand_potency INTEGER NOT NULL,
    controlling_faction_id INTEGER REFERENCES factions(id),
    control_scores_json TEXT NOT NULL DEFAULT '{}'
);

CREATE TABLE IF NOT EXISTS reputation (
    player_id INTEGER NOT NULL REFERENCES players(id) ON DELETE CASCADE,
    territory_id TEXT NOT NULL REFERENCES territories(id) ON DELETE CASCADE,
    local_rep INTEGER NOT NULL DEFAULT 0,
    PRIMARY KEY (player_id, territory_id)
);

CREATE TABLE IF NOT EXISTS orders (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    territory_id TEXT NOT NULL REFERENCES territories(id),
    client_type TEXT NOT NULL,
    product_type TEXT NOT NULL,
    product_id TEXT,
    min_quantity INTEGER NOT NULL,
    max_quantity INTEGER NOT NULL,
    min_potency INTEGER NOT NULL,
    max_toxicity INTEGER NOT NULL,
    base_payout INTEGER NOT NULL,
    deadline_hours INTEGER NOT NULL,
    required_rank_tier INTEGER NOT NULL,
    status TEXT NOT NULL DEFAULT 'available',
    assigned_player_id INTEGER REFERENCES players(id),
    accepted_at TEXT,
    created_at TEXT NOT NULL,
    expires_at TEXT NOT NULL,
    resolved_at TEXT
);

CREATE TABLE IF NOT EXISTS shipments (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    order_id INTEGER NOT NULL REFERENCES orders(id),
    player_id INTEGER NOT NULL REFERENCES players(id),
    territory_id TEXT NOT NULL REFERENCES territories(id),
    client_type TEXT NOT NULL,
    quantity INTEGER NOT NULL,
    avg_potency INTEGER NOT NULL DEFAULT 0,
    avg_toxicity INTEGER NOT NULL DEFAULT 0,
    avg_traceability INTEGER NOT NULL DEFAULT 0,
    item_ids_json TEXT NOT NULL DEFAULT '[]',
    satisfaction_score REAL NOT NULL DEFAULT 0,
    final_payout INTEGER NOT NULL DEFAULT 0,
    outcome TEXT NOT NULL,
    heat_delta INTEGER NOT NULL DEFAULT 0,
    rep_delta INTEGER NOT NULL DEFAULT 0,
    enforcement_event_json TEXT,
    submitted_at TEXT NOT NULL,
    resolved_at TEXT NOT NULL,
    result_seen_at TEXT
);

CREATE TABLE IF NOT EXISTS territory_effects (
    territory_id TEXT NOT NULL REFERENCES territories(id) ON DELETE CASCADE,
    effect_type TEXT NOT NULL,
    expires_at TEXT NOT NULL,
    PRIMARY KEY (territory_id, effect_type)
);

CREATE TABLE IF NOT EXISTS player_events (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    player_id INTEGER NOT NULL REFERENCES players(id) ON DELETE CASCADE,
    event_type TEXT NOT NULL,
    payload_json TEXT NOT NULL,
    created_at TEXT NOT NULL,
    seen_at TEXT
);

CREATE INDEX IF NOT EXISTS idx_orders_status ON orders(status);
CREATE INDEX IF NOT EXISTS idx_orders_territory ON orders(territory_id);
CREATE INDEX IF NOT EXISTS idx_orders_assigned ON orders(assigned_player_id, status);
CREATE UNIQUE INDEX IF NOT EXISTS idx_shipments_order_unique ON shipments(order_id);
CREATE INDEX IF NOT EXISTS idx_shipments_player_seen ON shipments(player_id, result_seen_at);
CREATE INDEX IF NOT EXISTS idx_player_events_player_seen ON player_events(player_id, seen_at);
"""


def utc_now() -> datetime:
    return datetime.now(timezone.utc)


def iso(dt: datetime | None = None) -> str:
    return (dt or utc_now()).isoformat(timespec="seconds").replace("+00:00", "Z")


def parse_iso(value: str | None) -> datetime | None:
    if not value:
        return None
    return datetime.fromisoformat(value.replace("Z", "+00:00"))


def token_hash(token: str) -> str:
    return hashlib.sha256(token.encode("utf-8")).hexdigest()


def database_path(db_path: str | None = None) -> Path:
    return Path(db_path or os.environ.get("UNDERWORLD_DB_PATH", "underworld.db"))


@asynccontextmanager
async def open_db(db_path: str | None = None) -> AsyncIterator[aiosqlite.Connection]:
    path = database_path(db_path)
    if path.parent != Path("."):
        path.parent.mkdir(parents=True, exist_ok=True)

    db = await aiosqlite.connect(path)
    db.row_factory = aiosqlite.Row
    await db.execute("PRAGMA foreign_keys = ON")
    await db.execute("PRAGMA journal_mode = WAL")
    await db.execute("PRAGMA busy_timeout = 5000")

    try:
        yield db
        await db.commit()
    except Exception:
        await db.rollback()
        raise
    finally:
        await db.close()


async def get_db(request: Request) -> AsyncIterator[aiosqlite.Connection]:
    async with open_db(request.app.state.db_path) as db:
        yield db


async def init_db(db_path: str | None = None) -> None:
    async with open_db(db_path) as db:
        await db.executescript(SCHEMA)
        await seed_territories(db)


async def seed_territories(db: aiosqlite.Connection) -> None:
    for spec in config.TERRITORIES.values():
        await db.execute(
            """
            INSERT INTO territories(
                id, name, heat, heat_capacity, base_demand_volume, base_demand_potency, controlling_faction_id, control_scores_json
            )
            VALUES (?, ?, 0, ?, ?, ?, NULL, ?)
            ON CONFLICT(id) DO UPDATE SET
                name = excluded.name,
                heat_capacity = excluded.heat_capacity,
                base_demand_volume = excluded.base_demand_volume,
                base_demand_potency = excluded.base_demand_potency
            """,
            (
                spec.id,
                spec.name,
                spec.heat_capacity,
                spec.base_demand_volume,
                spec.base_demand_potency,
                json.dumps({}),
            ),
        )


async def fetchone(db: aiosqlite.Connection, sql: str, params: tuple = ()) -> aiosqlite.Row | None:
    cursor = await db.execute(sql, params)
    try:
        return await cursor.fetchone()
    finally:
        await cursor.close()


async def fetchall(db: aiosqlite.Connection, sql: str, params: tuple = ()) -> list[aiosqlite.Row]:
    cursor = await db.execute(sql, params)
    try:
        return await cursor.fetchall()
    finally:
        await cursor.close()


async def insert_player_event(
    db: aiosqlite.Connection,
    player_id: int,
    event_type: str,
    payload: dict,
    created_at: str | None = None,
) -> None:
    await db.execute(
        """
        INSERT INTO player_events(player_id, event_type, payload_json, created_at)
        VALUES (?, ?, ?, ?)
        """,
        (player_id, event_type, json.dumps(payload), created_at or iso()),
    )
