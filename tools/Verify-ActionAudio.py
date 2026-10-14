"""Witness natural one-shot tails in the isolated game's actual output.

This does not claim human listening or require sounds to survive room teardown.
At least three distinct long source sounds must match both near onset and after
the removed 450 ms cutoff, at a consistent capture delay.
"""
import argparse
import json
import math
from pathlib import Path

import numpy as np
import soundfile as sf
from scipy.signal import fftconvolve, resample_poly

parser = argparse.ArgumentParser()
parser.add_argument("directory", type=Path)
args = parser.parse_args()
root = Path(__file__).resolve().parents[1]
directory = args.directory
clock = json.loads((directory / "audio-start.json").read_text(encoding="utf-8"))
if not clock["processLoopback"] or clock["discontinuities"]:
    raise ValueError("Require uninterrupted process-only recording")
events = json.loads((directory / "events.json").read_text(encoding="utf-8"))
source_root = Path(json.loads((root / ".local/original-reference.json").read_text(encoding="utf-8-sig"))["referenceDirectory"])
receipt = json.loads((root / "build/asset-receipt.json").read_text(encoding="utf-8"))["audio"]
audio, rate = sf.read(directory / "audio.wav", always_2d=True)
audio = audio.mean(axis=1)
cache, witnesses = {}, []
for event in events:
    if event["kind"] != "sfx" or not event["valid"]:
        continue
    ref = event["reference"]
    if ref not in cache:
        source, sr = sf.read(source_root / receipt[ref].replace("\\", "/"), always_2d=True)
        source = source.mean(axis=1)
        if sr != rate:
            common = math.gcd(sr, rate)
            source = resample_poly(source, rate // common, sr // common)
        active = np.flatnonzero(np.abs(source) > max(np.max(np.abs(source)) * .03, .001))
        cache[ref] = (source, active)
    source, active = cache[ref]
    if not len(active) or active[-1] < rate * .65:
        continue
    starts = [max(0, int(active[0]) - rate // 100), int(active[-1]) - rate // 8]
    matches = []
    for start in starts:
        witness = source[start:start + rate // 10]
        expected = (event["seconds"] - clock["seconds"]) * rate + start
        left, right = max(0, math.ceil(expected)), min(len(audio), int(expected) + len(witness) + rate // 2)
        segment = audio[left:right]
        if len(segment) < len(witness):
            break
        dots = fftconvolve(segment, witness[::-1], mode="valid")
        sums = np.concatenate(([0.0], np.cumsum(segment * segment)))
        energy = sums[len(witness):] - sums[:-len(witness)]
        corr = np.abs(dots) / np.sqrt(np.maximum(energy, 1e-20) * np.sum(witness * witness))
        best = int(np.argmax(corr))
        matches.append(dict(sourceSeconds=start / rate, correlation=float(corr[best]), delayMs=(left + best - expected) / rate * 1000))
    if len(matches) == 2 and min(m["correlation"] for m in matches) >= .7 and abs(matches[0]["delayMs"] - matches[1]["delayMs"]) <= 2:
        witnesses.append(dict(reference=ref, seconds=event["seconds"], gainDb=event["gainDb"], matches=matches))
distinct = sorted({w["reference"] for w in witnesses})
result = dict(passed=len(distinct) >= 3, distinctSounds=distinct, witnesses=witnesses,
              minimumCorrelation=.7, maximumDelayDifferenceMs=2,
              scope="matched emitted one-shot onset and >450ms tail; not all events or human listening")
(directory / "action-audio-proof.json").write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding="utf-8")
print(json.dumps({k: v for k, v in result.items() if k != "witnesses"}, ensure_ascii=False))
if not result["passed"]:
    raise SystemExit(1)
