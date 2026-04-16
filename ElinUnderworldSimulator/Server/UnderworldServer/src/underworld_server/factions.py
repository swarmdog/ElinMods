from __future__ import annotations

from fastapi import APIRouter, Depends, HTTPException, status

from . import config, schemas
from .auth import get_current_player
from .database import fetchall, fetchone, get_db, iso


router = APIRouter(prefix="/api/factions", tags=["factions"])


@router.post("/create", response_model=schemas.FactionCreateResponse)
async def create_faction(
    request: schemas.FactionCreateRequest,
    player=Depends(get_current_player),
    db=Depends(get_db),
) -> schemas.FactionCreateResponse:
    normalized_name = request.name.strip()
    if not normalized_name:
        raise HTTPException(status_code=status.HTTP_400_BAD_REQUEST, detail="Faction name cannot be blank")
    if player["underworld_rank"] < 2:
        raise HTTPException(status_code=status.HTTP_403_FORBIDDEN, detail="Must be rank Supplier or higher to create a faction")
    if player["faction_id"] is not None:
        raise HTTPException(status_code=status.HTTP_409_CONFLICT, detail="Already in a faction")
    existing = await fetchone(db, "SELECT id FROM factions WHERE name = ? COLLATE NOCASE", (normalized_name,))
    if existing is not None:
        raise HTTPException(status_code=status.HTTP_409_CONFLICT, detail="Faction name already taken")

    cursor = await db.execute(
        """
        INSERT INTO factions(name, leader_player_id, max_members, created_at)
        VALUES (?, ?, ?, ?)
        """,
        (normalized_name, player["id"], config.FACTION_MAX_MEMBERS_BASE, iso()),
    )
    faction_id = cursor.lastrowid
    await cursor.close()
    await db.execute(
        "INSERT INTO faction_members(faction_id, player_id, role, joined_at) VALUES (?, ?, 'leader', ?)",
        (faction_id, player["id"], iso()),
    )
    await db.execute("UPDATE players SET faction_id = ? WHERE id = ?", (faction_id, player["id"]))
    return schemas.FactionCreateResponse(faction_id=faction_id, name=normalized_name)


@router.post("/join", response_model=schemas.FactionJoinResponse)
async def join_faction(
    request: schemas.FactionJoinRequest,
    player=Depends(get_current_player),
    db=Depends(get_db),
) -> schemas.FactionJoinResponse:
    if player["faction_id"] is not None:
        raise HTTPException(status_code=status.HTTP_409_CONFLICT, detail="Already in a faction")

    faction = await fetchone(db, "SELECT * FROM factions WHERE id = ?", (request.faction_id,))
    if faction is None:
        raise HTTPException(status_code=status.HTTP_404_NOT_FOUND, detail="Faction not found")

    count_row = await fetchone(db, "SELECT COUNT(*) AS count FROM faction_members WHERE faction_id = ?", (request.faction_id,))
    if count_row["count"] >= faction["max_members"]:
        raise HTTPException(status_code=status.HTTP_409_CONFLICT, detail="Faction is full")

    await db.execute(
        "INSERT INTO faction_members(faction_id, player_id, role, joined_at) VALUES (?, ?, 'member', ?)",
        (request.faction_id, player["id"], iso()),
    )
    await db.execute("UPDATE players SET faction_id = ? WHERE id = ?", (request.faction_id, player["id"]))
    return schemas.FactionJoinResponse(status="joined", faction_name=faction["name"])


@router.get("/{faction_id}", response_model=schemas.FactionDetailResponse)
async def faction_detail(faction_id: int, player=Depends(get_current_player), db=Depends(get_db)) -> schemas.FactionDetailResponse:
    faction = await fetchone(
        db,
        """
        SELECT f.*, p.display_name AS leader_name
        FROM factions f
        JOIN players p ON p.id = f.leader_player_id
        WHERE f.id = ?
        """,
        (faction_id,),
    )
    if faction is None:
        raise HTTPException(status_code=status.HTTP_404_NOT_FOUND, detail="Faction not found")

    members = await fetchall(
        db,
        """
        SELECT p.display_name, fm.role, p.underworld_rank
        FROM faction_members fm
        JOIN players p ON p.id = fm.player_id
        WHERE fm.faction_id = ?
        ORDER BY CASE fm.role WHEN 'leader' THEN 0 WHEN 'officer' THEN 1 ELSE 2 END, p.display_name
        """,
        (faction_id,),
    )
    territories = await fetchall(db, "SELECT id FROM territories WHERE controlling_faction_id = ? ORDER BY id", (faction_id,))

    return schemas.FactionDetailResponse(
        id=faction["id"],
        name=faction["name"],
        leader=faction["leader_name"],
        member_count=len(members),
        max_members=faction["max_members"],
        controlled_territories=[row["id"] for row in territories],
        members=[
            schemas.FactionMemberDto(display_name=row["display_name"], role=row["role"], rank=row["underworld_rank"])
            for row in members
        ],
    )


