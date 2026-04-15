from __future__ import annotations

import uuid
from datetime import datetime, timezone

from fastapi import APIRouter, Depends, HTTPException, status
from pydantic import BaseModel, Field

from auth import get_current_account
from database import connect


router = APIRouter(prefix="/research-notes", tags=["research-notes"])

BLOCKED_WORDS = {"fuck", "shit", "nigger", "faggot", "retard", "kike", "cunt"}

_MAX_NOTES_PER_DAY = 3


class NoteCreateIn(BaseModel):
    title: str = Field(min_length=3, max_length=80)
    body: str = Field(min_length=10, max_length=800)


class NoteCreateResult(BaseModel):
    id: str
    created: bool


class NoteEntry(BaseModel):
    id: str
    title: str
    body: str
    rating: int
    created_at: str


class NotePullResponse(BaseModel):
    notes: list[NoteEntry]


class NoteRateIn(BaseModel):
    note_id: str = Field(min_length=1, max_length=128)
    value: int = Field(ge=-1, le=1)


class NoteRateResult(BaseModel):
    rated: bool
    new_rating: int


def _now_iso() -> str:
    return datetime.now(timezone.utc).isoformat(timespec="seconds").replace("+00:00", "Z")


def _check_profanity(text: str) -> bool:
    words = set(text.lower().split())
    return bool(words & BLOCKED_WORDS)


def _start_of_utc_day() -> str:
    now = datetime.now(timezone.utc)
    start = now.replace(hour=0, minute=0, second=0, microsecond=0)
    return start.isoformat(timespec="seconds").replace("+00:00", "Z")


@router.post("/create", response_model=NoteCreateResult)
def create_note(
    request: NoteCreateIn,
    account: dict = Depends(get_current_account),
) -> NoteCreateResult:
    if _check_profanity(request.title) or _check_profanity(request.body):
        raise HTTPException(
            status_code=status.HTTP_400_BAD_REQUEST,
            detail="Content contains prohibited language.",
        )

    account_id = account["id"]
    day_start = _start_of_utc_day()

    with connect() as db:
        # Rate limit check.
        count = db.execute(
            "SELECT COUNT(*) AS n FROM research_notes WHERE player_id = ? AND created_at >= ?",
            (account_id, day_start),
        ).fetchone()["n"]

        if count >= _MAX_NOTES_PER_DAY:
            raise HTTPException(
                status_code=status.HTTP_429_TOO_MANY_REQUESTS,
                detail=f"Maximum {_MAX_NOTES_PER_DAY} star papers per day.",
            )

        note_id = str(uuid.uuid4())
        db.execute(
            """
            INSERT INTO research_notes(id, player_id, title, body, created_at, rating)
            VALUES (?, ?, ?, ?, ?, 0)
            """,
            (note_id, account_id, request.title, request.body, _now_iso()),
        )

    return NoteCreateResult(id=note_id, created=True)


@router.get("/pull", response_model=NotePullResponse)
def pull_notes(
    limit: int = 5,
    account: dict = Depends(get_current_account),
) -> NotePullResponse:
    limit = min(max(limit, 1), 10)
    account_id = account["id"]
    now = _now_iso()

    with connect() as db:
        rows = db.execute(
            """
            SELECT rn.id, rn.title, rn.body, rn.rating, rn.created_at
            FROM research_notes rn
            WHERE rn.id NOT IN (SELECT note_id FROM research_note_pulls WHERE player_id = ?)
              AND rn.player_id != ?
            ORDER BY rn.rating DESC, rn.created_at DESC
            LIMIT ?
            """,
            (account_id, account_id, limit),
        ).fetchall()

        # Record the pulls so they aren't served again.
        for row in rows:
            db.execute(
                "INSERT OR IGNORE INTO research_note_pulls(player_id, note_id, pulled_at) VALUES (?, ?, ?)",
                (account_id, row["id"], now),
            )

    return NotePullResponse(
        notes=[
            NoteEntry(
                id=row["id"],
                title=row["title"],
                body=row["body"],
                rating=row["rating"],
                created_at=row["created_at"],
            )
            for row in rows
        ]
    )


@router.get("/mine", response_model=NotePullResponse)
def my_notes(
    limit: int = 50,
    account: dict = Depends(get_current_account),
) -> NotePullResponse:
    limit = min(max(limit, 1), 50)
    account_id = account["id"]

    with connect() as db:
        rows = db.execute(
            """
            SELECT id, title, body, rating, created_at
            FROM research_notes
            WHERE player_id = ?
            ORDER BY created_at DESC, rowid DESC
            LIMIT ?
            """,
            (account_id, limit),
        ).fetchall()

    return NotePullResponse(
        notes=[
            NoteEntry(
                id=row["id"],
                title=row["title"],
                body=row["body"],
                rating=row["rating"],
                created_at=row["created_at"],
            )
            for row in rows
        ]
    )


@router.post("/rate", response_model=NoteRateResult)
def rate_note(
    request: NoteRateIn,
    account: dict = Depends(get_current_account),
) -> NoteRateResult:
    if request.value == 0:
        raise HTTPException(
            status_code=status.HTTP_422_UNPROCESSABLE_ENTITY,
            detail="Rating value must be 1 or -1.",
        )

    account_id = account["id"]

    with connect() as db:
        # Verify note exists.
        note = db.execute(
            "SELECT id FROM research_notes WHERE id = ?",
            (request.note_id,),
        ).fetchone()
        if note is None:
            raise HTTPException(
                status_code=status.HTTP_404_NOT_FOUND,
                detail="Note not found.",
            )

        # Upsert rating.
        db.execute(
            """
            INSERT INTO research_note_ratings(player_id, note_id, value, rated_at)
            VALUES (?, ?, ?, ?)
            ON CONFLICT(player_id, note_id) DO UPDATE SET
                value = excluded.value,
                rated_at = excluded.rated_at
            """,
            (account_id, request.note_id, request.value, _now_iso()),
        )

        # Recalculate total rating.
        new_rating = db.execute(
            "SELECT COALESCE(SUM(value), 0) AS total FROM research_note_ratings WHERE note_id = ?",
            (request.note_id,),
        ).fetchone()["total"]

        db.execute(
            "UPDATE research_notes SET rating = ? WHERE id = ?",
            (new_rating, request.note_id),
        )

    return NoteRateResult(rated=True, new_rating=new_rating)
