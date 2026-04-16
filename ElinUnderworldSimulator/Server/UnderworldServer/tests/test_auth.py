import pytest

from .helpers import auth_header, make_client, register


@pytest.mark.asyncio
async def test_register_and_login(tmp_path):
    async for client, _app in make_client(tmp_path):
        registered = await register(client, display_name="ShadowTrader42")
        assert registered["display_name"] == "ShadowTrader42"
        assert registered["underworld_rank"] == 0

        login = await client.post("/api/login", json={"auth_token": registered["auth_token"]})
        assert login.status_code == 200
        assert login.json()["player_id"] == registered["player_id"]


@pytest.mark.asyncio
async def test_duplicate_display_name_is_rejected_for_new_install(tmp_path):
    async for client, _app in make_client(tmp_path):
        await register(client, display_name="ShadowTrader42")
        duplicate = await client.post(
            "/api/register",
            json={
                "install_key": "fedcba9876543210fedcba9876543210",
                "display_name": "ShadowTrader42",
                "game_version": "1",
                "mod_version": "0.1.0",
            },
        )
        assert duplicate.status_code == 400


@pytest.mark.asyncio
async def test_whitespace_display_name_is_rejected(tmp_path):
    async for client, _app in make_client(tmp_path):
        response = await client.post(
            "/api/register",
            json={
                "install_key": "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
                "display_name": "   ",
                "game_version": "1",
                "mod_version": "0.1.0",
            },
        )
        assert response.status_code == 400
        assert response.json()["detail"] == "Display name cannot be blank"


@pytest.mark.asyncio
async def test_register_recovers_after_player_record_reset(tmp_path):
    async for client, _app in make_client(tmp_path):
        registered = await register(client, display_name="ShadowTrader42", install_key="cccccccccccccccccccccccccccccccc")
        headers = auth_header(registered["auth_token"])

        ok = await client.get("/api/orders/available", headers=headers)
        assert ok.status_code == 200

        from underworld_server.database import open_db

        async with open_db(str(tmp_path / "underworld-test.db")) as db:
            await db.execute("DELETE FROM players WHERE install_key = ?", ("cccccccccccccccccccccccccccccccc",))

        unauthorized = await client.get("/api/orders/available", headers=headers)
        assert unauthorized.status_code == 401

        reregistered = await client.post(
            "/api/register",
            json={
                "install_key": "cccccccccccccccccccccccccccccccc",
                "display_name": "ShadowTrader42",
                "game_version": "1",
                "mod_version": "0.1.0",
            },
        )
        assert reregistered.status_code == 200
        assert reregistered.json()["auth_token"] != registered["auth_token"]


@pytest.mark.asyncio
async def test_protected_route_requires_bearer_token(tmp_path):
    async for client, _app in make_client(tmp_path):
        response = await client.get("/api/orders/available")
        assert response.status_code == 401

        registered = await register(client)
        response = await client.get("/api/orders/available", headers=auth_header(registered["auth_token"]))
        assert response.status_code == 200
