"""Authoring contact sheets from imported art, NOT host screenshots or test evidence."""
import argparse
import json
from pathlib import Path
from PIL import Image, ImageDraw
from motion_images import compose

parser = argparse.ArgumentParser()
parser.add_argument("roles", nargs="*")
args = parser.parse_args()
root = Path(__file__).resolve().parents[1]
out = root / "build/pacing-poses"
out.mkdir(parents=True, exist_ok=True)
for folder in sorted((root / "DohnaDohna/roles").iterdir()):
    if args.roles and folder.name not in args.roles:
        continue
    data = json.loads((folder / "motions.json").read_text(encoding="utf-8"))
    for cue in ("strike", "special", "hit"):
        motion = data["motions"][cue]
        frames = motion["frames"]
        sheet = Image.new("RGB", (8*180, ((len(frames)+7)//8)*160), "#343740")
        draw = ImageDraw.Draw(sheet)
        for i, frame in enumerate(frames):
            layers = [l for l in frame.get("CgLayers", {}).values() if not l["impact"]]
            art, origin = compose(root, layers)
            x, y = i % 8 * 180, i // 8 * 160
            if art is not None:
                art.thumbnail((176, 128))
                sheet.paste(art, (x+(180-art.width)//2, y+24+(130-art.height)//2), art)
            draw.text((x+3, y+2), f'{i} {frame["Frame"]}f X{frame["actionAnchorX"]:.0f}'
                      + (" HIT" if i in motion["positiveHits"] else ""), fill="#ffdb65")
        sheet.save(out / f"{folder.name}-{cue}.jpg")
        print(folder.name, cue, len(frames), motion["positiveHits"])
