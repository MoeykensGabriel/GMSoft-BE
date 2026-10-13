"""Reparto de demostracion: camion y chofer nuevos con seis semanas de historial.

Solo para desarrollo, contra la API local. Las ventas, envases, cobros, recepciones
y rendiciones las registra la API real, igual que en un dia de trabajo; despues se
llevan las fechas de cada salida al dia que le corresponde, porque la API siempre
fecha con la hora actual. El historial termina el sabado de la semana pasada.

Uso (PowerShell, desde GM-SoftBE, con la API levantada en http://localhost:5142):
    $env:DEMO_DRIVER_PASSWORD = '<contrasena del chofer>'
    python tools/demo-route/seed.py            # solo muestra el plan
    python tools/demo-route/seed.py --apply    # lo escribe
"""
import json
import os
import random
import re
import subprocess
import sys
import time
import uuid
import glob
import urllib.error
import urllib.request
from datetime import date, datetime, timedelta, timezone

BASE = os.environ.get("GMSOFT_BASE_URL", "http://localhost:5142").rstrip("/")
ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
MANIFEST = os.path.abspath(os.path.join(ROOT, "..", "demo-route-manifest.json"))
AR = timezone(timedelta(hours=-3))
WEEKS = 6
DAYS = {1: "Lunes", 2: "Martes", 3: "Miercoles", 4: "Jueves", 5: "Viernes", 6: "Sabado"}
VEHICLE = {"name": "Renault Kangoo", "licensePlate": "AE456KT", "type": "Van", "currentKilometers": 48200}
DRIVER = {"firstName": "Marcos", "lastName": "Díaz", "documentNumber": "32456789",
          "phone": "3815550142", "userName": "reparto2"}
NEW_PRODUCT = {"detail": "Bidon 12 lts", "commercialDetail": "Agua de mesa 12 litros",
               "salePrice": 3600, "tracking": "ByBalance", "isPublished": True}
B20, B12, SODA = "Bidon 20 lts", "Bidon 12 lts", "Soda 1.5 lts"

# Un camion reparte una zona distinta cada dia; una zona grande puede ocupar dos.
ZONE_OF_DAY = {1: "Yerba Buena", 2: "San Miguel de Tucumán", 3: "Banda del Río Salí",
               4: "San Miguel de Tucumán", 5: "Las Talitas", 6: "Lules"}

