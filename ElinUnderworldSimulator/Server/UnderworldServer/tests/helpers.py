from __future__ import annotations

import importlib
import sys
from pathlib import Path

import httpx


ROOT = Path(__file__).resolve().parents[1]
SRC = ROOT / "src"
sys.path.insert(0, str(SRC))


async def make_client(tmp_path):
    for name in list(sys.modules):
        if name == "underworld_server" or name.startswith("underworld_server."):
            sys.modules.pop(name, None)

    main = importlib.import_module("underworld_server.main")
    app = main.create_app(db_path=str(tmp_path / "underworld-test.db"), run_jobs=False)
    async with app.router.lifespan_context(app):
        transport = httpx.ASGITransport(app=app)
        async with httpx.AsyncClient(transport=transport, base_url="http://test") as client:
            yield client, app


async def register(client, display_name: str = "TestPlayer", install_key: str = "0123456789abcdef0123456789abcdef"):
    response = await client.post(
        "/api/register",
        json={
            "install_key": install_key,
            "display_name": display_name,
            "game_version": "1",
            "mod_version": "0.1.0",
        },
    )
    assert response.status_code == 200, response.text
    return response.json()


def auth_header(token: str) -> dict[str, str]:
    return {"Authorization": f"Bearer {token}"}
