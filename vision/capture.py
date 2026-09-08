"""Capture continuously into one slot so inference never drains an old-frame queue."""
import threading
import time


class LatestCapture:
    def __init__(self, open_camera):
        self.open_camera = open_camera
        self.changed = threading.Condition()
        self.stopping = threading.Event()
        self.latest = None
        self.error = None
        self.revision = 0
        self.thread = threading.Thread(target=self._run, name="latest-camera-frame", daemon=True)

    def _run(self):
        camera = None
        try:
            camera = self.open_camera()
            if not camera.isOpened():
                raise RuntimeError("无法打开摄像头，请检查相机权限或其他程序是否占用。")
            while not self.stopping.is_set():
                ok, image = camera.read()
                if not ok:
                    self.stopping.wait(.03)
                    continue
                sample = (image, int(time.time()*1000), time.monotonic())
                with self.changed:
                    self.revision += 1
                    self.latest = (self.revision, *sample)
                    self.changed.notify_all()
        except Exception as exc:
            with self.changed:
                self.error = exc
                self.changed.notify_all()
        finally:
            if camera is not None:
                camera.release()

    def take(self, after=0, timeout=.2):
        with self.changed:
            self.changed.wait_for(lambda: self.revision > after or self.error is not None
                                  or self.stopping.is_set(), timeout)
            if self.error is not None:
                raise RuntimeError(str(self.error)) from self.error
            return self.latest if self.revision > after else None

    def __enter__(self):
        self.thread.start()
        return self

    def __exit__(self, *args):
        self.stopping.set()
        with self.changed:
            self.changed.notify_all()
        self.thread.join(timeout=3)
