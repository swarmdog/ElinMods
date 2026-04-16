from __future__ import annotations

import random
from contextlib import asynccontextmanager

from fastapi import FastAPI

from . import auth, factions, jobs, orders, players, shipments, territories
from .database import init_db


def create_app(db_path: str | None = None, run_jobs: bool = True) -> FastAPI:
    @asynccontextmanager
    async def lifespan(app: FastAPI):
        await init_db(app.state.db_path)
        await jobs.ensure_initial_orders(app.state.db_path)
        app.state.job_tasks = []
        if app.state.run_jobs:
            app.state.job_tasks = await jobs.start_background_jobs(app)
        try:
            yield
        finally:
            if app.state.job_tasks:
                await jobs.stop_background_jobs(app.state.job_tasks)

    app = FastAPI(
        title="Elin Underworld Simulator API",
        version="0.1.0",
        lifespan=lifespan,
    )
    app.state.db_path = db_path
    app.state.run_jobs = run_jobs
    app.state.rng = random.Random()

    app.include_router(auth.router)
    app.include_router(orders.router)
    app.include_router(shipments.router)
    app.include_router(territories.router)
    app.include_router(factions.router)
    app.include_router(players.router)

    @app.get("/health")
    async def health() -> dict[str, str]:
        return {"status": "ok"}

    return app


app = create_app()
