"""Láminas técnicas de GMSoft, sin actores ni circuitos de usuario.

Reutiliza el renderizador local de bocetos; requiere Python y Pillow.
No conecta servicios, no lee secretos y no instala dependencias de las apps.
"""
from pathlib import Path
import importlib.util
import sys

sys.dont_write_bytecode = True

OUT = Path(__file__).resolve().parent
spec = importlib.util.spec_from_file_location('bocetos_gmsoft', OUT.parent/'caso-de-estudio'/'generar_diagramas.py')
sketch = importlib.util.module_from_spec(spec)
spec.loader.exec_module(sketch)
sketch.OUT = OUT
Board = sketch.Board
BLUE, GREEN, PURPLE, ORANGE, NEUTRAL = sketch.BLUE, sketch.GREEN, sketch.PURPLE, sketch.ORANGE, sketch.NEUTRAL


def stack():
    b = Board('Stack y capas', 'stack-capas')
    b.text('title', 50, 28, 1490, 'GMSoft · stack y dependencias', 37)
    b.text('subtitle', 52, 91, 1490, 'SPA React / API .NET 10 / PostgreSQL · monolito modular · código al 19/10/2026', 22)
    b.shape('browser-boundary', 50, 160, 1500, 225, PURPLE, True)
    b.text('browser-caption', 76, 178, 1400, 'NAVEGADOR · ejecución del frontend', 23)
    b.shape('backend-boundary', 50, 475, 1040, 625, BLUE, True)
    b.text('backend-caption', 435, 493, 620, 'BACKEND .NET 10 · un proceso', 23)
    b.arrow('react-http', [(528,290),(582,290)], source='react', target='http')
    b.arrow('send-request', [(795,363),(795,420),(215,420),(215,542)], source='http',target='api')
    b.arrow('api-app', [(358,630),(432,630)], True, source='api',target='application')
    b.arrow('app-domain', [(575,718),(575,797)], True, source='application',target='domain')
    b.arrow('api-data', [(358,665),(400,665),(400,1020),(925,1020),(925,978)], True, source='api',target='data')
    b.arrow('data-app', [(925,782),(925,750),(680,750),(680,718)], True, source='data',target='application')
    b.arrow('data-domain', [(782,865),(718,865)], True, source='data',target='domain')
    b.arrow('database', [(1068,860),(1120,860),(1120,630),(1152,630)], source='data',target='postgres')
    b.node('react',80,230,440,125,'REACT 19 + TYPESCRIPT 6\nVite 8 / Tailwind 4\nRouter + TanStack Query',PURPLE,23)
    b.node('http',590,230,410,125,'CLIENTE HTTP\nfetch + Bearer JWT\nErrores ProblemDetails',NEUTRAL,23)
    b.node('browser-state',1070,230,450,125,'ESTADO LOCAL\nQuery: memoria\nToken + outbox: localStorage',ORANGE,22)
    b.text('https-label',820,402,690,'HTTP(S) / JSON · autorización en la API',21)
    b.node('api',80,550,270,160,'API\nControllers / DI\nJWT + autorización\nSerilog + errores',BLUE,20)
    b.node('application',440,550,270,160,'APPLICATION\nMediatR + Mapster\nFluentValidation\nContratos / interfaces',GREEN,20)
    b.node('postgres',1160,550,360,160,'POSTGRESQL\nPersistencia relacional\nUUID · numeric(18,2)\nÍndices / FK / transacciones',BLUE,20)
    b.node('domain',440,805,270,130,'DOMAIN\nEntidades + enums\nSin EF Core ni HTTP',NEUTRAL,20)
    b.node('data',790,790,270,180,'DATA\nEF Core + Npgsql\nIdentity / repositorios\nImplementa contratos',PURPLE,20)
    b.text('composition-note',80,805,270,'API compone las capas.\nApplication no conoce Data.\nData implementa sus puertos.',20)
    b.text('database-caption',1160,490,360,'SERVICIO DE DATOS',23,'center')
    b.text('sql-caption',1150,750,380,'SQL / Npgsql\nFuera del proceso .NET',21,'center')
    b.text('legend',50,1130,1490,'Sólida = comunicación. Discontinua = dependencia entre proyectos.\nNode construye el frontend; en producción la web sirve archivos estáticos desde Nginx.',21)
    return b


