"""Check real Unity claw-sweep mix, source identity, contact timing and cleanup."""
import hashlib
import json
from pathlib import Path
import subprocess
import numpy as np
from scipy.io import wavfile

ROOT = Path(__file__).resolve().parents[1]
FOLDER = ROOT / 'artifacts/a177-claw-audio'
BASELINE = 'b0695f8ffb91574f16cf2f17c8cd7dcd46ad3a8a'


def main():
    for version in ('before', 'after'):
        for line in (FOLDER / version / 'sources.txt').read_text().splitlines():
            name, expected = line.rsplit(' ', 1)
            path = 'unity/Assets/Scripts/' + name
            data = (ROOT / path).read_bytes() if version == 'after' else subprocess.check_output(['git', 'show', BASELINE + ':' + path], cwd=ROOT)
            assert hashlib.sha256(data).hexdigest() == expected, (version, name)
    assert (FOLDER / 'before/events.csv').read_bytes() == (FOLDER / 'after/events.csv').read_bytes()
    assert '=passed' in (FOLDER / 'after/claw-checks.txt').read_text()
    result = dict(baseline=BASELINE, capture='Unity AudioListener final stereo mix, no microphone', event_timing='identical', clips={}, mixes={})
    for version in ('before', 'after'):
        rate, samples = wavfile.read(FOLDER / version / 'claw-source.wav')
        samples = samples.astype(np.float64)
        assert np.isfinite(samples).all() and .1 < abs(samples).max() < .98
        window = round(rate * .02)
        power = np.convolve(samples * samples, np.ones(window) / window, 'valid')
        crest = (int(power.argmax()) + window / 2) / rate
        result['clips'][version] = dict(seconds=len(samples)/rate, peak=float(abs(samples).max()), rms_crest_seconds=crest,
            rms=float(np.sqrt(np.mean(samples*samples))), first=float(samples[0]), last=float(samples[-1]))
        rate, mix = wavfile.read(FOLDER / version / 'mix.wav')
        assert mix.ndim == 2 and mix.shape[1] == 2 and np.isfinite(mix).all()
        assert 7.8 < len(mix)/rate < 8.3 and .05 < abs(mix).max() < .98
        result['mixes'][version] = dict(seconds=len(mix)/rate, peak=float(abs(mix).max()), sha256=hashlib.sha256((FOLDER / version / 'mix.wav').read_bytes()).hexdigest())
        excerpt = mix[:round(3.5*rate)]
        wavfile.write(FOLDER / version / 'claw-and-block.wav', rate, np.rint(np.clip(excerpt,-1,1)*32767).astype(np.int16))
    new = result['clips']['after']
    assert .30 < new['rms_crest_seconds'] < .39
    assert new['rms_crest_seconds'] > result['clips']['before']['rms_crest_seconds'] + .08
    assert abs(new['first']) < .001 and abs(new['last']) < .001
    result['animation_contact_seconds'] = .4
    result['limitations'] = 'Old source crest excludes its .94/1.02 playback pitch variation. New pitch is fixed at 1. The review isolates effects and guidance; project music and original Tiga cry are unchanged.'
    (FOLDER / 'comparison.json').write_text(json.dumps(result, ensure_ascii=False, indent=2) + '\n')
    print(json.dumps(result, ensure_ascii=False, indent=2))


if __name__ == '__main__':
    main()
