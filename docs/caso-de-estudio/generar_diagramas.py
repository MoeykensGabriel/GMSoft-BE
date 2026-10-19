"""Genera dos láminas de boceto y una escena Excalidraw editable. Python + Pillow.

No lee la base de datos ni modifica las aplicaciones. Todas las salidas quedan
junto a este archivo. Las figuras comparten coordenadas y etiquetas entre formatos.
"""
from __future__ import annotations

import base64
import hashlib
import html
import json
import math
import random
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

OUT = Path(__file__).resolve().parent
WIDTH, HEIGHT = 1600, 1200
BACKGROUND = '#fffdf8'
INK = '#243445'
BLUE = '#dcecfb'
GREEN = '#e4f2e0'
PURPLE = '#ece5f8'
ORANGE = '#fff0d3'
NEUTRAL = '#f5f3ef'
FONT_PATH = Path('C:/Windows/Fonts/comic.ttf')
if not FONT_PATH.exists():
    raise SystemExit('Definir FONT_PATH con una fuente TTF de estilo manuscrito.')


def font(size):
    return ImageFont.truetype(str(FONT_PATH), round(size))


def wrap(text, width, size):
    result = []
    for paragraph in text.split('\n'):
        if not paragraph:
            result.append('')
            continue
        line = ''
        for word in paragraph.split():
            candidate = f'{line} {word}'.strip()
            if line and font(size).getlength(candidate) > width:
                result.append(line)
                line = word
            else:
                line = candidate
        result.append(line)
    assert all(font(size).getlength(line) <= width + 1 for line in result), text
    return result


class Board:
    def __init__(self, name):
        self.name = name
        self.items = []

    def shape(self, key, x, y, w, h, fill=NEUTRAL, dashed=False):
        self.items.append(dict(kind='shape', key=key, x=x, y=y, w=w, h=h,
                               fill=fill, dashed=dashed))

    def text(self, key, x, y, w, text, size=22, align='left', container=None):
        lines = wrap(text, w, size)
        height = len(lines) * size * 1.35
        self.items.append(dict(kind='text', key=key, x=x, y=y, w=w, h=height,
                               text='\n'.join(lines), size=size, align=align,
                               container=container))
        return height

    def node(self, key, x, y, w, h, text, fill=NEUTRAL, size=22):
        self.shape(key, x, y, w, h, fill)
        lines = wrap(text, w-36, size)
        text_height = len(lines) * size * 1.35
        assert text_height <= h-16, (key, text_height, h)
        self.text(key+'-label', x+18, y+(h-text_height)/2, w-36,
                  '\n'.join(lines), size, 'center', key)

    def arrow(self, key, points, dashed=False, both=False, source=None, target=None):
        self.items.append(dict(kind='arrow', key=key, points=points,
                               dashed=dashed, both=both, source=source, target=target))