def transactions():
    b = Board('Datos y transacciones', 'datos-transacciones')
    b.text('title',50,28,1490,'GMSoft · datos, transacciones y concurrencia',35)
    b.text('subtitle',52,91,1490,'Unidad de trabajo + transacción SQL + bloqueos + restricciones · sin transacción distribuida',22)
    b.shape('tx-boundary',50,450,1500,550,BLUE,True)
    b.text('tx-caption',580,470,940,'POSTGRESQL · transacción del caso de uso',24)
    b.arrow('send-request',[(498,250),(562,250)],source='request',target='api')
    b.arrow('begin-tx',[(790,348),(790,400),(290,400),(290,542)],source='api',target='begin')
    b.arrow('to-write',[(508,635),(582,635)],source='begin',target='write')
    b.arrow('to-commit',[(1018,635),(1092,635)],source='write',target='commit')
    b.arrow('return-response',[(1310,542),(1310,348)],source='commit',target='response')
    b.node('request',50,160,440,180,'ENVÍO DEL NAVEGADOR\nclientRequestId (UUID)\nexpectedSessionId\nCuerpo conservado en outbox',PURPLE,22)
    b.node('api',570,160,440,180,'API\nJWT + permisos\nFluentValidation\nConsulta de reintento previo',GREEN,23)
    b.node('response',1100,160,420,180,'RESPUESTA\nÉxito o ProblemDetails\nSi se pierde después del commit:\nrepetir con la misma clave',ORANGE,21)
    b.node('begin',80,550,420,170,'1. BEGIN\nFOR UPDATE sobre el recurso\nReleer estado y clave\nValidar con datos actuales',BLUE,21)
    b.node('write',590,550,420,170,'2. ESCRITURAS\nEntidad y detalles\nMovimientos + saldos\nRegistros asociados',GREEN,22)
    b.node('commit',1100,550,420,170,'3. COMMIT\nConfirmar todo\nExcepción antes del commit:\ntransacción sin confirmar',BLUE,21)
    b.node('constraints',80,790,1440,155,'INTEGRIDAD Y CONCURRENCIA\nUNIQUE parcial · claves UUID únicas · FK RESTRICT · CHECK\nUPSERT / ON CONFLICT · advisory locks · bloqueo de tabla en operaciones puntuales\nConcurrencia optimista selectiva; no un versionado global de todas las filas',NEUTRAL,21)
    b.text('limits',50,1040,1490,'No fija un aislamiento Serializable. CreateExecutionStrategy existe; no aparece EnableRetryOnFailure.\nLa outbox está en el navegador: no es una cola transaccional de servidor ni garantiza «exactly once».',21)
    return b


def docker_security():
    b = Board('Docker, red y seguridad','docker-seguridad')
    b.text('title',50,28,1490,'GMSoft · Docker, red y seguridad',36)
    b.text('subtitle',52,91,1490,'Topología prevista en los archivos de despliegue; no verificada en ejecución.',23)
    b.shape('web-image',50,450,650,500,PURPLE,True)
    b.shape('api-image',820,450,700,500,BLUE,True)
    b.text('web-caption',78,473,600,'DOCKERFILE WEB · multietapa',24)
    b.text('api-caption',850,473,640,'DOCKERFILE API · multietapa',24)
    b.arrow('https',[(478,250),(582,250)],source='browser',target='edge')
    b.arrow('edge-web',[(790,348),(790,400),(520,400),(520,527)],source='edge',target='web-runtime')
    b.arrow('edge-api',[(900,348),(900,385),(1345,385),(1345,527)],source='edge',target='api-runtime')
    b.arrow('copy-web',[(358,610),(382,610)],source='web-build',target='web-runtime')
    b.arrow('copy-api',[(1128,610),(1192,610)],source='api-build',target='api-runtime')
    b.arrow('sql',[(1498,610),(1560,610),(1560,250),(1528,250)],source='api-runtime',target='postgres')
    b.node('browser',50,160,420,180,'NAVEGADOR\nSPA + fetch a la API\nJWT en localStorage\nCSP incorporada en el build',PURPLE,22)
    b.node('edge',590,160,420,180,'EDGE / PROXY\nHTTPS termina aquí\nHTTP hacia los servicios\nForwarded headers',NEUTRAL,22)
    b.node('postgres',1100,160,420,180,'POSTGRESQL EXTERNO\nRed privada prevista\nPersistencia fuera de las apps\nTLS según conexión',BLUE,21)
    b.text('https-label',480,197,100,'HTTPS',18,'center')
    b.text('sql-label',1490,425,55,'SQL',18,'center')
    b.node('web-build',80,535,270,150,'BUILD\nnode:22-alpine\nnpm ci + build\ndist/ + URL API',PURPLE,20)
    b.node('web-runtime',390,535,270,150,'RUNTIME\nnginx:alpine\nHTML / JS / CSS\nPORT / 8080',PURPLE,20)
    b.node('api-build',850,535,270,150,'BUILD\nSDK .NET 10\nrestore + publish\nRelease',BLUE,20)
    b.node('api-runtime',1200,535,290,150,'RUNTIME .NET 10\nKestrel · USER app\nHTTP en PORT / 8080\nJWT + autorización',BLUE,20)
    b.text('web-controls',80,740,580,'Nginx: rutas SPA y caché de assets\nCSP / frame-ancestors / HSTS\nVITE_API_URL: valor público de build\nNo hay Node.js en el runtime final',21)
    b.text('api-controls',850,740,640,'Autenticación requerida por defecto\nJWT HS256 + revisión de cuenta/sello\nLogin: rate limit y bloqueo temporal\nSecretos por entorno; Swagger solo dev',21)
    b.text('footer',50,1004,1490,'PostgreSQL no está dentro de estas imágenes. No se encontró Docker Compose.\nLos Dockerfiles no administran certificados, backups ni el volumen de PostgreSQL.\n/api/health comprueba el proceso; no consulta la base. TLS público e interno son controles distintos.',21)
    return b


if __name__=='__main__':
    boards=[stack(),transactions(),docker_security()]
    for board in boards:
        sketch.export_board(board)
    sketch.export_editable(boards,'gmsoft-tecnico.excalidraw')
    print('Tres láminas técnicas generadas: PNG, SVG y escena editable.')
