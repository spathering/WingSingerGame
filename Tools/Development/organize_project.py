"""One-time, idempotent project organization; keeps Unity .meta identities.
Run before opening Unity. First-party source paths and Markdown links are updated.
"""
from pathlib import Path
import json
import os
import re
import shutil
import stat

root=Path(__file__).resolve().parents[2]
moves={}
for name in ['ArduinoBreathInput','BreathInput','KeyboardBreathInput','ExternalBreathInput','BreathSensorClient','BreathSensorProtocol','WindowsSerialTransport']:
    moves[f'Assets/Scripts/Gameplay/{name}.cs']=f'Assets/Scripts/Input/{name}.cs'
for name in ['BreathPlayer','BreathCharacterAnimator','CharacterSpriteClip','LoopingPanorama']:
    moves[f'Assets/Scripts/Gameplay/{name}.cs']=f'Assets/Scripts/Gameplay/Presentation/{name}.cs'
for name in ['BreathCollectibleLayout','PaperCollectible']:
    moves[f'Assets/Scripts/Gameplay/{name}.cs']=f'Assets/Scripts/Gameplay/Collectibles/{name}.cs'
moves.update({
 'Assets/Scripts/Gameplay/BreathSession.cs':'Assets/Scripts/Gameplay/Core/BreathSession.cs',
 'Assets/Scripts/Gameplay/BreathLevelSettings.cs':'Assets/Scripts/Levels/BreathLevelSettings.cs',
 'Assets/Scripts/Gameplay/BreathHud.cs':'Assets/Scripts/UI/Legacy/BreathHud.cs',
 'Docs/BasicGameplay.md':'Docs/Design/BasicGameplay.md',
 'Docs/PaperCollection.md':'Docs/Design/PaperCollection.md',
 'Docs/ArtAssets.md':'Docs/Art/ArtAssets.md',
 'Docs/ArduinoInput.md':'Docs/Hardware/ArduinoInput.md',
 'Docs/ArduinoInputContext.md':'Docs/Handoffs/ArduinoInputContext.md',
 'Docs/WINGSINGER_91E5B8BCE69F99E9B99BE48AB8E687.md':'Docs/Hardware/HardwareIntegrationContext.md',
 'Docs/UnityMcpSetup.md':'Docs/Development/UnityMcpSetup.md',
})
for name in ['ArduinoInputSetup','BreathArtSetup','BreathDemoBuilder','PaperCollectionSetup','ArduinoInputValidation','BreathDemoValidation','PaperCollectionValidation','PlayerPresentationValidation']:
    category='Validation' if 'Validation' in name else 'Setup'
    moves[f'Assets/Editor/{name}.cs']=f'Assets/Editor/{category}/{name}.cs'
for name in ['ArduinoInputValidation.txt','ArtValidationReport.txt','CleanPresentationValidation.txt','PaperCollectionValidation.txt','PlayerPresentationValidation.txt','ValidationReport.txt']:
    moves['Docs/'+name]='Docs/Validation/'+name

def safe(path):
    resolved=path.resolve()
    if not resolved.is_relative_to(root): raise ValueError('Outside workspace: '+str(path))
    return resolved

def write(path,text):
    safe(path)
    path.chmod(path.stat().st_mode | stat.S_IWRITE)
    path.write_text(text,encoding='utf-8')

# Capture each document's original position before moving so relative links stay valid.
documents=[]
for folder in ['Docs','Firmware']:
    for p in (root/folder).rglob('*.md'):
        documents.append((p.relative_to(root).as_posix(),p.read_text(encoding='utf-8-sig')))
if (root/'README.md').exists(): documents.append(('README.md',(root/'README.md').read_text(encoding='utf-8-sig')))

for source,destination in moves.items():
    src,dst=safe(root/source),safe(root/destination)
    if not src.exists(): continue
    if dst.exists(): raise FileExistsError(dst)
    dst.parent.mkdir(parents=True,exist_ok=True)
    shutil.move(str(src),str(dst))
    meta=Path(str(src)+'.meta')
    if meta.exists(): shutil.move(str(meta),str(dst)+'.meta')

for original,text in documents:
    destination=moves.get(original,original)
    path=root/destination
    def link(match):
        label,target=match.group(1),match.group(2)
        if '://' in target or target.startswith(('#','mailto:')): return match.group(0)
        clean=target.strip('<>')
        fragment=''
        if '#' in clean: clean,fragment=clean.split('#',1); fragment='#'+fragment
        resolved=(root/original).parent/clean
        try: old=resolved.resolve().relative_to(root).as_posix()
        except ValueError: return match.group(0)
        new=moves.get(old,old)
        relative=os.path.relpath(root/new,path.parent).replace('\\','/')
        return f'[{label}]({relative}{fragment})'
    text=re.sub(r'\[([^\]]*)\]\(([^)]+)\)',link,text)
    for old,new in sorted(moves.items(),key=lambda pair:-len(pair[0])): text=text.replace(old,new)
    if path.read_text(encoding='utf-8-sig')!=text: write(path,text)

for folder in ['Assets/Scripts','Assets/Editor','Tools']:
    for p in (root/folder).rglob('*'):
        if p.suffix not in ['.cs','.csproj','.py','.md'] or 'obj' in p.parts or 'bin' in p.parts or p==Path(__file__).resolve(): continue
        text=p.read_text(encoding='utf-8-sig'); revised=text
        for old,new in sorted(moves.items(),key=lambda pair:-len(pair[0])): revised=revised.replace(old,new)
        if revised!=text: write(p,revised)
guidelines=root/'Docs/DevelopmentGuidelines.md'
text=guidelines.read_text(encoding='utf-8-sig')
for old,new in moves.items(): text=text.replace(old,new)
write(guidelines,text)
records=[]
for old,new in moves.items():
    meta=root/(new+'.meta'); guid=''
    if meta.exists():
        match=re.search(r'^guid: (\w+)',meta.read_text(),re.M)
        if match: guid=match.group(1)
    records.append(dict(previous=old,current=new,preservedGuid=guid))
out=root/'Docs/Development/StructureMigration.json'
out.parent.mkdir(parents=True,exist_ok=True)
out.write_text(json.dumps(records,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print(f'Organized {len(records)} source/document paths; Unity script GUIDs preserved.')
