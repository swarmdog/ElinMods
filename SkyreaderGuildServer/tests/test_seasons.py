"""Tests for the seasons API."""
import json
import sqlite3
import sys
from datetime import datetime, timedelta, timezone
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "src"))

from tests.helpers import auth_header, make_client, register


def test_current_season_with_active_row(tmp_path, monkeypatch):
    with make_client(tmp_path, monkeypatch) as client:
        account = register(client, "install-key-season-0001", "Starwatcher")

        response = client.get("/sky-season/current", headers=auth_header(account["auth_token"]))
        assert response.status_code == 200, response.text
        data = response.json()
        # The first seeded season should be active (starts_at = today midnight UTC).
        assert data["id"] == "crimson_showers"
        assert data["name"] == "Season of Crimson Showers"
        assert data["starts_at"] is not None
        assert data["ends_at"] is not None
        assert data["duration_days"] == 28
        assert data["modifiers"]["meteorChanceMultiplier"] == 1.3


def test_current_season_with_no_active_row(tmp_path, monkeypatch):
    with make_client(tmp_path, monkeypatch) as client:
        account = register(client, "install-key-season-0002", "Voidwalker")

        # Expire all seasons.
        db_path = tmp_path / "skyreader-test.db"
        with sqlite3.connect(db_path) as db:
            db.execute("UPDATE sky_seasons SET starts_at = '2020-01-01T00:00:00Z', ends_at = '2020-01-02T00:00:00Z'")

        response = client.get("/sky-season/current", headers=auth_header(account["auth_token"]))
        assert response.status_code == 200
        data = response.json()
        assert data["id"] == "default"
        assert data["starts_at"] is None


def test_season_modifiers_are_valid_json(tmp_path, monkeypatch):
    with make_client(tmp_path, monkeypatch) as client:
        account = register(client, "install-key-season-0003", "JsonChecker")
        response = client.get("/sky-season/current", headers=auth_header(account["auth_token"]))
        data = response.json()
        modifiers = data["modifiers"]
        assert isinstance(modifiers, dict)


def test_unauthenticated_returns_401(tmp_path, monkeypatch):
    with make_client(tmp_path, monkeypatch) as client:
        response = client.get("/sky-season/current")
        assert response.status_code == 401
