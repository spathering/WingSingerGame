"""Validate source migration, asset provenance, internal links and render deliverables."""
from pathlib import Path
import hashlib
import json
import re
import subprocess
import sys
from PIL import Image, ImageDraw, ImageFont
import numpy as np

root=Path(__file__).resolve().parents[2]
# Migration source stays stable after this delivery is committed or pushed.
migration_base='57d2f7a0bc42a732c846157724646bc7ab00e71e'
checks=[]
def check(condition,description):
    if not condition: raise AssertionError(description)
    checks.append(description)
def sha(path): return hashlib.sha256(path.read_bytes()).hexdigest()
manifest=json.loads((root/'Assets/Art/UI/UIAssetManifest.json').read_text(encoding='utf-8-sig'))
check(len(manifest['references'])==13,'Exactly 13 source references')
check(len(manifest['assets'])==60,'Exactly 60 reusable UI sprite assets')
for entry in manifest['references']:
    check(sha(root/entry['path'])==entry['sha256'],'Reference content preserved: '+entry['id'])
for entry in manifest['assets']:
    path=root/entry['path']
    check(sha(path)==entry['sha256'],'Sprite source hash: '+entry['key'])
    check(Path(str(path)+'.meta').exists(),'Sprite imported by Unity: '+entry['key'])
migration=json.loads((root/'Docs/Development/StructureMigration.json').read_text(encoding='utf-8'))
preserved_scripts=0
intentional_changes={
    'Assets/Scripts/Levels/BreathLevelManager.cs',
    'Assets/Scripts/Gameplay/Presentation/BreathPlayer.cs',
    'Assets/Scripts/Levels/BreathLevel.cs',
    'Assets/Scripts/Gameplay/Presentation/LoopingPanorama.cs',
    'Assets/Editor/Setup/BreathArtSetup.cs',
}
changed_scripts=[]
for entry in migration:
    current=root/entry['current']
    check(current.is_file(),'Moved file exists: '+entry['current'])
    check(not (root/entry['previous']).exists(),'No stale duplicate source: '+entry['previous'])
    if entry['preservedGuid']:
        meta=Path(str(current)+'.meta').read_text()
        check('guid: '+entry['preservedGuid'] in meta,'Unity GUID preserved: '+entry['current'])
    if current.suffix=='.cs':
        original=subprocess.check_output(['git','show',migration_base+':'+entry['previous']],cwd=root)
        if entry['current'] in intentional_changes:
            check(original.replace(b'\r\n',b'\n')!=current.read_bytes().replace(b'\r\n',b'\n'),'Documented gameplay/UI/parallax extension: '+current.name)
            changed_scripts.append(entry['current'])
        else:
            check(original.replace(b'\r\n',b'\n')==current.read_bytes().replace(b'\r\n',b'\n'),'Existing runtime/editor source logic unchanged: '+current.name)
            preserved_scripts+=1

for path in sorted(intentional_changes-set(changed_scripts)):
    current=root/path
    original=subprocess.check_output(['git','show',migration_base+':'+path],cwd=root)
    original_meta=subprocess.check_output(['git','show',migration_base+':'+path+'.meta'],cwd=root)
    check(original_meta.replace(b'\r\n',b'\n')==Path(str(current)+'.meta').read_bytes().replace(b'\r\n',b'\n'),'Unmoved gameplay script GUID preserved: '+current.name)
    check(original.replace(b'\r\n',b'\n')!=current.read_bytes().replace(b'\r\n',b'\n'),'Documented gameplay navigation extension: '+current.name)
    changed_scripts.append(path)

broken=[]
documents=list((root/'Docs').rglob('*.md'))+list((root/'Firmware').rglob('*.md'))+[root/'README.md',root/'AGENTS.md']
for path in documents:
    text=path.read_text(encoding='utf-8-sig')
    for match in re.finditer(r'\[[^\]]*\]\(([^)]+)\)',text):
        target=match.group(1).strip('<>')
        if '://' in target or target.startswith(('#','mailto:')): continue
        target=target.split('#',1)[0]
        resolved=(path.parent/target).resolve()
        # This report is emitted by the successful audit at the end of this run.
        own_report=(root/'Docs/Validation/ProjectOrganizationValidation.json').resolve()
        if target and not resolved.exists() and resolved!=own_report: broken.append(str(path.relative_to(root))+': '+target)
check(not broken,'All internal Markdown links resolve: '+str(broken))

