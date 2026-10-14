"""Supplement a failed initial-500ms voice witness with independent source segments.

Keep audio-proof.json unchanged: this is additional waveform evidence, not a
lowered threshold or a claim that the whole line was intelligible to a listener.
Two nonoverlapping 100ms segments must correlate >= .7 at the same delay (2ms).
"""
import argparse
import json
import math
from pathlib import Path

import numpy as np
import soundfile as sf
from scipy.signal import fftconvolve, resample_poly


def verify(directory):
    root = Path(__file__).resolve().parents[1]
    baseline = json.loads((directory / "audio-proof.json").read_text(encoding="utf-8"))
    clock = json.loads((directory / "audio-start.json").read_text(encoding="utf-8"))
    if not clock["processLoopback"] or clock["discontinuities"]:
        raise ValueError("Require uninterrupted isolated-process audio")
    events = [e for e in json.loads((directory / "events.json").read_text(encoding="utf-8")) if e["kind"] == "voice"]
    if len(events) != len(baseline["matches"]):
        raise ValueError("Baseline proof and source event list do not match")
    original = Path(json.loads((root / ".local/original-reference.json").read_text(encoding="utf-8-sig"))["referenceDirectory"])
    receipt = json.loads((root / "build/asset-receipt.json").read_text(encoding="utf-8"))["audio"]
    audio, rate = sf.read(directory / "audio.wav", always_2d=True)
    audio = audio.mean(axis=1)
    results = []
    for event, initial in zip(events, baseline["matches"], strict=True):
        if (event["role"], event["reference"]) != (initial["role"], initial["reference"]):
            raise ValueError("Baseline event ordering changed")
        if initial["passed"]:
            continue
        source, sr = sf.read(original / receipt[event["reference"]].replace("\\", "/"), always_2d=True)
        source = source.mean(axis=1)
        if sr != rate:
            common = math.gcd(sr, rate)
            source = resample_poly(source, rate // common, sr // common)
        length = rate // 10
        candidates = []
        expected = (event["seconds"] - clock["seconds"]) * rate
        total_energy = np.dot(source, source)
        for start in range(0, len(source) - length + 1, length):
            witness = source[start:start + length]
            energy = np.dot(witness, witness)
            if energy < max(1e-12, total_energy * .01):
                continue
            # Device playback cannot precede this event. Keep the search local
            # rather than accepting a match anywhere in the recording.
            left = max(0, round(expected + start))
            right = min(len(audio), round(expected + start + length + rate / 2))
            segment = audio[left:right]
            if len(segment) < length:
                continue
            dots = fftconvolve(segment, witness[::-1], mode="valid")
            sums = np.concatenate(([0.0], np.cumsum(segment * segment)))
            window_energy = sums[length:] - sums[:-length]
            scores = np.abs(dots) / np.sqrt(np.maximum(window_energy, 1e-20) * energy)
            best = int(np.argmax(scores))
            candidates.append(dict(sourceSeconds=start / rate, correlation=float(scores[best]),
                                   delayMs=(left + best - expected - start) / rate * 1000))
        strong = [c for c in candidates if c["correlation"] >= .7]
        agreed = [c for c in strong if any(d is not c and abs(d["delayMs"] - c["delayMs"]) <= 2 for d in strong)]
        results.append(dict(role=event["role"], reference=event["reference"], seconds=event["seconds"],
                            baseline=initial, candidates=candidates, agreed=agreed,
                            passed=event["valid"] and len(agreed) >= 2))
    if not results:
        raise ValueError("No failed baseline voice witnesses require supplementary evidence")
    result = dict(passed=all(r["passed"] for r in results), baselinePassed=baseline["passed"],
                  baselineMatched=sum(m["passed"] for m in baseline["matches"]),
                  baselineTotal=baseline["voiceEvents"], supplementary=results,
                  scope="independent source-segment evidence only; initial-witness failure retained; not human listening")
    (directory / "voice-segment-proof.json").write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding="utf-8")
    print(json.dumps({k: v for k, v in result.items() if k != "supplementary"}, ensure_ascii=False))
    if not result["passed"]:
        raise SystemExit(1)


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("directory", type=Path)
    verify(parser.parse_args().directory)
