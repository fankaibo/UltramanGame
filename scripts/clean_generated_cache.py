"""Dry-run by default: prune numbered render frames and download caches only.

Keep five representative frames per sequence, explicitly documented PNGs,
videos, metrics, assets, installed tools and the current app. Named screenshots
are retained unless historical native screenshot trimming is explicitly enabled.
The JSON manifest records every removed path; full image sequences need rerendering.
"""
import argparse
from collections import defaultdict
from datetime import datetime
import json
from pathlib import Path
import re
import time

ROOT = Path(__file__).resolve().parents[1]
CACHES = ('.cache/uv', '.cache/voice-uv', '.cache/cpu-wheels')


def plan(native_before=None):
    references = set()
    for doc in [ROOT / 'README.md', *(ROOT / 'docs').glob('*.md')]:
        references.update(re.findall(r'artifacts/[\w./-]+\.png', doc.read_text()))
    groups = defaultdict(list)
    for p in (ROOT / 'artifacts').rglob('*.png'):
        if (not p.is_symlink() and p.is_file()
                and re.fullmatch(r'(?:frame-)?\d+\.png', p.name)
                and 'reference' not in str(p.relative_to(ROOT)).lower()):
            groups[p.parent].append(p)
    selected = []
    for files in groups.values():
        if len(files) < 20:
            continue
        files.sort(key=lambda p: int(re.search(r'\d+', p.stem)[0]))
        keep = {files[round((len(files) - 1) * i / 4)] for i in range(5)}
        selected.extend((p, 'render-frame') for p in files
                        if p not in keep and str(p.relative_to(ROOT)) not in references)
    # Opt-in retention for historical automated player screenshots. Never touch
    # source/reference pictures, photo evidence, or any recent test directory.
    if native_before is not None:
        for directory in (ROOT / 'artifacts').rglob('native'):
            if not directory.is_dir() or directory.is_symlink():
                continue
            if (directory / '.retained-screenshots.json').exists():
                continue
            files = sorted(p for p in directory.glob('*.png')
                           if p.is_file() and not p.is_symlink())
            if len(files) < 20 or any(p.stat().st_mtime >= native_before for p in files):
                continue
            if 'reference' in str(directory.relative_to(ROOT)).lower():
                continue
            keep = {files[round((len(files) - 1) * i / 4)] for i in range(5)}
            keyframes = {'battle-entry.png', 'hero-recovered.png', 'beam-contact.png',
                         'monster-rush-left.png', 'monster-rush-right.png',
                         'defeat-settling.png', 'victory.png', 'guard-contact.png'}
            selected.extend((p, 'historical-native-screenshot') for p in files
                            if p not in keep and p.name not in keyframes
                            and 'photo' not in p.name.lower()
                            and str(p.relative_to(ROOT)) not in references)
    for cache in CACHES:
        base = ROOT / cache
        if base.is_symlink():
            continue
        selected.extend((p, 'download-cache') for p in base.rglob('*')
                        if p.is_file() and not p.is_symlink())
    items = []
    for p, kind in dict(selected).items():
        if not p.resolve().is_relative_to(ROOT):
            raise ValueError(f'Outside project: {p}')
        stat = p.stat()
        items.append(dict(path=str(p.relative_to(ROOT)), kind=kind,
                          bytes=stat.st_size, mtime_ns=stat.st_mtime_ns))
    return items


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--apply', action='store_true')
    parser.add_argument('--native-before', type=lambda v: datetime.strptime(v, '%Y-%m-%d').timestamp(),
                        help='Opt-in: trim native test screenshots older than this local date; retain photos, keyframes and documented images')
    parser.add_argument('--manifest', type=Path,
                        default=ROOT / 'logs' / ('cache-cleanup-' + time.strftime('%Y%m%d-%H%M%S') + '.json'))
    args = parser.parse_args()
    items = plan(args.native_before)
    report = dict(mode='apply' if args.apply else 'dry-run', files=len(items),
                  bytes=sum(i['bytes'] for i in items), removed=0, removed_bytes=0, items=items)
    args.manifest.parent.mkdir(parents=True, exist_ok=True)
    args.manifest.write_text(json.dumps(report, ensure_ascii=False, indent=2) + '\n')
    if args.apply:
        try:
            for item in items:
                p = ROOT / item['path']
                stat = p.stat()
                if p.is_symlink() or stat.st_size != item['bytes'] or stat.st_mtime_ns != item['mtime_ns']:
                    raise RuntimeError(f'File changed during cleanup: {p}')
                p.unlink()
                item['removed'] = True
                report['removed'] += 1
                report['removed_bytes'] += item['bytes']
        finally:
            args.manifest.write_text(json.dumps(report, ensure_ascii=False, indent=2) + '\n')
            # Persist the selection boundary: running cleanup again must not
            # select a new set of five from an already reduced directory.
            trimmed = {Path(item['path']).parent for item in items
                       if item.get('removed') and item['kind'] == 'historical-native-screenshot'}
            for relative in trimmed:
                directory = ROOT / relative
                marker = dict(manifest=str(args.manifest.resolve()),
                              retained=sorted(p.name for p in directory.glob('*.png')))
                (directory / '.retained-screenshots.json').write_text(json.dumps(marker, indent=2) + '\n')
    print(json.dumps({k: v for k, v in report.items() if k != 'items'}, ensure_ascii=False))
    print('Manifest:', args.manifest)


if __name__ == '__main__':
    main()
