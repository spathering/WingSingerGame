"""Slice the 13 supplied UI references and build reusable uGUI skins.

Usage: python Tools/UI/prepare_ui_assets.py SOURCE_DIRECTORY
Pillow and NumPy are required. Source files are read only; cut rectangles use
the 1902 x 1312 inspection coordinate space and retain native source resolution.
The clean background is separately produced with imagegen; this tool never
replaces it or modifies the original reference directory.
"""
from pathlib import Path
import hashlib
import json
import math
import shutil
import sys
import numpy as np
from PIL import Image, ImageDraw, ImageFilter

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'Assets/Art/UI'
REF = ROOT / 'Docs/Design/UI/References'
SOURCE = Path(sys.argv[1])
SCREENS = [
    ('01_HomeDashboard', 'Windsinger GameUI/01 Home/with taskbar.png'),
    ('02_HomeClean', 'Windsinger GameUI/02 Home/no taskbar.png'),
    ('03_LevelSelect', 'Windsinger GameUI/03 Levels/Darumadrop One.png'),
    ('04_ReadyForTakeoff', 'Windsinger GameUI/04 Ready for takeoff.png'),
    ('05_FlightPaused', 'Windsinger GameUI/05 Flight paused.png'),
    ('06_FlightComplete', 'Windsinger GameUI/06 Flight complete.png'),
    ('07_Settings', 'Windsinger GameUI/07 Settings/centered/no IP.png'),
    ('08_ControllerConnection', 'Windsinger GameUI/08 Controller connection.png'),
    ('09_FlightLog', 'Windsinger GameUI/09 Flight Log.png'),
    ('10_PracticeInhale', 'Windsinger GameUI 1/01 · Inhale/descend.png'),
    ('11_PracticeExhale', 'Windsinger GameUI 1/02 · Exhale/ascend.png'),
    ('12_PracticeFollow', 'Windsinger GameUI 1/03 · Follow/collect.png'),
    ('13_PracticeComplete', 'Windsinger GameUI 1/04 · Practice complete.png'),
]
assets, references = [], []
images = {}
CREAM = (255, 248, 237)


def fill_small_holes(alpha, maximum_area=4000):
    """Restore enclosed details such as blue goggle lenses, not large rope gaps."""
    transparent=np.asarray(alpha)<128
    labels=[]; parent=[]; previous=[]
    def find(n):
        while parent[n]!=n:
            parent[n]=parent[parent[n]]; n=parent[n]
        return n
    for y,row in enumerate(transparent):
        edges=np.flatnonzero(np.diff(np.r_[False,row,False]))
        current=[]; p=0
        for start,end in zip(edges[::2],edges[1::2]):
            label=len(parent); parent.append(label); current.append((int(start),int(end),label))
            while p<len(previous) and previous[p][1]<=start: p+=1
            q=p
            while q<len(previous) and previous[q][0]<end:
                parent[find(label)]=find(previous[q][2]); q+=1
            labels.append((y,int(start),int(end),label))
        previous=current
    counts={}; borders={}
    for y,start,end,label in labels:
        label=find(label); counts[label]=counts.get(label,0)+end-start
        borders[label]=borders.get(label,False) or y in (0,transparent.shape[0]-1) or start==0 or end==transparent.shape[1]
    result=np.array(alpha)
    for y,start,end,label in labels:
        label=find(label)
        if not borders[label] and counts[label]<=maximum_area: result[y,start:end]=255
    return Image.fromarray(result)


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def save(key, image, folder='Sprites', border=0, source='', rect=None, method='reference crop'):
    folder_path = OUT / folder
    folder_path.mkdir(parents=True, exist_ok=True)
    path = folder_path / (key + '.png')
    image.save(path, optimize=True)
    assets.append(dict(key=key, path=path.relative_to(ROOT).as_posix(),
                       width=image.width, height=image.height, border=border,
                       source=source, rect=rect or [], method=method, sha256=digest(path)))
    return image


