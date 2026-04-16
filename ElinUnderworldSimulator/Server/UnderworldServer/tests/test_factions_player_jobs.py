import pytest

from underworld_server.database import open_db

from .helpers import ROOT, SRC, auth_header, make_client, register


@pytest.mark.asyncio
async def test_player_status_and_territories(tmp_path):
    async for client, _app in make_client(tmp_path):
        player = await register(client, display_name="SupplierBoss")
        headers = auth_header(player["auth_token"])

        territories = await client.get("/api/territories", headers=headers)
        assert territories.status_code == 200
        assert len(territories.json()["territories"]) == 6

        status_before = await client.get("/api/player/status", headers=headers)
        assert status_before.status_code == 200
        assert status_before.json()["display_name"] == "SupplierBoss"


@pytest.mark.asyncio
async def test_faction_detail_flow(tmp_path):
    async for client, _app in make_client(tmp_path):
        leader = await register(client, display_name="Leader", install_key="aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")
        member = await register(client, display_name="Member", install_key="bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb")

        leader_headers = auth_header(leader["auth_token"])
        member_headers = auth_header(member["auth_token"])

        async with open_db(str(tmp_path / "underworld-test.db")) as db:
            await db.execute("UPDATE players SET total_rep = 2500, underworld_rank = 2 WHERE id = 1")

        created = await client.post("/api/factions/create", json={"name": "Night Owls"}, headers=leader_headers)
        assert created.status_code == 200
        faction_id = created.json()["faction_id"]

        joined = await client.post("/api/factions/join", json={"faction_id": faction_id}, headers=member_headers)
        assert joined.status_code == 200

        detail = await client.get(f"/api/factions/{faction_id}", headers=leader_headers)
        assert detail.status_code == 200
        data = detail.json()
        assert data["name"] == "Night Owls"
        assert data["member_count"] == 2


@pytest.mark.asyncio
async def test_faction_create_rejects_whitespace_name(tmp_path):
    async for client, _app in make_client(tmp_path):
        leader = await register(client, display_name="Leader", install_key="dddddddddddddddddddddddddddddddd")
        headers = auth_header(leader["auth_token"])

        async with open_db(str(tmp_path / "underworld-test.db")) as db:
            await db.execute("UPDATE players SET total_rep = 2500, underworld_rank = 2 WHERE id = ?", (leader["player_id"],))

        response = await client.post("/api/factions/create", json={"name": "   "}, headers=headers)
        assert response.status_code == 400
        assert response.json()["detail"] == "Faction name cannot be blank"


@pytest.mark.asyncio
async def test_job_helpers_run(tmp_path):
    async for client, _app in make_client(tmp_path):
        registered = await register(client)
        headers = auth_header(registered["auth_token"])
        territories = await client.get("/api/territories", headers=headers)
        assert territories.status_code == 200

        from underworld_server import jobs

        await jobs.decay_all_territory_heat(str(tmp_path / "underworld-test.db"))
        await jobs.generate_orders_for_all_territories(str(tmp_path / "underworld-test.db"))
        await jobs.expire_overdue_orders(str(tmp_path / "underworld-test.db"))
        await jobs.resolve_territory_control(str(tmp_path / "underworld-test.db"))


def test_cli_help_runs_from_packaged_source_layout():
    import os
    import subprocess
    import sys

    assert (ROOT / "pyproject.toml").exists()
    assert (ROOT / "src" / "underworld_server" / "__main__.py").exists()

    env = os.environ.copy()
    env["PYTHONPATH"] = str(SRC) + os.pathsep + env.get("PYTHONPATH", "")
    result = subprocess.run(
        [sys.executable, "-m", "underworld_server", "--help"],
        check=True,
        capture_output=True,
        text=True,
        env=env,
        cwd=str(ROOT),
    )

    assert "underworld-server" in result.stdout


