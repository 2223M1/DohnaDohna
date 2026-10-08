"""Read the PCK directory only; reject private/reference/authoring inputs."""
import struct
import sys
from pathlib import Path

path = Path(sys.argv[1])
with path.open("rb") as f:
    def u32(): return struct.unpack("<I", f.read(4))[0]
    def u64(): return struct.unpack("<Q", f.read(8))[0]
    assert f.read(4) == b"GDPC", "Not a Godot PCK"
    version = u32()
    assert version in (2, 3), f"Unsupported PCK format {version}"
    major, minor, patch = u32(), u32(), u32()
    flags, base = u32(), u64()
    if version == 3:
        directory_offset = u64()
        f.seek(directory_offset)
    else:
        f.read(64)
    count = u32()
    names = []
    for _ in range(count):
        size = u32()
        name = f.read(size).rstrip(b"\0").decode("utf-8")
        f.read(8 + 8 + 16 + 4)
        names.append(name)
    assert names, "Empty pack"
    for name in names:
        normalized = name.removeprefix("res://")
        assert ".." not in normalized.split("/"), name
        root = normalized.split("/")[0]
        # Godot emits script-path stubs even with embedded source disabled.
        script_stub = root in ("Code", "Cards", "Content", "Monsters", "Powers", "Relics", "Scripts") and normalized.endswith(".cs")
        assert root in ("DohnaDohna", ".godot") or normalized == "project.binary" or script_stub, name
        assert not normalized.endswith((".dll", ".wav", ".ogg", ".fspro")), name
    for role in ("kuma", "alyce", "antena", "tora", "kikuchiyo", "medhico", "joker", "zappa", "kirakira", "porno"):
        assert any(name.endswith(f"roles/{role}/motions.json") for name in names), role
    assert any(name.endswith("audio/fmod/DohnaDohna.bank") for name in names), "Missing audio bank"
    assert "DohnaDohna/effects.json" in names or "res://DohnaDohna/effects.json" in names, "Missing original line effect binding"
    print(f"PASS PCK v{version}: {count} entries; ten role timelines and own FMOD bank present.")
