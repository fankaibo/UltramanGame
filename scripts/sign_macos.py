"""Finalize this locally built development app and verify its ad-hoc signature."""
from pathlib import Path
import argparse
import shutil
import subprocess

ROOT=Path(__file__).resolve().parents[1]
APP=ROOT/'unity/Builds/TigaTraining.app'


def main():
    parser=argparse.ArgumentParser()
    parser.add_argument('--app',type=Path,default=APP,help='Sign an explicitly selected local build')
    app=parser.parse_args().app.expanduser().resolve()
    if not (app/'Contents/Info.plist').is_file():raise SystemExit('Build the macOS app first')
    for name in ('Tiga','Golza'):
        shutil.copyfile(ROOT/f'unity/Assets/Resources/Characters/{name}/ATTRIBUTION.txt',
                        app/f'Contents/Resources/{name}-ATTRIBUTION.txt')
    # Unity's incremental export can modify its pre-signed player dylib. Sign
    # nested code first, then seal the finished bundle. No account/certificate is used.
    for binary in sorted((app/'Contents').rglob('*.dylib'))+[app]:
        subprocess.run(['/usr/bin/codesign','--force','--sign','-','--timestamp=none',
                        '--preserve-metadata=identifier,entitlements,flags',str(binary)],check=True)
    subprocess.run(['/usr/bin/codesign','--verify','--deep','--strict',str(app)],check=True)
    print('Local development signature verified')


if __name__=='__main__':main()
