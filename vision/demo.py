"""Deterministic synthetic poses for integration checks; never opens a camera."""
import math
from types import SimpleNamespace


def landmarks_at(seconds):
    points = [SimpleNamespace(x=.5, y=.5, z=0., visibility=1.) for _ in range(33)]
    def set_point(index, x, y):
        points[index].x, points[index].y = x, y
    for index, x, y in [(0,.5,.15),(11,.65,.3),(12,.35,.3),(13,.69,.48),
                        (14,.31,.48),(15,.67,.61),(16,.33,.61),(23,.6,.68),(24,.4,.68)]:
        set_point(index,x,y)
    phase = seconds % 16
    if 2 <= phase < 4:  # transform: hands above shoulders
        set_point(13,.71,.21); set_point(15,.71,.06)
        set_point(14,.29,.21); set_point(16,.29,.06)
    elif 4 <= phase < 10:  # retract, extend, retract; three punches
        t = (phase-4) % 2
        extension = max(0., math.sin(t * math.pi))
        set_point(13,.69 + .08*extension,.48-.13*extension)
        set_point(15,.67 + .29*extension,.61-.28*extension)
    elif 10 <= phase < 12:  # shield
        set_point(13,.68,.48); set_point(15,.55,.32)
        set_point(14,.32,.48); set_point(16,.45,.32)
    elif 13 <= phase < 15:  # beam: one forearm vertical, other horizontal
        set_point(13,.54,.50); set_point(15,.54,.27)
        set_point(14,.28,.41); set_point(16,.53,.41)
    return points