def architecture():
    b = Board('Usuarios y arquitectura')
    b.text('title', 50, 28, 1490, 'GMSoft · de la oficina a la calle', 38)
    b.text('subtitle', 52, 91, 1490, 'Usuarios, componentes y datos · análisis del código al 19/10/2026', 23)
    b.shape('frontend', 40, 320, 1000, 340, PURPLE, True)
    b.shape('backend', 40, 735, 1000, 365, BLUE, True)
    b.arrow('admin-ui', [(270, 269), (270, 390)], source='admin', target='admin-view')
    b.arrow('driver-ui', [(790, 269), (790, 390)], source='driver', target='driver-view')
    b.arrow('physical', [(1007, 211), (1153, 211)], True, True, 'driver', 'customer')
    b.arrow('http', [(205, 633), (205, 797)], source='shared', target='api')
    b.arrow('api-app', [(347, 875), (392, 875)], source='api', target='application')
    b.arrow('app-data', [(667, 875), (712, 875)], source='application', target='data')
    b.arrow('data-db', [(1007, 875), (1152, 875)], source='data', target='postgres')
    b.arrow('app-domain', [(530, 953), (530, 987)], source='application', target='domain')
    b.node('admin', 60, 160, 420, 105, 'ADMIN / OFICINA\nPrepara, controla y recibe', BLUE, 24)
    b.node('driver', 580, 160, 420, 105, 'CHOFER / REPARTIDOR\nOpera durante la salida', GREEN, 24)
    b.node('customer', 1160, 160, 380, 105, 'CLIENTE / PROSPECTO\nSin cuenta propia en el sistema', ORANGE, 22)
    b.text('physical-label', 1013, 160, 140, 'trato\npresencial', 18, 'center')
    b.text('fe-caption', 65, 334, 170, 'FRONTEND\nWEB', 22)
    b.text('fe-caption-detail', 345, 334, 400, 'Una app · dos experiencias\nsegún el rol', 20)
    b.node('admin-view', 70, 397, 420, 125,
           'PANEL ADMIN (escritorio)\nCatálogo · recorridos · cargas\nRecepción · caja · reportes', BLUE, 22)
    b.node('driver-view', 570, 397, 420, 125,
           'CHOFER (móvil)\nRecorrido · ventas · envases\nVisitas · promociones', GREEN, 22)
    b.node('shared', 70, 550, 920, 75,
           'React + TypeScript + Vite\nRouter · Query · UI común · HTTP/JWT · outbox local', PURPLE, 21)
    b.node('two-balances', 1160, 340, 380, 290,
           'DOS SALDOS DIFERENTES\n\nDeuda de dinero ($)\nEnvases en poder del cliente\n\nPagar no devuelve el envase.\nDevolverlo no cancela la deuda.', ORANGE, 21)
    b.text('http-label', 230, 679, 770, 'HTTP / JSON + Bearer JWT\nEl navegador nunca consulta la BD directamente.', 21)
    b.text('be-caption', 405, 754, 610, 'UN BACKEND · monolito modular .NET 10', 23)
    b.node('api', 70, 805, 270, 140,
           'API\nControllers + permisos\nComposición / DI', BLUE, 22)
    b.node('application', 400, 805, 260, 140,
           'APPLICATION\nCasos de uso\nValidación + contratos', GREEN, 20)
    b.node('data', 720, 805, 280, 140,
           'DATA\nEF Core / Npgsql\nIdentity · migraciones', PURPLE, 22)
    b.node('domain', 400, 995, 260, 75, 'DOMAIN\nEntidades y enums', NEUTRAL, 21)
    b.node('postgres', 1160, 805, 380, 230,
           'POSTGRESQL\nUna base relacional\n\nNegocio + cuentas Identity\nSaldos, movimientos e historial', BLUE, 22)
    b.text('sql-label', 1049, 838, 95, 'SQL', 20, 'center')
    b.text('dependency-note', 70, 1010, 305, 'Llamadas en ejecución.\nData implementa los contratos\nde Application.', 17)
    b.text('domain-note', 700, 1011, 300, 'Domain no conoce\nEF Core, HTTP ni Identity.', 18)
    b.text('footer', 50, 1130, 1490,
           'Flecha sólida: uso / llamada. Discontinua: interacción presencial.\nVista lógica: no afirma dónde está desplegado el sistema. El detalle de dependencias está en el informe.', 19)
    return b


