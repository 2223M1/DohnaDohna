"""Generate source-derived placeholder artwork only; authored text lives in Sync-CatalogText.mjs."""
import importlib.util
import argparse
import json
from pathlib import Path
from PIL import Image
from motion_images import compose

ROOT = Path(__file__).resolve().parents[1]
spec = importlib.util.spec_from_file_location("role_import", ROOT / "tools/Import-OriginalRoles.py")
source = importlib.util.module_from_spec(spec)
spec.loader.exec_module(source)
roles = source.roles()
parser = argparse.ArgumentParser()
parser.add_argument("--assets-only", action="store_true", help="Do not overwrite authored localization or card metadata.")
args = parser.parse_args()
figures = {}
for role in roles:
    directory = ROOT / "DohnaDohna/roles" / role[0]
    data = json.loads((directory / "motions.json").read_text(encoding="utf-8"))
    frame = data["motions"]["idle"]["frames"][0]
    figure, _ = compose(ROOT, frame.get("CgLayers", {}).values())
    if figure is None:
        raise ValueError(f"No visible card portrait for {role[0]}")
    figures[role[0]] = figure.copy()
    # Original combat sprites are often smaller than the card canvas. Pillow's
    # thumbnail never enlarges them, leaving an unreadably tiny figure in-game.
    scale = min(460 / figure.width, 350 / figure.height)
    figure = figure.resize((round(figure.width * scale), round(figure.height * scale)), Image.Resampling.LANCZOS)
    artwork = Image.new("RGBA", (500, 380), "#253044")
    artwork.alpha_composite(figure, ((500 - figure.width) // 2, 365 - figure.height))
    artwork.save(directory / "card.png")
images = ROOT / "DohnaDohna/images/cards"
images.mkdir(parents=True, exist_ok=True)
shutil_image = Image.open(ROOT / "DohnaDohna/roles/kuma/card.png")
shutil_image.save(images / "squad.png")
icons = ROOT / "DohnaDohna/images/icons"
icons.mkdir(parents=True, exist_ok=True)
icon = Image.new("RGBA", (160, 160))
for i, role in enumerate(roles[:4]):
    portrait = figures[role[0]].copy()
    portrait.thumbnail((65, 145))
    icon.alpha_composite(portrait, (i * 31, 160 - portrait.height))
icon.save(icons / "squad.png")
print("Generated source-derived runtime artwork; authored content was not modified.")