def crop(key, screen, rect, mode='cream', polygon=None):
    source = images[screen]
    sx, sy = source.width / 1902, source.height / 1312
    box = [round(rect[0]*sx), round(rect[1]*sy), round(rect[2]*sx), round(rect[3]*sy)]
    image = source.crop(box).convert('RGBA')
    rgb = np.array(image)[:, :, :3].astype(float)
    r, g, b = rgb[:, :, 0], rgb[:, :, 1], rgb[:, :, 2]
    if mode == 'cream':
        corners=np.concatenate([rgb[:2,:2].reshape(-1,3),rgb[:2,-2:].reshape(-1,3),rgb[-2:,:2].reshape(-1,3),rgb[-2:,-2:].reshape(-1,3)])
        background=np.median(corners,axis=0)
        alpha = np.clip((np.abs(rgb - background).max(axis=2)-4)/12, 0, 1)
    elif mode == 'logo':
        alpha = (((r > 215) & (g > 215)) | ((r < 110) & (g < 135) & (b < 130))).astype(float)
    elif mode == 'sky':
        alpha = (~((b > 95) & (b > r*1.08) & (g > r*1.04)) & ~((g > 70) & (g > r*1.15) & (g > b*.98))).astype(float)
        if key in ('AirshipGuiding','AirshipComplete'):
            yy=np.arange(image.height)[:,None]/sy+rect[1]
            alpha[((r>190)&(g>190)&(b>190)) & (yy<(540 if key=='AirshipGuiding' else 650))] = 0
    elif mode == 'sky_blue':
        alpha = (~((b > 95) & (b > r*1.08) & (g > r*1.04))).astype(float)
    elif mode == 'blue':
        alpha = ((b > r*1.25) & (b > 110)).astype(float)
    elif mode == 'dark':
        alpha = ((r < 150) & (g < 160) & (b < 160)).astype(float)
    else:
        raise ValueError(mode)
    alpha_image = Image.fromarray((alpha*255).astype('uint8'))
    if polygon:
        mask = Image.new('L', image.size)
        ImageDraw.Draw(mask).polygon([(round(x*sx)-box[0], round(y*sy)-box[1]) for x,y in polygon], fill=255)
        alpha_image = Image.fromarray(np.minimum(np.array(alpha_image), np.array(mask)))
        alpha_image = fill_small_holes(alpha_image)
        alpha_image = alpha_image.filter(ImageFilter.MinFilter(3)).filter(ImageFilter.GaussianBlur(.45))
    image.putalpha(alpha_image)
    return save(key, image, source=screen, rect=box,
                method='reference crop + foreground alpha mask ('+mode+')')


def skin(key, fill, outline=None, radius=45, size=(192,192), width=3):
    scale=3
    image=Image.new('RGBA', (size[0]*scale, size[1]*scale))
    draw=ImageDraw.Draw(image)
    draw.rounded_rectangle((1,1,image.width-2,image.height-2), radius=radius*scale,
                           fill=fill, outline=outline, width=width*scale if outline else 0)
    image=image.resize(size, Image.Resampling.LANCZOS)
    return save(key, image, 'Skins', border=radius+4, method='code-native rounded UI skin; reference palette')


