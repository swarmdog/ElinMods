from __future__ import annotations

from fastapi import APIRouter, Depends, HTTPException, Query, status

from . import config, schemas
from .auth import get_current_player
from .database import fetchall, fetchone, get_db, iso, parse_iso, utc_now


router = APIRouter(prefix="/api/orders", tags=["orders"])


@router.get("/available", response_model=schemas.OrderListResponse)
async def available_orders(
    territory_id: str | None = Query(default=None),
    limit: int = Query(default=20, ge=1, le=100),
    player=Depends(get_current_player),
    db=Depends(get_db),
) -> schemas.OrderListResponse:
    now = iso()
    blocked_high_risk = False
    if player["under_investigation_until"]:
        investigation_until = parse_iso(player["under_investigation_until"])
        blocked_high_risk = investigation_until is not None and investigation_until > utc_now()

    params: list = [now, player["underworld_rank"]]
    clauses = [
        "o.status = 'available'",
        "o.expires_at > ?",
        "o.required_rank_tier <= ?",
    ]
    if territory_id:
        clauses.append("o.territory_id = ?")
        params.append(territory_id)
    if blocked_high_risk:
        clauses.append("o.client_type NOT IN ('broker', 'syndicate')")

    rows = await fetchall(
        db,
        f"""
        SELECT o.*, t.name AS territory_name
        FROM orders o
        JOIN territories t ON t.id = o.territory_id
        WHERE {' AND '.join(clauses)}
        ORDER BY o.created_at DESC, o.id DESC
        LIMIT ?
        """,
        tuple(params + [limit]),
    )

    return schemas.OrderListResponse(
        orders=[
            schemas.OrderDto(
                id=row["id"],
                territory_id=row["territory_id"],
                territory_name=row["territory_name"],
                client_type=row["client_type"],
                client_name=config.CLIENTS[row["client_type"]].display_name,
                product_type=row["product_type"],
                product_id=row["product_id"],
                min_quantity=row["min_quantity"],
                max_quantity=row["max_quantity"],
                min_potency=row["min_potency"],
                max_toxicity=row["max_toxicity"],
                base_payout=row["base_payout"],
                deadline_hours=row["deadline_hours"],
                created_at=row["created_at"],
                expires_at=row["expires_at"],
            )
            for row in rows
        ]
    )


@router.post("/accept", response_model=schemas.OrderAcceptResponse)
async def accept_order(
    request: schemas.OrderAcceptRequest,
    player=Depends(get_current_player),
    db=Depends(get_db),
) -> schemas.OrderAcceptResponse:
    order = await fetchone(db, "SELECT * FROM orders WHERE id = ?", (request.order_id,))
    if order is None:
        raise HTTPException(status_code=status.HTTP_404_NOT_FOUND, detail="Order not found")
    if order["status"] != "available" or parse_iso(order["expires_at"]) <= utc_now():
        raise HTTPException(status_code=status.HTTP_409_CONFLICT, detail="Order already claimed")
    if order["required_rank_tier"] > player["underworld_rank"]:
        raise HTTPException(status_code=status.HTTP_403_FORBIDDEN, detail="Insufficient rank for this order type")

    if player["under_investigation_until"]:
        investigation_until = parse_iso(player["under_investigation_until"])
        if investigation_until is not None and investigation_until > utc_now() and order["client_type"] in {"broker", "syndicate"}:
            raise HTTPException(status_code=status.HTTP_403_FORBIDDEN, detail="You are under investigation")

    accepted_at = iso()
    cursor = await db.execute(
        """
        UPDATE orders
        SET status = 'accepted', assigned_player_id = ?, accepted_at = ?
        WHERE id = ? AND status = 'available' AND expires_at > ?
        """,
        (player["id"], accepted_at, request.order_id, accepted_at),
    )
    updated_rows = cursor.rowcount
    await cursor.close()
    if updated_rows != 1:
        refreshed = await fetchone(db, "SELECT * FROM orders WHERE id = ?", (request.order_id,))
        if refreshed is None:
            raise HTTPException(status_code=status.HTTP_404_NOT_FOUND, detail="Order not found")
        if refreshed["required_rank_tier"] > player["underworld_rank"]:
            raise HTTPException(status_code=status.HTTP_403_FORBIDDEN, detail="Insufficient rank for this order type")
        if parse_iso(refreshed["expires_at"]) <= utc_now():
            raise HTTPException(status_code=status.HTTP_409_CONFLICT, detail="Order expired")
        raise HTTPException(status_code=status.HTTP_409_CONFLICT, detail="Order already claimed")

    updated = await fetchone(db, "SELECT * FROM orders WHERE id = ?", (request.order_id,))
    return schemas.OrderAcceptResponse(status="accepted", order_id=updated["id"], deadline=updated["expires_at"])
