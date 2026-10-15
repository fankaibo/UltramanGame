"""Isolate native person segmentation from camera capture and pose inference.

The main loop publishes into one replaceable slot. Only this background thread
waits for its spawned worker; a blocked native request can be killed independently.
Images remain in process memory and are never written to disk.
"""
import multiprocessing as mp
import threading
import time
import sys


def _segment_worker(connection, factory=None):
    if factory is None:
        from .segmentation import PersonSegmenter
        factory = PersonSegmenter
    model = None
    try:
        model = factory()
        connection.send('ready')
        while True:
            image, stamp = connection.recv()
            mask = model.mask(image)
            connection.send((mask, stamp))
    except (EOFError, BrokenPipeError, OSError):
        pass
    finally:
        if model is not None:
            model.close()
        connection.close()


class AsyncPersonCutout:
    def __init__(self, *, factory=None, request_timeout=3, startup_timeout=10, retry_delay=2):
        self.factory = factory
        self.request_timeout, self.startup_timeout, self.retry_delay = request_timeout, startup_timeout, retry_delay
        self.changed = threading.Condition()
        self.stopping = threading.Event()
        self.pending = self.latest = None
        self.process = None
        self.generations = self.failures = 0
        self.thread = threading.Thread(target=self._run, name='isolated-person-cutout', daemon=True)
        self.thread.start()

    def submit(self, image, captured_ms):
        with self.changed:
            self.pending = (image, captured_ms)
            self.changed.notify()

    def take_latest(self):
        with self.changed:
            result, self.latest = self.latest, None
            return result

    def _run(self):
        context = mp.get_context('spawn')
        while not self.stopping.is_set():
            connection = process = None
            try:
                connection, child = context.Pipe()
                process = context.Process(target=_segment_worker, args=(child, self.factory), daemon=True)
                self.process = process
                process.start()
                child.close()
                self.generations += 1
                print('[PhotoWorker] starting generation=' + str(self.generations), file=sys.stderr, flush=True)
                if not connection.poll(self.startup_timeout) or connection.recv() != 'ready':
                    raise TimeoutError('native initialization')
                first = True
                while not self.stopping.is_set():
                    with self.changed:
                        self.changed.wait_for(lambda: self.pending is not None or self.stopping.is_set(), timeout=.2)
                        sample, self.pending = self.pending, None
                    if sample is None:
                        continue
                    image, stamp = sample
                    connection.send(sample)
                    # The first Vision request may compile its model. This never pauses capture.
                    if not connection.poll(self.startup_timeout if first else self.request_timeout):
                        raise TimeoutError('native segmentation')
                    mask, result_stamp = connection.recv()
                    first = False
                    if result_stamp != stamp:
                        raise ValueError('cutout timestamp mismatch')
                    with self.changed:
                        self.latest = (image, mask, stamp)
            except (OSError, EOFError, ValueError, TimeoutError) as error:
                if not self.stopping.is_set():
                    self.failures += 1
                    print('[PhotoWorker] restarting reason=' + type(error).__name__ + '; pose and preview remain active', file=sys.stderr, flush=True)
            finally:
                if process is not None and process.pid is not None:
                    if process.is_alive():
                        process.terminate()
                    process.join(timeout=.4)
                    if process.is_alive():
                        process.kill()
                        process.join(timeout=.4)
                if connection is not None:
                    connection.close()
                self.process = None
            self.stopping.wait(self.retry_delay)

    def close(self):
        self.stopping.set()
        with self.changed:
            self.pending = self.latest = None
            self.changed.notify_all()
        process = self.process
        if process is not None and process.pid is not None and process.is_alive():
            process.terminate()
        self.thread.join(timeout=2)
