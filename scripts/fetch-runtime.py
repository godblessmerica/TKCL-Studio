# SPDX-License-Identifier: AGPL-3.0-or-later
"""Fetch and verify the pinned AppImage build tool and runtime."""
import hashlib
from pathlib import Path
import urllib.request

root = Path(__file__).resolve().parents[1]
tools = root / ".cache/tools"
assert tools.resolve().is_relative_to(root) and not tools.is_symlink()
tools.mkdir(parents=True, exist_ok=True)
for variant, digest in {
    "uruntime-appimage-squashfs-x86_64": "2153e68d63f570c7ebe3d1f38df0fa16814f34d839c5cce2ee42667e327708e8",
    "uruntime-appimage-squashfs-lite-x86_64": "32c6024a14ff375749ee3bed3330c1586a6dd2bfeddbbd7ceacc053aec2b27a3",
}.items():
    target = tools / variant
    assert target.resolve() == target and not target.is_symlink()
    if not target.exists() or hashlib.sha256(target.read_bytes()).hexdigest() != digest:
        url = f"https://github.com/VHSgunzo/uruntime/releases/download/v0.8.1/{variant}"
        with urllib.request.urlopen(url, timeout=60) as response:
            data = response.read()
        if hashlib.sha256(data).hexdigest() != digest:
            raise SystemExit(f"Checksum mismatch: {variant}")
        target.write_bytes(data)
    target.chmod(0o755)
