# UnderworldServer

Self-hostable FastAPI backend for the Elin Underworld Simulator mod.

## Local Run

```powershell
cd ElinUnderworldSimulator/Server/UnderworldServer
py -3 -m pip install -e .
py -3 -m underworld_server --host 127.0.0.1 --port 8001
```

The bundled C# bootstrapper installs and launches this package automatically when the mod is configured to use a loopback HTTP URL.