# (razon social, contacto, direccion, dias, estado). En orden de recorrido dentro de cada zona.
W, R, K = "White", "Red", "Black"
CUSTOMERS = {
    "Yerba Buena": [
        (None, "Lucía Fernández", "Av. Aconquija 1250", [1], W),
        ("Kiosco El Paso", "Ramón Suárez", "Av. Aconquija 1480", [1], R),
        (None, "Martín Aguirre", "Av. Aconquija 1835, Dpto 2", [1], W),
        ("Gimnasio Punto Fit", "Carla Medina", "Av. Aconquija 2100", [1], W),
        (None, "Sofía Robles", "Lobo de la Vega 340", [1], W),
        (None, "Jorge Paz", "Lobo de la Vega 615", [1], K),
        ("Estudio Contable Nieva & Asoc.", "Daniel Nieva", "Las Rosas 220, 1° piso", [1], R),
        (None, "Valeria Ibáñez", "Frías Silva 480", [1], W),
        (None, "Ricardo Molina", "Salas y Valdez 1120, Country Las Yungas lote 14", [1], W),
    ],
    "San Miguel de Tucumán": [
        ("Farmacia del Centro", "Graciela Luna", "25 de Mayo 480", [2, 4], W),
        (None, "Hernán Villagra", "San Martín 890, 3° B", [2], W),
        ("Estudio Jurídico Campero", "Luis Campero", "Crisóstomo Álvarez 650, of. 4", [2, 4], W),
        (None, "Mariana Ortiz", "Las Piedras 1120", [2], R),
        ("Consultorio Odontológico Sonrisas", "Paula Giménez", "Av. Sarmiento 720", [2, 4], W),
        (None, "Pablo Correa", "Santiago del Estero 1340", [2], K),
        ("Panadería La Espiga", "Oscar Brizuela", "Av. Mate de Luna 1890", [2, 4], R),
        (None, "Florencia Albornoz", "Av. Mate de Luna 2450, 5° A", [2], W),
        ("Colegio San Patricio", "Marta Juárez", "Av. Belgrano 1650", [2, 4], W),
        (None, "Diego Salazar", "Av. Roca 980", [4], W),
        ("Taller Mecánico Los Primos", "Sergio Ponce", "Av. Roca 1560", [4], R),
        (None, "Natalia Herrera", "Lavalle 2230", [4], W),
        ("Ferretería Don Tito", "Alberto Reynoso", "Av. Alem 1420", [4], K),
    ],
    "Banda del Río Salí": [
        ("Carnicería El Gauchito", "Miguel Coronel", "Av. San Martín 320", [3], W),
        (None, "Rosa Aráoz", "Av. San Martín 745", [3], R),
        (None, "Gustavo Lobo", "Belgrano 210", [3], W),
        ("Almacén Doña Chela", "Celia Vera", "Belgrano 580", [3], K),
        (None, "Julieta Palacios", "Rivadavia 430", [3], W),
        (None, "Mario Décima", "Rivadavia 915", [3], R),
        ("Escuela de Fútbol Los Pumas", "Fabián Sosa", "Sarmiento 120", [3], W),
        (None, "Analía Zelaya", "Independencia 640", [3], W),
        (None, "Cristian Barrionuevo", "Barrio Arcor, Mza C casa 8", [3], K),
    ],
    "Las Talitas": [
        (None, "Verónica Gómez", "Av. Francisco de Aguirre 1250", [5], R),
        ("Lubricentro El Cruce", "Raúl Toledo", "Av. Francisco de Aguirre 1900", [5], W),
        (None, "Emilia Cárdenas", "Ruta 9 km 1301, Barrio Los Sauces casa 22", [5], W),
        (None, "Tomás Leguizamón", "Pasaje Los Lapachos 85", [5], W),
        ("Peluquería Stilo", "Romina Ávila", "San Lorenzo 310", [5], K),
        (None, "Eduardo Quiroga", "San Lorenzo 760", [5], W),
        (None, "Claudia Maldonado", "Barrio El Mirador, Mza 5 lote 3", [5], R),
        ("Vivero Las Talitas", "Hugo Santillán", "Camino del Perú 2200", [5], W),
        (None, "Agustina Ruiz", "Barrio Villa Mariano Moreno, calle 12 n° 450", [5], W),
    ],
    "Lules": [
        (None, "Nicolás Brito", "Belgrano 150", [6], W),
        ("Despensa La Esquina", "Susana Cruz", "Belgrano 420", [6], W),
        (None, "Patricia Argañaraz", "San Martín 380", [6], R),
        ("Club Atlético Almirante Brown", "Javier Rojas", "San Martín 900", [6], W),
        (None, "Federico Navarro", "9 de Julio 265", [6], W),
        (None, "Liliana Chávez", "Sarmiento 510", [6], K),
        ("Hostal La Quebrada", "Andrés Figueroa", "Ruta 301 km 20", [6], W),
        (None, "Camila Soria", "Barrio San Rafael, casa 31", [6], R),
        (None, "Roberto Díaz Vélez", "Av. Jaime Freyre 640", [6], W),
    ],
}
HOME_BASKETS = [{B20: 2}, {B20: 1}, {B12: 2}, {B12: 1, SODA: 2}, {B20: 1, B12: 1}, {B12: 3}, {B20: 3}, {SODA: 3}]
SHOP_BASKETS = [{B20: 4}, {B12: 2, B20: 2}, {B20: 2, SODA: 4}, {B12: 4}, {B20: 3, SODA: 2}]


