from __future__ import annotations

import os
import sqlite3
from contextlib import contextmanager
from pathlib import Path
from typing import Iterator


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
        db.executescript(
            """
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
            """
        )
