import os
from pathlib import Path
import plistlib
import shutil
import subprocess
import tempfile
import unittest

from scripts.sign_macos import validate_payload


class BuildIntegrityTests(unittest.TestCase):
    def test_optional_license_pipe_and_failed_build_never_sign(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            scripts = root/'scripts'
            scripts.mkdir()
            shutil.copyfile(Path(__file__).resolve().parents[1]/'scripts/build_macos.sh',
                            scripts/'build_macos.sh')
            (scripts/'build_native.sh').write_text('exit 0\n')
            (scripts/'generate_voice.py').write_text('')
            (scripts/'sign_macos.py').write_text('raise RuntimeError("must not sign failed build")\n')
            settings = root/'unity/ProjectSettings'
            settings.mkdir(parents=True)
            (settings/'ProjectVersion.txt').write_text('m_EditorVersion: 6000.6.0f1\n')
            editor = root/'editor'
            editor.write_text('#!/bin/bash\nprintf "%s\\n" "$@" > "$0.args"\nexit 9\n')
            editor.chmod(0o755)
            for pipe in ('', 'LicenseClient-test'):
                env = dict(os.environ, UNITY_EDITOR=str(editor), UNITY_LICENSING_IPC=pipe,
                           ULTRAMAN_BUILD_OUTPUT=str(root/'candidate.app'))
                result = subprocess.run(['bash', str(scripts/'build_macos.sh'), '--release'],
                                        env=env, capture_output=True)
                self.assertEqual(result.returncode, 9, result.stderr.decode())
                args = Path(str(editor)+'.args').read_text().splitlines()
                self.assertEqual('-licensingIpc' in args, bool(pipe))
                if pipe:
                    self.assertEqual(args[args.index('-licensingIpc')+1], pipe)
                self.assertFalse((root/'candidate.app').exists())

    def test_empty_or_truncated_assembly_is_rejected_before_signing(self):
        with tempfile.TemporaryDirectory() as tmp:
            app = Path(tmp)/'Game.app'
            contents = app/'Contents'
            (contents/'MacOS').mkdir(parents=True)
            data = contents/'Resources/Data'
            (data/'Managed').mkdir(parents=True)
            (contents/'Info.plist').write_bytes(plistlib.dumps({'CFBundleExecutable': 'Game'}))
            (contents/'MacOS/Game').write_bytes(b'player')
            (data/'resources.assets').write_bytes(b'resources')
            dll = data/'Managed/Assembly-CSharp.dll'
            for invalid in (b'', b'MZ', b'\0'*1024):
                dll.write_bytes(invalid)
                with self.assertRaises(ValueError):
                    validate_payload(app)
            dll.write_bytes(b'MZ'+b'\0'*1024)
            validate_payload(app)  # Basic payload check, not full CLR validation.
            (data/'resources.assets').write_bytes(b'')
            with self.assertRaises(ValueError):
                validate_payload(app)

    def test_second_plugin_link_failure_preserves_both_existing_plugins(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            (root/'scripts').mkdir()
            shutil.copyfile(Path(__file__).resolve().parents[1]/'scripts/build_native.sh',
                            root/'scripts/build_native.sh')
            plugins = root/'unity/Assets/Plugins/macOS'
            plugins.mkdir(parents=True)
            for name in ('MusicPicker', 'Photo'):
                (plugins/f'libUltraman{name}.dylib').write_bytes(b'working-'+name.encode())
            commands = root/'bin'
            commands.mkdir()
            fake = commands/'xcrun'
            fake.write_text('''#!/bin/bash
fail=0
for arg in "$@"; do
  [ "$arg" = "native/PersonCutout.mm" ] && fail=1
done
out="${@: -1}"
printf partial > "$out"
exit "$fail"
''')
            fake.chmod(0o755)
            env = dict(os.environ, PATH=str(commands)+os.pathsep+os.environ['PATH'])
            result = subprocess.run(['bash', str(root/'scripts/build_native.sh')], env=env,
                                    capture_output=True)
            self.assertNotEqual(result.returncode, 0)
            for name in ('MusicPicker', 'Photo'):
                self.assertEqual((plugins/f'libUltraman{name}.dylib').read_bytes(),
                                 b'working-'+name.encode())
