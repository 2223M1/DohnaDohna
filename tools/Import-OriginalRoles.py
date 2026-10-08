"""Import only approved combat/selection resources; commercial inputs remain external.

Generated runtime output is gitignored. This is a reproducible asset transform, not a
second gameplay implementation. A missing reference fails the import.
"""
import argparse
import csv
import hashlib
import json
import re
import shutil
from pathlib import Path
from PIL import Image
from motion_images import compose, contact, ground_shadow

ROOT = Path(__file__).resolve().parents[1]
# Audited spelling discrepancy in the current motion table. Both names are recorded
# in the receipt; the input table and reference archive are never edited.
CG_ALIASES = {"爱丽丝伤害／０７": "爱丽丝／伤害／０７"}

# Selected action-table art reviewed as contact/target reaction, not emission.
# Source names are resolved before hashing; positional binding is independent.
# In particular DeathExplosionBefore is Antena's muzzle charge, and ALyCE's
# gunshot is a travelling streak: neither is a target impact despite its name.
IMPACT_IMAGES = {
    "特效／ヒット", "特效／萨尔萨爆炸２", "特效／爆炸２", "特效／トラ超必杀炎",
    "特效／斬Ｌ", "特效／斬Ｊ", "特效／斬Ｋ", "特效／测试／血", "特效／测试／枪／地面",
    "特效／测试／煙", "特效／小丑攻击２火柱", "特效／壁", "特效／扎帕攻击３＿２",
    "特效／绮菈绮菈ボム爆炸", "特效／珀尔诺縛り", "特效／珀尔诺縛り２",
    "特效／珀尔诺縛り３", "特效／珀尔诺フキダシ", "特效／珀尔诺フキダシ２", "特效／トラ超必杀光",
}
# Authored contact cues include mixed windup/contact recordings. Ordinary
# attacks omit those entire clips (approved), never crop the finisher source.
IMPACT_SOUNDS = {
    "阿熊_攻击１_019", "阿熊_攻击１强_021", "爱丽丝_攻击１_009", "爱丽丝_攻击３强_137",
    "安缇娜_攻击２_040", "安缇娜_攻击２强_041", "虎虎太郎_攻击１_018", "虎虎太郎_攻击３_023",
    "菊千代_攻击１_004", "菊千代_攻击１强_003", "菊千代_攻击１强_015",
    "菊千代_攻击１_021", "菊千代_攻击１强_032", "梅蒂可_攻击２_019", "梅蒂可_攻击２强_019",
    "小丑_攻击１_013", "小丑_攻击２_032", "扎帕_攻击１_018", "扎帕_攻击３_023",
    "绮菈绮菈_攻击１_008", "绮菈绮菈_攻击２_034", "珀尔诺_攻击３_027",
    "珀尔诺_攻击３_036", "珀尔诺_攻击３_068", "珀尔诺_攻击４_039",
}


def classify_impacts(frames):
    for frame in frames:
        for group, key in (("CgLayers", "CgName"), ("Effects", "FirstCgName")):
            for layer in frame.get(group, {}).values():
                layer["impact"] = layer[key].rsplit("／", 1)[0] in IMPACT_IMAGES
        if sound := frame.get("Sound"):
            for n in range(1, 4):
                sound[f"impact{n}"] = sound.get(f"Sound{n}", "") in IMPACT_SOUNDS


