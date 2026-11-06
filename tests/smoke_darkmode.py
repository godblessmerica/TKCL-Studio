# SPDX-License-Identifier: AGPL-3.0-or-later
"""Exercise the desktop test build with explicitly supplied local Dark Mode inputs."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import subprocess
import shutil
import uuid

root = Path(__file__).resolve().parents[1]
parser = argparse.ArgumentParser(description=__doc__)
for name in ("mod", "base", "update", "prod"):
    parser.add_argument("--" + name, required=True)
parser.add_argument("--title", default="")
args = parser.parse_args()
executable = os.environ.get("DOTNET_EXECUTABLE") or shutil.which("dotnet") or str(root / ".cache/dotnet/dotnet")
assembly = root / ".cache/test-bin/TKCLStudio.dll"
assert assembly.exists(), "Run ./test first"
command = [executable, str(assembly)]
data = root / ".cache" / ("packaged-test-" + uuid.uuid4().hex[:8])
assert data.resolve().is_relative_to(root) and not data.exists()
data.mkdir()
env = os.environ.copy()
env.update(TKCL_STUDIO_HOME=str(data), TKCL_STUDIO_SMOKE="1",
           TMPDIR=str(root / ".cache/tmp"), XDG_CACHE_HOME=str(root / ".cache/xdg"),
           REUSE_CHECK_DELAY="0", DOTNET_EnableDiagnostics="0")
env["TKCL_STUDIO_SMOKE_INPUTS"] = json.dumps({
    "Mod": args.mod, "Base": args.base, "Update": args.update,
    "Prod": args.prod, "Title": args.title,
})
completed = subprocess.run(command, env=env, capture_output=True, text=True, timeout=55)
(data / "launch.log").write_text(completed.stdout + "\n" + completed.stderr)
assert completed.returncode == 0, completed.stderr
result = json.loads((data / "smoke-result.json").read_text())
assert result["Success"], result
assert result["Stages"] == ["Reading mod package", "Opening local game files",
                            "Merging selected options", "Checking output files", "Build complete"]
output = Path(result["Output"])
assert output.resolve().is_relative_to(data)
assert len(list(output.rglob("*.zs"))) == 3
assert not (output / "exefs").exists()
assert not (data / "settings.json").exists(), "Remember off must delete saved paths"
assert not list((data / "work").iterdir()), "Package scratch must be cleaned"
assert not any(p.suffix in (".keys", ".nsp", ".xci") for p in data.rglob("*"))
for name in ("ui-empty.png", "ui-options.png", "ui-complete.png"):
    assert (data / name).stat().st_size > 10000

completed = subprocess.run(command + ["self-test"], env=env, capture_output=True, text=True, timeout=25)
assert completed.returncode == 0 and "required-option validation" in completed.stdout
bad = data / "bad.tkcl"
bad.write_bytes(b"NOTATKCL")
completed = subprocess.run(command + ["inspect", str(bad)], env=env, capture_output=True, text=True, timeout=25)
assert completed.returncode == 1 and "Expected TKMP" in completed.stdout
bad.unlink()
print(json.dumps({"passed": True, "evidence": str(data), "output": str(output),
                  "test_build_sha256": hashlib.sha256(assembly.read_bytes()).hexdigest()}, indent=2))