class Api:
    def __init__(self):
        self.logins = []

    def call(self, method, path, body=None, token=None, ok=(200, 201, 204)):
        request = urllib.request.Request(BASE + path, method=method,
            data=json.dumps(body).encode() if body is not None else None,
            headers={"Content-Type": "application/json", **({"Authorization": "Bearer " + token} if token else {})})
        try:
            response = urllib.request.urlopen(request, timeout=60)
        except urllib.error.HTTPError as error:
            response = error
        with response:
            raw, code = response.read().decode("utf-8", errors="replace"), response.code
        if code not in ok:
            shown = "<credenciales omitidas>" if path == "/api/auth/login" else json.dumps(body, ensure_ascii=False)
            raise RuntimeError(f"{method} {path} -> HTTP {code}\nPedido: {shown}\nRespuesta: {raw}")
        return json.loads(raw) if raw.strip() else None

    def login(self, user, password):
        now = time.monotonic()
        self.logins = [t for t in self.logins if now - t < 61]
        if len(self.logins) >= 8:  # la API limita los intentos de login por minuto
            time.sleep(max(0, 61 - (now - self.logins[0])))
        self.logins.append(time.monotonic())
        return self.call("POST", "/api/auth/login", {"userName": user, "password": password})


def settings():
    with open(os.path.join(ROOT, "GMSoft.API", "appsettings.Development.json"), encoding="utf-8") as file:
        config = json.load(file)
    connection = next(iter(config["ConnectionStrings"].values()))
    parts = {k.strip().lower(): v for k, v in (p.split("=", 1) for p in connection.split(";") if "=" in p)}
    if parts.get("host") not in ("localhost", "127.0.0.1"):
        raise SystemExit("Solo se siembra contra una base local.")
    if "localhost" not in BASE and "127.0.0.1" not in BASE:
        raise SystemExit("Solo se siembra contra una API local.")
    return config, parts


def sql(parts, statements):
    psql = next(iter(sorted(glob.glob(r"C:\Program Files\PostgreSQL\*\bin\psql.exe"), reverse=True)), "psql")
    env = {**os.environ, "PGPASSWORD": parts["password"], "PGCLIENTENCODING": "UTF8"}
    result = subprocess.run([psql, "-h", parts["host"], "-p", parts.get("port", "5432"), "-U", parts["username"],
                             "-d", parts["database"], "-v", "ON_ERROR_STOP=1", "-q", "-At", "-c",
                             "BEGIN;\n" + "\n".join(statements) + "\nCOMMIT;"],
                            env=env, capture_output=True, text=True, encoding="utf-8")
    if result.returncode != 0:
        raise RuntimeError("psql fallo: " + result.stderr.strip())
    return result.stdout


def instant(text):
    match = re.match(r"(.*?\.\d{1,6})\d*(Z|[+-]\d\d:\d\d)$", text)
    text = (match.group(1) + match.group(2)) if match else text
    return datetime.fromisoformat(text.replace("Z", "+00:00"))


