from __future__ import annotations

import hashlib
import os
import uuid
from datetime import datetime, timedelta, timezone
from typing import Annotated

from fastapi import APIRouter, Depends, Header, HTTPException, status
from jose import JWTError, jwt
from pydantic import BaseModel, Field

from database import connect


TOKEN_DAYS = 90
ALGORITHM = "HS256"

router = APIRouter(prefix="/guild", tags=["guild"])


class RegisterRequest(BaseModel):
    install_key: str = Field(min_length=16, max_length=128)
    display_name: str = Field(min_length=1, max_length=80)
    game_version: str | None = Field(default=None, max_length=40)
    mod_version: str | None = Field(default=None, max_length=40)


class RefreshRequest(BaseModel):
    install_key: str = Field(min_length=16, max_length=128)


class AuthResponse(BaseModel):
    auth_token: str
    account_id: str


def now_utc() -> datetime:
    return datetime.now(timezone.utc)


def iso(dt: datetime) -> str:
    return dt.isoformat(timespec="seconds").replace("+00:00", "Z")


def parse_iso(value: str) -> datetime:
    return datetime.fromisoformat(value.replace("Z", "+00:00"))


def jwt_secret() -> str:
    secret = os.environ.get("SKYREADER_JWT_SECRET")
    if not secret:
        raise RuntimeError("SKYREADER_JWT_SECRET must be set before starting the SkyreaderGuild server.")
    return secret


def token_hash(token: str) -> str:
    return hashlib.sha256(token.encode("utf-8")).hexdigest()


def create_token(account_id: str) -> str:
    issued = now_utc()
    expires = issued + timedelta(days=TOKEN_DAYS)
    token = jwt.encode(
        {
            "sub": account_id,
            "jti": str(uuid.uuid4()),
            "iat": int(issued.timestamp()),
            "exp": int(expires.timestamp()),
        },
        jwt_secret(),
        algorithm=ALGORITHM,
    )
    with connect() as db:
        db.execute(
            """
            INSERT INTO auth_tokens(token_hash, account_id, created_at, expires_at)
            VALUES (?, ?, ?, ?)
            """,
            (token_hash(token), account_id, iso(issued), iso(expires)),
        )
    return token


@router.post("/register-anon", response_model=AuthResponse)
def register_anon(request: RegisterRequest) -> AuthResponse:
    account_id = str(uuid.uuid4())
    seen = iso(now_utc())
    with connect() as db:
        row = db.execute(
            "SELECT id FROM guild_accounts WHERE install_key = ?",
            (request.install_key,),
        ).fetchone()
        if row is None:
            db.execute(
                """
                INSERT INTO guild_accounts(
                    id, install_key, display_name, highest_rank, game_version, mod_version, created_at, last_seen_at
                )
                VALUES (?, ?, ?, 0, ?, ?, ?, ?)
                """,
                (
                    account_id,
                    request.install_key,
                    request.display_name,
                    request.game_version,
                    request.mod_version,
                    seen,
                    seen,
                ),
            )
        else:
            account_id = row["id"]
            db.execute(
                """
                UPDATE guild_accounts
                SET display_name = ?, game_version = ?, mod_version = ?, last_seen_at = ?
                WHERE id = ?
                """,
                (request.display_name, request.game_version, request.mod_version, seen, account_id),
            )

    return AuthResponse(auth_token=create_token(account_id), account_id=account_id)


@router.post("/refresh-token", response_model=AuthResponse)
def refresh_token(request: RefreshRequest) -> AuthResponse:
    with connect() as db:
        row = db.execute(
            "SELECT id FROM guild_accounts WHERE install_key = ?",
            (request.install_key,),
        ).fetchone()
    if row is None:
        raise HTTPException(status_code=status.HTTP_404_NOT_FOUND, detail="Unknown install key.")
    return AuthResponse(auth_token=create_token(row["id"]), account_id=row["id"])


def get_current_account(authorization: Annotated[str | None, Header()] = None) -> dict:
    if authorization is None or not authorization.startswith("Bearer "):
        raise HTTPException(status_code=status.HTTP_401_UNAUTHORIZED, detail="Missing bearer token.")

    token = authorization.removeprefix("Bearer ").strip()
    try:
        payload = jwt.decode(token, jwt_secret(), algorithms=[ALGORITHM])
        account_id = payload.get("sub")
    except JWTError:
        raise HTTPException(status_code=status.HTTP_401_UNAUTHORIZED, detail="Invalid bearer token.") from None

    if not isinstance(account_id, str) or not account_id:
        raise HTTPException(status_code=status.HTTP_401_UNAUTHORIZED, detail="Invalid bearer token.")

    with connect() as db:
        token_row = db.execute(
            "SELECT expires_at FROM auth_tokens WHERE token_hash = ? AND account_id = ?",
            (token_hash(token), account_id),
        ).fetchone()
        if token_row is None or parse_iso(token_row["expires_at"]) <= now_utc():
            raise HTTPException(status_code=status.HTTP_401_UNAUTHORIZED, detail="Expired bearer token.")

        account = db.execute(
            "SELECT * FROM guild_accounts WHERE id = ?",
            (account_id,),
        ).fetchone()
        if account is None:
            raise HTTPException(status_code=status.HTTP_401_UNAUTHORIZED, detail="Unknown account.")

    return dict(account)