def workflow():
    b = Board('Ciclo del reparto y controles')
    b.text('title', 50, 28, 1490, 'Un reparto · tres controles que deben cerrar', 36)
    b.text('subtitle', 52, 91, 1490, 'La salida vincula productos, visitas, dinero y envases desde la carga hasta la recepción.', 22)
    for i, x in enumerate([50, 440, 830]):
        b.arrow(f'next-{i}', [(x+348, 265), (x+382, 265)], source=f'step-{i+1}', target=f'step-{i+2}')
    b.arrow('close-login', [(1390, 378), (1390, 403)], source='step-4', target='login')
    b.arrow('stock', [(1000, 378), (1000, 555), (275, 555), (275, 642)], source='step-3', target='stock-ledger')
    b.arrow('containers', [(1000, 378), (1000, 555), (800, 555), (800, 642)], source='step-3', target='container-ledger')
    b.arrow('money', [(1000, 378), (1000, 555), (1325, 555), (1325, 642)], source='step-3', target='money-ledger')
    b.node('step-1', 50, 160, 340, 210,
           '1. ADMIN · CARGA\nCamión y chofer\nLlenos + días\nResumen y confirmación', BLUE)
    b.node('step-2', 440, 160, 340, 210,
           '2. ABRIR SALIDA\nChofer: zona + km\nConsume carga preparada\nADMIN también puede abrir', GREEN)
    b.node('step-3', 830, 160, 340, 210,
           '3. VISITAR\nVenta / no compró\nEnvases / préstamos\nPruebas con prospectos', PURPLE)
    b.node('step-4', 1220, 160, 340, 210,
           '4. ADMIN · RECEPCIÓN\nContar llenos + vacíos\nKilómetros + efectivo\nCerrar y ver diferencias', BLUE)
    b.node('login', 1220, 411, 340, 104,
           'Salida cerrada\nChofer vuelve al login\nal sincronizar con la API', ORANGE, 20)
    b.text('atomic', 50, 462, 760, 'La visita se registra de forma atómica:', 25)
    b.text('atomic-detail', 50, 507, 760, '«No compró» no mueve stock, dinero ni envases.', 20)
    b.node('stock-ledger', 50, 650, 450, 230,
           'PRODUCTOS A BORDO\n\nCarga inicial + recargas\n− llenos entregados\n+ vacíos recuperados\n− descarga en recepción', BLUE, 22)
    b.node('container-ledger', 575, 650, 450, 230,
           'ENVASES Y PRÉSTAMOS\n\nSaldo anterior\n+ entregados al cliente\n− recuperados\nCantidades, no dinero', ORANGE, 22)
    b.node('money-ledger', 1100, 650, 450, 230,
           'DINERO\n\nVentas − cobros = deuda\nCobrar completo o dejar a cuenta\nEfectivo separado de\ntransferencia y tarjeta', GREEN, 22)
    b.shape('rules-box', 50, 930, 1500, 210, NEUTRAL, True)
    b.text('rules', 76, 949, 1448,
           'Sin carga no se abre · una salida por chofer y camión · sin stock no se vende.\n'
           'Mismo envío, misma clave: reintentar no debe duplicar la operación.\n'
           'La recepción compara lo esperado con lo contado; las diferencias quedan visibles.\n'
           'Promociones: circuito propio de prueba, retiro y posible conversión a cliente.', 22)
    return b


def uid(board, key):
    return hashlib.sha256(f'{board.name}/{key}'.encode()).hexdigest()[:20]


def rough_points(points, seed, closed=False):
    rng = random.Random(seed)
    result = []
    pairs = list(zip(points, points[1:]+([points[0]] if closed else [])))
    for start, end in pairs:
        count = max(2, int(math.dist(start, end) / 45))
        for i in range(count):
            t = i / count
            result.append((start[0]+(end[0]-start[0])*t+rng.uniform(-.75,.75),
                           start[1]+(end[1]-start[1])*t+rng.uniform(-.75,.75)))
    result.append(points[0] if closed else points[-1])
    return result


def draw_segment(draw, points, fill, width, dashed=False, scale=2):
    if not dashed:
        draw.line([(round(x*scale), round(y*scale)) for x,y in points], fill=fill, width=width, joint='curve')
        return
    for (x1,y1),(x2,y2) in zip(points, points[1:]):
        length = math.dist((x1,y1),(x2,y2))
        if not length:
            continue
        for start in range(0, math.ceil(length), 15):
            end = min(start+8, length)
            a = (x1+(x2-x1)*start/length, y1+(y2-y1)*start/length)
            z = (x1+(x2-x1)*end/length, y1+(y2-y1)*end/length)
            draw.line([(round(x*scale), round(y*scale)) for x,y in [a,z]], fill=fill, width=width)


