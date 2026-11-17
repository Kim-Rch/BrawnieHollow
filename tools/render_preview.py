"""Accurate static TMX art preview, not an in-game screenshot.

Run with --textures PATH to private, locally exported game textures. Filenames
can be Maps_spring_outdoorsTileSheet.png or spring_outdoorsTileSheet.png, etc.
Never commit or package that texture directory. Runtime crops aren't simulated.
"""
from pathlib import Path
import argparse
import xml.etree.ElementTree as ET
from PIL import Image

def main():
 p=argparse.ArgumentParser(description=__doc__)
 p.add_argument('--textures',type=Path,required=True)
 p.add_argument('--output',type=Path)
 args=p.parse_args()
 root=Path(__file__).resolve().parents[1]; assets=root/'BrawnieHollow/assets'
 m=ET.parse(assets/'hollow.tmx').getroot()
 w,h=int(m.get('width')),int(m.get('height'))
 sheets=[]
 for ts in m.findall('tileset'):
  source=ts.find('image').get('source').removeprefix('../')
  candidates=[args.textures/(source.replace('/','_')+'.png'),args.textures/(source.split('/')[-1]+'.png'),args.textures/(source+'.png')]
  file=next((f for f in candidates if f.is_file()),None)
  if file is None: raise FileNotFoundError(f'Export installed game texture {source} to {args.textures}; do not substitute generated art.')
  atlas=Image.open(file).convert('RGBA')
  assert atlas.size==(int(ts.find('image').get('width')),int(ts.find('image').get('height'))),source
  sheets.append((int(ts.get('firstgid')),int(ts.get('tilecount')),int(ts.get('columns')),atlas))
 im=Image.new('RGBA',(w*16,h*16),'#172c25')
 def draw_layer(layer):
  data=[int(v) for v in layer.find('data').text.split(',') if v.strip()]
  assert len(data)==w*h
  for i,gid in enumerate(data):
   if not gid: continue
   start,count,cols,atlas=next(t for t in sheets if t[0]<=gid<t[0]+t[1])
   n=gid-start; x,y=n%cols*16,n//cols*16
   im.alpha_composite(atlas.crop((x,y,x+16,y+16)),(i%w*16,i//w*16))
 for layer in m.findall('layer'):
  # Actors between Buildings and Front, matching tile depth semantics better
  # than painting them over the forest/cottage roofs.
  if layer.get('name')=='Front':
   for name,x,y in [('Bram',9,15),('Mira',22,13),('Pip',25,20)]:
    sprite=Image.open(assets/(name+'.png')).convert('RGBA')
    im.alpha_composite(sprite.crop((0,0,16,16)),(x*16,y*16))
  draw_layer(layer)
 out=args.output or root/'docs/village-preview.png'; out.parent.mkdir(parents=True,exist_ok=True)
 im.resize((w*32,h*32),Image.Resampling.NEAREST).save(out)
 print(f'{out}: {w}x{h} tiles, {len(sheets)} real vanilla sheets; static art preview.')
if __name__=='__main__': main()