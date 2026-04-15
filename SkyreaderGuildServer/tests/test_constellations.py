"""Tests for the constellations API."""
import json
import sqlite3
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "src"))

from tests.helpers import auth_header, make_client, register


def _submit_contribution(client, token, display_name, event_id, amount, kind="Extraction"):
    return client.post(
        "/contributions/batch",
        headers=auth_header(token),
        json={
            "display_name": display_name,
            "highest_rank": 200,
            "contributions": [
                {
                    "type": kind,
                    "amount": amount,
                    "local_event_id": event_id,
                    "timestamp": "2026-04-14T00:00:00Z",
                }
            ],
        },
    )


def test_join_constellation_and_view_progress(tmp_path, monkeypatch):
    with make_client(tmp_path, monkeypatch) as client:
        account = register(client, "install-key-const-0001", "Stargazer")
        token = account["auth_token"]

        # View available constellations.
        resp = client.get("/constellations/current", headers=auth_header(token))
        assert resp.status_code == 200
        data = resp.json()
        assert data["season_id"] == "crimson_showers"
        assert len(data["constellations"]) == 5
        assert data["player_constellation_id"] is None

        # Join one.
        join_resp = client.post(
            "/constellations/join",
            headers=auth_header(token),
            json={"constellation_id": "gale_crimson"},
        )
        assert join_resp.status_code == 200
        assert join_resp.json()["joined"] is True

        # Submit contributions.
        _submit_contribution(client, token, "Stargazer", "const-evt-001", 500)

        # Force re-aggregation by backdating the cached progress timestamp.
        import sqlite3
        db_path = tmp_path / "skyreader-test.db"
        with sqlite3.connect(db_path) as db:
            db.execute("UPDATE constellation_progress SET last_aggregated_at = '2020-01-01T00:00:00Z'")

        # View again — should show membership and progress.
        resp2 = client.get("/constellations/current", headers=auth_header(token))
        data2 = resp2.json()
        assert data2["player_constellation_id"] == "gale_crimson"

        # Find the gale constellation.
        gale = next(c for c in data2["constellations"] if c["id"] == "gale_crimson")
        assert gale["member_count"] == 1
        assert gale["progress"]["Extraction"] == 500


def test_cannot_join_twice_in_same_season(tmp_path, monkeypatch):
    with make_client(tmp_path, monkeypatch) as client:
        account = register(client, "install-key-const-0002", "Doubler")
        token = account["auth_token"]

        client.post("/constellations/join", headers=auth_header(token), json={"constellation_id": "gale_crimson"})
        resp = client.post("/constellations/join", headers=auth_header(token), json={"constellation_id": "mountain_crimson"})
        assert resp.status_code == 409


def test_current_with_no_active_season(tmp_path, monkeypatch):
    with make_client(tmp_path, monkeypatch) as client:
        account = register(client, "install-key-const-0003", "Voider")
        token = account["auth_token"]

        # Expire all seasons.
        db_path = tmp_path / "skyreader-test.db"
        with sqlite3.connect(db_path) as db:
            db.execute("UPDATE sky_seasons SET starts_at = '2020-01-01T00:00:00Z', ends_at = '2020-01-02T00:00:00Z'")

        resp = client.get("/constellations/current", headers=auth_header(token))
        assert resp.status_code == 200
        data = resp.json()
        assert data["season_id"] is None
        assert data["constellations"] == []


def test_unauthenticated_returns_401(tmp_path, monkeypatch):
    with make_client(tmp_path, monkeypatch) as client:
        resp = client.get("/constellations/current")
        assert resp.status_code == 401

        resp2 = client.post("/constellations/join", json={"constellation_id": "gale_crimson"})
        assert resp2.status_code == 401
