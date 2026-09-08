"""macOS Vision person mask, loaded only during an explicit photo session."""
import ctypes
from pathlib import Path


class PersonSegmenter:
    def __init__(self):
        library = Path(__file__).resolve().parents[1] / 'unity/Assets/Plugins/macOS/libUltramanPhoto.dylib'
        self.native = ctypes.CDLL(str(library))
        self.native.TigaCreatePersonCutout.restype = ctypes.c_void_p
        self.native.TigaDestroyPersonCutout.argtypes = [ctypes.c_void_p]
        self.native.TigaPersonMask.argtypes = [ctypes.c_void_p, ctypes.c_void_p, ctypes.c_int, ctypes.c_int, ctypes.c_void_p]
        self.native.TigaPersonMask.restype = ctypes.c_int
        self.context = self.native.TigaCreatePersonCutout()
        if not self.context:
            raise RuntimeError('Person cutout needs macOS 12 or newer')

    def mask(self, image):
        import cv2
        import numpy as np
        height, width = image.shape[:2]
        bgra = cv2.cvtColor(image, cv2.COLOR_BGR2BGRA)
        output = np.empty((height, width), np.uint8)
        status = self.native.TigaPersonMask(self.context, bgra.ctypes.data, width, height, output.ctypes.data)
        if status:
            raise RuntimeError('Person segmentation failed: ' + str(status))
        return cv2.GaussianBlur(output, (3, 3), 0).astype(np.float32) / 255

    def close(self):
        if self.context:
            self.native.TigaDestroyPersonCutout(self.context)
            self.context = None
