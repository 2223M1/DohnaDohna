"""Contact sheet of existing isolated-game frames; never synthesizes game proof."""
import argparse
from pathlib import Path
from PIL import Image, ImageDraw

parser = argparse.ArgumentParser()
parser.add_argument("directory", type=Path)
parser.add_argument("pattern")
parser.add_argument("output", type=Path)
args = parser.parse_args()
files = sorted(args.directory.glob(args.pattern))
if not files:
    raise ValueError("No captured frames match")
width, height, columns = 640, 360, 2
sheet = Image.new("RGB", (width * columns, (height + 28) * ((len(files) + columns - 1) // columns)), "#202020")
draw = ImageDraw.Draw(sheet)
for index, path in enumerate(files):
    frame = Image.open(path).convert("RGB")
    frame.thumbnail((width, height))
    x, y = index % columns * width, index // columns * (height + 28)
    sheet.paste(frame, (x, y))
    draw.text((x + 8, y + height + 4), path.name, fill="white")
sheet.save(args.output)
print(args.output.resolve())
