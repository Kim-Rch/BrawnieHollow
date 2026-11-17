from pathlib import Path
import xml.etree.ElementTree as ET
from PIL import Image
root=Path(__file__).resolve().parents[1]/'BrawnieHollow'/'assets'
m=ET.parse(root/'hollow.tmx').getroot()
assert (m.get('width'),m.get('height'))==('36','28')
assert {x.get('name') for x in m.findall('layer')}=={'Back','Buildings','Front','AlwaysFront'}
for layer in m.findall('layer'):
    assert len([x for x in layer.find('data').text.split(',') if x.strip()])==36*28
sources={ts.find('image').get('source') for ts in m.findall('tileset')}
assert sources=={'Maps/spring_outdoorsTileSheet','../Buildings/Log Cabin','../Buildings/Stone Cabin','../Buildings/Plank Cabin','../TileSheets/Craftables','../Buildings/Well'}, sources
assert not (root/'village.png').exists(), 'Legacy custom village atlas must not be packaged'
for layer in m.findall('layer'):
    for raw in layer.find('data').text.split(','):
        if not raw.strip(): continue
        gid=int(raw)
        if not gid: continue
        ts=max((t for t in m.findall('tileset') if int(t.get('firstgid'))<=gid),key=lambda t:int(t.get('firstgid')))
        assert gid-int(ts.get('firstgid'))<int(ts.get('tilecount')), (layer.get('name'),gid)

for name in ('Bram','Mira','Pip'):
    im=Image.open(root/(name+'.png'))
    assert im.size==(64,128)
    assert im.crop((0,64,16,80)).tobytes()!=im.crop((16,64,32,80)).tobytes()
    assert im.getbbox()
print('PASS: map layers, dimensions, tilesheet, three animated residents')
im=Image.open(root/'Bram.png')
assert im.crop((0,80,16,96)).tobytes()!=im.crop((16,80,32,96)).tobytes()
assert im.crop((0,96,16,112)).tobytes()!=im.crop((16,96,32,112)).tobytes()
print('PASS: dedicated two-frame hoe and sow animations')
