"""Resize supplied foreground frames, preserving straight alpha and the complete canvas."""
import concurrent.futures
import hashlib
import json
import re
import uuid
import sys
from pathlib import Path
from PIL import Image
from repair_foreground_edges import clean_edges, VERSION

SOURCE = Path('H:/PolyU/PM/WindSinger素材/11.2/hc1/hc1')
TARGET = Path('Assets/Art/Background/AnimatedForeground')
TARGET.mkdir(parents=True, exist_ok=True)
if '--pad-existing' in sys.argv:
    manifest_path = TARGET / 'Manifest.json'
    manifest = json.loads(manifest_path.read_text(encoding='utf-8'))
    def pad(entry):
        path = TARGET / entry['file']
        with Image.open(path) as image:
            if image.height == 213:
                output = Image.new('RGBA', (4096, 216))
                output.paste(image, (0, 0))
                output.save(path, compress_level=6)
        entry['outputBytes'] = path.stat().st_size
        return entry
    with concurrent.futures.ThreadPoolExecutor(max_workers=4) as pool:
        manifest['frames'] = list(pool.map(pad, manifest['frames']))
    manifest.update(height=216, contentHeight=213, outputBytes=sum(entry['outputBytes'] for entry in manifest['frames']))
    manifest_path.write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding='utf-8')
    print(manifest['outputBytes'])
    sys.exit(0)
paths = sorted(SOURCE.glob('*.png'))
assert len(paths) == 150

def convert(item):
    index, path = item
    data = path.read_bytes()
    with Image.open(path) as source:
        original_size = source.size
        assert original_size == (30000, 1562) and source.mode == 'RGBA'
        size = (4096, round(4096 * source.height / source.width))
        # Pillow's RGBA resize filters in premultiplied alpha, retaining transparent edges.
        content = source.resize(size, Image.Resampling.LANCZOS)
        content, repaired = clean_edges(content)
        frame = Image.new('RGBA', (4096, 216))
        frame.paste(content, (0, 0)) # Three transparent rows align BC7 blocks; runtime UVs exclude them.
        destination = TARGET / f'Forest_{index:03}.png'
        frame.save(destination, compress_level=6)
        meta = destination.with_suffix('.png.meta')
        if not meta.exists():
            template = Path('Assets/Art/Background/ForestForeground.png.meta').read_text(encoding='utf-8')
            template = re.sub(r'^guid: .*$', 'guid: ' + uuid.uuid4().hex, template, count=1, flags=re.M)
            template = re.sub(r'maxTextureSize: \d+', 'maxTextureSize: 4096', template)
            meta.write_text(template, encoding='utf-8')
    return dict(source=path.name, sha256=hashlib.sha256(data).hexdigest(), file=destination.name,
                sourceBytes=len(data), outputBytes=destination.stat().st_size, repairedEdgePixels=repaired)

with concurrent.futures.ThreadPoolExecutor(max_workers=4) as pool:
    frames = list(pool.map(convert, enumerate(paths)))
manifest = dict(source=str(SOURCE), sourceWidth=30000, sourceHeight=1562,
                width=4096, height=216, contentHeight=213, framesPerSecond=20, frames=frames,
                sourceBytes=sum(frame['sourceBytes'] for frame in frames),
                outputBytes=sum(frame['outputBytes'] for frame in frames), edgeCorrection=VERSION)
(TARGET / 'Manifest.json').write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding='utf-8')
print(json.dumps({key: manifest[key] for key in ('sourceBytes','outputBytes','width','height','framesPerSecond')}))
