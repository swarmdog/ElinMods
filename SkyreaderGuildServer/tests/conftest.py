import re
import uuid
from pathlib import Path

import pytest


@pytest.fixture
def tmp_path(request):
    root = Path(__file__).resolve().parents[1] / "worklog" / "pytest" / "test_tmp"
    root.mkdir(parents=True, exist_ok=True)

    safe_name = re.sub(r"[^A-Za-z0-9_.-]+", "_", request.node.name)
    path = root / f"{safe_name}-{uuid.uuid4().hex}"
    path.mkdir()
    return path
