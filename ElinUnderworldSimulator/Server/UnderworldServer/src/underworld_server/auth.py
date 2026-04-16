from __future__ import annotations

import secrets

from fastapi import APIRouter, Depends, Header, HTTPException, Request, status

from . import schemas
from .database import fetchone, get_db, iso, token_hash


router = APIRouter(prefix="/api", tags=["auth"])


def generate_token() -> str:
    return secrets.token_hex(32)


async def issue_token(db, player_id: int) -> str:
    token = generate_token()
    await db.execute(
        "UPDATE players SET auth_token_hash = ?, last_seen_at = ? WHERE id = ?",
        (token_hash(token), iso(), player_id),
    )
    return token


async def get_current_player(
    request: Request,
    authorization: str | None = Header(default=None),
    db=Depends(get_db),
):
    if authorization is None or not authorization.startswith("Bearer "):
        raise HTTPException(status_code=status.HTTP_401_UNAUTHORIZED, detail="Missing auth token")

    token = authorization.removeprefix("Bearer ").strip()
    player = await fetchone(
        db,
        "SELECT * FROM players WHERE auth_token_hash = ?",
        (token_hash(token),),
    )
    if player is None:
        raise HTTPException(status_code=status.HTTP_401_UNAUTHORIZED, detail="Invalid auth token")

    request.state.player_id = player["id"]
    await db.execute("UPDATE players SET last_seen_at = ? WHERE id = ?", (iso(), player["id"]))
    return player


@router.post("/register", response_model=schemas.RegisterResponse)
async def register(request: schemas.RegisterRequest, db=Depends(get_db)) -> schemas.RegisterResponse:
    display_name = request.display_name.strip()
    if not display_name:
        raise HTTPException(status_code=status.HTTP_400_BAD_REQUEST, detail="Display name cannot be blank")
    existing_by_name = await fetchone(
        db,
        "SELECT id, install_key FROM players WHERE display_name = ? COLLATE NOCASE",
        (display_name,),
    )
    existing_player = await fetchone(
        db,
        "SELECT * FROM players WHERE install_key = ?",
        (request.install_key,),
    )

    if existing_player is None and existing_by_name is not None:
        raise HTTPException(status_code=status.HTTP_400_BAD_REQUEST, detail="Display name already taken")

    if existing_player is not None:
        if existing_by_name is not None and existing_by_name["id"] != existing_player["id"]:
            raise HTTPException(status_code=status.HTTP_400_BAD_REQUEST, detail="Display name already taken")

        await db.execute(
            """
            UPDATE players
            SET display_name = ?, game_version = ?, mod_version = ?, last_seen_at = ?
            WHERE id = ?
            """,
            (display_name, request.game_version, request.mod_version, iso(), existing_player["id"]),
        )
        player_id = existing_player["id"]
    else:
        cursor = await db.execute(
            """
            INSERT INTO players(
                install_key, display_name, underworld_rank, total_rep, gold, game_version, mod_version, created_at, last_seen_at
            )
            VALUES (?, ?, 0, 0, 0, ?, ?, ?, ?)
            """,
            (request.install_key, display_name, request.game_version, request.mod_version, iso(), iso()),
        )
        player_id = cursor.lastrowid
        await cursor.close()

    player = await fetchone(db, "SELECT * FROM players WHERE id = ?", (player_id,))
    token = await issue_token(db, player_id)
    return schemas.RegisterResponse(
        player_id=player_id,
        auth_token=token,
        display_name=player["display_name"],
        underworld_rank=player["underworld_rank"],
    )


@router.post("/login", response_model=schemas.LoginResponse)
async def login(request: schemas.LoginRequest, db=Depends(get_db)) -> schemas.LoginResponse:
    player = await fetchone(
        db,
        "SELECT * FROM players WHERE auth_token_hash = ?",
        (token_hash(request.auth_token),),
    )
    if player is None:
        raise HTTPException(status_code=status.HTTP_401_UNAUTHORIZED, detail="Invalid token")

    await db.execute("UPDATE players SET last_seen_at = ? WHERE id = ?", (iso(), player["id"]))
    return schemas.LoginResponse(
        player_id=player["id"],
        display_name=player["display_name"],
        underworld_rank=player["underworld_rank"],
        total_rep=player["total_rep"],
        faction_id=player["faction_id"],
    )
