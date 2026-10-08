"""The audio acceptance tool must reject silent output and misleading traces."""
import copy
import json
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest

import numpy as np
import soundfile as sf


class NativeHitAudioTests(unittest.TestCase):
    def setUp(self):
        self.rate = 8000
        self.audio = np.zeros((11 * self.rate, 2))
        self.events = []
        self.clock = {"seconds": 0, "processLoopback": True, "discontinuities": 0}
        attack = "event:/sfx/enemy/enemy_attacks/twig_slime_s/twig_slime_s_attack"
        shield = "event:/sfx/block_break"
        slash = "event:/sfx/enemy/enemy_attacks/axebot/axebot_attack"
        for label, begin, refs, allowed in [
            ("baseline-attack", 1, [attack], True),
            ("native-tackle", 3, [attack, shield], True),
            ("isolated-break", 5, [shield], True),
            ("silent-break-control", 7, [shield], False),
            ("native-slash", 9, [slash], True),
        ]:
            for suffix, seconds in [("-begin", begin), ("-end", begin + 1)]:
                self.events.append({"reference": label + suffix, "seconds": seconds, "kind": "marker"})
            for index, ref in enumerate(refs):
                self.events.append({"reference": ref, "seconds": begin + index * .3 + .01,
                                    "kind": "native-sfx", "volume": 1, "allowed": allowed})
            if allowed:
                self.tone(begin + .05, begin + .25)

    def tone(self, start, end):
        left, right = round(start * self.rate), round(end * self.rate)
        self.audio[left:right] = (.01 * np.sin(np.arange(right - left) * .2))[:, None]

    def verify(self):
        with tempfile.TemporaryDirectory(prefix="dohna-audio-test-") as temporary:
            directory = Path(temporary)
            (directory / "audio-start.json").write_text(json.dumps(self.clock), encoding="utf-8")
            (directory / "events.json").write_text(json.dumps(self.events), encoding="utf-8")
            sf.write(directory / "audio.wav", self.audio, self.rate, subtype="FLOAT")
            result = subprocess.run([sys.executable, str(Path(__file__).with_name("Verify-NativeHitAudio.py")),
                                     str(directory)], capture_output=True, text=True)
            return result.returncode == 0

    def test_recorded_positive_and_silent_control(self):
        self.assertTrue(self.verify())

    def test_play_calls_without_output_fail(self):
        self.audio[:] = 0
        self.assertFalse(self.verify())

    def test_noisy_negative_control_fails(self):
        self.tone(7.05, 7.25)
        self.assertFalse(self.verify())

    def test_duplicate_native_sound_fails(self):
        event = next(e for e in self.events if e["kind"] == "native-sfx" and 3 < e["seconds"] < 4)
        self.events.append(copy.deepcopy(event))
        self.assertFalse(self.verify())

    def test_wrong_native_gain_fails(self):
        next(e for e in self.events if e["kind"] == "native-sfx" and 5 < e["seconds"] < 6)["volume"] = .1
        self.assertFalse(self.verify())

    def test_mod_sound_cannot_substitute_for_native_control(self):
        self.events.append({"reference": "role-voice", "seconds": 5.1, "kind": "voice"})
        self.assertFalse(self.verify())

    def test_whole_desktop_recording_rejected(self):
        self.clock["processLoopback"] = False
        self.assertFalse(self.verify())


if __name__ == "__main__":
    unittest.main()
