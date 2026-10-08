"""Index/cut real isolated Godot MovieWriter capture; never render a substitute.

MovieWriter uses fixed simulation delta. Clips have no claimed FMOD sound or
wall-clock performance evidence. Keep the original AVI and frame markers.
"""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess

parser = argparse.ArgumentParser()
parser.add_argument("recording", type=Path)
parser.add_argument("--ffmpeg", required=True, type=Path)
args = parser.parse_args()
root = args.recording.resolve()
movie = root / "rendered.avi"
log = (root / "stdout.log").read_text(encoding="utf-8-sig")
if not movie.is_file() or "Done recording movie at path:" not in log:
    raise ValueError("The actual game movie was not completed")
clips = root / "clips"
clips.mkdir(exist_ok=True)
pending, segments = {}, []
for line in log.splitlines():
    if not line.startswith("DOHNA_MOVIE_MARK "):
        continue
    mark = json.loads(line.removeprefix("DOHNA_MOVIE_MARK "))
    key = (mark["role"], mark["cue"])
    if mark["phase"] == "start":
        if key in pending:
            raise ValueError(f"Duplicate start: {key}")
        pending[key] = mark
    elif mark["phase"] == "end":
        start = pending.pop(key)
        a, b = start["drawnFrame"], mark["drawnFrame"]
        if b <= a:
            raise ValueError(f"Empty motion: {key}")
        name = "-".join(key)
        dest = clips / (name+".mp4")
        subprocess.run([str(args.ffmpeg), "-hide_banner", "-loglevel", "error", "-y",
            "-ss", str(a/60), "-i", str(movie), "-t", str((b-a)/60), "-an",
            "-c:v", "libx264", "-crf", "18", "-preset", "fast", "-pix_fmt", "yuv420p", str(dest)], check=True)
        # A compact time-ordered sheet samples the recording, not source artwork.
        subprocess.run([str(args.ffmpeg), "-hide_banner", "-loglevel", "error", "-y",
            "-i", str(dest), "-vf", f"fps={8/max(.01,(b-a)/60)},scale=640:-1,tile=4x2",
            "-frames:v", "1", str(clips/(name+".jpg"))], check=True)
        segments.append(dict(role=key[0], cue=key[1], startFrame=a, endFrame=b,
            fps=60, file=dest.name, sha256=hashlib.sha256(dest.read_bytes()).hexdigest(),
            evidence="actual isolated host viewport; fixed simulation delta; no FMOD audio"))
        print(name, f"{(b-a)/60:.3f}s", flush=True)
if pending:
    raise ValueError(f"Unfinished motion clips: {list(pending)}")
(clips / "index.json").write_text(json.dumps(segments, indent=2), encoding="utf-8")
