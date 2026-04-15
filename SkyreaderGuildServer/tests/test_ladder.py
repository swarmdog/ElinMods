import sqlite3
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT))

from tests.helpers import auth_header, make_client, register


def count_accounts(tmp_path):
    db_path = tmp_path / "skyreader-test.db"
    with sqlite3.connect(db_path) as db:
        return db.execute("SELECT COUNT(*) FROM guild_accounts").fetchone()[0]


def submit(client, token, display_name, event_id, amount, kind="Extraction"):
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


def test_register_submit_and_rank(tmp_path, monkeypatch):
    with make_client(tmp_path, monkeypatch) as client:
        assert client.get("/health").json() == {"status": "ok"}
        account = register(client, "install-key-alpha-0001", "Arkyn")

        response = submit(client, account["auth_token"], "Arkyn", "event-0001", 150)
        assert response.status_code == 200, response.text
        assert response.json()["accepted"] == 1
        assert response.json()["total_score"] == 150

        global_response = client.get("/ladder/global?limit=20")
        assert global_response.status_code == 200
        assert global_response.json()["entries"] == [
            {"rank": 1, "display_name": "Arkyn", "total_score": 150}
        ]

        self_response = client.get("/ladder/self", headers=auth_header(account["auth_token"]))
        assert self_response.status_code == 200
        assert self_response.json()["rank"] == 1
        assert self_response.json()["percentile"] == 100.0


def test_duplicate_local_event_is_not_counted_twice(tmp_path, monkeypatch):
    with make_client(tmp_path, monkeypatch) as client:
        account = register(client, "install-key-beta-0001", "Srikkther")

        first = submit(client, account["auth_token"], "Srikkther", "same-event", 90)
        second = submit(client, account["auth_token"], "Srikkther", "same-event", 90)

        assert first.status_code == 200
        assert second.status_code == 200
        assert second.json()["accepted"] == 0
        assert second.json()["duplicates"] == 1
        assert second.json()["total_score"] == 90


def test_validation_and_auth_failures(tmp_path, monkeypatch):
    with make_client(tmp_path, monkeypatch) as client:
        account = register(client, "install-key-gamma-0001", "Celeste")

        no_auth = client.get("/ladder/self")
        assert no_auth.status_code == 401

        invalid_amount = submit(client, account["auth_token"], "Celeste", "evt-bad", 0)
        assert invalid_amount.status_code == 422

        invalid_type = submit(client, account["auth_token"], "Celeste", "evt-bad-2", 5, kind="Fishing")
        assert invalid_type.status_code == 422


def test_register_existing_install_reuses_account(tmp_path, monkeypatch):
    with make_client(tmp_path, monkeypatch) as client:
        first = register(client, "install-key-delta-0001", "First Name")
        second = register(client, "install-key-delta-0001", "Second Name")

        assert first["account_id"] == second["account_id"]
        assert first["auth_token"] != second["auth_token"]


def test_refresh_known_install_key_returns_new_token(tmp_path, monkeypatch):
    with make_client(tmp_path, monkeypatch) as client:
        account = register(client, "install-key-epsilon-0001", "Refreshed")

        response = client.post(
            "/guild/refresh-token",
            json={"install_key": "install-key-epsilon-0001"},
        )

        assert response.status_code == 200, response.text
        refreshed = response.json()
        assert refreshed["account_id"] == account["account_id"]
        assert refreshed["auth_token"] != account["auth_token"]

        self_response = client.get("/ladder/self", headers=auth_header(refreshed["auth_token"]))
        assert self_response.status_code == 200, self_response.text


def test_refresh_unknown_install_key_returns_404_without_registering(tmp_path, monkeypatch):
    with make_client(tmp_path, monkeypatch) as client:
        response = client.post(
            "/guild/refresh-token",
            json={"install_key": "install-key-zeta-unknown"},
        )

        assert response.status_code == 404
        assert count_accounts(tmp_path) == 0
