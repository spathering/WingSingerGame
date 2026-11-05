"""Remove white matte contamination only on translucent, near-neutral edge pixels."""
from pathlib import Path
import concurrent.futures
import json
import numpy as np
from PIL import Image

VERSION = 'nearest-opaque-white-edge-v1'

def clean_edges(image):
    pixels = np.array(image.convert('RGBA'))
    alpha = pixels[:, :, 3]
    colors = pixels[:, :, :3].astype(np.float32)
    known = alpha >= 250
    original = colors.copy()
    for _ in range(8):
        sums = np.zeros_like(colors)
        counts = np.zeros(alpha.shape, dtype=np.float32)
        for dy, dx in ((-1,0),(1,0),(0,-1),(0,1)):
            valid = np.roll(known, (dy,dx), (0,1))
            if dy == -1: valid[-1,:] = False
            if dy == 1: valid[0,:] = False
            sums += np.roll(colors, (dy,dx), (0,1)) * valid[:,:,None]
            counts += valid
        fill = ~known & (counts > 0)
        colors[fill] = sums[fill] / counts[fill,None]
        known |= fill
    white = (original.min(axis=2) > 175) & (original.max(axis=2)-original.min(axis=2) < 50)
    repair = (alpha > 0) & (alpha < 250) & known & white
    pixels[repair,:3] = np.clip(colors[repair],0,255).astype(np.uint8)
    return Image.fromarray(pixels), int(repair.sum())

if __name__ == '__main__':
    folder = Path('Assets/Art/Background/AnimatedForeground')
    path = folder / 'Manifest.json'
    manifest = json.loads(path.read_text(encoding='utf-8'))
    if manifest.get('edgeCorrection') != VERSION:
        def repair(entry):
            file = folder / entry['file']
            with Image.open(file) as original:
                output, count = clean_edges(original)
            output.save(file, compress_level=6)
            entry.update(outputBytes=file.stat().st_size, repairedEdgePixels=count)
            return entry
        with concurrent.futures.ThreadPoolExecutor(max_workers=4) as pool:
            manifest['frames'] = list(pool.map(repair, manifest['frames']))
        manifest.update(edgeCorrection=VERSION, outputBytes=sum(f['outputBytes'] for f in manifest['frames']))
        path.write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8')
    print(json.dumps(dict(outputBytes=manifest['outputBytes'],repairedPixels=sum(f.get('repairedEdgePixels',0)for f in manifest['frames']))))
