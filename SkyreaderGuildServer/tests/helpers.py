import importlib
import os
import sys
from pathlib import Path

from fastapi.testclient import TestClient


ROOT = Path(__file__).resolve().parents[1]
SRC = ROOT / "src"
sys.path.insert(0, str(SRC))

ALL_MODULES = [
    "skyreaderguild_server.main",
    "skyreaderguild_server.auth",
    "skyreaderguild_server.contributions",
    "skyreaderguild_server.ladder",
    "skyreaderguild_server.database",
    "skyreaderguild_server.seasons",
    "skyreaderguild_server.constellations",
    "skyreaderguild_server.geometry",
    "skyreaderguild_server.comet",
    "skyreaderguild_server.research_notes",
]


def make_client(tmp_path, monkeypatch):
    monkeypatch.setenv("SKYREADER_JWT_SECRET", "test-secret-that-is-long-enough")
    monkeypatch.setenv("SKYREADER_DB_PATH", str(tmp_path / "skyreader-test.db"))

    for name in list(sys.modules):
        if name == "skyreaderguild_server" or name.startswith("skyreaderguild_server."):
            sys.modules.pop(name, None)

    main = importlib.import_module("skyreaderguild_server.main")
    return TestClient(main.app)


def register(client, install_key, display_name):
    response = client.post(
        "/guild/register-anon",
        json={
            "install_key": install_key,
            "display_name": display_name,
            "game_version": "1",
            "mod_version": "1.1.0",
        },
    )
    assert response.status_code == 200, response.text
    return response.json()


def auth_header(token):
    return {"Authorization": f"Bearer {token}"}
