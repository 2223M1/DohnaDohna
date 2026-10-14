"""Deterministic, local-only preparation of the selected original emotion stickers.

Commercial source and generated artwork stay outside the public source tree.
No model, inpainting, image generation, or runtime shader is involved.
"""
from __future__ import annotations

import argparse
import csv
import hashlib
import json
import re
import shutil
import zipfile
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFont
from scipy import ndimage as ndi

ROOT = Path(__file__).resolve().parents[1]
ROLES = {
    "kuma": "阿熊", "alyce": "爱丽丝", "antena": "安缇娜", "tora": "虎太郎",
    "kikuchiyo": "菊千代", "medhico": "梅蒂可", "joker": "小丑", "zappa": "扎帕",
    "kirakira": "绮菈绮菈", "porno": "珀尔诺",
}
REFERENCES = ["anchor", "bag_of_preparation", "burning_blood", "oddly_smooth_stone", "meal_ticket"]
RUNTIME_NAMES = ["relic_icon.png", "relic_icon_plus.png", "relic_outline.png",
                 "relic_big.png", "relic_big_plus.png", "relic_refinement.png"]
# White background pockets enclosed by ink, audited against the original at 3x.
# These are empty spaces between legs, arm/tool, hair/slippers, bowl/ponytail,
# and the raised arm/hair. Eye whites, clothes, highlights and hood lining stay.
EMPTY_POCKETS = {
    "kuma": [(180, 280)], "alyce": [(117, 227)],
    "kikuchiyo": [(244, 270)], "medhico": [(253, 163), (255, 193)],
    "zappa": [(123, 238)], "kirakira": [(116, 152)],
}
STYLE = {"big_size": 256, "small_size": 85, "master_size": 360,
         "body_extent_at_256": 224, "outline_radius_at_256": 10.5,
         "small_outline_radius": 3.0,
         "outline_alpha": 128, "supersampling": 8,
         "gold_mix": 0.88, "white_threshold": 246}


