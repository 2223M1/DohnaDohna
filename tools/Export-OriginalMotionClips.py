"""Encode native-engine screenshots with their actual system-clock timestamps.

The private fixture invokes the original visual controller, not complete combat
input/damage control. Screenshot I/O is intrusive; never claim a 60 fps capture
or FMOD/audio verification from these samples.
"""
import argparse
import hashlib
import json
import math
from pathlib import Path
import subprocess
from PIL import Image, ImageDraw

parser = argparse.ArgumentParser()
parser.add_argument("recording", type=Path)
parser.add_argument("--ffmpeg", required=True, type=Path)
args = parser.parse_args()
root = args.recording.resolve()
clips = root / "clips"
clips.mkdir(exist_ok=True)
repo = Path(__file__).resolve().parents[1]
records = []
for receipt_path in sorted(root.glob("*-receipt.json")):
    receipt = json.loads(receipt_path.read_text(encoding="utf-8"))
    role = receipt["role"]
    data = json.loads((repo / f"DohnaDohna/roles/{role}/motions.json").read_text(encoding="utf-8"))
    for cue, source in receipt["sourceMotions"].items():
        files = []
        for path in (root / "frames").glob(f"{role}-{cue}-*.bmp"):
            if path.stat().st_mtime_ns < receipt_path.stat().st_mtime_ns:
                continue  # Do not mix an earlier fixture run with this receipt.
            *_, index, timestamp = path.stem.split("-")
            files.append((int(index), int(timestamp), path))
        files.sort()
        duration = max(2, sum(max(0, f["Frame"]) for f in data["motions"][cue]["frames"])/60 + 1.5)
        expected = math.ceil(duration*60)
        if [i for i, _, _ in files] != list(range(expected)):
            raise ValueError(f"Incomplete/duplicate native recording: {role}/{cue}: {len(files)} != {expected}")
        gaps = [(b[1]-a[1])/1000 for a,b in zip(files, files[1:])]
        if any(t <= 0 for t in gaps):
            raise ValueError(f"Non-monotonic native timestamps: {role}/{cue}")
        name = f"{role}-{cue}"
        concat = clips / (name+".ffconcat")
        lines = ["ffconcat version 1.0"]
        for (_, _, path), gap in zip(files, gaps + [gaps[-1]]):
            lines += [f"file '{path.as_posix()}'", "option framerate 1000", f"duration {gap:.3f}"]
        lines += [f"file '{files[-1][2].as_posix()}'", "option framerate 1000"]
        concat.write_text("\n".join(lines)+"\n", encoding="utf-8")
        dest = clips / (name+".mp4")
        subprocess.run([str(args.ffmpeg), "-hide_banner", "-loglevel", "error", "-y",
            "-safe", "0", "-i", str(concat), "-an", "-fps_mode", "vfr", "-c:v", "libx264",
            "-crf", "18", "-preset", "fast", "-pix_fmt", "yuv420p", str(dest)], check=True)
        # Sample the authored action, not the extra idle tail of the fixture.
        authored = sum(max(0,f["Frame"]) for f in data["motions"][cue]["frames"])/60
        sheet = Image.new("RGB", (1920, 2*294), "#222222")
        draw = ImageDraw.Draw(sheet)
        for k in range(8):
            wanted = files[0][1] + authored*1000*k/8
            _, time, path = min(files, key=lambda x: abs(x[1]-wanted))
            with Image.open(path) as picture:
                picture = picture.convert("RGB").resize((480,270))
            x, y = (k%4)*480, (k//4)*294
            sheet.paste(picture, (x,y+24))
            draw.text((x+4,y+4), f"{name} +{(time-files[0][1])/1000:.3f}s", fill="white")
        sheet.save(clips/(name+".jpg"), quality=94)
        records.append(dict(role=role, cue=cue, sourceMotion=source, file=dest.name,
            sha256=hashlib.sha256(dest.read_bytes()).hexdigest(), frames=len(files),
            firstNativeMs=files[0][1], lastNativeMs=files[-1][1], maxGapMs=round(max(gaps)*1000),
            fixtureAinSha256=receipt["fixtureAinSha256"], evidence=receipt["scope"],
            timestamps="native system.GetTime; VFR; screenshot I/O may skip short poses; no audio"))
        print(name, len(files), flush=True)
(clips/"index.json").write_text(json.dumps(records,ensure_ascii=False,indent=2),encoding="utf-8")
