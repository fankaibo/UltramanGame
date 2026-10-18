"""Finalize this locally built development app and verify its ad-hoc signature."""
from pathlib import Path
import argparse
import shutil
import subprocess
import plistlib

ROOT=Path(__file__).resolve().parents[1]
APP=ROOT/'unity/Builds/TigaTraining.app'


def validate_payload(app):
    """Reject an incomplete export before replacing any existing signature."""
    with (app/'Contents/Info.plist').open('rb') as stream:
        executable=plistlib.load(stream)['CFBundleExecutable']
    if not executable or Path(executable).name!=executable:
        raise ValueError('Invalid player executable name')
    for file in (app/'Contents/MacOS'/executable,
                 app/'Contents/Resources/Data/resources.assets'):
        if not file.is_file() or file.stat().st_size==0:
            raise ValueError('Missing or empty player payload: '+str(file))
    assembly=app/'Contents/Resources/Data/Managed/Assembly-CSharp.dll'
    with assembly.open('rb') as stream:
        if stream.read(2)!=b'MZ' or assembly.stat().st_size<512:
            raise ValueError('Missing or truncated game assembly: '+str(assembly))


def main():
    parser=argparse.ArgumentParser()
    parser.add_argument('--app',type=Path,default=APP,help='Sign an explicitly selected local build')
    app=parser.parse_args().app.expanduser().resolve()
    if not (app/'Contents/Info.plist').is_file():raise SystemExit('Build the macOS app first')
    validate_payload(app)
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