def validate_motion(frames, label):
    """Reject unimplemented events at import, including non-Enable flag tracks."""
    events = {"Camera", "EnemyPosition", "EnemyMotion", "EnemyColor", "EnemyShake", "BgShake",
              "ScreenFlash", "LineEffect", "BgLighting", "Sound", "Voice", "Damage",
              "PreviousImages", "HideOtherPlayers", "HideTarget", "HideTargetParamView", "MoveFrame"}
    scalars = {"Frame", "OffsetX", "OffsetY", "ShadowOffsetX", "ShadowOffsetY", "actionAnchorX"}
    easings = {"Linear", "Jump", "EaseIn", "EaseOut", "EaseInOut"} | {
        direction + curve for direction in ("EaseIn", "EaseOut", "EaseInOut")
        for curve in ("Cubic", "Quad", "Exp", "Sine")}
    for index, frame in enumerate(frames):
        for key, value in frame.items():
            where = f"{label}/{index}/{key}"
            if key in scalars or key in ("CgLayers", "Effects", "shadows"):
                continue
            active = isinstance(value, dict) and any(value.get(flag) for flag in ("Enable", "Start", "End", "JumpStart", "JumpEnd"))
            if key not in events:
                if active or not isinstance(value, dict) and value:
                    raise ValueError(f"Unsupported active motion field {where}")
                continue
            if not active:
                continue
            if key == "MoveFrame" and set(value) - {"JumpStart", "JumpEnd"}:
                raise ValueError(f"Unsupported move marker {where}: {value}")
            for name, setting in value.items():
                if name.startswith("Easing") and setting not in easings:
                    raise ValueError(f"Unsupported easing {where}/{name}: {setting}")
            if key == "EnemyMotion" and (value["Mode"] not in ("Normal", "Fixed", "Range") or
                    value["MotionId"] not in ("伤害のけぞり", "伤害ダウン", "垂直ダウン", "通用固定伤害")):
                raise ValueError(f"Unaudited host-native hurt adaptation: {where}: {value}")
            if key == "EnemyShake" and value["Type"] not in ("Horizontal", "Vertical"):
                raise ValueError(f"Unsupported shake axis {where}: {value}")
        for group in ("CgLayers", "Effects"):
            for key, layer in frame.get(group, {}).items():
                if layer.get("DrawFilter") not in ("Normal", "Add", "Multiply"):
                    raise ValueError(f"Unsupported blend {label}/{index}/{group}/{key}")


def tree(path):
    stack = []
    result = {}
    for number, raw in enumerate(path.read_text(encoding="utf-8-sig").splitlines(), 1):
        line = raw.strip()
        if line == "};":
            break
        if not line:
            continue
        if line.startswith("tree "):
            stack.append(result)
        elif line == "},":
            stack.pop()
        elif match := re.fullmatch(r"(.+?) = \{", line):
            child = {}
            stack[-1][match[1]] = child
            stack.append(child)
        elif match := re.fullmatch(r"(.+?) = (.+),", line):
            value = match[2]
            try:
                if value.startswith("(list) {") and value.endswith("}"):
                    value = "[" + value[len("(list) {"):-1] + "]"
                stack[-1][match[1]] = json.loads(value)
            except json.JSONDecodeError as error:
                raise ValueError(f"Unsupported original value at {path}:{number}: {line}") from error
        else:
            raise ValueError(f"Unsupported original syntax at {path}:{number}: {line}")
    return result


def roles():
    text = (ROOT / "Content/RoleDefinition.cs").read_text(encoding="utf-8-sig")
    return re.findall(r'new\("([^"]+)", "([^"]+)", "([^"]+)", "([^"]+)",\s*"([^"]+)", "([^"]+)", "([^"]+)", "([^"]+)", "([^"]+)"\)', text)


def bind_action_anchors(frames, role, cue):
    """Host adaptation: AIN33555 shares body/effect generation position.

    Global equipment is a rigid formation composition; emitted effects capture
    its transform. Target groups below are impact art in the selected motions.
    """
    target_sequences = {"特效／ヒット", "特效／测试／血", "特效／小丑攻击２火柱",
                        "特效／珀尔诺縛り", "特效／珀尔诺縛り２", "特效／珀尔诺縛り３",
                        "特效／绮菈绮菈ボム爆炸", "特效／壁"}
    anchor = 0
    for frame in frames:
        local = [v for v in frame.get("CgLayers", {}).values()
                 if v.get("CgName") and v.get("IsShadow") and not v.get("IsGlobalPosition")]
        if local:
            # Porno's horse/equipment precedes her body in this original action.
            anchor = (local[-1] if role == "porno" and cue == "special" else local[0])["PosX"]
        frame["actionAnchorX"] = anchor
        for effect in frame.get("Effects", {}).values():
            sequence = effect["FirstCgName"].rsplit("／", 1)[0]
            effect["anchorBinding"] = ("target" if effect["AssignToTarget"] else
                                       "formation" if effect["IsGlobalPosition"] else
                                       "impact" if sequence in target_sequences else "actor")


