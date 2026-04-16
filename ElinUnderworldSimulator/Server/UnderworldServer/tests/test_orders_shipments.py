import asyncio
from datetime import timedelta

import pytest

from underworld_server import config
from underworld_server.database import fetchone, iso, open_db, utc_now
from underworld_server.resolution import enforcement_profile

from .helpers import auth_header, make_client, register


class FixedRng:
    def __init__(self, values, uniform_value=0.2):
        self.values = list(values)
        self.uniform_value = uniform_value

    def random(self):
        return self.values.pop(0) if self.values else 1.0

    def uniform(self, _a, _b):
        return self.uniform_value


@pytest.mark.asyncio
async def test_full_order_cycle(tmp_path):
    async for client, app in make_client(tmp_path):
        registered = await register(client)
        headers = auth_header(registered["auth_token"])

        orders = await client.get("/api/orders/available", headers=headers)
        assert orders.status_code == 200
        payload = orders.json()
        assert payload["orders"]
        order = payload["orders"][0]
        order_id = order["id"]

        accepted = await client.post("/api/orders/accept", json={"order_id": order_id}, headers=headers)
        assert accepted.status_code == 200
        assert accepted.json()["status"] == "accepted"
        assert accepted.json()["deadline"] == order["expires_at"]

        app.state.rng = FixedRng([1.0, 1.0, 1.0])
        quantity = order["min_quantity"]
        submitted = await client.post(
            "/api/shipments/submit",
            json={
                "order_id": order_id,
                "quantity": quantity,
                "avg_potency": order["min_potency"] + 20,
                "avg_toxicity": 10,
                "avg_traceability": 14,
                "item_ids": ["uw_tonic_whisper"] * quantity,
            },
            headers=headers,
        )
        assert submitted.status_code == 200
        assert submitted.json()["outcome"] in {"completed", "failed"}

        results = await client.get("/api/shipments/results", headers=headers)
        assert results.status_code == 200
        assert len(results.json()["results"]) >= 1


@pytest.mark.asyncio
async def test_accept_rejects_already_claimed_orders(tmp_path):
    async for client, _app in make_client(tmp_path):
        first = await register(client, display_name="First", install_key="11111111111111111111111111111111")
        second = await register(client, display_name="Second", install_key="22222222222222222222222222222222")
        headers1 = auth_header(first["auth_token"])
        headers2 = auth_header(second["auth_token"])

        orders = await client.get("/api/orders/available", headers=headers1)
        target = orders.json()["orders"][0]["id"]

        accept_one = await client.post("/api/orders/accept", json={"order_id": target}, headers=headers1)
        assert accept_one.status_code == 200

        accept_two = await client.post("/api/orders/accept", json={"order_id": target}, headers=headers2)
        assert accept_two.status_code == 409


@pytest.mark.asyncio
async def test_submit_rejects_expired_order_even_before_expiration_job(tmp_path):
    async for client, app in make_client(tmp_path):
        registered = await register(client)
        headers = auth_header(registered["auth_token"])

        orders = await client.get("/api/orders/available", headers=headers)
        order_id = orders.json()["orders"][0]["id"]
        accepted = await client.post("/api/orders/accept", json={"order_id": order_id}, headers=headers)
        assert accepted.status_code == 200

        async with open_db(str(tmp_path / "underworld-test.db")) as db:
            await db.execute(
                "UPDATE orders SET expires_at = ? WHERE id = ?",
                (iso(utc_now() - timedelta(minutes=1)), order_id),
            )

        app.state.rng = FixedRng([1.0, 1.0, 1.0])
        response = await client.post(
            "/api/shipments/submit",
            json={
                "order_id": order_id,
                "quantity": 10,
                "avg_potency": 55,
                "avg_toxicity": 10,
                "avg_traceability": 20,
                "item_ids": ["uw_tonic_whisper"] * 10,
            },
            headers=headers,
        )
        assert response.status_code == 409
        assert response.json()["detail"] == "Order expired"


@pytest.mark.asyncio
async def test_submit_rejects_quantity_below_minimum(tmp_path):
    async for client, _app in make_client(tmp_path):
        registered = await register(client)
        headers = auth_header(registered["auth_token"])

        orders = await client.get("/api/orders/available", headers=headers)
        order = orders.json()["orders"][0]
        accepted = await client.post("/api/orders/accept", json={"order_id": order["id"]}, headers=headers)
        assert accepted.status_code == 200

        async with open_db(str(tmp_path / "underworld-test.db")) as db:
            await db.execute("UPDATE orders SET min_quantity = 2, max_quantity = 6 WHERE id = ?", (order["id"],))

        response = await client.post(
            "/api/shipments/submit",
            json={
                "order_id": order["id"],
                "quantity": 1,
                "avg_potency": order["min_potency"],
                "avg_toxicity": 10,
                "avg_traceability": 10,
                "item_ids": ["uw_tonic_whisper"],
            },
            headers=headers,
        )
        assert response.status_code == 400
        assert response.json()["detail"] == "Quantity must be between 2 and 6"


