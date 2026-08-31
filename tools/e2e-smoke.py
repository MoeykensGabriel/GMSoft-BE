"""Prueba end to end del circuito de reparto de GMSoft contra la API corriendo."""
import json
import os
import random
import urllib.error
import urllib.request

# Configurables para poder correrla igual en local y en CI, que levanta la API en
# otro puerto y con otras credenciales.
BASE           = os.environ.get("GMSOFT_BASE_URL", "http://localhost:5142")
ADMIN_USER     = os.environ.get("GMSOFT_ADMIN_USER", "admin")
ADMIN_PASSWORD = os.environ.get("GMSOFT_ADMIN_PASSWORD", "Admin1234")

SUF = random.randint(1000, 9999)

fallos = []


def call(method, path, body=None, token=None):
    data = json.dumps(body).encode() if body is not None else None
    req = urllib.request.Request(BASE + path, data=data, method=method)
    req.add_header("Content-Type", "application/json")
    if token:
        req.add_header("Authorization", "Bearer " + token)
    try:
        with urllib.request.urlopen(req) as r:
            raw = r.read().decode()
            return json.loads(raw) if raw.strip() else None
    except urllib.error.HTTPError as e:
        detalle = e.read().decode()
        raise SystemExit(f"\nFALLO {method} {path} -> HTTP {e.code}\n{detalle}\n")


def check(etiqueta, obtenido, esperado):
    ok = obtenido == esperado
    if not ok:
        fallos.append(f"{etiqueta}: esperaba {esperado}, dio {obtenido}")
    print(f"  [{'OK ' if ok else 'MAL'}] {etiqueta}: {obtenido}"
          + ("" if ok else f"   (esperaba {esperado})"))


print("=" * 62)
print("  PREPARACION (admin)")
print("=" * 62)

admin = call("POST", "/api/auth/login",
             {"userName": ADMIN_USER, "password": ADMIN_PASSWORD})["token"]
print("  login admin OK")

ZONE_ID = call("POST", "/api/zones", {"name": f"Zona prueba {SUF}", "notes": None}, admin)
print(f"  zona       {ZONE_ID}")

vehiculo = call("POST", "/api/vehicles", {
    "name": f"Camioneta {SUF}",
    "licensePlate": f"AB{SUF}CD",
    "type": "Pickup",
    "currentKilometers": 120000,
}, admin)
print(f"  vehiculo   {vehiculo}")

producto = call("POST", "/api/products", {
    "detail": f"Bidon 20 litros {SUF}",
    "commercialDetail": "Agua mineral 20L",
    "salePrice": 3500,
    "tracking": "ByBalance",
    "isPublished": True,
    "imageUrl": None,
}, admin)
print(f"  producto   {producto}")

driver_user = f"chofer{SUF}"
chofer = call("POST", "/api/drivers", {
    "firstName": "Juan",
    "lastName": "Perez",
    "documentNumber": f"3{SUF}5678",
    "phone": "3811234567",
    "userName": driver_user,
    "email": None,
    "password": "Chofer1234",
    "vehicleId": vehiculo,
}, admin)
print(f"  chofer     {chofer}")

cliente = call("POST", "/api/customers", {
    "businessName": None,
    "contactName": f"Almacen Don Pedro {SUF}",
    "phone": "3819876543",
    "address": "Av Siempreviva 742",
    "email": None,
    "zoneId": ZONE_ID,
    "notes": "Timbre roto, golpear",
}, admin)
print(f"  cliente    {cliente}")

print()
print("=" * 62)
print("  CIRCUITO (chofer)")
print("=" * 62)

drv = call("POST", "/api/auth/login",
           {"userName": driver_user, "password": "Chofer1234"})
print(f"  login chofer OK, driverId en el token: {drv['driverId']}")
check("el token trae el DriverId correcto", drv["driverId"], chofer)
drv_token = drv["token"]

