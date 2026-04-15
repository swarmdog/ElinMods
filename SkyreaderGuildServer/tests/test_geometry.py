"""Tests for the geometry API."""
import sqlite3
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT))

from tests.helpers import auth_header, make_client, register


def test_submit_sample_and_fetch_summary(tmp_path, monkeypatch):
    with make_client(tmp_path, monkeypatch) as client:
        account = register(client, "install-key-geom-0001", "ShapeWatcher")
        token = account["auth_token"]

        resp = client.post(
            "/geometry/sample",
            headers=auth_header(token),
            json={"danger_band": 3, "shape_type": "Star", "room_count": 5},
        )
        assert resp.status_code == 200
        assert resp.json()["accepted"] is True

        # Submit a few more samples.
        for i, shape in enumerate(["Star", "Circle", "Star", "Diamond"]):
            client.post(
                "/geometry/sample",
                headers=auth_header(token),
                json={"danger_band": 3, "shape_type": shape, "room_count": 4 + i},
            )

        summary = client.get("/geometry/summary", headers=auth_header(token))
        assert summary.status_code == 200
        data = summary.json()
        assert data["total_samples"] == 5
        assert data["dominant_shape"] == "Star"
        assert "Star" in data["dominant_flavor"]


def test_invalid_shape_type_rejected(tmp_path, monkeypatch):
    with make_client(tmp_path, monkeypatch) as client:
        account = register(client, "install-key-geom-0002", "ShapeViolator")
        resp = client.post(
            "/geometry/sample",
            headers=auth_header(account["auth_token"]),
            json={"danger_band": 1, "shape_type": "Hexagon", "room_count": 3},
        )
        assert resp.status_code == 422


def test_dominant_shape_calculation(tmp_path, monkeypatch):
    with make_client(tmp_path, monkeypatch) as client:
        account = register(client, "install-key-geom-0003", "Calculator")
        token = account["auth_token"]

        # Submit more Circles than anything else.
        for i in range(5):
            client.post(
                "/geometry/sample",
                headers=auth_header(token),
                json={"danger_band": 1, "shape_type": "Circle", "room_count": 3},
            )
        for i in range(2):
            client.post(
                "/geometry/sample",
                headers=auth_header(token),
                json={"danger_band": 2, "shape_type": "Diamond", "room_count": 3},
            )

        summary = client.get("/geometry/summary", headers=auth_header(token))
        assert summary.json()["dominant_shape"] == "Circle"


def test_unauthenticated_returns_401(tmp_path, monkeypatch):
    with make_client(tmp_path, monkeypatch) as client:
        resp = client.post(
            "/geometry/sample",
            json={"danger_band": 1, "shape_type": "Star", "room_count": 3},
        )
        assert resp.status_code == 401

        resp2 = client.get("/geometry/summary")
        assert resp2.status_code == 401
