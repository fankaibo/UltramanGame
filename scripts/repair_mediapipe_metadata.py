"""Correct the verified 0.10.21 CPython 3.12 universal2 wheel's Intel-only tag.

The official wheel contains both ARM64 and Intel code. Only WHEEL and its
RECORD entry change; the native library must match the upstream binary exactly.
"""
import base64
import csv
import hashlib
from importlib.metadata import distribution
import io
from pathlib import Path
import platform
import sys

OLD_TAG = b"Tag: cp312-cp312-macosx_14_0_x86_64\n"
NEW_TAG = b"Tag: cp312-cp312-macosx_14_0_universal2\n"
ORIGINAL_WHEEL_HASH = "6mA6PC9UJ1VZbVkJNsihSnihqFnWW7LzMTrLmXjyy-8"
BINDING_HASH = "31f0b13e0f894ab9bf6609306edf099e6e359a130f8267d326863873cffbcfd8"


def digest(data):
    return base64.urlsafe_b64encode(hashlib.sha256(data).digest()).decode().rstrip("=")


def corrected(data):
    if digest(data.replace(NEW_TAG, OLD_TAG)) != ORIGINAL_WHEEL_HASH:
        raise RuntimeError("Unexpected MediaPipe WHEEL metadata; refusing to change it")
    return data.replace(OLD_TAG, NEW_TAG)


def main():
    if sys.platform != "darwin" or platform.machine() != "arm64":
        return
    dist = distribution("mediapipe")
    if dist.version != "0.10.21" or sys.version_info[:2] != (3, 12):
        raise RuntimeError("This metadata correction is only for MediaPipe 0.10.21 / Python 3.12")
    binary = Path(dist.locate_file("mediapipe/python/_framework_bindings.cpython-312-darwin.so"))
    if hashlib.sha256(binary.read_bytes()).hexdigest() != BINDING_HASH:
        raise RuntimeError("Unrecognised MediaPipe binary; refusing to change platform metadata")
    wheel_entry = next(f for f in dist.files if str(f).endswith(".dist-info/WHEEL"))
    wheel = Path(dist.locate_file(wheel_entry))
    original = wheel.read_bytes()
    updated = corrected(original)
    if original == updated:
        print("MediaPipe universal2 metadata already correct")
        return
    record = wheel.with_name("RECORD")
    rows = list(csv.reader(io.StringIO(record.read_text())))
    matches = [row for row in rows if row[0] == str(wheel_entry)]
    if len(matches) != 1 or matches[0][1:] != ["sha256=" + digest(original), str(len(original))]:
        raise RuntimeError("MediaPipe RECORD does not match WHEEL; refusing to change it")
    matches[0][1:] = ["sha256=" + digest(updated), str(len(updated))]
    output = io.StringIO(newline="")
    csv.writer(output).writerows(rows)
    wheel.with_suffix(".tmp").write_bytes(updated)
    record.with_suffix(".tmp").write_text(output.getvalue())
    wheel.with_suffix(".tmp").replace(wheel)
    record.with_suffix(".tmp").replace(record)
    print("Corrected official MediaPipe universal2 platform metadata; native bytes unchanged")


if __name__ == "__main__":
    main()
