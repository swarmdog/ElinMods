import re
import sys
import uuid
from pathlib import Path

import pytest


ROOT = Path(__file__).resolve().parents[1]
SRC = ROOT / "src"
sys.path.insert(0, str(SRC))


@pytest.fixture
def tmp_path(request):
    root = ROOT / "worklog" / "pytest" / "test_tmp"
    root.mkdir(parents=True, exist_ok=True)

    safe_name = re.sub(r"[^A-Za-z0-9_.-]+", "_", request.node.name)
    path = root / f"{safe_name}-{uuid.uuid4().hex}"
    path.mkdir()
    return path