print("
  La oficina carga el camion antes de que salga:")
call("POST", f"/api/vehicles/{vehiculo}/load", {
    "vehicleId": vehiculo,
    "items": [{"productId": producto, "quantity": 100}],
}, admin)
cargado = call("GET", f"/api/vehicles/{vehiculo}/load", token=admin)
check("lineas cargadas esperando salir", len(cargado), 1)
check("llenos arriba del camion", cargado[0]["quantity"], 100)

sesion = call("POST", "/api/sessions/open", {
    "zoneId": ZONE_ID,
    "kilometersAtOpen": 120000,
}, drv_token)
print(f"  sesion abierta {sesion}")

# La carga se la llevo la salida: si siguiera figurando pendiente, la proxima
# salida se la llevaria de nuevo y el camion tendria stock que no existe.
pendiente = call("GET", f"/api/vehicles/{vehiculo}/load", token=admin)
check("el camion queda vacio en el deposito", len(pendiente), 0)

actual = call("GET", "/api/sessions/current", token=drv_token)
linea = actual["stock"][0]
print("\n  Stock a bordo al salir:")
check("llenos cargados", linea["fullOnBoard"], 100)
check("vacios a bordo", linea["emptyOnBoard"], 0)

print("\n  Visita: vende 10 bidones, retira 8 vacios")
visita = call("POST", "/api/deliveries", {
    "customerId": cliente,
    "newCustomer": None,
    "type": "Sale",
    "items": [{"productId": producto, "quantity": 10}],
    "containersOut": [{"productId": producto, "quantity": 10}],
    "containersIn": [{"productId": producto, "quantity": 8}],
    "payment": None,
    "notes": None,
}, drv_token)
check("total de la venta", visita["total"], 35000)
check("saldo de cuenta del cliente", visita["saldoCuentaCliente"], 35000)

actual = call("GET", "/api/sessions/current", token=drv_token)
linea = actual["stock"][0]
print("\n  Stock a bordo despues de la visita:")
check("llenos (100 - 10 entregados)", linea["fullOnBoard"], 90)
check("vacios (8 levantados del cliente)", linea["emptyOnBoard"], 8)

cuenta = call("GET", f"/api/customers/{cliente}/account", token=drv_token)
print("\n  Cuenta del cliente:")
check("debe", cuenta["balance"], 35000)
check("envases en su poder (10 salieron, 8 volvieron)",
      cuenta["containers"][0]["quantity"], 2)
check("dias sin comprar", cuenta["daysWithoutPurchase"], 0)
check("movimientos en el resumen", len(cuenta["movements"]), 1)

print("\n  Cierre devolviendo TODO lo que quedaba (90 llenos + 8 vacios):")
cierre = call("POST", f"/api/sessions/{sesion}/close", {
    "id": sesion,
    "kilometersAtClose": 120050,
    "returns": [
        {"productId": producto, "state": "Full", "quantity": 90},
        {"productId": producto, "state": "Empty", "quantity": 8},
    ],
}, admin)
check("cuadra todo", cierre["cuadraTodo"], True)
check("lineas de faltante", len(cierre["faltante"]), 0)

print()
print("=" * 62)
print("  SEGUNDA VUELTA: forzar un faltante a proposito")
print("=" * 62)

call("POST", f"/api/vehicles/{vehiculo}/load", {
    "vehicleId": vehiculo,
    "items": [{"productId": producto, "quantity": 50}],
}, admin)

sesion2 = call("POST", "/api/sessions/open", {
    "zoneId": ZONE_ID,
    "kilometersAtOpen": 120050,
}, drv_token)

call("POST", "/api/deliveries", {
    "customerId": cliente, "newCustomer": None, "type": "Sale",
    "items": [{"productId": producto, "quantity": 5}],
    "containersOut": [{"productId": producto, "quantity": 5}],
    "containersIn": [], "payment": {"amount": 10000, "method": "Cash"},
    "notes": None,
}, drv_token)

print("  Vende 5 y cobra 10000 de los 17500. La oficina recibe 44 llenos en vez de 45:")
cierre2 = call("POST", f"/api/sessions/{sesion2}/close", {
    "id": sesion2,
    "kilometersAtClose": 120090,
    "returns": [{"productId": producto, "state": "Full", "quantity": 44}],
}, admin)
check("NO cuadra", cierre2["cuadraTodo"], False)
check("falta 1 bidon lleno", cierre2["faltante"][0]["fullOnBoard"], 1)

print("\n  Rendicion (admin): entrega 10000, que es lo que cobro")
rend = call("POST", f"/api/sessions/{sesion2}/settlement",
            {"id": sesion2, "amountReceived": 10000, "notes": None}, admin)
check("vendido", rend["totalSold"], 17500)
check("cobrado", rend["totalCollected"], 10000)
check("deuda nueva (vendido - cobrado)", rend["newDebt"], 7500)
check("diferencia de caja (cobrado - entregado)", rend["cashDifference"], 0)

cuenta = call("GET", f"/api/customers/{cliente}/account", token=admin)
print("\n  Cuenta final del cliente:")
check("debe (35000 + 17500 - 10000)", cuenta["balance"], 42500)
check("envases en su poder (2 + 5)", cuenta["containers"][0]["quantity"], 7)

print()
print("=" * 62)
print("  AJUSTES DE OFICINA sobre los envases")
print("=" * 62)

print("  La oficina cuenta y el cliente tiene 6, no 7:")
aj = call("POST", f"/api/customers/{cliente}/containers/adjust", {
    "customerId": cliente, "productId": producto,
    "realQuantity": 6, "reason": "Conteo en el domicilio",
}, admin)
check("saldo anterior", aj["previousQuantity"], 7)
check("saldo nuevo", aj["newQuantity"], 6)
check("diferencia asentada", aj["delta"], -1)

print()
print("  Contar de nuevo lo mismo no escribe nada:")
aj2 = call("POST", f"/api/customers/{cliente}/containers/adjust", {
    "customerId": cliente, "productId": producto,
    "realQuantity": 6, "reason": "Segundo conteo, igual",
}, admin)
check("delta cero", aj2["delta"], 0)

print()
print("  Se rompieron 2 en lo del cliente:")
call("POST", f"/api/customers/{cliente}/containers/loss", {
    "customerId": cliente, "productId": producto,
    "quantity": 2, "reason": "Rotos en el domicilio",
}, admin)
cuenta = call("GET", f"/api/customers/{cliente}/account", token=admin)
check("envases despues de la perdida (6 - 2)", cuenta["containers"][0]["quantity"], 4)

print()
print("  No se puede perder mas de lo que tiene:")
try:
    call("POST", f"/api/customers/{cliente}/containers/loss", {
        "customerId": cliente, "productId": producto,
        "quantity": 99, "reason": "Prueba",
    }, admin)
    fallos.append("dejo dar por perdidos 99 envases teniendo 4")
    print("  [MAL] dejo perder 99 teniendo 4")
except SystemExit:
    print("  [OK ] rechazado, como corresponde")

print()
print("=" * 62)
print("  REPORTES")
print("=" * 62)

# Un cliente de la misma zona que nunca compra, para el reporte de caidos.
cliente_frio = call("POST", "/api/customers", {
    "businessName": None, "contactName": f"Nunca compro {SUF}",
    "phone": "3810000000", "address": "Calle Falsa 123",
    "email": None, "zoneId": ZONE_ID, "notes": None,
}, admin)

print("  Envases en la calle:")
salida = call("GET", "/api/reports/containers-out", token=admin)
nuestro = [l for l in salida if l["productId"] == producto]
check("aparece nuestro producto", len(nuestro), 1)
check("envases afuera (6 ajustados - 2 perdidos)", nuestro[0]["quantityOut"], 4)
check("en manos de un solo cliente", nuestro[0]["customersHolding"], 1)

print()
print("  Deudores de la zona:")
deudores = call("GET", f"/api/reports/debtors?zoneId={ZONE_ID}&pageSize=50", token=admin)
check("un solo deudor en la zona", deudores["totalCount"], 1)
check("cuanto debe", deudores["items"][0]["balance"], 42500)
check("envases que tiene encima", deudores["items"][0]["containersHeld"], 4)
check("el que nunca compro no debe nada",
      cliente_frio in [d["customerId"] for d in deudores["items"]], False)

print()
print("  Clientes caidos (mas de 30 dias sin comprar):")
caidos = call("GET",
              f"/api/reports/inactive-customers?days=30&zoneId={ZONE_ID}&pageSize=50",
              token=admin)
check("solo el que nunca compro", caidos["totalCount"], 1)
check("es el correcto", caidos["items"][0]["customerId"], cliente_frio)
check("nunca compro", caidos["items"][0]["lastPurchaseAt"], None)
check("el que compro hoy no aparece",
      cliente in [c["customerId"] for c in caidos["items"]], False)

print()
print("=" * 62)
if fallos:
    print(f"  {len(fallos)} COMPROBACION(ES) FALLIDA(S)")
    for f in fallos:
        print(f"   - {f}")
    print("=" * 62)
    # Salir con error, si no CI da verde con comprobaciones fallidas.
    raise SystemExit(1)

print("  TODO OK")
print("=" * 62)
