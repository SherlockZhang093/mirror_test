from pathlib import Path
import json
from PIL import Image
import build_mirror_bird_idle_from_regenerated_wings as bird
import build_mirror_bird_idle_content as idle

ROOT=Path(__file__).resolve().parents[1]
BASE=ROOT/'Assets/MirrorTrial/Art/Boss/AerialMount/Previews'
PARTS=BASE/'AirIdleFivePart_v1'; WINGS=BASE/'AirIdleWingPoses_v1'; CELL=(192,128)

def gif(frames,path,fps):
    xs=[f.resize((1152,768),Image.Resampling.NEAREST) for f in frames]
    xs[0].save(path,save_all=True,append_images=xs[1:],duration=round(1000/fps),loop=0,disposal=2,transparency=0)

def save(frames,out,name,fps,extra):
    out.mkdir(parents=True,exist_ok=True)
    sheet=Image.new('RGBA',(192*len(frames),128)); keys=Image.new('RGBA',(192*4*len(frames),128*4))
    for i,f in enumerate(frames):
        sheet.alpha_composite(f,(i*192,0)); keys.alpha_composite(bird.display(f,4),(i*768,0))
    sheet.save(out/f'{name}.png'); gif(frames,out/f'{name}_6x.gif',fps); keys.save(out/f'{name}_Keyframes_4x.png')
    report={'frames':len(frames),'fps':fps,'canvas':[192,128],'formal_unity_assets_modified':False,**extra}
    (out/f'{name}_validation.json').write_text(json.dumps(report,indent=2),encoding='utf-8')

def five_part(action,poses,fps,upper_offsets,lower_offsets):
    body=bird.load(str(PARTS/'MirrorBird_Normalized_body_v1.png'))
    ut=bird.load(str(PARTS/'MirrorBird_Normalized_upper_tail_v1.png')); lt=bird.load(str(PARTS/'MirrorBird_Normalized_lower_tail_v1.png'))
    ups=bird.animate_tail(ut,bird.UPPER_TAIL_SOURCE_POINTS,upper_offsets); lows=bird.animate_tail(lt,bird.LOWER_TAIL_SOURCE_POINTS,lower_offsets)
    frames=[]; blank=Image.new('RGBA',CELL)
    for u,l,p in zip(ups,lows,poses):
        far=bird.load(str(WINGS/f'MirrorBird_FarWingPose{p:02d}_v1.png'))
        near=bird.shift_layer(bird.load(str(WINGS/f'MirrorBird_NearWingPose{p:02d}_v1.png')),(4,0))
        shoulder=bird.build_shoulder_feathers(bird.SHOULDER_FEATHER_TIPS[p-1])
        frames.append(bird.assemble(u,l,far,near,body,blank,shoulder))
    save(frames,BASE/f'{action}FivePart_v1',f'MirrorBird_{action}_FivePart_v1',fps,{'parts':['far_wing','near_wing','body','upper_tail','lower_tail'],'pose_sequence':poses,'body_core_fixed':True})

def normalized_reference(action,path,count,fps,source_anchor_y):
    src=Image.open(path).convert('RGBA'); cw=src.width//count
    alpha=src.getchannel('A'); opaque={(x,y) for y in range(src.height) for x in range(src.width) if alpha.getpixel((x,y))>96}
    components=[]
    while opaque:
        seed=opaque.pop(); stack=[seed]; comp={seed}
        while stack:
            x,y=stack.pop()
            for q in ((x-1,y),(x+1,y),(x,y-1),(x,y+1)):
                if q in opaque: opaque.remove(q); comp.add(q); stack.append(q)
        if len(comp)>100: components.append(comp)
    components=sorted(sorted(components,key=len,reverse=True)[:count],key=lambda c:min(x for x,y in c))
    items=[]
    for comp in components:
        xs=[p[0] for p in comp]; ys=[p[1] for p in comp]; box=(min(xs),min(ys),max(xs)+1,max(ys)+1)
        crop=src.crop(box); mask=Image.new('L',crop.size)
        for x,y in comp: mask.putpixel((x-box[0],y-box[1]),alpha.getpixel((x,y)))
        crop.putalpha(mask); items.append((crop,box))
    maxw=max(i[0].width for i in items); maxh=max(i[0].height for i in items)
    scale=min(125/maxw,82/maxh); master=bird.load(str(ROOT/'Assets/MirrorTrial/Art/Boss/AerialMount/Sheets/MirrorBird_MasterPose.png'))
    frames=[]
    for index,(c,box) in enumerate(items):
        small=c.resize((round(c.width*scale),round(c.height*scale)),Image.Resampling.NEAREST)
        source_anchor_x=(index+.5)*cw
        f=Image.new('RGBA',CELL); f.alpha_composite(small,(round(96+(box[0]-source_anchor_x)*scale),round(75+(box[1]-source_anchor_y)*scale)))
        frames.append(idle.project_palette(master,f))
    save(frames,BASE/f'{action}FivePart_v1',f'MirrorBird_{action}_NormalizedReference_v1',fps,{'fixed_scale':scale,'fixed_source_anchor':[cw//2,source_anchor_y],'target_anchor':[96,75],'generated_reference_only':True})

five_part('Dive',[2,3,4,5,4,3],10,[(0,0),(0,1),(1,2),(2,2),(1,1),(0,0)],[(0,0),(1,1),(2,2),(2,3),(1,2),(0,1)])
five_part('DiveRecover',[5,4,3,2],10,[(2,2),(1,2),(1,1),(0,0)],[(2,3),(2,2),(1,1),(0,1)])
normalized_reference('Turn',BASE/'TurnFivePart_v1/MirrorBird_TurnReference_v1_Alpha.png',8,16,550)
normalized_reference('Hit',BASE/'HitFivePart_v1/MirrorBird_HitReference_v1_Alpha.png',4,12,535)
