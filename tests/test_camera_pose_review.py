import json
from pathlib import Path
import tempfile
import unittest

import cv2
import numpy as np

from scripts.review_camera_pose import context_at, joint_metrics, read_segments, source_frames
from vision.demo import landmarks_at
from vision.protocol import FrameFactory


class CameraPoseReviewTests(unittest.TestCase):
    def test_low_confidence_cannot_be_counted_as_verified_high_hands(self):
        points = landmarks_at(2.5)
        frame = FrameFactory().make(points, 100000)
        self.assertIs(joint_metrics(frame)['both_wrists_above_shoulders'], True)
        points[15].visibility = .2
        metrics = joint_metrics(FrameFactory().make(points, 100000))
        self.assertIsNone(metrics['both_wrists_above_shoulders'])
        self.assertFalse(metrics['wrists']['L']['reliable_at_055'])

    def test_no_person_and_out_of_frame_are_not_low_hands(self):
        self.assertIsNone(joint_metrics(FrameFactory().make())['both_wrists_above_shoulders'])
        points = landmarks_at(2.5)
        points[16].x = 1.2
        self.assertIsNone(joint_metrics(FrameFactory().make(points))['both_wrists_above_shoulders'])

    def test_height_uses_each_anatomical_shoulder_and_reports_signed_depth(self):
        points = landmarks_at(0)
        points[15].y = points[11].y-.15
        points[15].z = -.30
        metrics = joint_metrics(FrameFactory().make(points))
        self.assertIs(metrics['both_wrists_above_shoulders'], False)
        self.assertAlmostEqual(metrics['wrists']['L']['height_shoulder_widths'], .5)
        self.assertAlmostEqual(metrics['wrists']['L']['depth_shoulder_widths'], 1)

    def test_context_changes_on_media_clock_without_overlap(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory)/'segments.json'
            path.write_text(json.dumps([dict(start=1, end=3, context='charged')]))
            segments = read_segments(path)
            self.assertEqual('battle', context_at(.99, 'battle', segments))
            self.assertEqual('charged', context_at(1, 'battle', segments))
            self.assertEqual('battle', context_at(3, 'battle', segments))
            for rows in ([dict(start=2, end=1, context='battle')],
                         [dict(start=0, end=3, context='battle'), dict(start=2, end=4, context='charged')],
                         [dict(start=0, end=3, context='unknown')]):
                path.write_text(json.dumps(rows))
                with self.assertRaises(ValueError):
                    read_segments(path)

    def test_still_repetition_is_explicit_and_mirror_is_applied_before_inference(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory)/'image.png'
            image = np.zeros((64, 96, 3), np.uint8)
            image[:, :16] = 255
            cv2.imwrite(str(path), image)
            frames = list(source_frames(path, 0, .1, True))
            self.assertEqual([0, 33, 67], [row[1] for row in frames])
            self.assertTrue(np.array_equal(frames[0][2], cv2.flip(image, 1)))

    def test_video_interval_preserves_source_indices_and_no_invented_frames(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory)/'fixture.avi'
            writer = cv2.VideoWriter(str(path), cv2.VideoWriter_fourcc(*'MJPG'), 10, (96, 64))
            self.assertTrue(writer.isOpened())
            for i in range(6):
                writer.write(np.full((64, 96, 3), i*30, np.uint8))
            writer.release()
            frames = list(source_frames(path, .2, .3))
            self.assertEqual([2, 3, 4], [row[0] for row in frames])
            self.assertEqual([200, 300, 400], [row[1] for row in frames])
            self.assertEqual([60, 90, 120], [round(float(row[2].mean())) for row in frames])


if __name__ == '__main__':
    unittest.main()
