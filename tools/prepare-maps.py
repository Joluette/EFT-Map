"""Prepare offline SVG layers and exact CRS calibration. Optional developer tool."""
import argparse, json, math, pathlib, tempfile, xml.etree.ElementTree as ET

p = argparse.ArgumentParser()
p.add_argument('--root', default=str(pathlib.Path(__file__).resolve().parents[1]))
p.add_argument('--scratch', default=None)
args = p.parse_args()
root = pathlib.Path(args.root)
assets = root / 'assets'
source = json.loads((assets / 'maps.upstream.json').read_text(encoding='utf-8-sig'))
names = {'customs':'Customs','factory':'Factory','ground-zero':'Ground Zero','interchange':'Interchange',
         'the-lab':'The Lab / Dark','the-labyrinth':'The Labyrinth','lighthouse':'Lighthouse',
         'reserve':'Reserve','shoreline':'Shoreline','streets-of-tarkov':'Streets of Tarkov',
         'terminal':'Terminal','woods':'Woods','icebreaker':'Icebreaker'}
ET.register_namespace('', 'http://www.w3.org/2000/svg')
ET.register_namespace('xlink', 'http://www.w3.org/1999/xlink')
out, jobs = [], []
scratch = pathlib.Path(args.scratch) if args.scratch else pathlib.Path(tempfile.mkdtemp(prefix='eft-map-prepare-'))
scratch.mkdir(exist_ok=True,parents=True)
(assets / 'maps').mkdir(exist_ok=True)
(assets / 'vectors').mkdir(exist_ok=True)

def crs(m, x, z):
    r=math.radians(m.get('coordinateRotation',0)); c,s=math.cos(r),math.sin(r)
    sx,tx,sz,ty=m.get('transform',[1,0,1,0])
    return sx*(x*c-z*s)+tx, -sz*(x*s+z*c)+ty

def extent(m, bounds):
    xs=[b[0] for b in bounds]; zs=[b[1] for b in bounds]
    pts=[crs(m,x,z) for x in xs for z in zs]
    return min(v[0] for v in pts),min(v[1] for v in pts),max(v[0] for v in pts),max(v[1] for v in pts)

def matrix(m, left, top, fx, fy):
    r=math.radians(m.get('coordinateRotation',0)); c,s=math.cos(r),math.sin(r)
    sx,tx,sz,ty=m.get('transform',[1,0,1,0])
    return [sx*c*fx,-sx*s*fx,-sz*s*fy,-sz*c*fy,(tx-left)*fx,(ty-top)*fy]

for location in source:
    m=next((m for m in location['maps'] if m.get('projection')=='interactive'),None)
    if not m: continue
    key=m['key']; vector=bool(m.get('svgPath'))
    if vector:
        filename=m['svgPath'].split('/')[-1]
        xml_path=assets/'sources'/filename
        xml=ET.parse(xml_path).getroot()
        base=m['svgLayer']
        rootgroups={g.get('id') for g in xml if g.tag.endswith('}g')}
        choices=[('base','Poziom bazowy',base,True)] + [(l['svgLayer'], l['name'], l['svgLayer'],True)
                 for l in m.get('layers',[]) if l.get('svgLayer') in rootgroups]
        choices += [('tile-'+str(i),entry['name'],entry['tilePath'],False)
                    for i,entry in enumerate(m.get('layers',[])) if entry.get('tilePath') and not entry.get('svgLayer')]
    else:
        choices=[('base','Infirmary' if key=='icebreaker' else 'Poziom bazowy',m['tilePath'],False)]
        choices += [(str(i),l['name'],l['tilePath'],False) for i,l in enumerate(m.get('layers',[]))
                    if l.get('tilePath') and l['tilePath']!=m['tilePath']]
    bounds=m.get('svgBounds',m['bounds']) if vector else m['bounds']
    l,t,r,b=extent(m,bounds)
    layers=[]
    for layerid,label,selection,is_vector in choices:
        if is_vector:
            l,t,r,b=extent(m,m.get('svgBounds',m['bounds']))
            factor=min(3210/(r-l),3210/(b-t))
            w,h=round((r-l)*factor),round((b-t)*factor)
            if key=='customs': w,h=3210,1632
            affine=matrix(m,l,t,w/(r-l),h/(b-t))
        else:
            l,t,r,b=extent(m,m['bounds'])
            zoom=0
            while max(r-l,b-t)*2**(zoom+1)<=2800 and zoom < m['maxZoom']: zoom+=1
            scale=2**zoom
            pl,pt=math.floor(l*scale),math.floor(t*scale)
            pr,pb=math.ceil(r*scale),math.ceil(b*scale)
            w,h=pr-pl,pb-pt
            affine=matrix(m,pl/scale,pt/scale,scale,scale)
        output=f'maps/{key}-{layerid}.png'
        layer={'Id':layerid,'Name':label,'Image':output,'Width':w,'Height':h,'Affine':affine,
               'Remote':not is_vector,'TileTemplate':'','TileSize':m.get('tileSize',256),'MaxTileZoom':m['maxZoom']}
        if is_vector:
            xml=ET.parse(xml_path).getroot()
            for child in list(xml):
                if child.tag.endswith('}g') and child.get('id') not in {base,selection} and child.get('data-keep-with-group')!=base:
                    xml.remove(child)
            # Leaflet's floor overlays sit above the base map, regardless of source group order.
            if selection!=base:
                selected=next((child for child in xml if child.get('id')==selection),None)
                if selected is not None: xml.remove(selected); xml.append(selected)
            xml.set('width',str(w));xml.set('height',str(h))
            prepared=assets/'vectors'/f'{key}-{layerid}.svg'
            ET.ElementTree(xml).write(prepared,encoding='utf-8',xml_declaration=True)
            layer['VectorImage']='vectors/'+prepared.name
            jobs.append({'input':str(prepared.resolve()),'output':str((assets/output).resolve())})
        else:
            layer.update(TileTemplate=selection,TileZoom=zoom,TileLeft=pl,TileTop=pt)
        layers.append(layer)
    labels=[]
    for item in m.get('labels',[]):
        labels.append({'Text':item['text'],'X':item['position'][0],'Z':item['position'][1]})
    out.append({'Id':key,'Name':names[key],'Author':m['author'],'AuthorLink':m['authorLink'],
                'License':'CC BY-NC-SA 4.0' if vector else 'Pobrane do osobistego podglądu; prawa autorów',
                'Rotation':m.get('coordinateRotation',0),'Bounds':bounds,'Layers':layers,'Labels':labels})
out.sort(key=lambda m:m['Name'])
(assets/'maps.json').write_text(json.dumps(out,ensure_ascii=False,indent=2),encoding='utf-8')
(scratch/'jobs.json').write_text(json.dumps(jobs),encoding='utf-8')
print(json.dumps({'maps':len(out),'offlineLayers':len(jobs),'remoteMaps':[m['Id'] for m in out if m['Layers'][0]['Remote']],'jobs':str(scratch/'jobs.json')}))
