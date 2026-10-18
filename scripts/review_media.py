"""Bounded image producers for synthetic player reviews; never generate pose input."""
import threading


class LatestReviewMedia:
    """At most one pending image request; its original capture time stays intact."""
    def __init__(self, consume, name):
        self._consume = consume
        self._condition = threading.Condition()
        self._latest = None
        self._stopping = False
        self._error = None
        self._worker = threading.Thread(target=self._run, name=name, daemon=True)
        self._worker.start()

    def publish(self, request):
        with self._condition:
            self._check_error()
            if self._stopping:
                raise RuntimeError("Review media producer already closed")
            self._latest = request
            self._condition.notify()

    def _check_error(self):
        if self._error is not None:
            raise RuntimeError("Synthetic review media producer failed") from self._error

    def _run(self):
        while True:
            with self._condition:
                self._condition.wait_for(lambda: self._stopping or self._latest is not None)
                if self._stopping:
                    return
                request, self._latest = self._latest, None
            try:
                self._consume(request)
            except Exception as error:
                with self._condition:
                    self._error = error
                return

    def close(self):
        with self._condition:
            self._stopping = True
            self._latest = None
            self._condition.notify()
        self._worker.join(timeout=5)
        if self._worker.is_alive():
            raise RuntimeError("Synthetic review media producer did not stop")
        self._check_error()
