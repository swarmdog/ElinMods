from __future__ import annotations

from contextlib import asynccontextmanager

from fastapi import FastAPI

from . import auth
from . import comet
from . import constellations
from . import contributions
from . import geometry
from . import ladder
from . import research_notes
from . import seasons
from .database import init_db


@asynccontextmanager
async def lifespan(app: FastAPI):
    auth.jwt_secret()
    init_db()
    yield


def create_app() -> FastAPI:
    app = FastAPI(
        title="SkyreaderGuild Starlight Ladder",
        version="2.1.0",
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

    return app


app = create_app()
