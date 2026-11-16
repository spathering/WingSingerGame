// Exact SVG rasterization for Unity's built-in Sprite importer; source SVGs are retained.
const fs = require('fs');
const path = require('path');
const sharp = require('C:/Users/11959/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/sharp');
const folder = path.resolve('Assets/Art/UI/Figma20261116');
(async () => {
  for (const file of fs.readdirSync(folder).filter(f => f.endsWith('.svg'))) {
    await sharp(path.join(folder, file), { density: 144 }).png().toFile(path.join(folder, file + '.png'));
  }
  console.log('Rasterized Figma SVGs with original view boxes and aspect ratios.');
})().catch(e => { console.error(e); process.exitCode = 1; });
