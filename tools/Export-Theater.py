"""Mux one real-time theater take using its QPC clocks and measured FMOD waveforms.

Adapted from NinjaSlayer sync_preview_audio.py. No fixed guessed delay, no audio
from another take, no game-speed changes. Requires the project's NumPy/SciPy.
"""
import argparse
import bisect
import csv
import json
import math
from pathlib import Path
import subprocess

import numpy as np
import soundfile as sf
from scipy.signal import fftconvolve, resample_poly

ROOT = Path(__file__).resolve().parents[1]
RATE = 48000


def read_audio(path):
    data, rate = sf.read(path, always_2d=True)
    data = data.mean(axis=1)
    if rate != RATE:
        common = math.gcd(rate, RATE)
        data = resample_poly(data, RATE // common, rate // common)
    return data


def locate(samples, template, lower, upper):
    lower, upper = max(0, round(lower * RATE)), min(len(samples), round(upper * RATE))
    window = samples[lower:upper]
    if len(window) < len(template) or np.dot(template, template) < 1e-12:
        return None
    dots = fftconvolve(window, template[::-1], mode="valid")
    energy = np.concatenate(([0.0], np.cumsum(window * window)))
    energy = energy[len(template):] - energy[:-len(template)]
    scores = np.abs(dots) / np.sqrt(np.maximum(energy, 1e-20) * np.dot(template, template))
    best = int(np.argmax(scores))
    return (lower + best) / RATE, float(scores[best])


def main(directory, output):
    output.mkdir(parents=True, exist_ok=False)
    video = json.loads((directory / "recording-start.json").read_text())
    proof = json.loads((directory / "video-proof.json").read_text())
    clock = json.loads((directory / "audio-start.json").read_text())
    timeline = json.loads((directory / "theater-timeline.json").read_text())
    if not clock["processLoopback"] or clock["discontinuities"]:
        raise ValueError("Need uninterrupted isolated-process audio from this take")
    if not timeline["finisher"] or any(e["hp"] != 0 for e in timeline["final"]["enemies"]):
        raise ValueError("Theater did not really finish combat")
    cues = timeline["cues"]
    if len({c["actor"] for c in cues if "card" in c and not c["cancel"]}) != 4:
        raise ValueError("Not all four members played cards")
    frames = list(csv.DictReader((directory / "video-frames.csv").open(newline="")))
    frame_times = [int(row["qpc"]) / video["frequency"] for row in frames]
    frame_ids = [int(row["frame"]) for row in frames]
    offset = video["timestamp"] / video["frequency"] - clock["seconds"]
    samples = read_audio(directory / "audio.wav")
    mapping = json.loads((ROOT / ".local/fmod/events.json").read_text(encoding="utf-8"))
    events = json.loads((directory / "events.json").read_text())
    cache, matches, skipped = {}, [], []
    for event in events:
        ref, when = event["reference"], event["seconds"]
        if ref not in mapping or not frame_times[0] <= when <= frame_times[-1]:
            continue
        if ref not in cache:
            cache[ref] = read_audio(mapping[ref]["asset"])
        source = cache[ref]
        cue = when - clock["seconds"]
        candidates = []
        # Two nonoverlapping 100ms source witnesses must independently agree
        # within 2ms. This avoids treating a coincident louder native sound as
        # a match for the quiet -20dB movement/voice event.
        for first in range(0, max(0, len(source) - 4800 + 1), 4800):
            chunk = source[first:first + 4800]
            if np.dot(chunk, chunk) < np.dot(source, source) * .01:
                continue
            found = locate(samples, chunk, cue - .08 + first / RATE,
                           cue + .5 + (first + len(chunk)) / RATE)
            if found and found[1] >= .7:
                candidates.append((found[0] - first / RATE, found[1], first, len(chunk)))
        consistent = [c for c in candidates if any(c[2] != d[2] and abs(c[0] - d[0]) <= .002 for d in candidates)]
        if not consistent:
            skipped.append({"reference": ref, "seconds": when, "reason": "no two high-confidence source witnesses"})
            continue
        located, score, first, length = max(consistent, key=lambda c: c[1])
        frame_index = bisect.bisect_left(frame_times, when)
        frame = frame_ids[frame_index]
        matches.append({"reference": ref, "videoFrame": frame, "videoSeconds": frame / 60,
                        "audioSeconds": located - offset, "correlation": score,
                        "delaySeconds": located - offset - frame / 60,
                        "sourceOffsetSamples": first, "sourceLengthSamples": length})
    if len(matches) < 3:
        raise ValueError(f"Only {len(matches)} independent waveform anchors; sync is unverified")
    median = float(np.median([m["delaySeconds"] for m in matches]))
    accepted = [m for m in matches if abs(m["delaySeconds"] - median) <= .035]
    if len(accepted) < 3 or max(m["videoSeconds"] for m in accepted) - min(m["videoSeconds"] for m in accepted) < 5:
        raise ValueError("Insufficient separated consistent anchors; refuse guessed audio delay")
    compensation = (min(m["delaySeconds"] for m in accepted) + max(m["delaySeconds"] for m in accepted)) / 2
    for match in matches:
        match["accepted"] = match in accepted
        match["residualSeconds"] = match["delaySeconds"] - compensation
    residual = max(abs(m["residualSeconds"]) for m in accepted)
    if residual > .04:
        raise ValueError("Measured audio/frame alignment exceeds 40ms")
    packets = [json.loads(line) for line in (directory / "audio-packets.jsonl").read_text().splitlines()]
    packet_error = max(abs(p["writtenFrames"] / RATE - (p["qpc"] - packets[0]["qpc"]) / 10_000_000) for p in packets)
    if packet_error > .004:
        raise ValueError("Audio packet clock drift exceeds 4ms")
    chain = json.loads((ROOT.parent / "Tools/STS2/toolchain.json").read_text(encoding="utf-8-sig"))
    duration = proof["encodedFrames"] / 60
    out = output / "DohnaDohna-four-role-theater.mp4"
    subprocess.run([chain["ffmpeg"], "-v", "error", "-i", str(directory / "video.mp4"),
        "-ss", f"{offset+compensation:.9f}", "-i", str(directory / "audio.wav"),
        "-map", "0:v:0", "-map", "1:a:0", "-c:v", "copy", "-c:a", "aac", "-b:a", "192k",
        "-t", str(duration), "-movflags", "+faststart", str(out)], check=True)
    encoded = subprocess.check_output([chain["ffmpeg"], "-v", "error", "-i", str(out), "-f", "f32le", "-ac", "1", "-ar", str(RATE), "-"])
    encoded = np.frombuffer(encoded, dtype=np.float32).astype(np.float64)
    for match in accepted:
        first = match["sourceOffsetSamples"]
        template = cache[match["reference"]][first:first + match["sourceLengthSamples"]]
        cue = match["videoSeconds"] + first / RATE
        found = locate(encoded, template, cue - .08, cue + .08 + len(template) / RATE)
        if not found or found[1] < .3 or abs(found[0] - cue) > .04:
            raise ValueError("Encoded AAC failed sync verification: " + str(match))
        match["encodedResidualSeconds"] = found[0] - cue
    result = {"passed": True, "rawDirectory": str(directory.resolve()), "clockOffsetSeconds": offset,
              "measuredPlaybackLatencySeconds": compensation, "maximumResidualSeconds": residual,
              "maximumEncodedResidualSeconds": max(abs(m["encodedResidualSeconds"]) for m in accepted),
              "maximumPacketClockErrorSeconds": packet_error, "acceptedAnchors": len(accepted),
              "matches": matches, "unmatched": skipped, "video": proof,
              "humanListening": "NOT RUN", "physicalInput": "NOT RUN"}
    (output / "audio-sync.json").write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding="utf-8")
    (output / "theater-timeline.json").write_text(json.dumps(timeline, ensure_ascii=False, indent=2), encoding="utf-8")
    print(json.dumps({"output": str(out), "duration": duration, "anchors": len(accepted),
                      "maximumEncodedResidualMs": result["maximumEncodedResidualSeconds"] * 1000,
                      "measuredCaptureFps": proof["measuredCaptureFps"]}))


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("directory", type=Path)
    parser.add_argument("output", type=Path)
    args = parser.parse_args()
    main(args.directory, args.output)