@pytest.mark.asyncio
async def test_submit_rejects_quantity_above_maximum(tmp_path):
    async for client, _app in make_client(tmp_path):
        registered = await register(client)
        headers = auth_header(registered["auth_token"])

        orders = await client.get("/api/orders/available", headers=headers)
        order = orders.json()["orders"][0]
        accepted = await client.post("/api/orders/accept", json={"order_id": order["id"]}, headers=headers)
        assert accepted.status_code == 200

        async with open_db(str(tmp_path / "underworld-test.db")) as db:
            await db.execute("UPDATE orders SET min_quantity = 2, max_quantity = 3 WHERE id = ?", (order["id"],))

        response = await client.post(
            "/api/shipments/submit",
            json={
                "order_id": order["id"],
                "quantity": 4,
                "avg_potency": order["min_potency"],
                "avg_toxicity": 10,
                "avg_traceability": 10,
                "item_ids": ["uw_tonic_whisper"] * 4,
            },
            headers=headers,
        )
        assert response.status_code == 400
        assert response.json()["detail"] == "Quantity must be between 2 and 3"


@pytest.mark.asyncio
async def test_duplicate_submit_only_creates_one_shipment(tmp_path):
    async for client, app in make_client(tmp_path):
        registered = await register(client)
        headers = auth_header(registered["auth_token"])

        orders = await client.get("/api/orders/available", headers=headers)
        order = orders.json()["orders"][0]
        accepted = await client.post("/api/orders/accept", json={"order_id": order["id"]}, headers=headers)
        assert accepted.status_code == 200

        app.state.rng = FixedRng([1.0, 1.0, 1.0, 1.0, 1.0, 1.0])
        payload = {
            "order_id": order["id"],
            "quantity": order["min_quantity"],
            "avg_potency": order["min_potency"] + 10,
            "avg_toxicity": 10,
            "avg_traceability": 14,
            "item_ids": ["uw_tonic_whisper"] * order["min_quantity"],
        }
        first, second = await asyncio.gather(
            client.post("/api/shipments/submit", json=payload, headers=headers),
            client.post("/api/shipments/submit", json=payload, headers=headers),
        )

        assert sorted([first.status_code, second.status_code]) == [200, 409]

        async with open_db(str(tmp_path / "underworld-test.db")) as db:
            shipment_count = await fetchone(db, "SELECT COUNT(*) AS count FROM shipments WHERE order_id = ?", (order["id"],))
            player_row = await fetchone(db, "SELECT total_rep, gold FROM players WHERE id = ?", (registered["player_id"],))
        assert shipment_count["count"] == 1
        assert player_row["total_rep"] > 0
        assert player_row["gold"] > 0


@pytest.mark.asyncio
async def test_heat_clamps_to_territory_capacity_and_scales_heat_level(tmp_path):
    async for client, app in make_client(tmp_path):
        registered = await register(client)
        headers = auth_header(registered["auth_token"])

        orders = await client.get("/api/orders/available", headers=headers)
        order = orders.json()["orders"][0]
        accepted = await client.post("/api/orders/accept", json={"order_id": order["id"]}, headers=headers)
        assert accepted.status_code == 200

        async with open_db(str(tmp_path / "underworld-test.db")) as db:
            await db.execute(
                "UPDATE territories SET heat = 9, heat_capacity = 10 WHERE id = ?",
                (order["territory_id"],),
            )

        app.state.rng = FixedRng([1.0, 1.0, 1.0])
        submitted = await client.post(
            "/api/shipments/submit",
            json={
                "order_id": order["id"],
                "quantity": order["min_quantity"],
                "avg_potency": order["min_potency"] + 5,
                "avg_toxicity": 10,
                "avg_traceability": 10,
                "item_ids": ["uw_tonic_whisper"] * order["min_quantity"],
            },
            headers=headers,
        )
        assert submitted.status_code == 200
        assert submitted.json()["territory_heat_after"] == 10

        territories = await client.get("/api/territories", headers=headers)
        territory = next(item for item in territories.json()["territories"] if item["id"] == order["territory_id"])
        assert territory["heat"] == 10
        assert territory["heat_level"] == "lockdown"


def test_heat_levels_and_enforcement_scale_with_capacity():
    assert config.heat_level_name(18, 60) == "clear"
    assert config.heat_level_name(19, 60) == "elevated"
    assert config.heat_level_name(31, 60) == "high"
    assert config.heat_level_name(43, 60) == "critical"
    assert config.heat_level_name(52, 60) == "lockdown"
    assert enforcement_profile(45, 60, False) == enforcement_profile(90, 120, False)


@pytest.mark.asyncio
async def test_submit_can_trigger_bust(tmp_path):
    async for client, app in make_client(tmp_path):
        registered = await register(client)
        headers = auth_header(registered["auth_token"])

        async with open_db(str(tmp_path / "underworld-test.db")) as db:
            await db.execute("UPDATE territories SET heat = 95")

        orders = await client.get("/api/orders/available", headers=headers)
        order = orders.json()["orders"][0]
        order_id = order["id"]
        await client.post("/api/orders/accept", json={"order_id": order_id}, headers=headers)

        app.state.rng = FixedRng([0.0, 1.0, 1.0])
        quantity = order["min_quantity"]
        busted = await client.post(
            "/api/shipments/submit",
            json={
                "order_id": order_id,
                "quantity": quantity,
                "avg_potency": max(order["min_potency"], 55),
                "avg_toxicity": 10,
                "avg_traceability": 20,
                "item_ids": ["uw_tonic_whisper"] * quantity,
            },
            headers=headers,
        )
        assert busted.status_code == 200
        assert busted.json()["enforcement_event"]["type"] == "bust"