def path_asset(key, points, dashed=True, color=CREAM, arrow=True, width=12):
    s=2
    image=Image.new('RGBA',(800*s,550*s))
    draw=ImageDraw.Draw(image)
    p=[]
    for i in range(241):
        t=i/240
        x=sum(math.comb(3,j)*(1-t)**(3-j)*t**j*points[j][0] for j in range(4))
        y=sum(math.comb(3,j)*(1-t)**(3-j)*t**j*points[j][1] for j in range(4))
        p.append((x*s,y*s))
    for i in range(240):
        if not dashed or (i//6)%2==0:
            draw.line((p[i],p[i+1]), fill=color, width=width*s)
            draw.ellipse((p[i][0]-width*s/2,p[i][1]-width*s/2,p[i][0]+width*s/2,p[i][1]+width*s/2),fill=color)
    if arrow:
        x,y=p[-1]; dx,dy=x-p[-8][0],y-p[-8][1]; mag=math.hypot(dx,dy); dx/=mag; dy/=mag
        for side in (-1,1):
            draw.line(((x-dx*40*s+side*dy*27*s,y-dy*40*s-side*dx*27*s),(x,y)),fill=color,width=width*s)
    if key.startswith('RhythmWave'):
        image=image.crop(image.getbbox())
        image.thumbnail((800,550),Image.Resampling.LANCZOS)
    else:
        image=image.resize((800,550),Image.Resampling.LANCZOS)
    save(key,image,'Illustrations',method='code-native cubic UI guide path')


REF.mkdir(parents=True, exist_ok=True)
for name, relative in SCREENS:
    path=SOURCE/relative
    if not path.is_file(): raise FileNotFoundError(path)
    images[name]=Image.open(path).convert('RGB')
    destination=REF/(name+'.png')
    shutil.copy2(path,destination)
    references.append(dict(id=name, source=relative, path=destination.relative_to(ROOT).as_posix(),
                           width=images[name].width,height=images[name].height,sha256=digest(path)))

crop('Logo','01_HomeDashboard',(74,42,454,129),'logo')
crop('ForestEmblem','03_LevelSelect',(863,7,991,122),'sky_blue')
crop('AirshipNeutral','04_ReadyForTakeoff',(203,151,815,1090),'sky',
     [(490,160),(570,182),(710,279),(797,386),(805,474),(740,563),(717,744),(720,877),(690,969),(639,1046),(553,1081),(467,1060),(396,1028),(348,956),(312,878),(305,755),(258,606),(226,502),(211,415),(246,329),(365,229),(440,184)])
crop('AirshipGuiding','11_PracticeExhale',(285,237,770,812),'sky',
     [(500,242),(650,249),(738,298),(757,363),(722,463),(710,536),(705,664),(672,737),(600,795),(499,808),(426,776),(390,720),(367,676),(349,544),(324,467),(294,423),(297,337),(348,322),(378,280)])
crop('AirshipComplete','06_FlightComplete',(28,132,767,1221),'sky',
     [(393,138),(470,146),(635,246),(728,357),(753,475),(702,588),(683,715),(686,916),(654,1050),(596,1150),(510,1204),(421,1213),(278,1170),(179,1129),(121,1068),(109,906),(83,737),(52,566),(28,450),(51,348),(170,239),(298,180)])
crop('MascotRelaxed','05_FlightPaused',(419,38,809,334),'sky',
     [(605,43),(683,46),(747,84),(797,147),(803,224),(773,292),(746,329),(600,331),(545,303),(477,311),(425,224),(457,201),(475,113),(534,67)])
crop('ControllerDevice','08_ControllerConnection',(1183,403,1674,547),'cream')

icon_specs=[
 ('IconSettings','01_HomeDashboard',(1610,58,1660,107),'cream'),
 ('IconLog','01_HomeDashboard',(1347,59,1394,109),'cream'),
 ('IconInhale','04_ReadyForTakeoff',(1053,708,1115,766),'cream'),
 ('IconExhale','04_ReadyForTakeoff',(1308,708,1371,765),'cream'),
 ('IconPractice','04_ReadyForTakeoff',(1570,708,1622,765),'cream'),
 ('IconCheck','04_ReadyForTakeoff',(1054,413,1100,447),'cream'),
 ('IconHeart','06_FlightComplete',(926,933,975,978),'cream'),
 ('IconCalendar','06_FlightComplete',(931,784,978,836),'cream'),
 ('IconGamepad','01_HomeDashboard',(1425,1106,1471,1145),'cream'),
 ('IconBluetooth','08_ControllerConnection',(1610,440,1674,531),'blue'),
 ('IconBattery','08_ControllerConnection',(1041,829,1093,870),'cream'),
 ('IconSignal','08_ControllerConnection',(1041,901,1095,951),'cream'),
 ('IconFire','09_FlightLog',(160,515,245,601),'cream'),
 ('IconAudio','07_Settings',(430,338,477,376),'cream'),
 ('IconDisplay','07_Settings',(429,629,478,673),'cream'),
 ('IconHelp','07_Settings',(440,924,472,963),'cream'),
 ('IconLanguage','07_Settings',(434,1073,482,1116),'cream'),
 ('IconLink','10_PracticeInhale',(1376,74,1424,119),'cream'),
 ('IconLock','03_LevelSelect',(753,643,785,685),'dark'),
 ('IconPause','10_PracticeInhale',(101,76,128,112),'cream'),
 ('IconChevronLeft','01_HomeDashboard',(1005,537,1037,588),'cream'),
 ('IconPlay','03_LevelSelect',(964,949,994,990),'cream'),
]
for key,screen,rect,mode in icon_specs: crop(key,screen,rect,mode)
white_check=Image.open(OUT/'Sprites/IconCheck.png').convert('RGBA')
white_check_rgb=Image.new('RGBA',white_check.size,'white'); white_check_rgb.putalpha(white_check.getchannel('A'))
save('IconCheckWhite',white_check_rgb,source='04_ReadyForTakeoff',method='white variant preserving extracted check alpha')
left=Image.open(OUT/'Sprites/IconChevronLeft.png')
save('IconChevronRight',left.transpose(Image.Transpose.FLIP_LEFT_RIGHT),method='mirrored extracted chevron')

paper=json.loads((ROOT/'Assets/Art/Paper/PaperManifest.json').read_text(encoding='utf-8-sig'))
for clip in paper['clips']:
    key='PaperStar' if clip['name']=='star' else 'Crane'
    atlas=Image.open(ROOT/'Assets/Art/Paper'/clip['atlas'])
    padding=clip['padding']
    save(key,atlas.crop((padding,padding,padding+clip['frameWidth'],padding+clip['frameHeight'])),
         'Illustrations',source='Assets/Art/Paper/'+clip['atlas'],method='first transparent frame of existing project atlas')

skin('PanelCream',CREAM,'#efd5ad')
skin('PanelOutline',CREAM,'#efd5ad',radius=32)
for name,color in [('Cream',CREAM),('Blue','#d8f1fb'),('Peach','#ffe7d5'),('Lavender','#eee3fc'),('Green','#e4ecd3'),('Track','#efd5ad'),('White','#ffffff')]:
    skin('Fill'+name,color,radius=42)
for name,color,line in [('OrangeNormal','#ff8500','#eb6c00'),('OrangeHover','#ff9a24','#eb6c00'),('OrangePressed','#e67500','#c75b00'),('Disabled','#c8c5ba','#aaa99d'),('Cream',CREAM,'#efd5ad'),('CreamHover','#fff0d4','#edc792'),('Blue','#d8f1fb',None)]:
    skin('Button'+name,color,line,radius=60,size=(256,128))
skin('ToggleOff','#b7b8ba',radius=64,size=(256,128))
skin('ToggleOn','#438b3d',radius=64,size=(256,128))
skin('SliderHandle','#ff8500','#ffffff',radius=63,size=(128,128),width=15)
skin('PillWhite','#ffffff',radius=64,size=(256,128))
save('PixelWhite',Image.new('RGBA',(4,4),'white'),'Skins',method='code-native solid pixel')
path_asset('PathDescend',[(30,60),(300,20),(500,460),(770,460)])
path_asset('PathAscend',[(30,490),(300,480),(500,170),(770,50)])
path_asset('PathFollow',[(30,300),(350,610),(380,110),(770,160)],arrow=False,width=9)
path_asset('RhythmWaveBlue',[(5,285),(220,260),(260,100),(500,270)],False,'#00a5df',False,10)
path_asset('RhythmWaveOrange',[(5,155),(240,300),(420,285),(790,160)],False,'#ff8524',False,10)

bg=OUT/'Backgrounds/ForestValley.png'
assets.append(dict(key='ForestValley',path=bg.relative_to(ROOT).as_posix(),width=Image.open(bg).width,
                   height=Image.open(bg).height,border=0,source='12_PracticeFollow',rect=[],method='imagegen clean background reconstruction',sha256=digest(bg)))
manifest=dict(referenceWidth=2420,referenceHeight=1668,layoutWidth=1902,layoutHeight=1312,
              font='Assets/Art/UI/Fonts/DarumadropOne-Regular.ttf',references=references,assets=assets)
(OUT/'UIAssetManifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')

# Human-readable QA sheets do not enter Unity's runtime texture catalog.
thumbs=Image.new('RGB',(1200,math.ceil(len(assets)/8)*135),'#40585c')
draw=ImageDraw.Draw(thumbs)
for i,asset in enumerate(assets):
    thumb=Image.open(ROOT/asset['path']).convert('RGBA'); thumb.thumbnail((136,100))
    x=(i%8)*150+(150-thumb.width)//2; y=(i//8)*135
    thumbs.paste(thumb,(x,y),thumb); draw.text(((i%8)*150+4,y+105),asset['key'],fill='white')
qa=ROOT/'Docs/Design/UI/Previews'; qa.mkdir(parents=True,exist_ok=True)
thumbs.save(qa/'UIAssetContactSheet.png')
print(f'Prepared {len(assets)} reusable sprites and {len(references)} references. Manifest: {OUT / "UIAssetManifest.json"}')