@pytest.mark.asyncio
async def test_faction_leave_removes_member(tmp_path):
    async for client, _app in make_client(tmp_path):
        leader = await register(client, display_name="Leader", install_key="aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")
        member = await register(client, display_name="Member", install_key="bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb")
        leader_h = auth_header(leader["auth_token"])
        member_h = auth_header(member["auth_token"])

        async with open_db(str(tmp_path / "underworld-test.db")) as db:
            await db.execute("UPDATE players SET total_rep = 2500, underworld_rank = 2 WHERE id = ?", (leader["player_id"],))

        created = await client.post("/api/factions/create", json={"name": "Leavers"}, headers=leader_h)
        faction_id = created.json()["faction_id"]
        await client.post("/api/factions/join", json={"faction_id": faction_id}, headers=member_h)

        detail_before = await client.get(f"/api/factions/{faction_id}", headers=leader_h)
        assert detail_before.json()["member_count"] == 2

        left = await client.post("/api/factions/leave", headers=member_h)
        assert left.status_code == 200
        assert left.json()["status"] == "left"

        detail_after = await client.get(f"/api/factions/{faction_id}", headers=leader_h)
        assert detail_after.json()["member_count"] == 1

        status_resp = await client.get("/api/player/status", headers=member_h)
        assert status_resp.json()["faction"] is None


@pytest.mark.asyncio
async def test_faction_leader_cannot_leave(tmp_path):
    async for client, _app in make_client(tmp_path):
        leader = await register(client, display_name="LeaderStuck", install_key="cccccccccccccccccccccccccccccccc")
        leader_h = auth_header(leader["auth_token"])

        async with open_db(str(tmp_path / "underworld-test.db")) as db:
            await db.execute("UPDATE players SET total_rep = 2500, underworld_rank = 2 WHERE id = ?", (leader["player_id"],))

        created = await client.post("/api/factions/create", json={"name": "Trapped"}, headers=leader_h)
        assert created.status_code == 200

        left = await client.post("/api/factions/leave", headers=leader_h)
        assert left.status_code == 409


@pytest.mark.asyncio
async def test_faction_disband_removes_all(tmp_path):
    async for client, _app in make_client(tmp_path):
        leader = await register(client, display_name="DisbandLead", install_key="ddddddddddddddddddddddddddddddaa")
        member = await register(client, display_name="DisbandMem", install_key="eeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeebb")
        leader_h = auth_header(leader["auth_token"])
        member_h = auth_header(member["auth_token"])

        async with open_db(str(tmp_path / "underworld-test.db")) as db:
            await db.execute("UPDATE players SET total_rep = 2500, underworld_rank = 2 WHERE id = ?", (leader["player_id"],))

        created = await client.post("/api/factions/create", json={"name": "Doomed"}, headers=leader_h)
        faction_id = created.json()["faction_id"]
        await client.post("/api/factions/join", json={"faction_id": faction_id}, headers=member_h)

        disbanded = await client.delete(f"/api/factions/{faction_id}", headers=leader_h)
        assert disbanded.status_code == 200
        assert disbanded.json()["status"] == "disbanded"

        leader_status = await client.get("/api/player/status", headers=leader_h)
        assert leader_status.json()["faction"] is None
        member_status = await client.get("/api/player/status", headers=member_h)
        assert member_status.json()["faction"] is None

        detail = await client.get(f"/api/factions/{faction_id}", headers=leader_h)
        assert detail.status_code == 404


@pytest.mark.asyncio
async def test_faction_promote_member_to_officer(tmp_path):
    async for client, _app in make_client(tmp_path):
        leader = await register(client, display_name="PromLeader", install_key="fffffffffffffffffffffffffffffff1")
        member = await register(client, display_name="PromMember", install_key="fffffffffffffffffffffffffffffff2")
        leader_h = auth_header(leader["auth_token"])
        member_h = auth_header(member["auth_token"])

        async with open_db(str(tmp_path / "underworld-test.db")) as db:
            await db.execute("UPDATE players SET total_rep = 2500, underworld_rank = 2 WHERE id = ?", (leader["player_id"],))

        created = await client.post("/api/factions/create", json={"name": "Promoters"}, headers=leader_h)
        faction_id = created.json()["faction_id"]
        await client.post("/api/factions/join", json={"faction_id": faction_id}, headers=member_h)

        promoted = await client.post(f"/api/factions/{faction_id}/promote", json={"player_id": member["player_id"]}, headers=leader_h)
        assert promoted.status_code == 200
        assert promoted.json()["new_role"] == "officer"

        detail = await client.get(f"/api/factions/{faction_id}", headers=leader_h)
        member_entry = next(m for m in detail.json()["members"] if m["display_name"] == "PromMember")
        assert member_entry["role"] == "officer"


