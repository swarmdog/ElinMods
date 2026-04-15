from __future__ import annotations

from contextlib import asynccontextmanager

from fastapi import FastAPI

import auth
import comet
import constellations
import contributions
import geometry
import ladder
import research_notes
import seasons
from database import init_db


@asynccontextmanager
async def lifespan(app: FastAPI):
    auth.jwt_secret()
    init_db()
    yield


app = FastAPI(
    title="SkyreaderGuild Starlight Ladder",
    version="2.0.0",
    lifespan=lifespan,
)

app.include_router(auth.router)
app.include_router(contributions.router)
app.include_router(ladder.router)
app.include_router(seasons.router)
app.include_router(constellations.router)
app.include_router(geometry.router)
app.include_router(comet.router)
app.include_router(research_notes.router)


@app.get("/health")
def health() -> dict[str, str]:
    return {"status": "ok"}
