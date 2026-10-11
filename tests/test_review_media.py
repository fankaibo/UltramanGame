"""A slow image encoder must not manufacture gaps or fresh timestamps for poses."""
import threading
import unittest
from scripts.review_media import LatestReviewMedia


class ReviewMediaTests(unittest.TestCase):
    def test_blocked_encoder_keeps_only_latest_original_timestamp(self):
        started, release, finished = threading.Event(), threading.Event(), threading.Event()
        received = []

        def consume(request):
            received.append(request)
            if len(received) == 1:
                started.set()
                release.wait(2)
            else:
                finished.set()

        producer = LatestReviewMedia(consume, 'test bounded media')
        try:
            producer.publish(('first', 101))
            self.assertTrue(started.wait(1))
            for stamp in range(102, 202):
                producer.publish(('frame', stamp))
            # Publishing is independent even though encoding is still blocked.
            self.assertEqual([('first', 101)], received)
            release.set()
            self.assertTrue(finished.wait(1))
        finally:
            release.set()
            producer.close()
        self.assertEqual([('first', 101), ('frame', 201)], received)

    def test_encoding_failure_is_reported_and_worker_stops(self):
        failed = threading.Event()

        def consume(request):
            failed.set()
            raise ValueError('test encoding error')

        producer = LatestReviewMedia(consume, 'test failed media')
        producer.publish(101)
        self.assertTrue(failed.wait(1))
        with self.assertRaisesRegex(RuntimeError, 'producer failed'):
            producer.close()
        self.assertFalse(producer._worker.is_alive())
