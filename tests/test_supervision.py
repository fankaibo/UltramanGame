import json
import os
from pathlib import Path
import signal
import socket
import sys
import tempfile
import time
import unittest

from vision.preview import HEADER as PREVIEW_HEADER
from vision.photo import HEADER as PHOTO_HEADER
from vision.supervision import CameraSession, RetryBudget, _Heartbeat

ROOT = Path(__file__).resolve().parents[1]


class RetryTests(unittest.TestCase):
    def test_retry_budget_is_bounded_and_ages_failures_out(self):
        budget = RetryBudget()
        self.assertEqual([1, 2, 4, None], [budget.failed(t) for t in (0, 2, 5, 10)])
        self.assertEqual(1, budget.failed(131))

    def test_only_advancing_processed_frames_count_as_liveness(self):
        heartbeat = _Heartbeat()
        for message in ({"pose_port": 1234}, {"event": "frame", "sequence": True}, [],
                        {"event": "frame", "sequence": 0}):
            heartbeat.receive(message)
        self.assertIsNone(heartbeat.time())
        heartbeat.receive({"event": "frame", "sequence": 3})
        received = heartbeat.time()
        heartbeat.receive({"event": "frame", "sequence": 3})
        heartbeat.receive({"event": "frame", "sequence": 2})
        self.assertEqual(received, heartbeat.time())


@unittest.skipUnless(os.name == "posix", "macOS/Linux inherited sockets")
class RecoveryTests(unittest.TestCase):
    def session(self, **options):
        log = self.enterContext(tempfile.TemporaryFile(mode="a+b", buffering=0))
        options.setdefault("retries", RetryBudget(delays=(.02, .04, .08)))
        return self.enterContext(CameraSession(ROOT, log, demo=True, report=lambda _: None, **options))

    def until(self, session, condition, timeout=10):
        end = time.monotonic() + timeout
        while not condition() and time.monotonic() < end:
            session.tick()
            time.sleep(.02)
        self.assertTrue(condition(), f"state={session.state}, generation={session.generation}")

    def pose(self, session):
        with socket.create_connection(("127.0.0.1", session.ports["pose_port"]), timeout=3) as connection:
            with connection.makefile("rb") as stream:
                return json.loads(stream.readline())

    def images(self, session):
        for name, header, magic, signature in (("preview", PREVIEW_HEADER, b"UGP1", b"\xff\xd8"),
                                               ("photo", PHOTO_HEADER, b"UGF1", b"\x89PNG")):
            with socket.create_connection(("127.0.0.1", session.ports[name + "_port"]), timeout=3) as connection:
                with connection.makefile("rb") as stream:
                    data = header.unpack(stream.read(header.size))
                    self.assertEqual(magic, data[0])
                    self.assertEqual(1, data[3], "demo is explicitly labelled synthetic")
                    self.assertLess(abs(time.time()*1000 - data[1]), 1500)
                    self.assertTrue(stream.read(data[2]).startswith(signature))

    def test_native_exit_reconnects_pose_preview_and_photo_on_the_same_sockets(self):
        session = self.session()
        self.until(session, lambda: session.state == "running")
        before = self.pose(session)
        self.images(session)
        ports = dict(session.ports)
        old = session.process
        old.kill()
        old.wait(timeout=3)
        session.tick()
        self.assertEqual("retrying", session.state)
        # Holding the actual bound sockets closes the port-probe/release race.
        for port in ports.values():
            with socket.socket() as contender:
                contender.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
                with self.assertRaises(OSError):
                    contender.bind(("127.0.0.1", port))
        self.until(session, lambda: session.state == "running")
        self.assertNotEqual(old.pid, session.process.pid)
        self.assertEqual(ports, session.ports)
        after = self.pose(session)
        self.assertNotEqual(before["streamId"], after["streamId"])
        self.images(session)
        child = session.process
        session.close()
        self.assertIsNotNone(child.poll())
        for port in ports.values():
            with socket.socket() as released:
                released.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
                released.bind(("127.0.0.1", port))

    def test_hung_worker_is_reaped_before_replacement(self):
        session = self.session(stall_timeout=1.4)
        self.until(session, lambda: session.state == "running")
        child = session.process
        child.send_signal(signal.SIGSTOP)
        self.until(session, lambda: session.generation == 2 and session.state == "running")
        self.assertIsNotNone(child.poll())
        self.assertTrue(self.pose(session)["tracked"])

    def test_repeated_failure_stops_after_three_restarts(self):
        session = self.session(command=[sys.executable, "-c", "raise SystemExit(7)"])
        self.until(session, lambda: session.state == "failed")
        self.assertEqual(4, session.generation)
        self.assertIsNone(session.process)
        session.tick()
        self.assertEqual(4, session.generation)

    def test_startup_without_processed_frame_times_out(self):
        session = self.session(command=[sys.executable, "-c", "import time;time.sleep(30)"],
            startup_timeout=.2, retries=RetryBudget(delays=()))
        child = session.process
        self.until(session, lambda: session.state == "failed")
        self.assertIsNotNone(child.poll())

    def test_closing_during_backoff_never_restarts_the_camera(self):
        session = self.session(command=[sys.executable, "-c", "raise SystemExit(7)"],
            retries=RetryBudget(delays=(1,)))
        self.until(session, lambda: session.state == "retrying")
        session.close()
        session.tick()
        self.assertEqual("closed", session.state)
        self.assertEqual(1, session.generation)
        self.assertIsNone(session.process)
