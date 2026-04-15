# SkyreaderGuild Starlight Ladder Server

Self-hostable FastAPI backend for the SkyreaderGuild Elin mod's global Starlight Ladder.

## Quick Start

```powershell
cd SkyreaderGuildServer
python -m venv .venv
.venv\Scripts\Activate.ps1
python -m pip install -e .
$env:SKYREADER_JWT_SECRET = "replace-with-a-long-random-secret"
$env:SKYREADER_DB_PATH = "skyreader.db"
skyreader-guild-server --host 127.0.0.1 --port 8000
```

Open `http://127.0.0.1:8000/docs` for the generated API docs.

## Configuration

- `SKYREADER_JWT_SECRET`: Required. HMAC secret used to sign bearer tokens.
- `SKYREADER_DB_PATH`: Optional. SQLite database path. Defaults to `skyreader.db`.

Use HTTPS when hosting for other players. The mod defaults to `http://localhost:8000` for local testing.
