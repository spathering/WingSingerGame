"""Import full GIF animations and crop existing props for tree-ground defect covers.
Run from the project root with Pillow and numpy. Source files stay unchanged.
"""
from pathlib import Path
import json
import numpy as np
from PIL import Image
from convert_character_gifs import convert_files

source = Path('H:/PolyU/PM/WindSinger素材/11.11')
target = Path('Assets/Art/Hazards')
convert_files([source / f'{name}.gif' for name in ('eagle', 'foxRock', 'hiveSwarm', 'trapGif')],
              target, 'HazardManifest.json')
for index, name in enumerate(('combo_rock_grass_purple', 'combo_two_rocks_yellow_flowers',
                              'combo_pebbles_mushroom_fern')):
    with Image.open(Path('Assets/Art/UI/Frontgrounds') / (name + '.png')) as original:
        frame = original.convert('RGBA')
        frame.crop(frame.getbbox()).save(target / f'GroundCover_{index}.png')

with Image.open('Assets/Art/Background/AnimatedForeground/Forest_000.png') as original:
    pixels = np.array(original)
# Transparent ground gaps plus the black defects in the ground band.
ground = pixels[365:397, :, :3].astype(np.int16)
blue_gaps = ((ground[:, :, 2] > ground[:, :, 0] + 10) & (ground[:, :, 2] > ground[:, :, 1] + 5)).mean(0) > .22
mask = blue_gaps | ((pixels[360:401, :, 3] < 100).mean(0) > .65) | (
    ((pixels[350:401, :, :3].max(2) < 8) & (pixels[350:401, :, 3] > 220)).sum(0) > 5)
groups = []
for x in np.where(mask)[0]:
    if not groups or x - groups[-1][-1] > 12:
        groups.append([int(x)])
    else:
        groups[-1].append(int(x))
spans = [dict(center=(g[0] + g[-1] + 1) / 2 / 8192,
              width=(g[-1] - g[0] + 20) / 8192) for g in groups if len(g) > 3]
(target / 'GroundGaps.json').write_text(json.dumps(dict(spans=spans)), encoding='utf-8')
print(f'{len(spans)} repeated ground defect spans; originals unchanged')
