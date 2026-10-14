"""Crop checked original battle-outfit portraits for the selection screen.

The source's alpha, pigment and resolution are preserved. Only transparent
padding and the lower body are removed; no AI artwork or enlarged combat frames.
Commercial input/output stay local, with a receipt under build/select-art.
"""
import csv
import hashlib
import json
from pathlib import Path

from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[1]
ROLES = {
    "kuma": ("阿熊", "打斗", .57), "alyce": ("爱丽丝", "打斗", .57),
    "antena": ("安缇娜", "打斗", .60), "tora": ("虎太郎", "打斗", .57),
    "kikuchiyo": ("菊千代", "战Ｂ", .57), "medhico": ("梅蒂可", "打斗", .59),
    "joker": ("小丑", "打斗", .57), "zappa": ("扎帕", "打斗", .59),
    "kirakira": ("绮菈绮菈", "打斗", .59), "porno": ("珀尔诺", "打斗", .59),
}


def main():
    original = Path(json.loads((ROOT / ".local/original-reference.json").read_text("utf-8-sig"))["referenceDirectory"])
    with (original / "assets/manifest.csv").open(encoding="utf-8-sig", newline="") as stream:
        sources = {Path(r["original_name"]).stem: r for r in csv.DictReader(stream)
                   if r["source"] == "dohnadohnaCG.afa" and r["kind"] == "image" and r["output"]}
    evidence = ROOT / "build/select-art"
    evidence.mkdir(parents=True, exist_ok=True)
    sheet = Image.new("RGB", (1500, 760), (22, 27, 41))
    records = []
    for index, (role, (name, costume, fraction)) in enumerate(ROLES.items()):
        key = f"立绘／{name}／{costume}／基本"
        row = sources[key]
        source = original / row["output"]
        digest = hashlib.sha256(source.read_bytes()).hexdigest()
        if digest != row["sha256"]:
            raise ValueError(f"Original source changed: {key}")
        image = Image.open(source).convert("RGBA")
        left, top, right, bottom = image.getbbox()
        # ALyCE's rifle extends far above her head; crop that excess barrel,
        # not her face. Then discard padding left by lower-body equipment.
        crop = (left, top + (round((bottom - top) * .16) if role == "alyce" else 0),
                right, round(top + (bottom - top) * fraction))
        upper = image.crop(crop)
        inner = upper.getbbox()
        crop = (crop[0] + inner[0], crop[1] + inner[1], crop[0] + inner[2], crop[1] + inner[3])
        bust = image.crop(crop)
        target = ROOT / "DohnaDohna/roles" / role / "selection_bust.png"
        bust.save(target)
        records.append({"role": role, "source": key, "sourceSha256": digest,
                        "sourceSize": image.size, "crop": crop, "size": bust.size,
                        "outputSha256": hashlib.sha256(target.read_bytes()).hexdigest()})
        thumb = bust.copy()
        thumb.thumbnail((290, 340), Image.Resampling.LANCZOS)
        cell_x, cell_y = (index % 5) * 300, (index // 5) * 380
        sheet.paste(thumb, (cell_x + (300 - thumb.width) // 2, cell_y + 25), thumb)
        ImageDraw.Draw(sheet).text((cell_x + 12, cell_y + 360), role, fill="white")
    sheet.save(evidence / "battle-busts.jpg")
    (evidence / "receipt.json").write_text(json.dumps(records, ensure_ascii=False, indent=2), encoding="utf-8")
    print(f"Prepared {len(records)} native-resolution battle busts; receipt: {evidence}")


if __name__ == "__main__":
    main()