def import_roles(original):
    with (original / "assets/manifest.csv").open(encoding="utf-8-sig", newline="") as file:
        rows = list(csv.DictReader(file))
    images = {Path(r["original_name"]).stem: r for r in rows
              if r["kind"] == "image" and r["source"] == "dohnadohnaCG.afa" and r["output"]}
    audio = {}
    for row in rows:
        if row["kind"] in ("sound", "voice") and row["output"]:
            audio[Path(row["original_name"]).stem] = row
            audio[Path(row["original_name"].split("／")[-1]).stem] = row
    motions_path = original / "reverse/data/current/33_モーション情報.x"
    motions = tree(motions_path)
    voices = tree(original / "reverse/data/current/11_ボイス情報.x")
    receipts = []
    audio_refs = {}

    def image_ref(cg):
        requested = cg
        cg = CG_ALIASES.get(cg, cg)
        if cg not in images:
            raise FileNotFoundError(f"Original image reference not in current source: {cg}")
        row = images[cg]
        source = original / row["output"].replace("\\", "/")
        digest = hashlib.sha256(source.read_bytes()).hexdigest()
        if digest != row["sha256"]:
            raise ValueError(f"Source checksum mismatch: {source}")
        target = ROOT / "DohnaDohna/images/original" / (digest[:20] + ".png")
        target.parent.mkdir(parents=True, exist_ok=True)
        if not target.exists():
            shutil.copy2(source, target)
        receipts.append({"reference": requested, "resolvedReference": cg, "source": row["output"], "sha256": digest})
        return "res://" + target.relative_to(ROOT).as_posix()

    def walk_images(value):
        if not isinstance(value, dict):
            return
        for key, child in list(value.items()):
            if key == "FirstCgName" and isinstance(child, str) and child:
                base = child.rsplit("／", 1)[0]
                sequence = []
                index = 1
                # Original AIN29506 formats %02D (full-width digits), not ASCII %02d.
                while (candidate := base + "／" + f"{index:02d}".translate(str.maketrans("0123456789", "０１２３４５６７８９"))) in images:
                    sequence.append(image_ref(candidate))
                    index += 1
                if not sequence:
                    raise ValueError(f"Empty original effect sequence: {child}")
                value["images"] = sequence
                value[key] = image_ref(child)
            elif key == "CgName" and isinstance(child, str) and child:
                value[key] = image_ref(child)
            elif key in ("Sound1", "Sound2", "Sound3", "Voice1", "Voice2", "Voice3") and child:
                reference = str(child)
                if reference not in audio:
                    raise FileNotFoundError(f"Original audio reference missing: {reference}")
                row = audio[reference]
                audio_refs[reference] = row["output"]
            elif isinstance(child, dict):
                walk_images(child)

    for role_id, name, english, japanese, color, motion_base, strike, special, special_name in roles():
        source_role = motions[motion_base]
        selected = {"idle": "待机", "strike": strike, "special": special,
                    "hit": "伤害のけぞり", "dead": "伤害死亡", "cast": "攻击４", "return": "ジャンプ"}
        compiled = {}
        for cue, motion_name in selected.items():
            if motion_name not in source_role:
                raise KeyError(f"Missing original motion {motion_base}/{motion_name}")
            motion = json.loads(json.dumps(source_role[motion_name], ensure_ascii=False))
            frames = [value for key, value in motion.items() if key.isdecimal()]
            positive = [i for i, frame in enumerate(frames)
                        if frame.get("Damage", {}).get("Enable", 0) == 1
                        and frame.get("Damage", {}).get("Percent", 0) > 0]
            if cue in ("strike", "special") and len(positive) != 1:
                raise ValueError(f"Expected one positive hit: {motion_base}/{motion_name}: {positive}")
            validate_motion(frames, f"{motion_base}/{motion_name}")
            bind_action_anchors(frames, role_id, cue)
            classify_impacts(frames)
            walk_images(motion)
            for frame in frames:
                shadows = []
                # Only layers sharing a projection anchor may be baked together.
                # Keep each original projection elevation; runtime translation is shared.
                groups = {}
                for layer in frame.get("CgLayers", {}).values():
                    if layer.get("IsShadow"):
                        key = (bool(layer.get("IsGlobalPosition")), layer.get("PosX", 0), max(0, -layer.get("PosY", 0)), layer["impact"])
                        groups.setdefault(key, []).append(layer)
                for (global_position, anchor_x, elevation, impact), layers in groups.items():
                    image, origin = compose(ROOT, layers, shadow=True)
                    if image is None:
                        continue
                    image, origin = ground_shadow(image, origin)
                    digest = hashlib.sha256(image.tobytes()+str(image.size).encode()).hexdigest()[:20]
                    target = ROOT / "DohnaDohna/images/shadows" / (digest+".png")
                    target.parent.mkdir(parents=True, exist_ok=True)
                    image.save(target)
                    shadows.append({"image":"res://"+target.relative_to(ROOT).as_posix(),
                                    "origin":list(origin),"global":global_position,
                                    "elevation":elevation,"anchorX":anchor_x,"impact":impact})
                frame["shadows"] = shadows
            compiled[cue] = {"sourceMotion": motion_name, "positiveHits": positive, "frames": frames,
                             "metadata": {k:v for k,v in motion.items() if not k.isdecimal()}}
        directory = ROOT / "DohnaDohna/roles" / role_id
        directory.mkdir(parents=True, exist_ok=True)
        portrait = image_ref("系统／打斗结算／顔／" + name)
        shutil.copy2(ROOT / portrait.removeprefix("res://"), directory / "portrait.png")
        poster_ref = "系统／失手切入图／" + name
        poster = image_ref(poster_ref) if poster_ref in images else None
        death_voice = voices[name]["伤害死亡"]["voice"]
        hurt_voice = voices[name]["伤害"]["voice"]
        for reference in death_voice + hurt_voice:
            if reference not in audio:
                raise FileNotFoundError(f"Original death voice missing: {reference}")
            audio_refs[reference] = audio[reference]["output"]
        idle_image, idle_origin = compose(ROOT, compiled["idle"]["frames"][0]["CgLayers"].values())
        ground = contact(idle_image, idle_origin)
        payload = {"roleId": role_id, "name": name, "color": color,
                   "groundContact": ground, "bodyHeight": ground[1]-idle_origin[1],
                   "motionSource": motion_base, "poster": poster, "deathVoice": death_voice,
                   "hurtVoice": hurt_voice, "motions": compiled}
        (directory / "motions.json").write_text(json.dumps(payload, ensure_ascii=False, separators=(",", ":")), encoding="utf-8")
    # The host supplies its own buttons. Only the background is addressed through
    # this map; figures and circles are selected explicitly from SceneTitle below.
    title_refs = {"系统／标题／背景": image_ref("系统／标题／背景")}
    title_dir = ROOT / "DohnaDohna/images/original"
    (title_dir / "title.json").write_text(json.dumps(title_refs, ensure_ascii=False), encoding="utf-8")
    title_path = original / "reverse/data/pact/Scene/20_Title/Title/SceneTitle.x"
    title_parts = tree(title_path)["ルート部件"]["子部件"]
    layout = []
    figure_bounds = []
    for key in ("CharacterAlice", "CharacterKirakira", "CharacterMedhico", "CharacterKikuchiyo", "CharacterAntena", "CharacterPorno"):
        part = title_parts[key]
        child = next(iter(part["子部件"].values()))
        layout.append({"name": key, "position": part["座標"], "offset": child["座標"],
                       "image": image_ref(child["种类別情報"]["普通状態"]["ＣＧ名"])})
        entry = layout[-1]
        with Image.open(ROOT / entry["image"].removeprefix("res://")) as picture:
            bounds = picture.convert("RGBA").getbbox()
        x, y = (entry["position"][i] + entry["offset"][i] for i in (0, 1))
        figure_bounds.append([x+bounds[0], y+bounds[1], x+bounds[2], y+bounds[3]])
    circles = []
    for key, period in (("Circle1", 100), ("Circle2", -120), ("Circle3", 200)):
        part = title_parts[key]
        circles.append({"position": part["座標"], "scale": part["拡大縮小"], "alpha": part["アルファ"] / 255,
                        "filter": part["描画フィルタ"], "period": period,
                        "image": image_ref(part["种类別情報"]["普通状態"]["ＣＧ名"])})
    bounds = [min(b[i] for b in figure_bounds) if i < 2 else max(b[i] for b in figure_bounds) for i in range(4)]
    (title_dir / "title-layout.json").write_text(json.dumps({"figures": layout, "circles": circles,
        "figureBounds": bounds}, ensure_ascii=False), encoding="utf-8")
    (ROOT / "DohnaDohna/effects.json").write_text(json.dumps({"lineTexture": image_ref("LineEffect")}), encoding="utf-8")
    evidence = ROOT / "build/asset-receipt.json"
    evidence.parent.mkdir(parents=True, exist_ok=True)
    evidence.write_text(json.dumps({"motionSha256": hashlib.sha256(motions_path.read_bytes()).hexdigest(),
                                  "roles": len(roles()), "images": receipts,
                                  "audio": audio_refs}, ensure_ascii=False, indent=2), encoding="utf-8")
    print(f"Imported {len(roles())} roles, {len(set(r['sha256'] for r in receipts))} unique images; {len(audio_refs)} audio references.")


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--original", type=Path)
    args = parser.parse_args()
    original = args.original or Path(json.loads((ROOT / ".local/original-reference.json").read_text(encoding="utf-8-sig"))["referenceDirectory"])
    import_roles(original)
