#!/usr/bin/env python3
"""Build the temporary blue/silver Acro Smasher presentation palette.

The source mesh is still the locally supplied Dandit Truth variant.  This
script only derives a reversible colour treatment; it must not be described as
an Acro Smasher mesh replacement.
"""
from pathlib import Path
from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
SOURCE_BODY = ROOT / "unity/Assets/Resources/Characters/Geed/Textures/Geed_Tex_dandittruth.png"
SOURCE_PHOTO = ROOT / "unity/Assets/Resources/Characters/Geed/Photo.png"
OUT_BODY = SOURCE_BODY.with_name("Geed_AcroSmasherPalette.png")
OUT_PHOTO = SOURCE_PHOTO.with_name("Photo_AcroSmasherPalette.png")


def convert(source: Path, target: Path) -> None:
    image = Image.open(source).convert("RGBA")
    pixels = []
    for red, green, blue, alpha in image.getdata():
        maximum = max(red, green, blue)
        minimum = min(red, green, blue)
        red_chroma = max(0, red - max(green, blue))
        luminance = 0.28 * red + 0.60 * green + 0.12 * blue

        if red_chroma > 24 and red > 45:
            # Belial crimson -> the deep blue/cobalt panel used by Acro Smasher.
            shade = max(0.45, min(1.30, luminance / 110.0))
            pixels.append((
                int(min(255, 27 * shade)),
                int(min(255, 72 * shade + 12)),
                int(min(255, 172 * shade + 25)),
                alpha,
            ))
        elif maximum < 72 and maximum - minimum < 38:
            # Keep the folds but lift the black panels into cool silver/graphite.
            value = int(76 + luminance * 1.55)
            pixels.append((
                min(255, int(value * 0.82)),
                min(255, int(value * 0.92)),
                min(255, value),
                alpha,
            ))
        else:
            pixels.append((red, green, blue, alpha))

    image.putdata(pixels)
    target.parent.mkdir(parents=True, exist_ok=True)
    image.save(target, "PNG", optimize=True)


def main() -> None:
    convert(SOURCE_BODY, OUT_BODY)
    convert(SOURCE_PHOTO, OUT_PHOTO)
    print(f"wrote {OUT_BODY}")
    print(f"wrote {OUT_PHOTO}")


if __name__ == "__main__":
    main()
