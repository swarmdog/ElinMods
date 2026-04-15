"""Tests for the comet heatmap API."""
import importlib
import sqlite3
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT))

from tests.helpers import auth_header, make_client, register


def _site_payload(site_id: str, site_name: str, world_x: int, world_y: int, touched: int, cleansed: int):
    return {
        "site_id": site_id,
        "site_name": site_name,
        "world_x": world_x,
        "world_y": world_y,
        "touched_count": touched,
        "cleansed_count": cleansed,
    }


def test_report_and_fetch_heatmap(tmp_path, monkeypatch):
    with make_client(tmp_path, monkeypatch) as client:
        account = register(client, "install-key-comet-0001", "CometTracker")
        token = account["auth_token"]

        resp = client.post(
            "/comet/report",
            headers=auth_header(token),
            json=_site_payload("palmia", "Palmia", 34, 25, 10, 8),
        )
        assert resp.status_code == 200
        assert resp.json()["accepted"] is True

        heatmap = client.get("/comet/heatmap", headers=auth_header(token))
        assert heatmap.status_code == 200
        data = heatmap.json()

        assert len(data["sites"]) == 1
        palmia = data["sites"][0]
        assert palmia["site_id"] == "palmia"
        assert palmia["site_name"] == "Palmia"
        assert palmia["world_x"] == 34
        assert palmia["world_y"] == 25
        assert palmia["touched"] == 10
        assert palmia["cleansed"] == 8
        assert palmia["status"] == "Calm"  # 8/10 = 0.8


def test_negative_world_coords_accepted(tmp_path, monkeypatch):
    with make_client(tmp_path, monkeypatch) as client:
        account = register(client, "install-key-comet-0001b", "NegativeCoords")
        token = account["auth_token"]

        resp = client.post(
            "/comet/report",
            headers=auth_header(token),
            json=_site_payload("palmia", "Palmia", 34, -25, 10, 8),
        )
        assert resp.status_code == 200

        heatmap = client.get("/comet/heatmap", headers=auth_header(token))
        assert heatmap.status_code == 200
        palmia = heatmap.json()["sites"][0]
        assert palmia["world_x"] == 34
        assert palmia["world_y"] == -25


def test_status_tiers(tmp_path, monkeypatch):
    with make_client(tmp_path, monkeypatch) as client:
        account = register(client, "install-key-comet-0002", "TierTester")
        token = account["auth_token"]

        client.post("/comet/report", headers=auth_header(token),
                    json=_site_payload("palmia", "Palmia", 34, 25, 100, 90))
        client.post("/comet/report", headers=auth_header(token),
                    json=_site_payload("mysilia", "Mysilia", 30, 57, 100, 60))
        client.post("/comet/report", headers=auth_header(token),
                    json=_site_payload("noyel", "Noyel", 71, 15, 100, 25))
        client.post("/comet/report", headers=auth_header(token),
                    json=_site_payload("derphy", "Derphy", 5, 36, 100, 5))

        heatmap = client.get("/comet/heatmap", headers=auth_header(token))
        data = heatmap.json()
        status_map = {r["site_id"]: r["status"] for r in data["sites"]}

        assert status_map["palmia"] == "Calm"
        assert status_map["mysilia"] == "Stirring"
        assert status_map["noyel"] == "Troubled"
        assert status_map["derphy"] == "Overrun"


def test_multiple_reports_same_site_aggregate(tmp_path, monkeypatch):
    with make_client(tmp_path, monkeypatch) as client:
        account = register(client, "install-key-comet-0003", "RepeatReporter")
        token = account["auth_token"]

        client.post("/comet/report", headers=auth_header(token),
                    json=_site_payload("palmia", "Palmia", 34, 25, 4, 1))
        client.post("/comet/report", headers=auth_header(token),
                    json=_site_payload("palmia", "Palmia", 34, 25, 6, 3))

        heatmap = client.get("/comet/heatmap", headers=auth_header(token))
        palmia = next(r for r in heatmap.json()["sites"] if r["site_id"] == "palmia")

        assert palmia["touched"] == 10
        assert palmia["cleansed"] == 4
        assert palmia["status"] == "Troubled"


def test_unknown_site_rejected(tmp_path, monkeypatch):
    with make_client(tmp_path, monkeypatch) as client:
        account = register(client, "install-key-comet-0004", "BadSite")
        resp = client.post(
            "/comet/report",
            headers=auth_header(account["auth_token"]),
            json=_site_payload("atlantis", "Atlantis", 99, 99, 5, 3),
        )
        assert resp.status_code == 422


def test_legacy_region_tables_replaced_on_init(tmp_path, monkeypatch):
    monkeypatch.setenv("SKYREADER_DB_PATH", str(tmp_path / "legacy.db"))

    db_path = tmp_path / "legacy.db"
    with sqlite3.connect(db_path) as db:
        db.executescript(
            """
            CREATE TABLE comet_regions (
                id TEXT PRIMARY KEY,
                name TEXT NOT NULL,
                description TEXT NOT NULL
            );
            CREATE TABLE comet_heat_reports (
                id TEXT PRIMARY KEY,
                player_id TEXT NOT NULL,
                region_id TEXT NOT NULL,
                touched_count INTEGER NOT NULL DEFAULT 0,
                cleansed_count INTEGER NOT NULL DEFAULT 0,
                reported_at TEXT NOT NULL
            );
            CREATE TABLE comet_heat_buckets (
                season_id TEXT NOT NULL,
                region_id TEXT NOT NULL,
                touched_reports INTEGER NOT NULL DEFAULT 0,
                cleansed_reports INTEGER NOT NULL DEFAULT 0,
                last_aggregated_at TEXT NOT NULL,
                PRIMARY KEY (season_id, region_id)
            );
            """
        )

    sys.modules.pop("database", None)
    database = importlib.import_module("database")
    database.init_db()

    with sqlite3.connect(db_path) as db:
        tables = {row[0] for row in db.execute("SELECT name FROM sqlite_master WHERE type='table'")}
        assert "comet_regions" not in tables
        assert "comet_sites" in tables

        report_columns = {row[1] for row in db.execute("PRAGMA table_info(comet_heat_reports)")}
        assert "site_id" in report_columns
        assert "region_id" not in report_columns


def test_unauthenticated_returns_401(tmp_path, monkeypatch):
    with make_client(tmp_path, monkeypatch) as client:
        resp = client.post(
            "/comet/report",
            json=_site_payload("palmia", "Palmia", 34, 25, 1, 1),
        )
        assert resp.status_code == 401

        resp2 = client.get("/comet/heatmap")
        assert resp2.status_code == 401
