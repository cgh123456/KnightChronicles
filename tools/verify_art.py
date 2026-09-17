"""Inspect baked PNG metadata, direction uniqueness, motion and alpha margins; create QA contact sheets."""
import os,json,hashlib
from pathlib import Path
from PIL import Image,ImageDraw,ImageFont
ROOT=Path(__file__).resolve().parent.parent
ART=ROOT/'unity/KnightChronicles/Assets/Resources/Art';DEST=ROOT/'assets/models/world/qa';DEST.mkdir(parents=True,exist_ok=True)
try:FONT=ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',15)
except OSError:FONT=ImageFont.load_default()
def board(w,h):
    out=Image.new('RGB',(w,h),(32,36,40));d=ImageDraw.Draw(out)
    for y in range(0,h,16):
        for x in range(0,w,16):
            if (x//16+y//16)%2:d.rectangle((x,y,x+15,y+15),fill=(39,43,47))
    return out
def cellpaste(out,im,box,label):
    x,y,w,h=box;image=im.copy();image.thumbnail((w-12,h-28),Image.Resampling.LANCZOS)
    out.paste(image,(x+(w-image.width)//2,y+4),image)
    ImageDraw.Draw(out).text((x+8,y+h-23),label,font=FONT,fill=(235,213,170))
world=sorted((ART/'World').glob('*.png'));worldboard=board(1400,((len(world)+6)//7)*220)
report={'world':[],'characters':[],'issues':[]}
manifest=json.loads((ROOT/'assets/models/world/world_manifest.json').read_text(encoding='utf-8'))
for i,path in enumerate(world):
    im=Image.open(path);box=im.getchannel('A').getbbox() if im.mode=='RGBA' else None
    record={'key':path.stem,'size':im.size,'mode':im.mode,'alphaBounds':box,'inManifest':path.stem in {m['key'] for m in manifest}}
    report['world'].append(record);cellpaste(worldboard,im,(i%7*200,i//7*220,200,220),path.stem)
    if im.mode!='RGBA' or box is None:report['issues'].append(path.name+' missing RGBA/visible pixels')
worldboard.save(DEST/'world-contact-sheet.png')
chars=[('KnightVariant',ART/'Sprites',256,'Attack'),('Skeleton_Minion',ART/'Enemies',192,'attack'),('Skeleton_Rogue',ART/'Enemies',192,'attack'),('Skeleton_Mage',ART/'Enemies',192,'attack'),('Skeleton_Warrior',ART/'Enemies',192,'attack')]
directionboard=board(1440,len(chars)*205);motionboard=board(1440,len(chars)*205)
directions=['S','SE','E','NE','N','NW','W','SW']
for row,(name,folder,cell,action) in enumerate(chars):
    for path in sorted(folder.glob(name+'_*_sheet.png')):
        meta=json.loads(path.with_suffix('.json').read_text(encoding='utf-8'));im=Image.open(path)
        hashes=[];margins=[];motion=[]
        for r in range(8):
            framehash=[]
            for c in range(meta['columns']):
                frame=im.crop((c*cell,r*cell,(c+1)*cell,(r+1)*cell));digest=hashlib.sha256(frame.tobytes()).hexdigest()
                framehash.append(digest)
                box=frame.getchannel('A').point(lambda a:255 if a>8 else 0).getbbox()
                if box:margins.append(min(box[0],box[1],cell-box[2],cell-box[3]))
                else:report['issues'].append(path.name+f' empty frame {r}/{c}')
            hashes.append(framehash[0]);motion.append(len(set(framehash)))
        record={'sheet':path.name,'size':im.size,'frame':cell,'columns':meta['columns'],'uniqueDirections':len(set(hashes)),'minimumAlphaMargin':min(margins),'motionFramesPerDirection':motion,'cameraElevation':meta.get('cameraElevation'),'orthoScale':meta.get('orthoScale')}
        report['characters'].append(record)
        if im.size!=(meta['columns']*cell,8*cell):report['issues'].append(path.name+' metadata dimensions mismatch')
        if len(set(hashes))<8:report['issues'].append(path.name+' direction rows not unique')
        if min(margins)<1:report['issues'].append(path.name+' frame border clips visible pixels')
    target=Image.open(folder/(name+'_'+action+'_sheet.png'));meta=json.loads((folder/(name+'_'+action+'_sheet.json')).read_text())
    for r in range(8):cellpaste(directionboard,target.crop((0,r*cell,cell,(r+1)*cell)),(r*180,row*205,180,205),name.replace('Skeleton_','')+' '+directions[r])
    for c in range(8):
        frame=min(meta['columns']-1,round(c*(meta['columns']-1)/7))
        cellpaste(motionboard,target.crop((frame*cell,0,(frame+1)*cell,cell)),(c*180,row*205,180,205),name.replace('Skeleton_','')+' '+str(frame))
directionboard.save(DEST/'character-directions-contact-sheet.png');motionboard.save(DEST/'character-motion-contact-sheet.png')
(DEST/'art-qa.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps({'worldPNGs':len(world),'characterSheets':len(report['characters']),'issues':report['issues']},ensure_ascii=False))
