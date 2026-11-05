"""Pack supplied UI PNG frames; use GIF total duration as sequence timing reference."""
from pathlib import Path
import hashlib
import json
import math
import shutil
from PIL import Image

source = Path('H:/PolyU/PM/WindSinger素材/11.2/WindSinger_UI_IP_GIF_PNG')
target = Path('Assets/Art/UI/Animation')
target.mkdir(parents=True, exist_ok=True)
clips = []
for folder in sorted((source / 'png').iterdir()):
    if not folder.is_dir():
        continue
    paths = sorted(folder.glob('frame_*.png'))
    gif_path = source / 'gif' / (folder.name + '.gif')
    with Image.open(gif_path) as gif:
        total_ms = 0
        for i in range(gif.n_frames):
            gif.seek(i)
            total_ms += gif.info.get('duration', 100)
    with Image.open(paths[0]) as first:
        width, height = 384, round(384 * first.height / first.width)
    padding = 2
    count = len(paths) * 2
    columns = math.ceil(math.sqrt(count * (height + 4) / (width + 4)))
    rows = math.ceil(count / columns)
    atlas = Image.new('RGBA', (columns * (width + 4), rows * (height + 4)))
    digest = hashlib.sha256(b'ui-premultiplied-interpolation-20fps-v1')
    frames = []
    for i, path in enumerate(paths):
        digest.update(path.read_bytes())
        with Image.open(path) as original:
            frame = original.convert('RGBA').resize((width, height), Image.Resampling.LANCZOS)
            frames.append(frame)
    for i, frame in enumerate(frames):
        # Premultiply alpha to keep transparent edges clean; include the loop boundary.
        middle = Image.blend(frame.convert('RGBa'), frames[(i + 1) % len(frames)].convert('RGBa'), .5).convert('RGBA')
        for index, output in ((i * 2, frame), (i * 2 + 1, middle)):
            atlas.paste(output, ((index % columns) * (width + 4) + 2, (index // columns) * (height + 4) + 2))
    assert max(atlas.size) <= 8192
    atlas.save(target / (folder.name + '.png'))
    clips.append(dict(name=folder.name, sourceFile=str(folder), sourceSha256=digest.hexdigest(),
        atlas=folder.name + '.png', frameWidth=width, frameHeight=height, padding=2,
        columns=columns, rows=rows, durationsMs=[total_ms // count] * count))
    assert total_ms % count == 0 and total_ms // count == 50
    print(folder.name, count, total_ms, atlas.size)
(target / 'UIAnimationManifest.json').write_text(json.dumps(dict(clips=clips), indent=2, ensure_ascii=False), encoding='utf-8')
background = Path('H:/PolyU/PM/WindSinger素材/11.2/背景.png')
destination = Path('Assets/Art/Background/SkyPanorama.png')
if background.read_bytes() != destination.read_bytes():
    shutil.copyfile(background, destination)