def export_board(board):
    image = Image.new('RGB', (WIDTH*2, HEIGHT*2), BACKGROUND)
    draw = ImageDraw.Draw(image)
    embedded = base64.b64encode(FONT_PATH.read_bytes()).decode()
    svg = [f'<svg xmlns="http://www.w3.org/2000/svg" width="{WIDTH}" height="{HEIGHT}" viewBox="0 0 {WIDTH} {HEIGHT}" role="img" aria-labelledby="title desc">',
           f'<title id="title">GMSoft: {html.escape(board.name)}</title>',
           '<desc id="desc">Diagrama de estilo boceto basado en el código al 19 de octubre de 2026. Texto y flechas editables en el archivo Excalidraw adjunto.</desc>',
           f'<style>@font-face{{font-family:Sketch;src:url(data:font/ttf;base64,{embedded})}}text{{font-family:Sketch,"Comic Sans MS",cursive;fill:{INK}}}</style>',
           f'<rect width="{WIDTH}" height="{HEIGHT}" fill="{BACKGROUND}"/>']
    for item in board.items:
        seed = int(uid(board, item['key'])[:8], 16)
        if item['kind'] == 'shape':
            x,y,w,h = [item[k] for k in ['x','y','w','h']]
            assert 0 <= x < x+w <= WIDTH and 0 <= y < y+h <= HEIGHT
            draw.rounded_rectangle([x*2,y*2,(x+w)*2,(y+h)*2], radius=10, fill=item['fill'])
            svg.append(f'<rect x="{x}" y="{y}" width="{w}" height="{h}" rx="5" fill="{item["fill"]}"/>')
            pts = [(x,y),(x+w,y),(x+w,y+h),(x,y+h)]
            for pass_ in range(2):
                points = rough_points(pts, seed+pass_, True)
                draw_segment(draw, points, INK, 2 if pass_ else 3, item['dashed'])
                stroke = ' '.join(f'{a:.2f},{z:.2f}' for a,z in points)
                dash = 'stroke-dasharray="8 7"' if item['dashed'] else ''
                svg.append(f'<polyline points="{stroke}" fill="none" stroke="{INK}" stroke-width="{1 if pass_ else 1.5}" {dash} stroke-linejoin="round" stroke-linecap="round"/>')
        elif item['kind'] == 'arrow':
            points = item['points']
            stroke = ' '.join(f'{a},{z}' for a,z in points)
            dash = 'stroke-dasharray="8 7"' if item['dashed'] else ''
            svg.append(f'<polyline points="{stroke}" fill="none" stroke="{INK}" stroke-width="1.8" {dash} stroke-linejoin="round" stroke-linecap="round"/>')
            draw_segment(draw, points, INK, 4, item['dashed'])
            heads = [(points[-2], points[-1])]
            if item['both']:
                heads.append((points[1], points[0]))
            for before, tip in heads:
                angle = math.atan2(tip[1]-before[1], tip[0]-before[0])
                sides = [(tip[0]-12*math.cos(angle+a),tip[1]-12*math.sin(angle+a)) for a in [-.45,.45]]
                head = [sides[0],tip,sides[1]]
                draw_segment(draw, head, INK, 4)
                svg.append(f'<polyline points="{" ".join(f"{x:.2f},{y:.2f}" for x,y in head)}" fill="none" stroke="{INK}" stroke-width="1.8" stroke-linecap="round"/>')
        else:
            assert item['y']+item['h'] <= HEIGHT, item['key']
            lines = item['text'].split('\n')
            size = item['size']
            actual_font = font(size*2)
            for i,line in enumerate(lines):
                x = item['x']
                if item['align'] == 'center':
                    x += (item['w']-font(size).getlength(line))/2
                baseline = item['y']+size+i*size*1.35
                draw.text((x*2,baseline*2),line,font=actual_font,fill=INK,anchor='ls')
                svg.append(f'<text x="{x:.2f}" y="{baseline:.2f}" font-size="{size}">{html.escape(line)}</text>')
    svg.append('</svg>')
    stem = 'usuarios-arquitectura' if board.name.startswith('Usuarios') else 'ciclo-reparto'
    image.save(OUT/f'{stem}.png', optimize=True)
    (OUT/f'{stem}.svg').write_text('\n'.join(svg), encoding='utf-8')


