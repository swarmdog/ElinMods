from __future__ import annotations

from contextlib import asynccontextmanager

from fastapi import FastAPI

import auth
import contributions
import ladder
from database import init_db


@asynccontextmanager
async def lifespan(app: FastAPI):
    auth.jwt_secret()
    init_db()
    yield


app = FastAPI(
    title="SkyreaderGuild Starlight Ladder",
    version="1.0.0",
    lifespan=lifespan,
)

app.include_router(auth.router)
app.include_router(contributions.router)
app.include_router(ladder.router)


@app.get("/health")
def health() -> dict[str, str]:
    return {"status": "ok"}