@router.post("/leave", response_model=schemas.FactionLeaveResponse)
async def leave_faction(
    player=Depends(get_current_player),
    db=Depends(get_db),
) -> schemas.FactionLeaveResponse:
    if player["faction_id"] is None:
        raise HTTPException(status_code=status.HTTP_409_CONFLICT, detail="Not in a faction")

    faction = await fetchone(db, "SELECT * FROM factions WHERE id = ?", (player["faction_id"],))
    if faction is None:
        raise HTTPException(status_code=status.HTTP_404_NOT_FOUND, detail="Faction not found")
    if faction["leader_player_id"] == player["id"]:
        raise HTTPException(status_code=status.HTTP_409_CONFLICT, detail="Leaders must disband or transfer leadership before leaving")

    await db.execute("DELETE FROM faction_members WHERE faction_id = ? AND player_id = ?", (player["faction_id"], player["id"]))
    await db.execute("UPDATE players SET faction_id = NULL WHERE id = ?", (player["id"],))
    return schemas.FactionLeaveResponse(status="left", faction_name=faction["name"])


@router.delete("/{faction_id}", response_model=schemas.FactionDisbandResponse)
async def disband_faction(
    faction_id: int,
    player=Depends(get_current_player),
    db=Depends(get_db),
) -> schemas.FactionDisbandResponse:
    faction = await fetchone(db, "SELECT * FROM factions WHERE id = ?", (faction_id,))
    if faction is None:
        raise HTTPException(status_code=status.HTTP_404_NOT_FOUND, detail="Faction not found")
    if faction["leader_player_id"] != player["id"]:
        raise HTTPException(status_code=status.HTTP_403_FORBIDDEN, detail="Only the leader can disband the faction")

    await db.execute("UPDATE players SET faction_id = NULL WHERE faction_id = ?", (faction_id,))
    await db.execute("DELETE FROM faction_members WHERE faction_id = ?", (faction_id,))
    await db.execute(
        """
        UPDATE territories
        SET controlling_faction_id = NULL, control_scores_json = '{}'
        WHERE controlling_faction_id = ?
        """,
        (faction_id,),
    )
    await db.execute("DELETE FROM factions WHERE id = ?", (faction_id,))
    return schemas.FactionDisbandResponse(status="disbanded", faction_name=faction["name"])


@router.post("/{faction_id}/promote", response_model=schemas.FactionPromoteResponse)
async def promote_member(
    faction_id: int,
    request: schemas.FactionPromoteRequest,
    player=Depends(get_current_player),
    db=Depends(get_db),
) -> schemas.FactionPromoteResponse:
    faction = await fetchone(db, "SELECT * FROM factions WHERE id = ?", (faction_id,))
    if faction is None:
        raise HTTPException(status_code=status.HTTP_404_NOT_FOUND, detail="Faction not found")
    if faction["leader_player_id"] != player["id"]:
        raise HTTPException(status_code=status.HTTP_403_FORBIDDEN, detail="Only the leader can promote members")

    member = await fetchone(
        db,
        "SELECT fm.*, p.display_name FROM faction_members fm JOIN players p ON p.id = fm.player_id WHERE fm.faction_id = ? AND fm.player_id = ?",
        (faction_id, request.player_id),
    )
    if member is None:
        raise HTTPException(status_code=status.HTTP_404_NOT_FOUND, detail="Player is not a member of this faction")
    if member["role"] == "leader":
        raise HTTPException(status_code=status.HTTP_409_CONFLICT, detail="Cannot promote the leader")

    await db.execute(
        "UPDATE faction_members SET role = 'officer' WHERE faction_id = ? AND player_id = ?",
        (faction_id, request.player_id),
    )
    return schemas.FactionPromoteResponse(status="promoted", player_display_name=member["display_name"], new_role="officer")
