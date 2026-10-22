"""Decode source GIFs to padded Unity atlases; preserve composited frames and timing.

Usage: python Tools/convert_character_gifs.py SOURCE_DIRECTORY
Requires Pillow. Run from the project root. Originals are never modified.
"""
import hashlib
import json
import math
from pathlib import Path
import sys
from PIL import Image


def convert(source: Path, destination: Path):
    return convert_files(sorted(source.glob("*.gif")), destination)


def convert_files(paths, destination: Path, manifest_name="CharacterManifest.json"):
    destination.mkdir(parents=True, exist_ok=True)
    clips = []
    for path in paths:
        with Image.open(path) as gif:
            width = 256
            height = round(width * gif.height / gif.width)
            padding = 2
            cell_w, cell_h = width + padding * 2, height + padding * 2
            columns = math.ceil(math.sqrt(gif.n_frames * cell_h / cell_w))
            rows = math.ceil(gif.n_frames / columns)
            # Match decoded transparent frame edges; opaque chroma padding would leak under bilinear filtering.
            atlas = Image.new("RGBA", (columns * cell_w, rows * cell_h), (0, 0, 0, 0))
            durations = []
            for index in range(gif.n_frames):
                gif.seek(index)
                # Pillow applies GIF disposal and partial frame compositing on seek.
                frame = gif.convert("RGBA").resize((width, height), Image.Resampling.LANCZOS)
                atlas.paste(frame, ((index % columns) * cell_w + padding,
                                    (index // columns) * cell_h + padding))
                durations.append(max(10, gif.info.get("duration", 50)))
            name = path.stem
            atlas.save(destination / (name + ".png"), optimize=True)
            clips.append(dict(name=name, sourceFile=path.name,
                              sourceSha256=hashlib.sha256(path.read_bytes()).hexdigest(),
                              atlas=name + ".png", frameWidth=width, frameHeight=height,
                              padding=padding, columns=columns, rows=rows, durationsMs=durations))
            print(f"{name}: {gif.n_frames} frames, {sum(durations)/1000:g}s, atlas {atlas.size}")
    (destination / manifest_name).write_text(
        json.dumps(dict(clips=clips), ensure_ascii=False, indent=2), encoding="utf-8")


if __name__ == "__main__":
    convert(Path(sys.argv[1]), Path("Assets/Art/Character"))
