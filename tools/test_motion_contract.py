import importlib.util
import json
import pathlib
import unittest

ROOT = pathlib.Path(__file__).resolve().parents[1]
spec = importlib.util.spec_from_file_location("import_roles", ROOT / "tools/Import-OriginalRoles.py")
importer = importlib.util.module_from_spec(spec)
spec.loader.exec_module(importer)


class MotionContractTests(unittest.TestCase):
    def test_generated_ground_preserves_authored_horizontal_station(self):
        paths = list((ROOT / "DohnaDohna/roles").glob("*/motions.json"))
        self.assertEqual(len(paths), 10)
        for path in paths:
            data = json.loads(path.read_text(encoding="utf-8"))
            self.assertEqual(data["groundContact"][0], 0, data["roleId"])
            self.assertGreater(data["bodyHeight"], 0)

    def test_used_motion_fields(self):
        count = 0
        for path in (ROOT / "DohnaDohna/roles").glob("*/motions.json"):
            data = json.loads(path.read_text(encoding="utf-8"))
            for cue, motion in data["motions"].items():
                importer.validate_motion(motion["frames"], f"{data['roleId']}/{cue}")
                count += 1
        self.assertEqual(count, 70)

    def test_reject_unknown_start_track(self):
        with self.assertRaisesRegex(ValueError, "Unsupported active"):
            importer.validate_motion([{"Unknown": {"Start": 1}}], "fixture")

    def test_reject_unknown_end_track(self):
        with self.assertRaisesRegex(ValueError, "Unsupported active"):
            importer.validate_motion([{"Unknown": {"End": 1}}], "fixture")

    def test_reject_unknown_easing(self):
        with self.assertRaisesRegex(ValueError, "Unsupported easing"):
            importer.validate_motion([{"Camera": {"Enable": 1, "EasingType": "NotImplemented"}}], "fixture")

    def test_generated_bindings(self):
        allowed = {"actor", "formation", "impact", "target"}
        for path in (ROOT / "DohnaDohna/roles").glob("*/motions.json"):
            data = json.loads(path.read_text(encoding="utf-8"))
            for motion in data["motions"].values():
                for frame in motion["frames"]:
                    self.assertIsInstance(frame["actionAnchorX"], (int, float))
                    for effect in frame.get("Effects", {}).values():
                        self.assertIn(effect["anchorBinding"], allowed)
                        if effect["AssignToTarget"]:
                            self.assertEqual(effect["anchorBinding"], "target")
                        elif effect["IsGlobalPosition"]:
                            self.assertEqual(effect["anchorBinding"], "formation")

    def test_impact_classification_is_independent_of_attachment(self):
        frames = [{"Effects": {
            "beam": {"FirstCgName": "特效／安缇娜攻击１レーザー／０１", "IsGlobalPosition": 1},
            "explosion": {"FirstCgName": "特效／萨尔萨爆炸２／０１", "IsGlobalPosition": 1},
            "charge": {"FirstCgName": "特效／死亡爆炸前／０９"},
            "equipment": {"FirstCgName": "特效／珀尔诺攻击４ウマ／０４", "AssignToTarget": 1}},
            "CgLayers": {"target": {"CgName": "特效／珀尔诺縛り／０１"}},
            "Sound": {"Sound1": "阿熊_攻击１_019", "Sound2": "阿熊_攻击１_001"}}]
        importer.classify_impacts(frames)
        effects = frames[0]["Effects"]
        self.assertTrue(effects["explosion"]["impact"])
        self.assertFalse(effects["beam"]["impact"])
        self.assertFalse(effects["charge"]["impact"])
        self.assertFalse(effects["equipment"]["impact"])
        self.assertTrue(frames[0]["CgLayers"]["target"]["impact"])
        self.assertTrue(frames[0]["Sound"]["impact1"])
        self.assertFalse(frames[0]["Sound"]["impact2"])

    def test_all_attacks_have_explicit_impact_metadata(self):
        for path in (ROOT / "DohnaDohna/roles").glob("*/motions.json"):
            data = json.loads(path.read_text(encoding="utf-8"))
            for cue in ("strike", "special"):
                flags = []
                for f in data["motions"][cue]["frames"]:
                    for g in ("Effects", "CgLayers"):
                        for item in f.get(g, {}).values():
                            self.assertIsInstance(item["impact"], bool)
                            flags.append(item["impact"])
                    for shadow in f["shadows"]:
                        self.assertIsInstance(shadow["impact"], bool)
                    if "Sound" in f:
                        for n in range(1, 4):
                            self.assertIsInstance(f["Sound"][f"impact{n}"], bool)
                # ALyCE's special is a travelling bullet without separate hit art;
                # Tora's special is a lightning stroke, not an impact flash.
                if (data["roleId"], cue) not in (("alyce", "special"), ("tora", "special")):
                    self.assertTrue(any(flags), (data["roleId"], cue))

    def test_antena_keeps_emitter_composition(self):
        data = json.loads((ROOT / "DohnaDohna/roles/antena/motions.json").read_text(encoding="utf-8"))
        for cue, index, offsets in (("strike", 39, [(523, -182)]),
                                    ("special", 40, [(655, -184), (523, -182)])):
            frame = data["motions"][cue]["frames"][index]
            effects = list(frame["Effects"].values())
            self.assertEqual([(e["PosX"], e["PosY"]) for e in effects], offsets)
            self.assertTrue(all(e["anchorBinding"] == "formation" for e in effects))
            self.assertEqual(frame["actionAnchorX"], 20)
            self.assertTrue(any(l["PosX"] == 36 and l["PosY"] == -360
                                and l["IsGlobalPosition"] for l in frame["CgLayers"].values()))


if __name__ == "__main__":
    unittest.main()
