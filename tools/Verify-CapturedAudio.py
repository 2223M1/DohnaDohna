"""Match original voice waveforms against captured output from ONLY the test game PID.

Event/handle registration alone is not an audible-output assertion. This script
checks the actual decoded Windows output; it does not claim human listening QA.
"""
import argparse
import json
import math
from pathlib import Path

import numpy as np
import soundfile as sf
from scipy.signal import fftconvolve, resample_poly

ROOT = Path(__file__).resolve().parents[1]
parser = argparse.ArgumentParser()
parser.add_argument("directory", type=Path)
args = parser.parse_args()
directory = args.directory
clock = json.loads((directory / "audio-start.json").read_text(encoding="utf-8"))
if not clock["processLoopback"]:
    raise ValueError("Reject whole-desktop capture; only the isolated game PID is allowed")
events = json.loads((directory / "events.json").read_text(encoding="utf-8"))
original = Path(json.loads((ROOT / ".local/original-reference.json").read_text(encoding="utf-8-sig"))["referenceDirectory"])
receipt = json.loads((ROOT / "build/asset-receipt.json").read_text(encoding="utf-8"))["audio"]
captured, rate = sf.read(directory / "audio.wav", always_2d=True)
captured = captured.mean(axis=1)
cache = {}
matches = []
for event in events:
    if event["kind"] != "voice":
        continue
    ref = event["reference"]
    if ref not in cache:
        samples, source_rate = sf.read(original / receipt[ref].replace("\\", "/"), always_2d=True)
        samples = samples.mean(axis=1)
        if source_rate != rate:
            common = math.gcd(source_rate, rate)
            samples = resample_poly(samples, rate // common, source_rate // common)
        active = np.flatnonzero(np.abs(samples) > max(np.max(np.abs(samples)) * 0.02, 0.001))
        if not len(active):
            raise ValueError("Silent original voice: " + ref)
        # A 0.5-second voiced witness avoids counting source leading silence or
        # requiring pure-visual tails to survive a native room exit.
        start = max(0, int(active[0]) - rate // 100)
        witness = samples[start:min(len(samples), start + rate // 2)]
        cache[ref] = (start, witness)
    start, witness = cache[ref]
    expected = (event["seconds"] - clock["seconds"]) * rate + start
    left = max(0, int(expected) - rate // 2)
    right = min(len(captured), int(expected) + len(witness) + rate)
    segment = captured[left:right]
    if len(segment) < len(witness):
        raise ValueError("Capture does not cover voice event: " + ref)
    dots = fftconvolve(segment, witness[::-1], mode="valid")
    cumulative = np.concatenate(([0.0], np.cumsum(segment * segment)))
    energy = cumulative[len(witness):] - cumulative[:-len(witness)]
    normalizer = np.sqrt(np.maximum(energy, 1e-20) * np.sum(witness * witness))
    correlation = np.abs(dots) / normalizer
    index = int(np.argmax(correlation))
    value = float(correlation[index])
    matches.append({"role": event["role"], "reference": ref, "correlation": round(value, 4),
                    "latencyMs": round((left + index - expected) / rate * 1000, 2),
                    "passed": event["valid"] and value >= 0.15})
if not matches:
    raise ValueError("No actual voice events captured")
result = {"passed": all(m["passed"] for m in matches), "voiceEvents": len(matches),
          "roles": sorted({m["role"] for m in matches}), "threshold": 0.15,
          "minimumCorrelation": round(min(m["correlation"] for m in matches), 4), "matches": matches,
          "scope": "process-only Windows output waveform matching; not human listening QA"}
(directory / "audio-proof.json").write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding="utf-8")
print(json.dumps({k: v for k, v in result.items() if k != "matches"}, ensure_ascii=False))
if not result["passed"]:
    raise SystemExit(1)
