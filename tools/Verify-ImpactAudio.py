"""Recorded native contact sounds in real card plays; calls alone do not pass.

Uses native isolated baselines from the same process and output device. It is
waveform evidence, not a claim of human listening or original battle recording.
"""
import argparse
import json
from pathlib import Path
import numpy as np
import soundfile as sf
from scipy.signal import fftconvolve

p = argparse.ArgumentParser()
p.add_argument('directory', type=Path)
directory = p.parse_args().directory
clock = json.loads((directory/'audio-start.json').read_text(encoding='utf-8'))
if not clock['processLoopback'] or clock['discontinuities']:
    raise ValueError('Uninterrupted isolated-game audio required')
events = json.loads((directory/'events.json').read_text(encoding='utf-8'))
samples, rate = sf.read(directory/'audio.wav', always_2d=True)
samples = samples.mean(axis=1)
markers = {e['reference']: e['seconds'] for e in events if e['kind'] == 'marker'}

def segment(begin, end):
    a,b = [round((t-clock['seconds'])*rate) for t in (begin,end)]
    if not 0 <= a < b <= len(samples):
        raise ValueError('Incomplete contact audio window')
    return samples[a:b]

references = ['blunt_attack.mp3','slash_attack.mp3','heavy_attack.mp3','event:/sfx/characters/attack_fire']
templates = {}
for ref in references:
    label = 'impact-baseline-' + ('fire' if ref.startswith('event:') else ref)
    data = segment(markers[label+'-begin'],markers[label+'-end']-.1)
    active = np.flatnonzero(np.abs(data) > .001)
    if not len(active) or np.sqrt(np.mean(data*data)) < 1e-4:
        raise ValueError('Silent native baseline: '+ref)
    a = max(0,active[0]-int(rate*.005))
    templates[ref] = data[a:a+int(rate*.25)]

results = []
for label in sorted(k[:-6] for k in markers if k.startswith('impact-') and k.endswith('-begin') and '-baseline-' not in k):
    begin,end = markers[label+'-begin'],markers[label+'-end']
    native = [e for e in events if begin <= e['seconds'] < end and e['kind'] in ('native-sfx','native-tmp') and e['reference'] in references]
    voices = [e for e in events if begin <= e['seconds'] < end and e['kind'] == 'voice']
    if label.endswith('-cinematic'):
        # This is the visual-only cinematic probe; genuine finishers use the
        # native command and are separately checked by the Finisher scenario.
        results.append(dict(label=label,passed=not native and not voices,nativeContactCount=len(native),voiceCount=len(voices)))
        continue
    if voices:
        raise ValueError('Ordinary attack called character voice: '+label)
    if len(native) != 1 or native[0]['volume'] != 1:
        raise ValueError('Native contact duplicated/missing/wrong volume: '+label)
    e = native[0]
    witness = templates[e['reference']]
    clip = segment(max(begin,e['seconds']-.1),min(end,e['seconds']+1.5))
    dots = fftconvolve(clip,witness[::-1],mode='valid')
    energies = np.concatenate(([0],np.cumsum(clip*clip)))
    energies = energies[len(witness):]-energies[:-len(witness)]
    correlation = np.abs(dots)/np.sqrt(np.maximum(energies,1e-20)*np.sum(witness*witness))
    best = float(np.max(correlation))
    results.append(dict(label=label,reference=e['reference'],correlation=best,nativeContactCount=1,passed=best>=.15))
if len(results) != 40:
    raise ValueError(f'Expected twenty ordinary and twenty cinematic samples, found {len(results)}')
proof = dict(passed=all(r['passed'] for r in results),method='same-process recorded native reference correlation plus event counts, 0.15 threshold',
             humanListening='NOT RUN',results=results)
(directory/'impact-audio-proof.json').write_text(json.dumps(proof,indent=2),encoding='utf-8')
print(json.dumps(proof))
if not proof['passed']:
    raise SystemExit(1)
