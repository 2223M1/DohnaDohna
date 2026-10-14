"""Index/cut real isolated Godot MovieWriter capture; never render a substitute.

MovieWriter uses fixed simulation delta. Clips have no claimed FMOD sound or
wall-clock performance evidence. Keep the original AVI and frame markers.
"""
import argparse
import hashlib
import html
import json
from pathlib import Path
import subprocess

parser = argparse.ArgumentParser()
parser.add_argument("recording", type=Path)
parser.add_argument("--ffmpeg", required=True, type=Path)
parser.add_argument("--cues", help="Comma-separated cue names; raw recording always retained")
args = parser.parse_args()
root = args.recording.resolve()
movie = root / "rendered.avi"
log = (root / "stdout.log").read_text(encoding="utf-8-sig")
realtime = root / "localappdata/audio/recording-start.json"
clock = json.loads(realtime.read_text()) if realtime.is_file() else None
if clock:
    movie = realtime.parent / "video.mp4"
    if not (realtime.parent / "video-proof.json").is_file():
        raise ValueError("Incomplete realtime recording")
elif not movie.is_file() or "Done recording movie at path:" not in log:
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
        if args.cues and key[1] not in args.cues.split(','):
            continue
        if clock:
            a = round((start["qpc"] - clock["timestamp"]) / clock["frequency"] * 60)
            b = round((mark["qpc"] - clock["timestamp"]) / clock["frequency"] * 60)
        else:
            a, b = start["drawnFrame"], mark["drawnFrame"]
        if b <= a:
            if b == a and key[1].endswith("-Instant"):
                segments.append(dict(role=key[0], cue=key[1], startFrame=a, endFrame=b,
                    fps=60, file=None, evidence="Instant native gate: no rendered frames expected"))
                continue
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
            evidence="actual isolated host viewport; " + ("QPC wall-clock" if clock else "fixed simulation delta") + "; silent clip, raw audio stored separately"))
        print(name, f"{(b-a)/60:.3f}s", flush=True)
if pending:
    raise ValueError(f"Unfinished motion clips: {list(pending)}")
(clips / "index.json").write_text(json.dumps(segments, indent=2), encoding="utf-8")
items = "\n".join(
    f'<li><a href="{html.escape(s["file"], quote=True)}">{html.escape(s["role"] + " / " + s["cue"])}</a>'
    f' · {(s["endFrame"] - s["startFrame"]) / s["fps"]:.2f}s</li>'
    for s in segments if s["file"])
(clips / "index.html").write_text(
    '<!doctype html><meta charset="utf-8"><title>DohnaDohna 动作实录</title>'
    '<style>body{max-width:960px;margin:40px auto;background:#18202a;color:#eef2f6;'
    'font:16px/1.8 system-ui}a{color:#8ec9ff}ul{columns:2}</style>'
    '<h1>DohnaDohna 动作实录</h1><p>实际隔离宿主录像的逐项片段，不是素材拼接。'
    '这些切片为静音；原始同进程录音另存，有声四人剧场请使用交付链接。</p>'
    f'<ul>{items}</ul><p><a href="index.json">帧范围与SHA256</a></p>', encoding="utf-8")