@pytest.mark.asyncio
async def test_faction_promote_rejects_non_leader(tmp_path):
    async for client, _app in make_client(tmp_path):
        leader = await register(client, display_name="RealLeader", install_key="11111111111111111111111111111111")
        member = await register(client, display_name="FakeLead", install_key="22222222222222222222222222222222")
        leader_h = auth_header(leader["auth_token"])
        member_h = auth_header(member["auth_token"])

        async with open_db(str(tmp_path / "underworld-test.db")) as db:
            await db.execute("UPDATE players SET total_rep = 2500, underworld_rank = 2 WHERE id = ?", (leader["player_id"],))

        created = await client.post("/api/factions/create", json={"name": "AuthTest"}, headers=leader_h)
        faction_id = created.json()["faction_id"]
        await client.post("/api/factions/join", json={"faction_id": faction_id}, headers=member_h)

        promoted = await client.post(f"/api/factions/{faction_id}/promote", json={"player_id": leader["player_id"]}, headers=member_h)
        assert promoted.status_code == 403


@pytest.mark.asyncio
async def test_territory_control_single_faction(tmp_path):
    async for client, app in make_client(tmp_path):
        leader = await register(client, display_name="ControlLead", install_key="33333333333333333333333333333333")
        leader_h = auth_header(leader["auth_token"])

        async with open_db(str(tmp_path / "underworld-test.db")) as db:
            await db.execute("UPDATE players SET total_rep = 2500, underworld_rank = 2 WHERE id = ?", (leader["player_id"],))

        created = await client.post("/api/factions/create", json={"name": "Controllers"}, headers=leader_h)
        faction_id = created.json()["faction_id"]

        import json
        async with open_db(str(tmp_path / "underworld-test.db")) as db:
            scores = json.dumps({str(faction_id): 50000})
            await db.execute("UPDATE territories SET control_scores_json = ? WHERE id = 'derphy_underground'", (scores,))

        from underworld_server import jobs
        await jobs.resolve_territory_control(str(tmp_path / "underworld-test.db"))

        territories = await client.get("/api/territories", headers=leader_h)
        derphy = next(t for t in territories.json()["territories"] if t["id"] == "derphy_underground")
        assert derphy["controlling_faction_id"] == faction_id


@pytest.mark.asyncio
async def test_influence_decay(tmp_path):
    async for client, _app in make_client(tmp_path):
        await register(client, display_name="DecayTest", install_key="44444444444444444444444444444444")

        import json
        # Use two close scores so neither faction triggers the 1.2x lead required for control.
        # This avoids a FK constraint on controlling_faction_id while testing pure decay math.
        async with open_db(str(tmp_path / "underworld-test.db")) as db:
            scores = json.dumps({"98": 10000, "99": 9500})
            await db.execute("UPDATE territories SET control_scores_json = ? WHERE id = 'derphy_underground'", (scores,))

        from underworld_server import jobs
        await jobs.resolve_territory_control(str(tmp_path / "underworld-test.db"))

        async with open_db(str(tmp_path / "underworld-test.db")) as db:
            from underworld_server.database import fetchone
            row = await fetchone(db, "SELECT control_scores_json FROM territories WHERE id = 'derphy_underground'")
            decayed = json.loads(row["control_scores_json"])
            assert decayed["98"] == 9000  # 10000 * 0.9
            assert decayed["99"] == 8550  # 9500 * 0.9

