"""Build a labeled comparison from actual player stage captures, not mockups."""
import hashlib
import json
from pathlib import Path
from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / 'artifacts/a173-finisher-hud'


def main():
    canvas = Image.new('RGB', (1920, 1136), (10, 18, 30))
    draw = ImageDraw.Draw(canvas)
    inputs = []
    for row, stage in enumerate(('beam-contact', 'beam-sustain')):
        for col, version in enumerate(('a172-beam-volume', 'a173-finisher-hud')):
            path = ROOT / 'artifacts' / version / 'guided/native' / (stage + '.png')
            with Image.open(path) as capture:
                assert capture.size == (1920, 1080)
                canvas.paste(capture.convert('RGB').resize((960, 540), Image.Resampling.LANCZOS), (col * 960, row * 568 + 28))
            draw.text((col * 960 + 14, row * 568 + 8), ('BEFORE' if col == 0 else 'AFTER') + '  |  ' + stage, fill=(225, 240, 255))
            inputs.append({'path': str(path.relative_to(ROOT)), 'sha256': hashlib.sha256(path.read_bytes()).hexdigest()})
    canvas.save(OUT / 'player-before-after.jpg', quality=94)
    (OUT / 'comparison.json').write_text(json.dumps({'inputs': inputs, 'size': list(canvas.size),
        'note': 'Matching event stages from two native synthetic-input runs; wall-clock and exact animation frames can differ. No gameplay pixels replaced or retouched.'}, indent=2) + '\n')
    print('Saved two-stage native HUD comparison')


if __name__ == '__main__':
    main()
