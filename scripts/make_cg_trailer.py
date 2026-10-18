"""Make a short showcase cut from a passed Unity cinematic render.

The source movie is rendered by CinematicReview, so this script only edits
already verified game frames and event-aligned audio; it does not invent shots.
"""
from __future__ import annotations

import argparse
import hashlib
import json
import shutil
import subprocess
from pathlib import Path


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--source", type=Path, default=Path("artifacts/cinematic-combat/full-battle.mp4"))
    parser.add_argument("--output", type=Path, default=Path("artifacts/cg-trailer/ultraman-cg-trailer.mp4"))
    parser.add_argument("--ffmpeg", default="ffmpeg")
    args = parser.parse_args()
    source = args.source.resolve()
    output = args.output.resolve()
    if not source.is_file():
        parser.error(f"missing verified Unity source movie: {source}")
    output.parent.mkdir(parents=True, exist_ok=True)
    if source == output:
        parser.error('source and trailer output must differ')

    # Establishing shot, monster attack/contact, beam close-up and victory.
    # These cuts preserve complete action beats and total about 22 seconds.
    cuts = [(0, 6), (14.6, 19.1), (40.8, 47.2), (56.6, 61.57)]
    pieces: list[str] = []
    for index, (start, end) in enumerate(cuts):
        pieces.extend([
            f"[0:v]trim=start={start}:end={end},setpts=PTS-STARTPTS[v{index}]",
            f"[0:a]atrim=start={start}:end={end},asetpts=PTS-STARTPTS[a{index}]",
        ])
    video_audio = "".join(f"[v{i}][a{i}]" for i in range(len(cuts)))
    pieces.append(video_audio + f"concat=n={len(cuts)}:v=1:a=1[v][a]")
    subprocess.run([
        args.ffmpeg, "-hide_banner", "-loglevel", "error", "-y", "-i", str(source),
        "-filter_complex", ";".join(pieces), "-map", "[v]", "-map", "[a]",
        "-c:v", "libx264", "-crf", "18", "-pix_fmt", "yuv420p",
        "-c:a", "aac", "-b:a", "192k", "-movflags", "+faststart", str(output),
    ], check=True)
    # Publish the full movie and its trailer from the same source. Previously
    # the default silently reused an older copy in cg-trailer after a rerender.
    full_movie = output.parent / 'full-battle.mp4'
    if full_movie.resolve() != source:
        shutil.copy2(source, full_movie)
    (output.parent / 'source.json').write_text(json.dumps({
        'source': str(source), 'source_sha256': hashlib.sha256(source.read_bytes()).hexdigest(),
        'trailer': str(output), 'cuts': cuts,
    }, indent=2) + '\n')
    print(f"{output} ({sum(end - start for start, end in cuts):.2f}s)")


if __name__ == "__main__":
    main()