def sha(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def matte_sticker(image: Image.Image, role: str):
    """Remove exterior sticker white, protecting whites enclosed by authored ink."""
    src = np.asarray(image.convert("RGBA"))
    rgb = src[..., :3].astype(np.float64)
    alpha = src[..., 3]
    white = (rgb.min(2) >= 246) & (np.ptp(rgb, axis=2) <= 12)
    passable = white | (alpha < 250)
    seed = np.zeros(passable.shape, bool)
    seed[0, :] = seed[-1, :] = True
    seed[:, 0] = seed[:, -1] = True
    for x, y in EMPTY_POCKETS.get(role, []):
        if not white[y, x]:
            raise ValueError(f"Audited empty-pocket seed changed: {role}/{x}/{y}")
        seed[y, x] = True
    exterior = ndi.binary_propagation(seed, mask=passable)
    keep = ~exterior
    # At a flattened white/ink boundary, recover coverage against the known
    # white matte. Only the narrow boundary may change; interior RGB is exact.
    distance = ndi.distance_transform_edt(keep)
    outside_distance = ndi.distance_transform_edt(~keep)
    edge = (distance <= 1.5) & keep
    edge |= exterior & (outside_distance <= 1.1) & (alpha >= 250)
    foreground = np.stack([ndi.minimum_filter(rgb[..., c], size=3) for c in range(3)], axis=-1)
    denominator = 255.0 - foreground
    coverage = np.clip(np.sum((255.0-rgb)*denominator, axis=2)
                       / np.maximum(np.sum(denominator**2, axis=2), 1), 0, 1)
    matte = keep.astype(np.float64)
    matte[edge] = coverage[edge]
    recovered = np.clip((rgb - (1-coverage[..., None])*255)
                        / np.maximum(coverage[..., None], 1/255), 0, 255)
    cleaned = rgb.copy()
    cleaned[edge] = recovered[edge]
    cleaned[matte == 0] = 0
    result = np.dstack([np.rint(cleaned), np.rint(matte*255)]).astype(np.uint8)
    core = distance > 1.5
    if not np.array_equal(result[core, :3], src[core, :3]):
        raise ValueError(f"Original interior pigment changed: {role}")
    return Image.fromarray(result), {
        "keep": Image.fromarray(keep.astype(np.uint8)*255),
        "alpha": Image.fromarray(result[..., 3]),
        "removed": Image.fromarray(exterior.astype(np.uint8)*255),
    }


def on_background(image, color):
    bg = Image.new("RGBA", image.size, color)
    bg.alpha_composite(image)
    return bg.convert("RGB")


def font(size):
    return ImageFont.truetype("C:/Windows/Fonts/msyh.ttc", size)


def gild(image):
    """Gold gradient mapping with restrained metal reflections, alpha unchanged."""
    a = np.asarray(image).copy()
    rgb = a[..., :3].astype(np.float64)/255
    # Luminance preserves the authored shading and line hierarchy. Specular
    # bands are static and applied to pigment only, never to the outline.
    light = rgb @ np.array([0.2126, 0.7152, 0.0722])
    stops = np.array([0, .12, .30, .50, .70, .88, 1.0])
    colors = np.array([[13, 10, 7], [62, 37, 12], [133, 82, 17],
                       [192, 133, 30], [237, 185, 58], [255, 222, 124], [255, 247, 204]])/255
    gold = np.stack([np.interp(light, stops, colors[:, c]) for c in range(3)], axis=-1)
    y, x = np.mgrid[:a.shape[0], :a.shape[1]]
    box = image.getbbox()
    diagonal = ((x-box[0]) + .62*(y-box[1])) / max(box[2]-box[0], box[3]-box[1])
    reflection = .13*np.exp(-((diagonal-.55)/.075)**2)
    reflection += .055*np.exp(-((diagonal-1.18)/.15)**2)
    reflection *= np.clip((light-.12)/.6, 0, 1)
    gold += reflection[..., None] * (1-gold)
    output = STYLE["gold_mix"]*gold + (1-STYLE["gold_mix"])*rgb
    a[..., :3] = np.rint(np.clip(output, 0, 1)*255).astype(np.uint8)
    a[a[..., 3] == 0, :3] = 0
    return Image.fromarray(a)


def fit(image, size, box):
    """Use the same transform for both versions; filter premultiplied colors."""
    crop = image.crop(box)
    factor = (STYLE["body_extent_at_256"]*size/256) / max(crop.size)
    dimensions = tuple(max(1, round(v*factor)) for v in crop.size)
    scaled = crop.convert("RGBa").resize(dimensions, Image.Resampling.LANCZOS).convert("RGBA")
    canvas = Image.new("RGBA", (size, size))
    canvas.paste(scaled, ((size-dimensions[0])//2, (size-dimensions[1])//2))
    return canvas


def contour(image):
    size = image.width
    ss = STYLE["supersampling"]
    alpha = image.getchannel("A").resize((size*ss, size*ss), Image.Resampling.BICUBIC)
    inside = np.asarray(alpha) >= 128
    distance = ndi.distance_transform_edt(~inside)
    radius = STYLE["small_outline_radius"] if size == 85 else STYLE["outline_radius_at_256"]*size/256
    expanded = distance <= radius*ss
    mask = Image.fromarray(expanded.astype(np.uint8)*255).resize((size, size), Image.Resampling.LANCZOS)
    # The native small-outline asset is white; NRelic tints it black at 128/255.
    outline = Image.new("RGBA", image.size, "white")
    outline.putalpha(mask)
    return outline


def composite_outline(image, mask):
    layer = Image.new("RGBA", image.size)
    layer.putalpha(mask.getchannel("A").point(lambda a: round(a*STYLE["outline_alpha"]/255)))
    layer.alpha_composite(image)
    return layer


def checker(size):
    y, x = np.mgrid[:size[1], :size[0]]
    pattern = ((x//12 + y//12) % 2)*24+162
    return Image.fromarray(np.repeat(pattern[..., None], 3, axis=2).astype(np.uint8)).convert("RGBA")


def native_atlas(vanilla, name, which):
    path = vanilla / "images/atlases" / (which+".sprites") / (name+".tres")
    t = path.read_text(encoding="utf-8")
    rect = [int(x) for x in re.search(r"region = Rect2\((.*?)\)", t)[1].split(",")]
    margin = [int(x) for x in re.search(r"margin = Rect2\((.*?)\)", t)[1].split(",")]
    atlas = Image.open(vanilla / "images/atlases" / (which+".png")).convert("RGBA")
    im = Image.new("RGBA", (rect[2]+margin[2], rect[3]+margin[3]))
    im.paste(atlas.crop((rect[0], rect[1], rect[0]+rect[2], rect[1]+rect[3])), (margin[0], margin[1]))
    return im


def reference_evidence(vanilla):
    evidence = []
    for name in REFERENCES:
        path = vanilla / "images/relics" / (name+".png")
        large = np.asarray(Image.open(path).convert("RGBA"))
        visible = large[..., 3] > 64
        body = large[..., 3] > 192
        edge = visible & ~ndi.binary_erosion(visible)
        widths = ndi.distance_transform_edt(~body)[edge & ~body]
        dark = large[(large[..., :3].max(2) < 16) & (large[..., 3] > 96) & (large[..., 3] < 160)]
        rgba, counts = np.unique(dark, axis=0, return_counts=True)
        dominant = rgba[counts.argmax()].tolist()
        small = native_atlas(vanilla, name, "relic_atlas")
        mask = native_atlas(vanilla, name, "relic_outline_atlas")
        predicted = np.asarray(contour(small))[..., 3]
        expected = np.asarray(mask)[..., 3]
        iou = np.sum((predicted >= 128) & (expected >= 128))/np.sum((predicted >= 128) | (expected >= 128))
        evidence.append({"name": name, "source": str(path), "sha256": sha(path),
                         "big_edge_to_body_q10_median_q90": np.quantile(widths, [.1, .5, .9]).tolist(),
                         "dominant_outline_rgba": dominant,
                         "small_contour_iou": float(iou),
                         "small_contour_mean_alpha_error": float(np.mean(np.abs(predicted.astype(float)-expected))/255)})
    return evidence


def save_image(image, path, receipt):
    path.parent.mkdir(parents=True, exist_ok=True)
    image.save(path, optimize=True)
    receipt.append({"path": str(path.relative_to(ROOT)), "size": list(image.size), "sha256": sha(path)})


def review_sheets(output, originals, finished, vanilla):
    # Large, lossless sheets stay local. The overview is concise enough for delivery.
    overview = Image.new("RGB", (5*410, 2*270), "#22282e")
    d = ImageDraw.Draw(overview)
    for i, (role, name) in enumerate(ROLES.items()):
        x, y = (i%5)*410, (i//5)*270
        for j, version in enumerate(["normal", "gold"]):
            im = finished[role][256][version].resize((200, 200), Image.Resampling.LANCZOS)
            overview.paste(on_background(im, "#22282e"), (x+j*205, y+32))
        d.text((x+12, y+5), name + "  普通 / 强化", fill="#efdec0", font=font(18))
        for j, version in enumerate(["normal", "gold"]):
            im = finished[role][85][version].resize((60, 60), Image.Resampling.LANCZOS)
            overview.paste(on_background(im, "#22282e"), (x+140+j*72, y+204))
    overview.save(output / "普通与金色强化总览.png")
    for background in ["dark", "light", "checker"]:
        sheet = Image.new("RGB", (4*360, len(ROLES)*390), "#373a3e")
        d = ImageDraw.Draw(sheet)
        for row, (role, name) in enumerate(ROLES.items()):
            sample = Image.open(vanilla / "images/relics" / (REFERENCES[row%len(REFERENCES)]+".png")).convert("RGBA")
            sample = sample.resize((360, 360), Image.Resampling.LANCZOS)
            items = [originals[role], finished[role][360]["normal"], finished[role][360]["gold"], sample]
            for col, im in enumerate(items):
                bg = checker(im.size) if background == "checker" else Image.new("RGBA", im.size, "#dedbd5" if background == "light" else "#22282e")
                bg.alpha_composite(im)
                sheet.paste(bg.convert("RGB"), (col*360, row*390))
            d.text((8, row*390+362), name + "  原图 | 普通 | 金色强化 | 原版参照", fill="white", font=font(17))
        sheet.save(output / ("review-"+background+".png"))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--original", type=Path)
    parser.add_argument("--vanilla", type=Path)
    parser.add_argument("--output", type=Path, default=ROOT / "build/relic-art")
    parser.add_argument("--review-mattes", action="store_true")
    parser.add_argument("--install-assets", action="store_true", help="Write selected generated resources into ignored runtime role folders; does not install the Mod.")
    args = parser.parse_args()
    args.output = args.output.resolve()
    if not args.output.is_relative_to((ROOT / "build").resolve()) or args.output == (ROOT / "build").resolve():
        raise ValueError("Private output must be a dedicated directory under the project's build directory.")
    original = args.original or Path(json.loads((ROOT / ".local/original-reference.json").read_text(encoding="utf-8-sig"))["referenceDirectory"])
    folder = original / "assets/images/current/界面与图标/情感图标"
    with (original / "assets/manifest.csv").open(encoding="utf-8-sig", newline="") as handle:
        source_rows = {row["original_name"]: row for row in csv.DictReader(handle)
                       if row["kind"] == "image" and row["source"] == "dohnadohnaCG.afa"
                       and row["original_name"].startswith("情感图标／")}
    args.output.mkdir(parents=True, exist_ok=True)
    vanilla = args.vanilla or ROOT.parent / "NinjaSlayer/Slay the Spire 2/Slay the Spire 2 v0.111.0"
    receipts, inputs, originals, finished = [], [], {}, {}
    sheet = Image.new("RGB", (3*360, len(ROLES)*390), "#373a3e")
    draw = ImageDraw.Draw(sheet)
    for row, (role, name) in enumerate(ROLES.items()):
        source = Image.open(folder / (name + ".png")).convert("RGBA")
        if source.size != (360, 360):
            raise ValueError(f"Unexpected original dimensions for {name}: {source.size}")
        source_row = source_rows["情感图标／"+name+".pcf"]
        digest = sha(folder / (name+".png"))
        if digest != source_row["sha256"]:
            raise ValueError(f"Selected original differs from its source manifest: {name}")
        inputs.append({"role": role, "name": name, "path": str(folder / (name+".png")), "sha256": digest,
                       "archive": source_row["source"], "entry": source_row["index"], "original_name": source_row["original_name"]})
        originals[role] = source
        clean, masks = matte_sticker(source, role)
        work = args.output / "work" / role
        work.mkdir(parents=True, exist_ok=True)
        clean.save(work / "foreground.png")
        for key, mask in masks.items():
            mask.save(work / (key + ".png"))
        if not args.review_mattes:
            gold = gild(clean)
            box = clean.getbbox()
            finished[role] = {}
            asset_folder = args.output / "runtime" / role
            for size in [85, 256, 360]:
                normal_body, gold_body = fit(clean, size, box), fit(gold, size, box)
                if not np.array_equal(np.asarray(normal_body)[..., 3], np.asarray(gold_body)[..., 3]):
                    raise ValueError(f"Alpha changed during gold treatment: {role}/{size}")
                mask = contour(normal_body)
                normal, enhanced = composite_outline(normal_body, mask), composite_outline(gold_body, mask)
                finished[role][size] = {"normal": normal, "gold": enhanced}
                if not np.array_equal(np.asarray(normal)[..., 3], np.asarray(enhanced)[..., 3]):
                    raise ValueError(f"Alpha changed during outline composition: {role}/{size}")
                outer = np.asarray(normal_body)[..., 3] == 0
                if not np.array_equal(np.asarray(normal)[outer], np.asarray(enhanced)[outer]):
                    raise ValueError(f"Gold contaminated external outline: {role}/{size}")
                border = np.asarray(normal.getchannel("A"))
                if max(border[0].max(), border[-1].max(), border[:, 0].max(), border[:, -1].max()) > 2:
                    raise ValueError(f"Clipped silhouette: {role}/{size}")
                if size == 85:
                    for filename, im in [("relic_icon.png", normal_body), ("relic_icon_plus.png", gold_body), ("relic_outline.png", mask)]:
                        save_image(im, asset_folder / filename, receipts)
                elif size == 256:
                    save_image(normal, asset_folder / "relic_big.png", receipts)
                    save_image(enhanced, asset_folder / "relic_big_plus.png", receipts)
                else:
                    save_image(normal, args.output / "masters" / (name+".png"), receipts)
                    save_image(enhanced, args.output / "masters" / (name+"_强化.png"), receipts)
            card = Image.new("RGBA", (500, 380), "#253044")
            card.alpha_composite(finished[role][360]["gold"], (70, 10))
            save_image(card, asset_folder / "relic_refinement.png", receipts)
            if args.install_assets:
                target = ROOT / "DohnaDohna/roles" / role
                target.mkdir(parents=True, exist_ok=True)
                for filename in RUNTIME_NAMES:
                    file = asset_folder / filename
                    shutil.copy2(file, target / file.name)
        for col, im in enumerate([on_background(source, "#373a3e"), on_background(clean, "#373a3e"), on_background(clean, "#dddddd")]):
            sheet.paste(im, (col*360, row*390))
        draw.text((10, row*390+360), name + " / source | clean dark | clean light", fill="white", font=font(17))
    sheet.save(args.output / "matte-review.png")
    if args.review_mattes:
        print(args.output / "matte-review.png")
        return
    reference = reference_evidence(vanilla)
    if min(r["small_contour_iou"] for r in reference) < .96:
        raise ValueError("Contour kernel no longer matches audited native references.")
    review_sheets(args.output, originals, finished, vanilla)
    receipt = {"generator": "tools/Prepare-RelicArt.py", "generator_sha256": sha(Path(__file__)),
               "native_host": "0.111.0", "style": STYLE, "empty_pockets": EMPTY_POCKETS,
               "sources": inputs, "references": reference, "outputs": receipts,
               "checks": {"twenty_variants": True, "shared_alpha": True, "unchanged_outline": True,
                          "normal_interior_rgb_preserved": True, "inputs_match_manifest": True, "no_clipping": True},
               "runtime_assets_written": args.install_assets}
    (args.output / "receipt.json").write_text(json.dumps(receipt, ensure_ascii=False, indent=2)+"\n", encoding="utf-8")
    readme = """# DohnaDohna 情感图标遗物素材

10 名角色 × 普通／金色强化。两版的构图、尺寸、Alpha 和外描边一致。

- masters/：360×360 透明成品母版，可直接使用。
- runtime/<role>/：relic_big 与 relic_big_plus 是 256×256 带描边大图；
  relic_icon 与 relic_icon_plus 是 85×85 无外描边主体；relic_outline 是两版共用的白色轮廓蒙版。
  小图由 STS2 NRelic 将轮廓染黑并以 128/255 透明度叠在主体背后，不要重复添加描边。
  relic_refinement 是 500×380 金色强化选择卡预览。
- work/<role>/：原尺寸的干净主体、保留区域、最终 Alpha、删除区域，供精修和复核。
- 普通与金色强化总览.png：包含正常显示大小的并排预览。
- receipt.json：原作文件和原版参照的来源、SHA256、处理参数、输出校验与检查结果。

原始输入保留在 receipt.json 指向的 DohnaDohnaOriginal 资料目录，未覆盖。
使用项目 tools/Prepare-RelicArt.py 可重新生成；详见 Docs/authoring/relic-art.md。
图片采用直通 Alpha；所有缩放先转预乘 Alpha 后采样，再转换回直通 Alpha。
外描边采用原版半透明黑色轮廓，没有另加颗粒噪声或运行时滤镜。
人物与原画来源 Alicesoft；描边参照来源 Mega Crit。素材与蒙版属于本地制作输出，未提交公开源码仓库。
"""
    (args.output / "README.md").write_text(readme, encoding="utf-8")
    archive_path = args.output / "DohnaDohna-遗物素材-普通与金色强化.zip"
    bundle = [p for folder in ("masters", "runtime", "work") for p in (args.output / folder).rglob("*.png")]
    bundle += [args.output / f for f in ("README.md", "receipt.json", "普通与金色强化总览.png")]
    with zipfile.ZipFile(archive_path, "w", compression=zipfile.ZIP_DEFLATED, compresslevel=9) as archive:
        for path in sorted(bundle):
            info = zipfile.ZipInfo(path.relative_to(args.output).as_posix(), (2026, 10, 9, 0, 0, 0))
            info.compress_type = zipfile.ZIP_DEFLATED
            archive.writestr(info, path.read_bytes())
    (args.output / "SHA256SUMS").write_text(sha(archive_path)+"  "+archive_path.name+"\n", encoding="utf-8")
    print(json.dumps({"roles": len(ROLES), "outputs": len(receipts), "native_contour_iou_min": min(r["small_contour_iou"] for r in reference), "output": str(args.output)}, ensure_ascii=False))


if __name__ == "__main__":
    main()
