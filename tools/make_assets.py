"""Generate vanilla-reference hollow.tmx; never modify user-owned sprites.
Author: Rachman Nurhakim. Run: python tools/make_assets.py
Tile coordinates inspected against installed game textures. No art embedded.
"""
from pathlib import Path
import random
import xml.etree.ElementTree as ET
ROOT=Path(__file__).resolve().parents[1]
W,H=36,28
SHEETS=[('outdoors','Maps/spring_outdoorsTileSheet',400,1264),
 ('log','../Buildings/Log Cabin',240,112),('stone','../Buildings/Stone Cabin',240,112),
 ('plank','../Buildings/Plank Cabin',240,112),('craftables','../TileSheets/Craftables',128,1472),
 ('well','../Buildings/Well',48,80)]
def generate():
 rng=random.Random(13); sheets={}; first=1
 for name,source,width,height in SHEETS:
  sheets[name]=(first,width//16,width*height//256); first+=width*height//256
 layers={n:[0]*(W*H) for n in ('Back','Buildings','Front','AlwaysFront')}
 def put(layer,x,y,sheet,index):
  assert 0<=x<W and 0<=y<H
  start,_,count=sheets[sheet]; assert 0<=index<count
  layers[layer][y*W+x]=start+index
 for y in range(H):
  for x in range(W):
   n=rng.random(); put('Back',x,y,'outdoors',151 if n<.025 else 152 if n<.05 else 175)
 dirt=set()
 def area(x0,y0,x1,y1):
  dirt.update((x,y) for y in range(y0,y1+1) for x in range(x0,x1+1))
 area(6,10,30,13); area(17,8,19,26)
 for hx in (4,15,26): area(hx+1,7,hx+3,11)
 area(9,14,14,18); area(25,17,32,23)
 # Verified grass-edged dirt patch; right source column is 3, not 2.
 patch=[[200,201,203],[225,226,228],[250,251,253]]
 for x,y in dirt:
  col=0 if (x-1,y) not in dirt else 2 if (x+1,y) not in dirt else 1
  row=0 if (x,y-1) not in dirt else 2 if (x,y+1) not in dirt else 1
  put('Back',x,y,'outdoors',patch[row][col])
 for y in range(15,18):
  for x in range(10,14): put('Back',x,y,'outdoors',226)
 # Complete first-stage 5x7 cabins; roofs extend north of 5x5 footprints.
 for hx,sheet in zip((4,15,26),('log','stone','plank')):
  for sy in range(7):
   for sx in range(5): put('Front' if sy<2 else 'Buildings',hx+sx,1+sy,sheet,sy*15+sx)
 # Vanilla rail fence is two tiles tall; only its original bottom row blocks.
 for y in (14,18):
  for x in range(9,15):
   top=358 if x==9 else 360 if x==14 else 359
   put('Front',x,y-1,'outdoors',top)
   put('Buildings',x,y,'outdoors',top+25)
 for x,y in ((22,11),(15,12)):
  put('Front',x-1,y,'outdoors',387); put('Buildings',x,y,'outdoors',388)
 def craftable(n,x,y):
  sx,sy=n%8,n//8*2
  put('Front',x,y-1,'craftables',sy*8+sx)
  put('Buildings',x,y,'craftables',(sy+1)*8+sx)
 craftable(146,19,11); craftable(130,15,16); craftable(37,18,25)
 for hx in (4,15,26): craftable(37,hx+4,9)
 # Wide well east of bench, original collision/trigger anchor (23,12).
 for sy in range(5):
  for sx in range(3): put('Buildings' if (sx,sy)==(0,4) else 'Front',23+sx,8+sy,'well',sy*3+sx)
 pond={(x,y) for y in range(18,23) for x in range(26,32) if (x-28.5)**2/10+(y-20)**2/6<1}
 for x,y in pond:
  put('Back',x,y,'outdoors',1229); put('Buildings',x,y,'outdoors',1229)
 # Real water beneath transparent vanilla bank pieces.
 for y in range(18,23):
  for x in range(26,32):
   if y==18: n=1182 if x==26 else 1184 if x==31 else 1183
   elif y==22: n=1257 if x==26 else 1311 if x==31 else 1258
   elif x==26: n=1207
   elif x==31: n=1209
   else: continue
   put('Back',x,y,'outdoors',1229); put('Front',x,y,'outdoors',n)
 put('Front',29,20,'outdoors',1293)  # vanilla lily pad
 # Empty upper campfire tile provides invisible blocking boundary.
 blank=(146//8*2)*8+146%8
 for y in range(H):
  for x in range(W):
   if x in (0,1,34,35) or y in (0,1,27):
    if not layers['Buildings'][y*W+x]: put('Buildings',x,y,'craftables',blank)
 def tree(x,y,variant=0):
  for sy in range(6):
   for sx in range(3):
    dx,dy=x+sx,y+sy
    if 0<=dx<W and 0<=dy<H: put('AlwaysFront',dx,dy,'outdoors',sy*25+sx+variant*3)
 for y in (-3,3,9,15,21): tree(-1,y); tree(34,y,1)
 for x in range(2,34,3):
  if x != 17: tree(x,25,(x//3)%2)
 for x in (2,10,12,21,23,32):
  for sx in range(2): put('Front',x+sx,0,'outdoors',sx+3)
 m=ET.Element('map',version='1.0',orientation='orthogonal',renderorder='right-down',width=str(W),height=str(H),tilewidth='16',tileheight='16')
 props=ET.SubElement(m,'properties')
 for n,v in [('Outdoors','T'),('NoSpawn','All'),('NoFurniture','T')]: ET.SubElement(props,'property',name=n,value=v)
 for name,source,width,height in SHEETS:
  start,cols,count=sheets[name]
  ts=ET.SubElement(m,'tileset',firstgid=str(start),name=name,tilewidth='16',tileheight='16',tilecount=str(count),columns=str(cols))
  ET.SubElement(ts,'image',source=source,width=str(width),height=str(height))
 for name,data in layers.items():
  layer=ET.SubElement(m,'layer',name=name,width=str(W),height=str(H))
  ET.SubElement(layer,'data',encoding='csv').text='\n'+',\n'.join(','.join(map(str,data[y*W:(y+1)*W])) for y in range(H))+'\n'
 ET.indent(m); ET.ElementTree(m).write(ROOT/'BrawnieHollow/assets/hollow.tmx',encoding='utf-8',xml_declaration=True)
 print('Generated hollow.tmx with six vanilla references; sprites untouched.')
if __name__=='__main__': generate()