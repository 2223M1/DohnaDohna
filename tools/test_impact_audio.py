"""Reject calls-only/silent and double-play evidence with synthetic waveforms."""
import json
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest
import numpy as np
import soundfile as sf


class ImpactAudioTests(unittest.TestCase):
    def run_fixture(self, bad=None):
        rate = 8000
        samples = np.zeros(55*rate)
        events = []
        names = ['blunt_attack.mp3','slash_attack.mp3','heavy_attack.mp3','event:/sfx/characters/attack_fire']
        waves = [np.random.default_rng(n).normal(0,.05,int(rate*.3)) for n in range(4)]
        def mark(label,t): events.append(dict(reference=label,kind='marker',seconds=t))
        def audio(ref,t,kind='native-tmp'): events.append(dict(reference=ref,kind=kind,seconds=t,volume=1))
        for i,ref in enumerate(names):
            t=i+1
            label='impact-baseline-'+('fire' if i==3 else ref)
            mark(label+'-begin',t); mark(label+'-end',t+.8)
            audio(ref,t+.1,'native-sfx' if i==3 else 'native-tmp')
            samples[int((t+.1)*rate):int((t+.1)*rate)+len(waves[i])] = waves[i]
        for n in range(20):
            i=n%4;t=6+n*2;label=f'impact-role{n}-strike'
            mark(label+'-begin',t);mark(label+'-end',t+.8)
            audio(names[i],t+.1,'native-sfx' if i==3 else 'native-tmp')
            if bad != 'silent' or n != 0:
                samples[int((t+.1)*rate):int((t+.1)*rate)+len(waves[i])] = waves[i]
            if n==0 and bad=='double': audio(names[i],t+.15)
            if n==0 and bad=='voice': audio('role-voice',t+.1,'voice')
            mark(label+'-cinematic-begin',t+1);mark(label+'-cinematic-end',t+1.8)
            if n==0 and bad=='cinematic_voice': audio('role-voice',t+1.1,'voice')
        with tempfile.TemporaryDirectory() as temp:
            root=Path(temp)
            (root/'events.json').write_text(json.dumps(events),encoding='utf-8')
            (root/'audio-start.json').write_text(json.dumps(dict(seconds=0,processLoopback=True,discontinuities=0)),encoding='utf-8')
            sf.write(root/'audio.wav',samples,rate,subtype='FLOAT')
            return subprocess.run([sys.executable,str(Path(__file__).with_name('Verify-ImpactAudio.py')),temp],capture_output=True).returncode

    def test_recorded_native_output(self): self.assertEqual(self.run_fixture(),0)
    def test_calls_without_sound_rejected(self): self.assertNotEqual(self.run_fixture('silent'),0)
    def test_duplicate_native_sound_rejected(self): self.assertNotEqual(self.run_fixture('double'),0)
    def test_ordinary_character_voice_rejected(self): self.assertNotEqual(self.run_fixture('voice'),0)
    def test_visual_only_cinematic_voice_rejected(self): self.assertNotEqual(self.run_fixture('cinematic_voice'),0)


if __name__=='__main__': unittest.main()
