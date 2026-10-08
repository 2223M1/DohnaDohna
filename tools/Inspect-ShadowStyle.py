"""Local visual evidence only. Extract native atlas shadows; never package them."""
import json
from pathlib import Path
from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[1]
NATIVE = ROOT.parent / "NinjaSlayer/Slay the Spire 2/Slay the Spire 2 v0.111.0/animations"
items = []
for folder, name in (("characters/ironclad", "ironclad"), ("monsters/leaf_slime_s", "leaf_slime_s")):
    directory = NATIVE / folder
    lines = (directory / (name + ".atlas")).read_text().splitlines()
    index = lines.index("shadow")
    bounds = list(map(int, lines[index + 1].split(":")[1].split(",")))
    page = next(line for line in lines[:index] if line.endswith(".png"))
    x, y, w, h = bounds
    rotated = any(line.strip() == "rotate:90" for line in lines[index+1:index+4])
    image = Image.open(directory / page).convert("RGBA")
    image = image.crop((x, y, x + (h if rotated else w), y + (w if rotated else h)))
    if rotated:
        image = image.transpose(Image.Transpose.ROTATE_90)
    items.append((name + " / native", image))
items.append(("NinjaSlayer / existing", Image.open(ROOT.parent / "NinjaSlayer/NinjaSlayer/NinjaSlayer/images/shadows/ninja_slayer_shadow.png")))
output = Image.new("RGB", (800, len(items)*180), "#aaa493")
draw = ImageDraw.Draw(output)
for i, (name, image) in enumerate(items):
    image = image.convert("RGBA")
    print(name, image.size, "alpha", image.getchannel("A").getextrema(), "center", image.getpixel((image.width//2, image.height//2)))
    draw.text((16, i*180+12), name, fill="black")
    image.thumbnail((650, 110))
    output.paste(image, ((800-image.width)//2, i*180+50), image)
output.save(ROOT / "build/native-shadow-style.png")
