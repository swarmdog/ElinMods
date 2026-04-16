from __future__ import annotations

import argparse


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(
        prog="underworld-server",
        description="Run the Underworld local FastAPI server.",
    )
    parser.add_argument("--host", default="127.0.0.1")
    parser.add_argument("--port", type=int, default=8001)
    parser.add_argument("--log-level", default="warning")
    return parser


def main(argv: list[str] | None = None) -> int:
    parser = build_parser()
    args = parser.parse_args(argv)

    import uvicorn

    uvicorn.run(
        "underworld_server.main:app",
        host=args.host,
        port=args.port,
        log_level=args.log_level,
    )
    return 0
