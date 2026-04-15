import os
import subprocess
import sys
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]


def test_cli_help_runs_from_packaged_source_layout():
    assert (ROOT / "pyproject.toml").exists()
    assert (ROOT / "src" / "skyreaderguild_server" / "__main__.py").exists()

    env = os.environ.copy()
    env["PYTHONPATH"] = str(ROOT / "src") + os.pathsep + env.get("PYTHONPATH", "")
    result = subprocess.run(
        [sys.executable, "-m", "skyreaderguild_server", "--help"],
        check=True,
        capture_output=True,
        text=True,
        env=env,
        cwd=str(ROOT),
    )

    assert "skyreader-guild-server" in result.stdout