def plan(today):
    """Clientes, semanas en que compra cada uno y que lleva. Deterministico."""
    rng = random.Random(20261013)
    monday = today - timedelta(days=today.weekday())
    people, phones = [], set()
    for zone, rows in CUSTOMERS.items():
        for business, contact, address, days, status in rows:
            phone = None
            while phone is None or phone in phones:
                phone = "381" + str(rng.randint(4000000, 6999999))
            phones.add(phone)
            last = {W: 1, R: rng.choice([3, 4]), K: rng.choice([5, 6])}[status]
            weeks = set(range(last, WEEKS + 1))
            if status == W and rng.random() < 0.4:
                weeks.discard(rng.choice([2, 3, 4, 5]))
            basket = dict(rng.choice(SHOP_BASKETS if business else HOME_BASKETS))
            people.append({"zone": zone, "business": business, "contact": contact, "address": address,
                           "days": days, "status": status, "phone": phone, "weeks": weeks, "last": last,
                           "basket": basket, "missed": last - 1,
                           "method": "Transfer" if rng.random() < 0.16 else "Cash",
                           "hoards": rng.random() < 0.12})
    # Ventas a cuenta: cuatro que quedaron debiendo en su ultima compra y dos que despues pagaron.
    owing = rng.sample([p for p in people if p["status"] != W], 4)
    for person in owing:
        person["credit_week"], person["pays_week"] = person["last"], None
    for person in rng.sample([p for p in people if p["status"] == W and {1, 2} <= p["weeks"]], 2):
        person["credit_week"], person["pays_week"] = 2, 1
    visits = []  # (semana, dia, persona, cantidades)
    for week in range(WEEKS, 0, -1):
        for person in people:
            if week not in person["weeks"]:
                continue
            # Con dos dias de visita compra una vez por semana, alternando el dia.
            day = person["days"][week % len(person["days"])]
            items = {}
            for product, quantity in person["basket"].items():
                if rng.random() < 0.25:
                    quantity = max(1, min(4, quantity + rng.choice([-1, 1])))
                items[product] = quantity
            visits.append((week, day, person, items))
    return monday, people, visits


def show(monday, people, visits):
    print(f"Historial: {WEEKS} semanas, del {monday - timedelta(days=7 * WEEKS)} al {monday - timedelta(days=2)} "
          f"(nada en la semana del {monday}).")
    print(f"Camion {VEHICLE['name']} {VEHICLE['licensePlate']} | chofer {DRIVER['firstName']} {DRIVER['lastName']} "
          f"(usuario {DRIVER['userName']}) | producto nuevo {NEW_PRODUCT['detail']} ${NEW_PRODUCT['salePrice']}")
    for day, name in DAYS.items():
        todays = [p for p in people if day in p["days"]]
        count = {s: sum(1 for p in todays if p["status"] == s) for s in (W, R, K)}
        print(f"\n{name} - {ZONE_OF_DAY[day]}: {len(todays)} clientes | al dia {count[W]} | rojo {count[R]} | negro {count[K]}")
        for person in todays:
            label = (person["business"] + " (" + person["contact"] + ")") if person["business"] else person["contact"]
            basket = " + ".join(f"{q} {p}" for p, q in person["basket"].items())
            extra = " | a cuenta" if person.get("credit_week") and not person.get("pays_week") else ""
            print(f"  {label:<52} {person['address']:<50} {person['status']:<5} {person['missed']} sem | {basket}{extra}")
    print(f"\nTotal: {len(people)} clientes, {WEEKS * len(DAYS)} salidas, {len(visits)} ventas.")