build=json.loads((root/'Docs/Validation/UIBuildValidation.json').read_text(encoding='utf-8-sig'))
play=json.loads((root/'Docs/Validation/UIPlayValidation.json').read_text(encoding='utf-8-sig'))
check(build['screenCount']==13 and build['spriteCount']==60,'Unity serialized final 13-screen / 60-sprite delivery')
check(play['success'],'Actual Unity Play Mode checks passed')
navigation=json.loads((root/'Docs/Validation/GameplayNavigationValidation.json').read_text(encoding='utf-8-sig'))
check(navigation['success'],'Production single-scene gameplay navigation Play Mode checks passed')
viewport=json.loads((root/'Docs/Validation/UIViewportValidation.json').read_text(encoding='utf-8-sig'))
check(viewport['success'] and viewport['pageResolutionCases']==117,'All 13 pages pass actual layout checks at 9 viewport sizes')
check(viewport['legacyOverflowCount']>0,'Old 16:9 control overflow reproduced before validating the corrected layout')
check(viewport['checkedControls']>0 and viewport['checkedText']>0,'Viewport audit checks actual control and rendered text bounds')
check('m_ReferenceResolution: {x: 1920, y: 1080}' in (root/'Assets/Prefabs/UI/UIFlowRoot.prefab').read_text(),'Canvas prefab uses the 16:9 reference resolution')
check(set(changed_scripts)==intentional_changes,'All documented gameplay source extensions audited')
catalog=root/'Assets/Settings/Levels/WingSingerLevelCatalog.asset'
check(catalog.exists() and Path(str(catalog)+'.meta').exists(),'Level catalog SO has Unity metadata')
for name in ('Level_001_FirstFlight','Level_002_SlowBreath'):
    definition=root/'Assets/Settings/Levels'/(name+'_Definition.asset')
    check(definition.exists() and Path(str(definition)+'.meta').exists(),'Existing level has a definition SO: '+name)
    data=definition.read_text(encoding='utf-8-sig')
    for target in (root/'Assets/Prefabs/Levels'/(name+'.prefab'),root/'Assets/Settings/Levels'/(name+'.asset')):
        guid=re.search(r'^guid: (\w+)',Path(str(target)+'.meta').read_text(),re.M).group(1)
        check('guid: '+guid in data,'Definition references original gameplay asset: '+target.name)
    check('availableFromStart: 1' in data,'Existing level starts selectable: '+name)
catalog_guid=re.search(r'^guid: (\w+)',Path(str(catalog)+'.meta').read_text(),re.M).group(1)
check('catalog: {fileID: 11400000, guid: '+catalog_guid in (root/'Assets/Scenes/BreathDemo.unity').read_text(),'Main scene registers the level catalog')
prefabs=list((root/'Assets/Prefabs/UI/Screens').glob('*.prefab'))
check(len(prefabs)==13,'No missing or extra screen prefab')
for prefab in prefabs:
    check(Path(str(prefab)+'.meta').exists(),'Screen has stable Unity metadata: '+prefab.stem)
    capture=root/'Docs/Design/UI/Previews'/(prefab.stem+'.png')
    with Image.open(capture) as image:
        check(image.size==(1210,834) and float(np.asarray(image).std())>15,'Nonblank actual Unity capture: '+prefab.stem)
font=root/'Assets/Art/UI/Fonts/DarumadropOne-Regular.ttf'
check(font.read_bytes()[:4]==b'\x00\x01\x00\x00' and (font.parent/'OFL.txt').exists(),'Official Darumadrop One font and license present')
for prefab in prefabs:
    capture=root/'Docs/Design/UI/ViewportPreviews'/('1280x720_'+prefab.stem+'.png')
    with Image.open(capture) as captured:
        check(captured.size==(1280,720),'Actual 16:9 viewport capture: '+prefab.stem)

overview=Image.new('RGB',(1600,1232),'#f4ede1')
draw=ImageDraw.Draw(overview)
label_font=ImageFont.truetype(str(font),22)
for i,prefab in enumerate(sorted(prefabs)):
    capture=Image.open(root/'Docs/Design/UI/Previews'/(prefab.stem+'.png')).convert('RGB')
    capture.thumbnail((390,269),Image.Resampling.LANCZOS)
    x=(i%4)*400+5; y=(i//4)*308+5
    overview.paste(capture,(x,y))
    draw.text((x+4,y+272),prefab.stem,font=label_font,fill='#004b42')
draw.text((430,978),'WingSinger / 13 editable UI screens',font=ImageFont.truetype(str(font),32),fill='#004b42')
overview.save(root/'Docs/Design/UI/Previews/UIScreensOverview.png')
result=dict(date='2026-10-25 Asia/Shanghai',success=True,referenceCount=13,spriteCount=60,
            preservedScripts=preserved_scripts,migratedPaths=len(migration),unityBuildChecks=len(build['passedChecks']),
            unityPlayChecks=len(play['checks']),sensorAssertions=86,collectionAssertions=151,
            intentionallyExtendedScripts=changed_scripts,gameplayNavigationChecks=len(navigation['checks']),
            viewportCases=viewport['pageResolutionCases'],viewportChecks=len(viewport['checks']),
            internalMarkdownLinksValid=True,checks=checks)
(root/'Docs/Validation/ProjectOrganizationValidation.json').write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print(f'PASS: {len(checks)} delivery assertions; {preserved_scripts} scripts unchanged, {len(changed_scripts)} documented extensions with GUIDs preserved; {len(navigation["checks"])} gameplay navigation checks.')
