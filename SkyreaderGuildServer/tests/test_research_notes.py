"""Tests for the research notes (star papers) API."""
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT))

from tests.helpers import auth_header, make_client, register


def test_create_and_pull_note(tmp_path, monkeypatch):
    with make_client(tmp_path, monkeypatch) as client:
        writer = register(client, "install-key-notes-0001", "ScribeAlpha")
        reader = register(client, "install-key-notes-0002", "ReaderBeta")

        # Writer creates a note.
        resp = client.post(
            "/research-notes/create",
            headers=auth_header(writer["auth_token"]),
            json={"title": "Meteor Fragment Analysis", "body": "The fragments contain trace amounts of astral energy that can be extracted."},
        )
        assert resp.status_code == 200
        assert resp.json()["created"] is True
        note_id = resp.json()["id"]

        # Reader pulls notes — should see the one the writer created.
        pull = client.get("/research-notes/pull?limit=5", headers=auth_header(reader["auth_token"]))
        assert pull.status_code == 200
        notes = pull.json()["notes"]
        assert len(notes) == 1
        assert notes[0]["title"] == "Meteor Fragment Analysis"
        assert notes[0]["id"] == note_id


def test_profanity_filter_rejects(tmp_path, monkeypatch):
    with make_client(tmp_path, monkeypatch) as client:
        account = register(client, "install-key-notes-0003", "ProfaneWriter")

        resp = client.post(
            "/research-notes/create",
            headers=auth_header(account["auth_token"]),
            json={"title": "This is fuck", "body": "A perfectly normal research note about stars and stuff."},
        )
        assert resp.status_code == 400
        assert "prohibited" in resp.json()["detail"].lower()


def test_rate_limit_per_day(tmp_path, monkeypatch):
    with make_client(tmp_path, monkeypatch) as client:
        account = register(client, "install-key-notes-0004", "ProlificWriter")
        token = account["auth_token"]

        for i in range(3):
            resp = client.post(
                "/research-notes/create",
                headers=auth_header(token),
                json={"title": f"Research Note {i}", "body": f"This is a detailed analysis number {i} of the meteor phenomenon."},
            )
            assert resp.status_code == 200, f"Note {i} failed: {resp.text}"

        # 4th note should be rate-limited.
        resp = client.post(
            "/research-notes/create",
            headers=auth_header(token),
            json={"title": "One More Note", "body": "This should be rejected by the rate limiter."},
        )
        assert resp.status_code == 429


def test_rating_updates_score(tmp_path, monkeypatch):
    with make_client(tmp_path, monkeypatch) as client:
        writer = register(client, "install-key-notes-0005", "RatedWriter")
        rater = register(client, "install-key-notes-0006", "RaterPerson")

        create_resp = client.post(
            "/research-notes/create",
            headers=auth_header(writer["auth_token"]),
            json={"title": "Excellent Discovery", "body": "A groundbreaking observation about rift geometries and their influence."},
        )
        note_id = create_resp.json()["id"]

        # Rate it +1.
        rate_resp = client.post(
            "/research-notes/rate",
            headers=auth_header(rater["auth_token"]),
            json={"note_id": note_id, "value": 1},
        )
        assert rate_resp.status_code == 200
        assert rate_resp.json()["new_rating"] == 1

        # Rate it again — should upsert, not double.
        rate_resp2 = client.post(
            "/research-notes/rate",
            headers=auth_header(rater["auth_token"]),
            json={"note_id": note_id, "value": -1},
        )
        assert rate_resp2.status_code == 200
        assert rate_resp2.json()["new_rating"] == -1


def test_pull_excludes_own_notes(tmp_path, monkeypatch):
    with make_client(tmp_path, monkeypatch) as client:
        account = register(client, "install-key-notes-0007", "SelfReader")
        token = account["auth_token"]

        client.post(
            "/research-notes/create",
            headers=auth_header(token),
            json={"title": "My Own Note", "body": "I wrote this and should not see it when pulling notes."},
        )

        pull = client.get("/research-notes/pull?limit=5", headers=auth_header(token))
        assert pull.status_code == 200
        assert len(pull.json()["notes"]) == 0


def test_pull_excludes_already_pulled(tmp_path, monkeypatch):
    with make_client(tmp_path, monkeypatch) as client:
        writer = register(client, "install-key-notes-0008", "WriterDup")
        reader = register(client, "install-key-notes-0009", "ReaderDup")

        client.post(
            "/research-notes/create",
            headers=auth_header(writer["auth_token"]),
            json={"title": "One Time Note", "body": "This note should only appear in the first pull."},
        )

        first_pull = client.get("/research-notes/pull?limit=5", headers=auth_header(reader["auth_token"]))
        assert len(first_pull.json()["notes"]) == 1

        second_pull = client.get("/research-notes/pull?limit=5", headers=auth_header(reader["auth_token"]))
        assert len(second_pull.json()["notes"]) == 0


def test_mine_returns_only_own_notes_newest_first(tmp_path, monkeypatch):
    with make_client(tmp_path, monkeypatch) as client:
        writer = register(client, "install-key-notes-0010", "MineWriter")
        other = register(client, "install-key-notes-0011", "OtherWriter")

        first = client.post(
            "/research-notes/create",
            headers=auth_header(writer["auth_token"]),
            json={"title": "First Mine", "body": "This is the first submitted paper for the mine endpoint."},
        )
        assert first.status_code == 200

        other_resp = client.post(
            "/research-notes/create",
            headers=auth_header(other["auth_token"]),
            json={"title": "Not Mine", "body": "This paper belongs to another account and must not appear."},
        )
        assert other_resp.status_code == 200

        second = client.post(
            "/research-notes/create",
            headers=auth_header(writer["auth_token"]),
            json={"title": "Second Mine", "body": "This is the second submitted paper for the mine endpoint."},
        )
        assert second.status_code == 200

        mine = client.get("/research-notes/mine?limit=50", headers=auth_header(writer["auth_token"]))
        assert mine.status_code == 200
        notes = mine.json()["notes"]
        assert [note["title"] for note in notes] == ["Second Mine", "First Mine"]
        assert {note["id"] for note in notes} == {first.json()["id"], second.json()["id"]}


def test_unauthenticated_returns_401(tmp_path, monkeypatch):
    with make_client(tmp_path, monkeypatch) as client:
        resp = client.post(
            "/research-notes/create",
            json={"title": "No Auth", "body": "Should be rejected because no token."},
        )
        assert resp.status_code == 401

        resp2 = client.get("/research-notes/pull")
        assert resp2.status_code == 401

        resp3 = client.get("/research-notes/mine")
        assert resp3.status_code == 401

        resp4 = client.post("/research-notes/rate", json={"note_id": "fake", "value": 1})
        assert resp4.status_code == 401