def apply(api, config, parts, monday, people, visits, password):
    admin = api.login(config["Seed"]["AdminUserName"], config["Seed"]["AdminPassword"])["token"]
    a = lambda method, path, body=None: api.call(method, path, body, admin)
    listing = lambda path: a("GET", path + ("&" if "?" in path else "?") + "page=1&pageSize=100")["items"]

    # Conflictos: nada de esto debe existir todavia.
    if any(v["licensePlate"] == VEHICLE["licensePlate"] for v in listing("/api/vehicles")):
        raise SystemExit("Ya existe un vehiculo con patente " + VEHICLE["licensePlate"] + ". No se escribe nada.")
    if any(d.get("userName") == DRIVER["userName"] for d in listing("/api/drivers")):
        raise SystemExit("Ya existe el usuario " + DRIVER["userName"] + ". No se escribe nada.")
    if os.path.exists(MANIFEST):
        raise SystemExit("Ya hay un manifiesto de esta demostracion: " + MANIFEST)

    manifest = {"createdAt": datetime.now(AR).isoformat(), "zones": {}, "newZones": [], "customers": [], "sessions": []}
    save = lambda: json.dump(manifest, open(MANIFEST, "w", encoding="utf-8"), ensure_ascii=False, indent=1)

    zones = {z["name"]: z["id"] for z in listing("/api/zones")}
    for name in dict.fromkeys(ZONE_OF_DAY.values()):
        if name not in zones:
            zones[name] = a("POST", "/api/zones", {"name": name})
            manifest["newZones"].append(zones[name])
    manifest["zones"] = {name: zones[name] for name in dict.fromkeys(ZONE_OF_DAY.values())}
    products = {p["detail"]: p for p in listing("/api/products")}
    if NEW_PRODUCT["detail"] not in products:
        manifest["newProduct"] = a("POST", "/api/products", NEW_PRODUCT)
        products = {p["detail"]: p for p in listing("/api/products")}
    for needed in (B20, B12, SODA):
        if needed not in products:
            raise SystemExit("Falta el producto " + needed)
    price = {name: float(products[name]["salePrice"]) for name in (B20, B12, SODA)}
    pid = {name: products[name]["id"] for name in (B20, B12, SODA)}
    vehicle = manifest["vehicle"] = a("POST", "/api/vehicles", VEHICLE)
    manifest["driver"] = a("POST", "/api/drivers", {**DRIVER, "password": password, "vehicleId": vehicle})
    save()
    for person in people:  # el orden de alta es el orden de recorrido de la zona
        person["id"] = a("POST", "/api/customers", {
            "businessName": person["business"], "contactName": person["contact"], "phone": person["phone"],
            "address": person["address"], "email": None, "zoneId": zones[person["zone"]], "notes": None,
            "visitDays": person["days"], "vehicleId": vehicle})
        person["held"], person["debt"] = {B20: 0, B12: 0, SODA: 0}, 0.0
        manifest["customers"].append(person["id"])
    save()
    print(f"Creados: {len(manifest['newZones'])} zonas, vehiculo, chofer y {len(people)} clientes.", flush=True)

    driver = {"token": api.login(DRIVER["userName"], password)["token"]}

    def d(method, path, body=None):
        try:
            return api.call(method, path, body, driver["token"])
        except RuntimeError as error:
            if "HTTP 401" not in str(error):
                raise
            driver["token"] = api.login(DRIVER["userName"], password)["token"]
            return api.call(method, path, body, driver["token"])

    rng, km = random.Random(7), VEHICLE["currentKilometers"]
    for week in range(WEEKS, 0, -1):
        for day in DAYS:
            target = monday - timedelta(days=7 * week) + timedelta(days=day - 1)
            todays = [v for v in visits if v[0] == week and v[1] == day]
            load = {name: sum(v[3].get(name, 0) for v in todays) + 2 for name in (B20, B12, SODA)}
            a("POST", f"/api/vehicles/{vehicle}/load", {"clientRequestId": str(uuid.uuid4()), "routeDays": [day],
              "items": [{"productId": pid[n], "quantity": q} for n, q in load.items()]})
            session = d("POST", "/api/sessions/open", {"zoneId": zones[ZONE_OF_DAY[day]], "kilometersAtOpen": km})
            cash, sold = 0.0, []
            for _, _, person, items in todays:
                total = sum(price[n] * q for n, q in items.items())
                returned = {n: max(0, person["held"][n] - (1 if person["hoards"] and rng.random() < 0.35 else 0))
                            for n in items if person["held"][n] > 0}
                if person.get("credit_week") == week:
                    payment, paid = None, 0.0
                elif person.get("pays_week") == week and person["debt"] > 0:
                    paid = total + person["debt"]
                    payment = {"method": person["method"], "amount": paid}
                else:
                    payment, paid = {"method": person["method"]}, total
                result = d("POST", "/api/deliveries", {
                    "clientRequestId": str(uuid.uuid4()), "customerId": person["id"], "type": "Sale",
                    "items": [{"productId": pid[n], "quantity": q} for n, q in items.items()], "containersOut": [],
                    "containersIn": [{"productId": pid[n], "quantity": q} for n, q in returned.items() if q > 0],
                    "payment": payment})
                for name, quantity in items.items():
                    person["held"][name] += quantity - returned.get(name, 0)
                person["debt"] += total - paid
                if payment and payment["method"] == "Cash":
                    cash += paid
                sold.append((result["deliveryId"], person["id"]))
            detail = a("GET", f"/api/sessions/{session}")
            returns = [{"productId": line["productId"], "state": state, "quantity": line[key]}
                       for line in detail["stock"] for state, key in (("Full", "fullOnBoard"), ("Empty", "emptyOnBoard"))
                       if line[key] > 0]
            km += rng.randint(25, 60)
            closed = a("POST", f"/api/sessions/{session}/close", {"kilometersAtClose": km, "returns": returns})
            if not closed.get("cuadraTodo", True):
                raise RuntimeError(f"La recepcion de {target} no cuadra: {closed}")
            a("POST", f"/api/sessions/{session}/settlement", {"amountReceived": cash})
            backdate(parts, session, sold, instant(detail["openedAt"]), target)
            manifest["sessions"].append(session)
            save()
            print(f"  semana -{week} {DAYS[day]:<9} {target} | {len(todays)} ventas | efectivo ${cash:,.0f} | km {km}", flush=True)
    finish(parts, manifest, monday)
    return verify(a, vehicle, people)


