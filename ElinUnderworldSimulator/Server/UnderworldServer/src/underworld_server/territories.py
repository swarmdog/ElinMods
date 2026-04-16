from __future__ import annotations

from fastapi import APIRouter, Depends

from . import config, schemas
from .auth import get_current_player
from .database import fetchall, get_db


router = APIRouter(prefix="/api/territories", tags=["territories"])


@router.get("", response_model=schemas.TerritoriesResponse)
async def list_territories(player=Depends(get_current_player), db=Depends(get_db)) -> schemas.TerritoriesResponse:
    rows = await fetchall(
        db,
        """
        SELECT
            t.id,
            t.name,
            t.heat,
            t.heat_capacity,
            t.controlling_faction_id,
            f.name AS controlling_faction,
            COUNT(o.id) AS available_orders_count
        FROM territories t
        LEFT JOIN factions f ON f.id = t.controlling_faction_id
        LEFT JOIN orders o ON o.territory_id = t.id AND o.status = 'available'
        GROUP BY t.id, t.name, t.heat, t.heat_capacity, t.controlling_faction_id, f.name
        ORDER BY t.name
        """
    )

    return schemas.TerritoriesResponse(
        territories=[
            schemas.TerritoryDto(
                id=row["id"],
                name=row["name"],
                heat=row["heat"],
                heat_level=config.heat_level_name(row["heat"], row["heat_capacity"]),
                controlling_faction=row["controlling_faction"],
                controlling_faction_id=row["controlling_faction_id"],
                available_orders_count=row["available_orders_count"],
            )
            for row in rows
        ]
    )
