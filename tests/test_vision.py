import json
import math
import socket
import unittest

from vision.bridge import PoseBridge
from vision.demo import landmarks_at
from vision.protocol import FrameFactory, encode, MAX_LINE_BYTES


class ProtocolTests(unittest.TestCase):
    def test_frame_is_bounded_and_no_pixels(self):
        line=encode(FrameFactory().make(landmarks_at(0),1000))
        self.assertLess(len(line),MAX_LINE_BYTES)
        frame=json.loads(line)
        self.assertTrue(frame['tracked'])
        self.assertEqual(33,len(frame['points']))
        self.assertEqual({'schema','streamId','sequence','capturedMs','tracked','points'},set(frame))

    def test_no_person_and_incomplete_pose_are_explicit(self):
        f=FrameFactory()
        for points in [None,[],landmarks_at(0)[:5]]:
            self.assertFalse(f.make(points)['tracked'])

    def test_nan_invalidates_whole_pose(self):
        points=landmarks_at(0);points[15].x=math.nan
        result=FrameFactory().make(points)
        self.assertFalse(result['tracked'])
        encode(result)

    def test_sequence_increases_and_stream_changes_on_restart(self):
        a,b=FrameFactory(),FrameFactory()
        self.assertNotEqual(a.stream_id,b.stream_id)
        self.assertEqual(1,a.make()['sequence'])
        self.assertEqual(2,a.make()['sequence'])

    def test_late_subscriber_receives_latest_only(self):
        with PoseBridge(0) as bridge:
            f=FrameFactory()
            for i in range(100):bridge.publish(f.make(landmarks_at(0)))
            with socket.create_connection(bridge.address,timeout=2) as sock:
                frame=json.loads(sock.makefile('rb').readline())
                self.assertEqual(100,frame['sequence'])
                self.assertEqual('127.0.0.1',bridge.address[0])

    def test_subscribers_receive_loss_and_service_releases_port(self):
        with PoseBridge(0) as bridge:
            address=bridge.address
            f=FrameFactory()
            with socket.create_connection(address,timeout=2) as one, socket.create_connection(address,timeout=2) as two:
                bridge.publish(f.make())
                for sock in [one,two]:self.assertFalse(json.loads(sock.makefile('rb').readline())['tracked'])
        with PoseBridge(address[1]) as restarted:
            self.assertEqual(address,restarted.address)

    def test_bad_frame_does_not_replace_latest(self):
        with PoseBridge(0) as bridge:
            bridge.publish(FrameFactory().make())
            previous=bridge.latest
            with self.assertRaises(ValueError):bridge.publish({'invalid':float('nan')})
            self.assertEqual(previous,bridge.latest)


if __name__=='__main__':unittest.main()