def backdate(parts, session, sold, opened, target):
    """Lleva una salida completa a su dia: carga 7:25, apertura 8:00, visitas cada 13 minutos."""
    wanted = datetime(target.year, target.month, target.day, 8, 0, tzinfo=AR)
    base = (wanted - opened).total_seconds()
    close = base + (20 + 13 * len(sold) + 40) * 60

    def shift(table, columns, seconds, where):
        sets = ", ".join(f'"{c}" = "{c}" + interval \'{seconds:.6f} seconds\'' for c in columns)
        return f'UPDATE "{table}" SET {sets} WHERE {where};'

    stamps = ["CreatedAt", "UpdatedAt"]
    statements = [
        shift("DeliverySessions", ["OpenedAt", "CreatedAt"], base, f"\"Id\" = '{session}'"),
        shift("DeliverySessions", ["ClosedAt", "UpdatedAt"], close, f"\"Id\" = '{session}'"),
        shift("VehicleLoads", ["LoadedAt"] + stamps, base - 35 * 60, f"\"ConsumedBySessionId\" = '{session}'"),
        shift("SessionStockMovements", ["OccurredAt"] + stamps, base,
              f"\"DeliverySessionId\" = '{session}' AND \"Type\" = 0"),
        shift("SessionStockMovements", ["OccurredAt"] + stamps, close,
              f"\"DeliverySessionId\" = '{session}' AND \"Type\" = 4"),
        shift("SessionCashSettlements", ["ReceivedAt"] + stamps, close + 6 * 60, f"\"DeliverySessionId\" = '{session}'"),
    ]
    for index, (delivery, customer) in enumerate(sold):
        at = base + (20 + 13 * index) * 60
        statements += [
            shift("Deliveries", ["DeliveredAt"] + stamps, at, f"\"Id\" = '{delivery}'"),
            shift("DeliveryItems", stamps, at, f"\"DeliveryId\" = '{delivery}'"),
            shift("ContainerMovements", ["OccurredAt"] + stamps, at, f"\"DeliveryId\" = '{delivery}'"),
            shift("SessionStockMovements", ["OccurredAt"] + stamps, at, f"\"DeliveryId\" = '{delivery}'"),
            shift("Payments", ["PaidAt"] + stamps, at,
                  f"\"DeliverySessionId\" = '{session}' AND \"CustomerId\" = '{customer}'"),
        ]
    sql(parts, statements)


