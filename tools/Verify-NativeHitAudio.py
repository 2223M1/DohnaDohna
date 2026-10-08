"""Verify native output with source-isolated positive/negative controls.

FMOD can vary native samples/pitch, so a fixed waveform template is not a reliable
oracle. Routing records the same shield-breaking command with its native one-shot
enabled and disabled by a TEST-ONLY gate. Both recorded samples and event ownership
must agree. This is output verification, not human listening or physical input QA.
"""
import argparse
import json
from collections import Counter
from pathlib import Path

import numpy as np
import soundfile as sf

parser = argparse.ArgumentParser()
parser.add_argument("directory", type=Path)
directory = parser.parse_args().directory
clock = json.loads((directory / "audio-start.json").read_text(encoding="utf-8"))
if not clock["processLoopback"] or clock["discontinuities"]:
    raise ValueError("An uninterrupted isolated-process capture is required")
events = json.loads((directory / "events.json").read_text(encoding="utf-8"))
audio, rate = sf.read(directory / "audio.wav", always_2d=True)
markers = {e["reference"]: e["seconds"] for e in events if e["kind"] == "marker"}
ATTACK = "event:/sfx/enemy/enemy_attacks/twig_slime_s/twig_slime_s_attack"
BREAK = "event:/sfx/block_break"
SLASH = "event:/sfx/enemy/enemy_attacks/axebot/axebot_attack"


def samples(begin, end):
    left, right = (round((t - clock["seconds"]) * rate) for t in (begin, end))
    if not 0 <= left < right <= len(audio):
        raise ValueError("Recording does not cover the complete witness")
    return audio[left:right]


def levels(clip):
    return {"rms": float(np.sqrt(np.mean(clip * clip))),
            "peak": float(np.max(np.abs(clip))), "seconds": len(clip) / rate}


def witness(label, expected, enabled=True):
    begin, end = (markers[label + suffix] for suffix in ("-begin", "-end"))
    # Leave 100ms before the next fixture operation; do not include its sound.
    recorded = levels(samples(begin, end - .1))
    inside = [e for e in events if begin <= e["seconds"] < end and e["kind"] != "marker"]
    native = [e for e in inside if e["kind"] == "native-sfx"]
    recorded.update({"label": label, "nativeCalls": native,
                     "callsMatch": Counter(e["reference"] for e in native) == Counter(expected)
                     and all(e["volume"] == 1 and e["allowed"] == enabled for e in native),
                     "modAudioCount": sum(e["kind"] in ("voice", "sfx") for e in inside)})
    return recorded


positive = witness("isolated-break", [BREAK])
negative = witness("silent-break-control", [BREAK], enabled=False)
attack = witness("baseline-attack", [ATTACK])
tackle = witness("native-tackle", [ATTACK, BREAK])
slash = witness("native-slash", [SLASH])

# Identify a native-only onset in the REAL monster move before shield break or
# role feedback starts, after a quiet lead-in. This is not the manual baseline.
# The gate is open for this entire normal monster attack.
begin = markers["native-tackle-begin"]
other = min(e["seconds"] for e in events
            if e["seconds"] > begin and e["kind"] in ("native-sfx", "voice", "sfx")
            and e["reference"] != ATTACK)
onset = levels(samples(begin, other - .01))
quiet = levels(samples(begin - .2, begin - .02))
onset["quietLeadIn"] = quiet

# Absolute floors reject silent/quantization-only captures; contrast additionally
# requires >=20dB RMS separation from the paired control.
thresholds = {"witnessRmsMinimum": 1e-4, "silentPeakMaximum": 1e-5,
              "contrastMinimum": 10, "onsetRmsMinimum": 1e-5, "onsetPeakMinimum": 1e-4}
controls_pass = (positive["callsMatch"] and negative["callsMatch"]
                 and positive["modAudioCount"] == negative["modAudioCount"] == 0
                 and positive["rms"] >= thresholds["witnessRmsMinimum"]
                 and negative["peak"] <= thresholds["silentPeakMaximum"]
                 and positive["rms"] >= 10 * max(negative["rms"], 1e-5))
onset_pass = (onset["seconds"] >= .1 and quiet["peak"] <= 1e-5
              and onset["rms"] >= 1e-5 and onset["peak"] >= 1e-4
              and onset["rms"] >= 10 * max(quiet["rms"], 1e-7))
normal_pass = all(w["callsMatch"] and w["rms"] >= 1e-4 for w in (attack, tackle, slash))
result = {"passed": controls_pass and onset_pass and normal_pass,
          "method": "source-isolated output with silent control, plus native-only real-attack onset",
          "thresholds": thresholds, "controlsPassed": controls_pass, "onsetPassed": onset_pass,
          "nativeAttackOnset": onset, "witnesses": [positive, negative, attack, tackle, slash],
          "scope": "process-only Windows waveform and native event ownership; not human listening"}
(directory / "native-audio-proof.json").write_text(json.dumps(result, indent=2), encoding="utf-8")
print(json.dumps(result))
if not result["passed"]:
    raise SystemExit(1)