def export_editable(boards):
    elements = []
    for index,board in enumerate(boards):
        offset = index * (WIDTH+120)
        frame_id = uid(board,'frame')
        def base(key, type_, x, y, w, h):
            seed = int(uid(board,key)[:8],16) % (2**31-1)
            return dict(id=uid(board,key),type=type_,x=x+offset,y=y,width=w,height=h,angle=0,
                        strokeColor=INK,backgroundColor='transparent',fillStyle='solid',
                        strokeWidth=1.5,strokeStyle='solid',roughness=1.3,opacity=100,
                        groupIds=[],frameId=frame_id,roundness=None,seed=seed,version=1,
                        versionNonce=seed,index=None,isDeleted=False,boundElements=[],
                        updated=1792364400000,link=None,locked=False)
        frame = base('frame','frame',0,0,WIDTH,HEIGHT)
        frame.update(frameId=None,name=f'{index+1:02d} · {board.name}')
        elements.append(frame)
        for item in board.items:
            key = item['key']
            kind = item['kind']
            if kind == 'shape':
                e = base(key,'rectangle',*[item[k] for k in ['x','y','w','h']])
                e.update(backgroundColor=item['fill'],roundness={'type':3},
                         strokeStyle='dashed' if item['dashed'] else 'solid')
            elif kind == 'text':
                e = base(key,'text',*[item[k] for k in ['x','y','w','h']])
                e.update(fontSize=item['size'],fontFamily=1,text=item['text'],originalText=item['text'],
                         textAlign=item['align'],verticalAlign='middle',lineHeight=1.35,
                         autoResize=True,containerId=uid(board,item['container']) if item['container'] else None)
            else:
                pts = item['points']
                x,y=pts[0]
                e = base(key,'arrow',x,y,max(p[0] for p in pts)-min(p[0] for p in pts),max(p[1] for p in pts)-min(p[1] for p in pts))
                e.update(points=[[px-x,py-y] for px,py in pts],lastCommittedPoint=None,
                         startBinding={'elementId':uid(board,item['source']),'focus':0,'gap':8} if item['source'] else None,
                         endBinding={'elementId':uid(board,item['target']),'focus':0,'gap':8} if item['target'] else None,
                         startArrowhead='arrow' if item['both'] else None,endArrowhead='arrow',
                         elbowed=False,roundness={'type':2},strokeStyle='dashed' if item['dashed'] else 'solid')
            elements.append(e)
    by_id={e['id']:e for e in elements}
    for e in elements:
        if e['type']=='text' and e.get('containerId'):
            by_id[e['containerId']]['boundElements'].append({'id':e['id'],'type':'text'})
        if e['type']=='arrow':
            for binding in [e['startBinding'],e['endBinding']]:
                if binding:
                    by_id[binding['elementId']]['boundElements'].append({'id':e['id'],'type':'arrow'})
    assert len(by_id)==len(elements), 'IDs repetidos'
    assert all(e['frameId'] is None or e['frameId'] in by_id for e in elements)
    for e in elements:
        assert all(bound['id'] in by_id for bound in e['boundElements'])
    scene=dict(type='excalidraw',version=2,source='https://excalidraw.com',elements=elements,
               appState=dict(viewBackgroundColor=BACKGROUND,gridSize=None,zoom={'value':.65},
                             scrollX=20,scrollY=20),files={})
    (OUT/'gmsoft-arquitectura.excalidraw').write_text(json.dumps(scene,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    print(f'Escena editable: {len(elements)} elementos; vínculos e IDs verificados.')


if __name__=='__main__':
    boards=[architecture(),workflow()]
    for board in boards:
        export_board(board)
    export_editable(boards)
    print('Dos PNG y dos SVG generados sin recortes de texto.')