def finish(parts, manifest, monday):
    """Altas anteriores al historial y ultima visita igual a la ultima compra."""
    start = datetime.combine(monday - timedelta(days=7 * WEEKS + 4), datetime.min.time(), AR) + timedelta(hours=10)
    stamp = f"'{start.isoformat()}'"
    ids = ", ".join(f"'{c}'" for c in manifest["customers"])
    statements = [
        f'UPDATE "Customers" c SET "CreatedAt" = {stamp}, "UpdatedAt" = {stamp}, '
        f'"LastVisitAt" = (SELECT max(d."DeliveredAt") FROM "Deliveries" d WHERE d."CustomerId" = c."Id") '
        f'WHERE c."Id" IN ({ids});',
        f'UPDATE "CustomerContainerBalances" b SET '
        f'"CreatedAt" = (SELECT min(m."OccurredAt") FROM "ContainerMovements" m WHERE m."CustomerId" = b."CustomerId" AND m."ProductId" = b."ProductId"), '
        f'"UpdatedAt" = (SELECT max(m."OccurredAt") FROM "ContainerMovements" m WHERE m."CustomerId" = b."CustomerId" AND m."ProductId" = b."ProductId") '
        f'WHERE b."CustomerId" IN ({ids});',
        f'UPDATE "Vehicles" SET "CreatedAt" = {stamp} WHERE "Id" = \'{manifest["vehicle"]}\';',
        f'UPDATE "Drivers" SET "CreatedAt" = {stamp}, "UpdatedAt" = {stamp} WHERE "Id" = \'{manifest["driver"]}\';',
    ]
    for zone in manifest["newZones"]:
        statements.append(f'UPDATE "Zones" SET "CreatedAt" = {stamp}, "UpdatedAt" = {stamp} WHERE "Id" = \'{zone}\';')
    if manifest.get("newProduct"):
        statements.append(f'UPDATE "Products" SET "CreatedAt" = {stamp}, "UpdatedAt" = {stamp} WHERE "Id" = \'{manifest["newProduct"]}\';')
    sql(parts, statements)


def verify(a, vehicle, people):
    """Lo que dice la API de cada cliente contra lo planeado."""
    actual = {c["id"]: c for c in a("GET", f"/api/customers?vehicleId={vehicle}&page=1&pageSize=100")["items"]}
    wrong = 0
    for person in people:
        seen = actual.get(person["id"])
        account = a("GET", f"/api/customers/{person['id']}/account")
        held = {line["productDetail"]: line["quantity"] for line in account.get("containers", [])}
        problems = []
        if not seen or seen["activityStatus"] != person["status"] or seen["weeksWithoutPurchase"] != person["missed"]:
            problems.append(f"estado {seen and seen['activityStatus']}/{seen and seen['weeksWithoutPurchase']} "
                            f"en vez de {person['status']}/{person['missed']}")
        if abs(float(account["balance"]) - person["debt"]) > 0.005:
            problems.append(f"saldo {account['balance']} en vez de {person['debt']}")
        if any(held.get(n, 0) != q for n, q in person["held"].items()):
            problems.append(f"envases {held} en vez de {person['held']}")
        if problems:
            wrong += 1
            print("  NO COINCIDE " + person["contact"] + ": " + "; ".join(problems))
    count = {s: sum(1 for p in people if p["status"] == s) for s in (W, R, K)}
    print(f"Verificacion por la API: {len(people) - wrong} de {len(people)} clientes coinciden con el plan "
          f"(al dia {count[W]}, rojo {count[R]}, negro {count[K]}); "
          f"con deuda {sum(1 for p in people if p['debt'] > 0)}.")
    return wrong == 0


def main():
    config, parts = settings()
    monday, people, visits = plan(datetime.now(AR).date())
    show(monday, people, visits)
    if "--apply" not in sys.argv:
        print("\nSolo plan: no se escribio nada. Agrega --apply para sembrarlo.")
        return 0
    password = os.environ.get("DEMO_DRIVER_PASSWORD")
    if not password:
        raise SystemExit("Falta la variable DEMO_DRIVER_PASSWORD con la contrasena del chofer.")
    return 0 if apply(Api(), config, parts, monday, people, visits, password) else 1


if __name__ == "__main__":
    sys.exit(main())
