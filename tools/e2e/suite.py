"""Jornadas completas: datos, salida real, recepcion y lecturas finales."""
import os
import time
import traceback
from datetime import datetime, timedelta
from http_support import Day, Failure, Http, local_date, uid


def stock(day, full, empty=0, product=None):
    line = next(x for x in day.current()["stock"] if x["productId"] == (product or day.product))
    day.h.eq("llenos a bordo", line["fullOnBoard"], full)
    day.h.eq("vacios a bordo", line["emptyOnBoard"], empty)


def account(day, balance, containers, customer=None):
    result = day.account(customer)
    day.h.eq("saldo de cuenta", result["balance"], balance)
    day.h.eq("saldo envases", sum(x["quantity"] for x in result["containers"]), containers)
    return result


def totals(day, summary, **expected):
    for key, value in expected.items():
        day.h.eq("resumen " + key, summary["dayTotals"]["totals"][key], value)


def promotion_retry(h, actual, original):
    # Un reintento devuelve exactamente la misma respuesta, fechas incluidas.
    h.eq("campos de respuesta promocion", set(actual), set(original))
    for key, expected in original.items():
        h.eq("reintento promocion " + key, actual[key], expected)


def normal(day):
    h = day.h
    second = day.new_customer()
    day.start()
    h.eq("venta a cuenta", day.sale(10, 8)["total"], 35000)
    h.eq("venta efectivo calculado por servidor", day.sale(5, 3, {"method": "Cash"}, second)["total"], 17500)
    stock(day, 85, 11)
    account(day, 35000, 2)
    account(day, 0, 2, second)
    summary = day.finish(17500)
    totals(day, summary, fullLoaded=100, fullSold=15, fullReturned=85, fullDifference=0,
           emptyCollected=11, emptyReturned=11, emptyDifference=0)
    h.eq("dinero diario", summary["dayTotals"]["money"], {
        "cashExpected": 17500, "cashDeclared": 17500, "cashDifference": 0, "transfer": 0, "card": 0})
    detailed = day.a("GET", f"/api/sessions/{day.session}/detailed-settlement")
    h.eq("dos clientes en liquidacion", len(detailed), 2)
    for row in detailed:
        h.eq("liquidacion/cuenta", row["balance"], day.account(row["customerId"])["balance"])
    h.eq("liquidacion efectivo", sum(x["cash"] for x in detailed), 17500)
    h.eq("liquidacion vendidos", sum(l["amount"] for x in detailed for l in x["lines"]), 52500)
    h.eq("liquidacion envases retirados", sum(l["quantity"] for x in detailed for l in x["returnedContainers"]), 11)


def retries(day):
    h = day.h
    path = f"/api/vehicles/{day.vehicle}/load"
    body = day.load_body()
    original = day.a("POST", path, body)
    h.eq("reintento carga", day.a("POST", path, body), original)
    h.eq("una carga", sum(x["quantity"] for x in day.a("GET", path)), 100)
    day.open()
    sale = day.sale_body(10, 8, {"method": "Cash"})
    original = day.d("POST", "/api/deliveries", sale)
    h.eq("reintento venta", day.d("POST", "/api/deliveries", sale), original)
    restock = day.restock_body()
    path = f"/api/sessions/{day.session}/restocks"
    original_restock = day.a("POST", path, restock)
    h.eq("reintento recarga", day.a("POST", path, restock), original_restock)
    promotion = day.promotion_body()
    original = day.d("POST", "/api/promotions", promotion)
    promotion_retry(h, day.d("POST", "/api/promotions", promotion), original)
    h.eq("una recarga", len(day.current()["restocks"]), 1)
    h.eq("una venta", len(day.d("GET", f"/api/sessions/{day.session}/deliveries")), 1)
    h.eq("una promocion", len(day.d("GET", "/api/promotions/pending")), 1)
    stock(day, 90, 8)
    account(day, 0, 2)
    close_path = f"/api/promotions/{original['promotionId']}/close"
    close_body = day.close_promotion()
    closed = day.d("POST", close_path, close_body)
    promotion_retry(h, day.d("POST", close_path, close_body), closed)
    account(day, 0, 3, closed["customerId"])
    summary = day.finish(35000)
    totals(day, summary, fullLoaded=105, fullSold=13, fullDifference=0)
    h.eq("recarga reintentada tras cierre", day.a("POST", path, restock), original_restock)
    promotion_retry(h, day.d("POST", close_path, close_body), closed)
    h.eq("estado no cambia tras reintentos", day.summary(), summary)


def restocks(day):
    h = day.h
    day.start(20)
    day.sale(5, 2, {"method": "Cash"})
    before = next(s for s in day.a("GET", "/api/sessions/active-departures") if s["sessionId"] == day.session)
    body = day.restock_body()
    path = f"/api/sessions/{day.session}/restocks"
    added = day.a("POST", path, body)
    h.eq("notas normalizadas", added["notes"], "En ruta")
    h.eq("recarga vista por chofer", day.current()["restocks"], [added])
    h.eq("recarga vista por admin", day.a("GET", f"/api/sessions/{day.session}")["restocks"], [added])
    stock(day, 18, 2)
    stock(day, 2, product=day.products[1])
    after = next(s for s in day.a("GET", "/api/sessions/active-departures") if s["sessionId"] == day.session)
    h.eq("home conserva carga inicial", after, before)
    h.eq("home carga inicial", before["initialLoad"][0]["quantity"], 20)
    summary = day.finish(17500)
    totals(day, summary, fullLoaded=25, fullSold=5, fullReturned=20, fullDifference=0)
    h.problem("POST", path, day.restock_body(), day.admin, 409)
    h.eq("rechazo no cambia resumen", day.summary(), summary)


def promotions(day):
    h = day.h
    settings = day.a("GET", "/api/promotions/settings")
    try:
        day.a("PUT", "/api/promotions/settings", {"pickupDays": 3})
        day.start(20)
        original_count = day.customers()["totalCount"]
        first = day.d("POST", "/api/promotions", day.promotion_body())
        h.eq("prospecto sin cliente", first["customerId"], None)
        h.eq("fecha configurable", first["pickupDate"], str(local_date(first["registeredAt"]) + timedelta(days=3)))
        day.a("PUT", "/api/promotions/settings", {"pickupDays": 10})
        second = day.d("POST", "/api/promotions", day.promotion_body())
        h.eq("nuevo plazo", second["pickupDate"], str(local_date(second["registeredAt"]) + timedelta(days=10)))
        path = f"/api/promotions/{first['promotionId']}"
        h.eq("fecha congelada", day.a("GET", path)["pickupDate"], first["pickupDate"])
        h.eq("prospectos no crean clientes", day.customers()["totalCount"], original_count)
        h.eq("dos pendientes", {p["id"] for p in day.d("GET", "/api/promotions/pending")},
             {first["promotionId"], second["promotionId"]})
        listing = day.a("GET", f"/api/promotions?vehicleId={day.vehicle}&pageSize=100")
        h.eq("listado admin", listing["totalCount"], 2)
        report = day.report_product()
        h.eq("envases en prospectos", report["promotionQuantityOut"], 6)
        h.eq("envases afuera totales", report["quantityOut"], 6)
        h.eq("sin clientes tenedores", report["customersHolding"], 0)
        converted = day.d("POST", path + "/close", day.close_promotion())
        h.eq("conversion", converted["status"], "Converted")
        account(day, 0, 3, converted["customerId"])
        customer = day.a("GET", f"/api/customers/{converted['customerId']}")
        h.eq("conversion sin visita", customer["lastVisitAt"], None)
        h.eq("camion convertido", customer["vehicleId"], day.vehicle)
        customers = day.customers()
        h.eq("conversion crea solo un cliente", customers["totalCount"], original_count + 1)
        h.eq("convertido al final", customers["items"][-1]["id"], converted["customerId"])
        second_path = f"/api/promotions/{second['promotionId']}"
        closed = day.d("POST", second_path + "/close", day.close_promotion(False))
        h.eq("sin conversion", closed["status"], "NotConverted")
        detail = day.a("GET", second_path)
        h.eq("perdida registrada", detail["lines"][0]["containersLost"], 1)
        h.eq("recuperados registrados", detail["lines"][0]["containersReturned"], 2)
        for p in [path, second_path]:
            h.problem("POST", p + "/close", day.close_promotion(), day.token, 409)
        h.eq("sin pendientes", day.d("GET", "/api/promotions/pending"), [])
        report = day.report_product()
        h.eq("solo quedan envases convertidos", report["quantityOut"], 3)
        h.eq("sin envases de prospectos", report["promotionQuantityOut"], 0)
        stock(day, 14, 2)
        day.sale(1, payment={"method": "Cash"})
        summary = day.finish(3500)
        totals(day, summary, fullLoaded=20, fullSold=7, fullReturned=13, emptyReturned=2)
    finally:
        day.a("PUT", "/api/promotions/settings", settings)


def routes(day):
    h = day.h
    second, third = day.new_customer(), day.new_customer()
    other_vehicle = day.new_vehicle()
    route = day.a("GET", day.route_path())
    h.eq("orden inicial", [x["id"] for x in route["items"]], [day.customer, second, third])
    body = {"vehicleId": day.vehicle, "zoneId": day.zone, "day": 1, "version": route["version"],
            "customerIds": [third, second, day.customer], "changes": [
                {"id": second, "visitDays": [2, 4], "vehicleId": other_vehicle}]}
    saved = day.a("PUT", "/api/route-planning", body)
    h.eq("orden guardado", [x["id"] for x in saved["items"]], [third, day.customer])
    h.eq("version nueva", saved["version"] != route["version"], True)
    moved = day.a("GET", f"/api/customers/{second}")
    h.eq("camion nuevo", moved["vehicleId"], other_vehicle)
    h.eq("dias nuevos", moved["visitDays"], [2, 4])
    h.eq("recorrido destino", [x["id"] for x in day.a("GET", day.route_path(2, other_vehicle))["items"]], [second])
    body["customerIds"] = [day.customer, third]
    body["changes"] = []
    h.problem("PUT", "/api/route-planning", body, day.admin, 409)
    h.eq("version vieja no modifica", day.a("GET", day.route_path()), saved)
    h.eq("listado admin ordenado", [c["id"] for c in day.customers()["items"]], [third, day.customer])
    day.start()
    h.eq("hoja chofer ordenada", [c["id"] for c in day.customers(True)["items"]], [third, day.customer])
    day.sale(2, customer=third)
    day.sale(1, payment={"method": "Cash"})
    day.finish(3500)
    account(day, 7000, 2, third)
    account(day, 0, 1)


def differences(day):
    day.start(20)
    day.sale(6, 5, {"method": "Cash"})
    summary = day.finish(20500, full_missing=2, empty_missing=1)
    totals(day, summary, fullLoaded=20, fullSold=6, fullReturned=12, fullDifference=2,
           emptyCollected=5, emptyReturned=4, emptyDifference=1)
    day.h.eq("faltante efectivo", summary["dayTotals"]["money"]["cashDifference"], 500)
    settlement = day.a("GET", f"/api/sessions/{day.session}/settlement")
    day.h.eq("rendicion coincide", settlement["cashDifference"], 500)
    account(day, 0, 1)


def authorization(day):
    h = day.h
    other = Day(h, day.admin)
    day.start()
    other.start()
    promotion = other.d("POST", "/api/promotions", other.promotion_body())
    h.call("GET", "/api/customers", status=401)
    endpoints = [("POST", f"/api/vehicles/{day.vehicle}/load", day.load_body()),
        ("POST", f"/api/sessions/{day.session}/restocks", day.restock_body()),
        ("GET", day.route_path(), None), ("PUT", "/api/route-planning", {}),
        ("GET", f"/api/sessions/daily-summary?vehicleId={day.vehicle}&date={day.date}", None),
        ("GET", "/api/promotions", None), ("GET", "/api/promotions/settings", None),
        ("PUT", "/api/promotions/settings", {"pickupDays": 5}),
        ("GET", "/api/sessions/active-departures", None)]
    for method, path, body in endpoints:
        day.d(method, path, body, status=403)
    day.a("POST", "/api/deliveries", day.sale_body(), status=403)
    for path in [f"/api/vehicles/{other.vehicle}", f"/api/vehicles/{other.vehicle}/load",
                 f"/api/vehicles/{day.vehicle}", f"/api/vehicles/{day.vehicle}/load", "/api/container-units"]:
        day.d("GET", path, status=403)
    h.problem("GET", f"/api/vehicles/{other.vehicle}/load/summary", None, day.token, 403)
    h.eq("resumen de su camion", day.d("GET", f"/api/vehicles/{day.vehicle}/load/summary")["lines"], [])
    before = other.current()
    for suffix in ["", "/status", "/deliveries"]:
        h.problem("GET", f"/api/sessions/{other.session}{suffix}", None, day.token, 403)
    h.problem("POST", f"/api/sessions/{other.session}/keep-alive", None, day.token, 403)
    day.d("POST", f"/api/sessions/{other.session}/close", {"returns": []}, status=403)
    for suffix in ["", "/account", "/prices"]:
        h.problem("GET", f"/api/customers/{other.customer}{suffix}", None, day.token, 400)
    day.d("PUT", f"/api/customers/{other.customer}", {}, status=403)
    h.problem("POST", "/api/deliveries", day.sale_body(customer=other.customer), day.token, 400)
    h.problem("POST", f"/api/promotions/{promotion['promotionId']}/close", day.close_promotion(), day.token, 403)
    day.d("GET", f"/api/promotions/{promotion['promotionId']}", status=403)
    h.eq("promociones ajenas ocultas", day.d("GET", "/api/promotions/pending"), [])
    listing = day.d("GET", f"/api/customers?vehicleId={other.vehicle}&zoneId={other.zone}&pageSize=100")
    h.eq("filtros no escapan alcance", other.customer in [x["id"] for x in listing["items"]], False)
    h.eq("ataques no alteran salida", other.current(), before)
    account(other, 0, 0)
    other.d("POST", f"/api/promotions/{promotion['promotionId']}/close", other.close_promotion())
    for d in [day, other]:
        d.sale(1, payment={"method": "Cash"})
        d.finish(3500)


def validations(day):
    h = day.h
    h.problem("POST", "/api/sessions/open", {"zoneId": day.zone, "kilometersAtOpen": day.km}, day.token, 409)
    for quantity in [0, -1]:
        h.problem("POST", f"/api/vehicles/{day.vehicle}/load", day.load_body(quantity), day.admin, 400, "quantity")
    body = day.load_body()
    body["routeDays"] = [0, 8]
    h.problem("POST", f"/api/vehicles/{day.vehicle}/load", body, day.admin, 400, "routeDays")
    day.start(20)
    original = day.current()
    body = day.sale_body(1)
    body["type"] = "Promotion"
    rejection = h.problem("POST", "/api/deliveries", body, day.token, 400, "type")
    h.eq("promocion antigua indica circuito vigente",
         any("POST /api/promotions" in message for messages in rejection["errors"].values() for message in messages), True)
    for quantity in [0, -1]:
        h.problem("POST", "/api/deliveries", day.sale_body(quantity), day.token, 400, "quantity")
    body = day.restock_body()
    body["items"] *= 2
    path = f"/api/sessions/{day.session}/restocks"
    h.problem("POST", path, body, day.admin, 400, "items")
    body = day.restock_body()
    body["clientRequestId"] = "no-es-uuid"
    h.problem("POST", path, body, day.admin, 400, "clientRequestId")
    body = day.promotion_body()
    body["prospect"]["visitDays"] = [1, 1]
    h.problem("POST", "/api/promotions", body, day.token, 400, "visitDays")
    for query, field in [("pageSize=101", "pageSize"), ("page=0", "page"), ("page=21474837", "page")]:
        h.problem("GET", "/api/promotions?" + query, None, day.admin, 400, field)
    h.problem("GET", day.route_path(8), None, day.admin, 400, "day")
    for path in [f"/api/customers/{uid()}", f"/api/sessions/{uid()}", f"/api/promotions/{uid()}"]:
        h.problem("GET", path, None, day.admin, 404)
    body = day.restock_body()
    body["items"][0]["productId"] = uid()
    h.problem("POST", f"/api/sessions/{day.session}/restocks", body, day.admin, 404)
    h.problem("POST", "/api/deliveries", day.sale_body(21), day.token, 409)
    h.problem("POST", f"/api/vehicles/{day.vehicle}/load", day.load_body(), day.admin, 409)
    h.eq("rechazos no mutan salida", day.current(), original)
    account(day, 0, 0)
    day.sale(2, 1, {"method": "Cash"})
    summary = day.finish(7000)
    totals(day, summary, fullLoaded=20, fullSold=2, fullReturned=18, fullDifference=0)


def visits(day):
    h = day.h
    second = day.new_customer([2])
    excluded = day.new_customer([3])
    day.load(20)
    day.a("PUT", f"/api/vehicles/{day.vehicle}/load/route-days", {"routeDays": [1, 2]}, 204)
    day.open()
    h.eq("dias copiados", day.current()["routeDays"], [1, 2])
    h.eq("hoja dias y orden", [x["id"] for x in day.customers(True)["items"]], [day.customer, second])
    h.problem("POST", "/api/deliveries", day.sale_body(21), day.token, 409)
    body = day.sale_body(1)
    body["customerId"] = excluded
    h.problem("POST", "/api/deliveries", body, day.token, 400)
    day.sale(2, 1, {"method": "Cash"})
    body = day.sale_body(1)
    body["customerId"] = None
    body["newCustomer"] = day.prospect([1, 2])
    day.d("POST", "/api/deliveries", body)
    sheet = day.customers(True)["items"]
    h.eq("alta al final", [x["id"] for x in sheet[:2]], [day.customer, second])
    h.eq("tres clientes sin duplicar dias", len(sheet), 3)
    h.eq("nombre del alta", sheet[-1]["contactName"], body["newCustomer"]["contactName"])
    account(day, 3500, 1, sheet[-1]["id"])
    day.finish(7000)
    day.start(5, [1, 2])
    day.sale(1, customer=second)
    day.finish()


def legacy_activity(day):
    """Conserva los numeros del smoke original y agrega actividad observable hoy."""
    h = day.h
    cold = day.new_customer()
    day.start()
    h.eq("actividad antes de recibir", day.a("GET", f"/api/customers/{cold}")["weeksWithoutPurchase"], 0)
    body = day.sale_body(10, 8)
    request_id = body.pop("clientRequestId")
    h.problem("POST", "/api/deliveries", body, day.token, 400, "clientRequestId")
    body["clientRequestId"] = request_id
    body["containersOut"] = [{"productId": day.product, "quantity": 10}]
    sale = day.d("POST", "/api/deliveries", body)
    h.eq("venta original", sale["total"], 35000)
    h.eq("saldo devuelto por venta original", sale["saldoCuentaCliente"], 35000)
    first_account = account(day, 35000, 2)
    h.eq("dias sin compra", first_account["daysWithoutPurchase"], 0)
    h.eq("un movimiento", len(first_account["movements"]), 1)
    stock(day, 90, 8)
    day.finish()
    missed = day.a("GET", f"/api/customers/{cold}")
    h.eq("una semana recibida sin venta", missed["weeksWithoutPurchase"], 1)
    h.eq("umbral inicial blanco", missed["activityStatus"], "White")
    day.start(50)
    day.sale(5, payment={"amount": 10000, "method": "Cash"})
    day.finish(10000, full_missing=1)
    settlement = day.a("GET", f"/api/sessions/{day.session}/settlement")
    for key, expected in {"totalSold": 17500, "totalCollected": 10000, "newDebt": 7500, "cashDifference": 0}.items():
        h.eq("rendicion original " + key, settlement[key], expected)
    account(day, 42500, 7)
    path = f"/api/customers/{day.customer}/containers"
    adjust = {"productId": day.product, "realQuantity": 6, "reason": "Conteo e2e"}
    result = day.a("POST", path + "/adjust", adjust)
    for key, expected in {"previousQuantity": 7, "newQuantity": 6, "delta": -1}.items():
        h.eq("ajuste " + key, result[key], expected)
    h.eq("conteo repetido sin delta", day.a("POST", path + "/adjust", adjust)["delta"], 0)
    day.a("POST", path + "/loss", {"productId": day.product, "quantity": 2, "reason": "Rotura e2e"}, 204)
    account(day, 42500, 4)
    h.problem("POST", path + "/loss", {"productId": day.product, "quantity": 99, "reason": "Prueba"}, day.admin, 409)
    report = day.report_product()
    h.eq("envases afuera", report["quantityOut"], 4)
    h.eq("un tenedor", report["customersHolding"], 1)
    debtors = day.a("GET", f"/api/reports/debtors?zoneId={day.zone}&pageSize=50")
    h.eq("un deudor", debtors["totalCount"], 1)
    h.eq("deuda reportada", debtors["items"][0]["balance"], 42500)
    h.eq("envases reportados", debtors["items"][0]["containersHeld"], 4)
    inactive = day.a("GET", f"/api/reports/inactive-customers?days=30&zoneId={day.zone}&pageSize=50")
    h.eq("solo nunca compro", [x["customerId"] for x in inactive["items"]], [cold])
    h.eq("sin compra inventada", inactive["items"][0]["lastPurchaseAt"], None)
    h.eq("dos salidas cuentan una semana", day.a("GET", f"/api/customers/{cold}")["weeksWithoutPurchase"], 1)
    day.start(5)
    day.sale(1, customer=cold)
    day.finish()
    for customer in [cold, day.customer]:
        detail = day.a("GET", f"/api/customers/{customer}")
        h.eq("venta reinicia actividad", detail["weeksWithoutPurchase"], 0)
        h.eq("color tras compra", detail["activityStatus"], "White")
        h.eq("cuenta coincide actividad", day.account(customer)["weeksWithoutPurchase"], 0)
    for customer in day.customers()["items"]:
        h.eq("listado coincide actividad", customer["weeksWithoutPurchase"], 0)
        h.eq("listado coincide color", customer["activityStatus"], "White")


def no_purchase(day):
    h = day.h
    missing = day.new_customer()
    day.start()
    path = f"/api/sessions/{day.session}/unvisited-customers"
    h.eq("sin visitar inicial", day.a("GET", path)["total"], 2)
    body = {"clientRequestId": uid(), "customerId": day.customer, "type": "NoPurchase",
            "noPurchaseReason": "NotHome", "items": [], "containersOut": [], "containersIn": []}
    for invalid in [{"noPurchaseReason": None}, {"noPurchaseReason": "Other"},
                    {"items": [{"productId": day.product, "quantity": 1}]},
                    {"payment": {"method": "Cash", "amount": 1}}, {"clientRequestId": None}]:
        h.problem("POST", "/api/deliveries", {**body, **invalid}, day.token, 400)
    first = day.d("POST", "/api/deliveries", body)
    h.eq("sin compra cero", first["total"], 0)
    h.eq("reintento sin compra", day.d("POST", "/api/deliveries", body), first)
    correction = {**body, "clientRequestId": uid(), "noPurchaseReason": "Other", "notes": " Volver mañana "}
    h.eq("corrige misma visita", day.d("POST", "/api/deliveries", correction), first)
    h.eq("reintento viejo", day.d("POST", "/api/deliveries", body), first)
    visits = day.d("GET", f"/api/sessions/{day.session}/deliveries")
    h.eq("una visita", len(visits), 1)
    h.eq("ultimo motivo", visits[0]["noPurchaseReason"], "Other")
    h.eq("nota normalizada", visits[0]["notes"], "Volver mañana")
    h.eq("solo queda sin visitar", [c["id"] for c in day.d("GET", path)["items"]], [missing])
    stock(day, 100)
    result = account(day, 0, 0)
    h.eq("sin movimiento de cuenta", result["movements"], [])
    h.eq("no inventa compra", result["lastPurchaseAt"], None)
    h.eq("ultimo motivo en cuenta", result["lastNoPurchase"]["reason"], "Other")
    settlement = day.a("GET", f"/api/sessions/{day.session}/detailed-settlement")[0]
    h.eq("sin renglones vendidos", settlement["lines"], [])
    h.eq("motivo en liquidacion", settlement["noPurchases"][0]["reason"], "Other")
    day.sale(1)
    h.eq("venta oculta ultima sin compra", day.account()["lastNoPurchase"], None)
    h.problem("POST", "/api/deliveries", {**body, "clientRequestId": uid()}, day.token, 409)
    h.eq("historial conserva visita", len(day.d("GET", f"/api/sessions/{day.session}/deliveries")), 2)
    day.finish()
    h.eq("sin visitar al cierre", [c["id"] for c in day.a("GET", path)["items"]], [missing])
    # Con la salida cerrada el chofer ya no ve los clientes actuales del recorrido.
    day.d("GET", path, status=403)


def clear_load(day):
    h = day.h
    path = f"/api/vehicles/{day.vehicle}/load"
    day.load(30)
    day.load(10)
    h.eq("dos renglones arriba", len(day.a("GET", path)), 2)
    day.d("DELETE", path, status=403)
    day.d("GET", path + "/removed", status=403)
    h.eq("vaciar devuelve renglones", day.a("DELETE", path), 2)
    h.eq("camion vacio", day.a("GET", path), [])
    removed = day.a("GET", path + "/removed")
    h.eq("historial cantidades", sorted(x["quantity"] for x in removed), [10, 30])
    h.eq("historial quien", {x["removedByUserName"] for x in removed},
         {os.environ.get("GMSOFT_ADMIN_USER", "admin")})
    removed_at = [datetime.fromisoformat(x["removedAt"].replace("Z", "+00:00")) for x in removed]
    h.eq("historial reciente primero", removed_at, sorted(removed_at, reverse=True))
    h.eq("vaciar vacio", day.a("DELETE", path), 0)
    h.problem("POST", "/api/sessions/open", {"zoneId": day.zone, "kilometersAtOpen": day.km}, day.token, 409)
    day.load(5)
    line = day.a("GET", path)[0]
    day.a("DELETE", f"{path}/{line['id']}", status=204)
    h.eq("bajar un renglon queda en historial", len(day.a("GET", path + "/removed")), 3)
    day.start(5)
    h.eq("vaciar con salida abierta no toca lo que salio", day.a("DELETE", path), 0)
    stock(day, 5)
    h.problem("DELETE", f"{path}/{line['id']}", None, day.admin, 404)
    day.sale(1, payment={"method": "Cash"})
    day.finish(3500)
    h.eq("historial intacto", len(day.a("GET", path + "/removed")), 3)


def admin_open(day):
    h = day.h
    day.load(20)
    body = {"zoneId": day.zone, "kilometersAtOpen": day.km}
    h.problem("POST", "/api/sessions/open", {**body, "driverId": day.driver}, day.token, 403)
    h.problem("POST", "/api/sessions/open", body, day.admin, 403)
    h.problem("POST", "/api/sessions/open", {**body, "driverId": uid()}, day.admin, 404)
    day.session = day.a("POST", "/api/sessions/open", {**body, "driverId": day.driver}, 201)
    current = day.current()
    h.eq("chofer ve la salida abierta por admin", current["id"], day.session)
    h.eq("queda quien la abrio", current["openedByUserId"] is not None, True)
    h.eq("dentro de horario sin marca", current["openedOutsideWorkingHours"], False)
    day.date = local_date(current["openedAt"])
    h.problem("POST", "/api/sessions/open", {**body, "driverId": day.driver}, day.admin, 409)
    day.sale(2, payment={"method": "Cash"})
    day.finish(7000)


def deactivated_driver(day):
    h = day.h
    day.start(20)
    driver = day.a("GET", f"/api/drivers/{day.driver}")
    body = {k: driver[k] for k in ["firstName", "lastName", "documentNumber", "phone", "vehicleId"]}
    path = f"/api/drivers/{day.driver}"
    day.a("PUT", path, {"id": day.driver, **body, "isActive": False}, 204)
    # El token sigue vigente, pero la cuenta ya no: el pedido siguiente se corta.
    day.d("GET", "/api/sessions/current", status=401)
    day.d("POST", "/api/deliveries", day.sale_body(1), status=401)
    h.logins.append(time.monotonic())  # Cuenta para el limite de login de la API.
    h.eq("login desactivado", h.call("POST", "/api/auth/login",
         {"userName": driver["userName"], "password": "Chofer1234"}, None, 403)["status"], 403)
    h.eq("rechazos no venden", day.a("GET", f"/api/sessions/{day.session}/deliveries"), [])
    day.a("PUT", path, {"id": day.driver, **body, "isActive": True}, 204)
    h.problem("PUT", path + "/password", {"id": day.driver, "newPassword": "corta"}, day.admin, 400, "newPassword")
    h.eq("token vigente antes del cambio", day.d("GET", "/api/sessions/current")["id"], day.session)
    day.a("PUT", path + "/password", {"id": day.driver, "newPassword": "Nueva12345"}, 204)
    # Cambiar la contraseña invalida los tokens ya emitidos, aunque no hayan vencido.
    day.d("GET", "/api/sessions/current", status=401)
    day.d("POST", f"/api/sessions/{day.session}/keep-alive", status=401)
    h.logins.append(time.monotonic())
    h.call("POST", "/api/auth/login", {"userName": driver["userName"], "password": "Chofer1234"}, None, 401)
    day.token = h.login(driver["userName"], "Nueva12345")["token"]
    # La renovación conserva el sello nuevo: el token renovado sigue valiendo.
    day.token = day.d("POST", f"/api/sessions/{day.session}/keep-alive")["token"]
    h.eq("token renovado vale", day.d("GET", "/api/sessions/current")["id"], day.session)
    day.sale(1, payment={"method": "Cash"})
    day.finish(3500)


def removal(day):
    h = day.h
    second = day.new_customer()
    day.start()
    day.sale(3)
    path = f"/api/customers/{day.customer}/removal-requests"
    sheet = lambda: {c["id"] for c in day.d("GET", f"/api/customers?vehicleId={day.vehicle}&zoneId={day.zone}"
                                              f"&onlyActive=true&pageSize=100&visitDays=1")["items"]}
    unvisited = f"/api/sessions/{day.session}/unvisited-customers"
    h.eq("hoja inicial", sheet(), {day.customer, second})
    body = {"clientRequestId": uid(), "reason": "Moved", "notes": None, "containersNotReturned": False}
    h.problem("POST", path, {**body, "reason": "Other"}, day.token, 400)
    day.a("POST", path, body, status=403)
    h.eq("envases bloquean", h.problem("POST", path, body, day.token, 409)["containers"][0]["quantity"], 3)
    h.eq("el bloqueo no deja baja", day.a("GET", f"/api/customers/{day.customer}")["removalPending"], False)
    body = {**body, "clientRequestId": uid(), "containersNotReturned": True}
    first = day.d("POST", path, body)
    h.eq("baja pendiente", first["status"], "Pending")
    h.eq("deuda registrada", first["debt"], 10500)
    h.eq("envases registrados", [c["quantity"] for c in first["containers"]], [3])
    h.eq("reintento misma baja", day.d("POST", path, body)["id"], first["id"])
    h.problem("POST", path, {**body, "clientRequestId": uid()}, day.token, 409)
    h.eq("sale de la hoja", sheet(), {second})
    h.eq("sale de sin visitar", [c["id"] for c in day.d("GET", unvisited)["items"]], [second])
    h.eq("venta bloqueada", h.problem("POST", "/api/deliveries", day.sale_body(1), day.token, 409)["detail"],
         "El cliente fue dado de baja; lo confirma la oficina")
    h.eq("admin ve la marca", day.a("GET", f"/api/customers/{day.customer}")["removalPending"], True)
    day.d("GET", "/api/customers/removal-requests", status=403)
    other = day.d("POST", f"/api/customers/{second}/removal-requests",
                  {"clientRequestId": uid(), "reason": "Other", "notes": "Prueba", "containersNotReturned": False})
    h.eq("sin envases ni deuda pasa directo", (other["containers"], other["debt"]), ([], 0))
    h.eq("hoja vacia", sheet(), set())
    pending = day.a("GET", "/api/customers/removal-requests?status=Pending&pageSize=100")
    h.eq("pendientes listadas", {first["id"], other["id"]} <= {r["id"] for r in pending["items"]}, True)
    resolve = "/api/customers/removal-requests/{}/{}"
    day.d("POST", resolve.format(first["id"], "confirm"), status=403)
    h.eq("rechazo vuelve a la hoja", day.a("POST", resolve.format(other["id"], "reject"))["status"], "Rejected")
    h.eq("vuelve el segundo", sheet(), {second})
    confirmed = day.a("POST", resolve.format(first["id"], "confirm"))
    h.eq("confirmada", confirmed["status"], "Confirmed")
    h.eq("repetir confirmar", day.a("POST", resolve.format(first["id"], "confirm"))["id"], first["id"])
    h.problem("POST", resolve.format(first["id"], "reject"), None, day.admin, 409)
    customer = day.a("GET", f"/api/customers/{day.customer}")
    h.eq("inactivo, no borrado", (customer["isActive"], customer["removalPending"]), (False, False))
    kept = account(day, 10500, 3)
    h.eq("renglones de la baja", sorted(r["kind"] for r in kept["statement"] if r["kind"].startswith("Removal")),
         ["RemovalConfirmed", "RemovalRequested"])
    day.sale(1, payment={"method": "Cash"}, customer=second)
    day.finish(3500)


def working_hours(day):
    """Va al final: el horario es de toda la empresa y la API no permite borrarlo."""
    h = day.h

    def week(working):
        return {"days": [{"dayOfWeek": d, "isWorkingDay": working,
                          "from": "00:00:00" if working else None, "until": "23:59:00" if working else None,
                          "openingCutoff": None} for d in range(1, 8)]}

    previous = day.a("GET", "/api/working-hours")
    day.d("PUT", "/api/working-hours", week(False), status=403)
    invalid = week(True)
    invalid["days"][0].update({"from": "20:00:00", "until": "08:00:00"})
    h.problem("PUT", "/api/working-hours", invalid, day.admin, 400)
    h.problem("PUT", "/api/working-hours", {"days": week(True)["days"][:6]}, day.admin, 400)
    closed = day.a("PUT", "/api/working-hours", week(False))
    h.eq("horario guardado", closed["isConfigured"], True)
    h.eq("chofer puede leer horario", day.d("GET", "/api/working-hours")["days"], closed["days"])
    try:
        path = f"/api/vehicles/{day.vehicle}/load"
        load = day.load_body(20)
        h.eq("tipo fuera de horario", h.problem("POST", path, load, day.admin, 409)["type"], "outside-working-hours")
        h.eq("rechazo no carga", day.a("GET", path), [])
        day.d("POST", path, {**load, "confirmOutsideWorkingHours": True}, status=403)
        loaded = day.a("POST", path, {**load, "confirmOutsideWorkingHours": True})
        h.eq("reintento devuelve lo registrado", day.a("POST", path, load), loaded)
        h.eq("carga marcada", [x["isOutsideWorkingHours"] for x in day.a("GET", path)], [True])
        body = {"zoneId": day.zone, "kilometersAtOpen": day.km}
        h.eq("chofer bloqueado", h.problem("POST", "/api/sessions/open", body, day.token, 409)["type"],
             "outside-working-hours")
        h.eq("chofer no saltea", h.problem("POST", "/api/sessions/open",
             {**body, "confirmOutsideWorkingHours": True}, day.token, 409)["type"], "outside-working-hours")
        admin_body = {**body, "driverId": day.driver}
        h.eq("admin sin confirmar", h.problem("POST", "/api/sessions/open", admin_body, day.admin, 409)["type"],
             "outside-working-hours")
        day.session = day.a("POST", "/api/sessions/open", {**admin_body, "confirmOutsideWorkingHours": True}, 201)
        current = day.current()
        day.date = local_date(current["openedAt"])
        h.eq("apertura marcada", current["openedOutsideWorkingHours"], True)
        restock_path = f"/api/sessions/{day.session}/restocks"
        restock = day.restock_body()
        h.eq("recarga bloqueada", h.problem("POST", restock_path, restock, day.admin, 409)["type"],
             "outside-working-hours")
        added = day.a("POST", restock_path, {**restock, "confirmOutsideWorkingHours": True})
        h.eq("recarga marcada", added["isOutsideWorkingHours"], True)
        day.sale(3, payment={"method": "Cash"})
        day.d("POST", "/api/deliveries", {"clientRequestId": uid(), "customerId": day.new_customer(),
              "type": "NoPurchase", "noPurchaseReason": "NotHome", "items": [], "containersOut": [],
              "containersIn": []})
        visits = day.d("GET", f"/api/sessions/{day.session}/deliveries")
        h.eq("visitas no bloqueadas y marcadas", [v["isOutsideWorkingHours"] for v in visits], [True, True])
        summary = day.finish(10500)
        detail = day.a("GET", f"/api/sessions/{day.session}")
        h.eq("recepcion marcada", detail["closedOutsideWorkingHours"], True)
        h.eq("rendicion marcada", detail["settlementOutsideWorkingHours"], True)
        h.eq("rendicion marcada en su consulta",
             day.a("GET", f"/api/sessions/{day.session}/settlement")["isOutsideWorkingHours"], True)
        row = next(s for s in summary["sessions"] if s["sessionId"] == day.session)
        h.eq("eventos del resumen", sorted(e["operation"] for e in row["outsideWorkingHoursEvents"]),
             sorted(["initial-load", "session-open", "restock", "visit", "visit", "session-close", "settlement"]))
        h.eq("detalle y resumen coinciden", detail["outsideWorkingHoursEvents"], row["outsideWorkingHoursEvents"])
    finally:
        # Sin DELETE: si no había horario se deja uno que cubre el día, para no trabar otra corrida.
        day.a("PUT", "/api/working-hours", {"days": previous["days"]} if previous["isConfigured"] else week(True))


SCENARIOS = [("01 Dia normal", normal), ("02 Reintentos", retries), ("03 Recarga en ruta", restocks),
    ("04 Promociones", promotions), ("05 Organizar recorridos", routes), ("06 Faltantes y diferencias", differences),
    ("07 Autorizacion y aislamiento", authorization), ("08 Validaciones y errores", validations),
    ("09 Visitas del dia", visits), ("10 Circuito original y actividad", legacy_activity), ("11 Visita sin compra", no_purchase),
    ("12 Vaciar carga e historial", clear_load), ("13 Apertura por admin", admin_open),
    ("14 Chofer desactivado y contraseña", deactivated_driver), ("15 Baja de clientes", removal),
    ("16 Horario laboral", working_hours)]


def main():
    h = Http()
    started = time.monotonic()
    failed = []
    try:
        admin = h.login(os.environ.get("GMSOFT_ADMIN_USER", "admin"),
                        os.environ.get("GMSOFT_ADMIN_PASSWORD", "Admin1234"))["token"]
    except Exception as error:
        print(f"FALLA autenticacion inicial: {error}", flush=True)
        print(f"TOTAL 0 recorridos | {h.checks} verificaciones | 1 falla no conocida | {time.monotonic()-started:.2f}s", flush=True)
        return 1
    for name, scenario in SCENARIOS:
        before, known_before, begin = h.checks, len(h.known), time.monotonic()
        try:
            scenario(Day(h, admin))
        except Exception as error:
            failed.append(name)
            print(f"FALLA {name} | {h.checks - before} verificaciones | {time.monotonic()-begin:.2f}s\n{error}", flush=True)
            if not isinstance(error, Failure):
                print(h.last, flush=True)
                traceback.print_exc()
        else:
            label = "FALLA CONOCIDA" if len(h.known) > known_before else "OK"
            print(f"{label} {name} | {h.checks - before} verificaciones | {time.monotonic()-begin:.2f}s", flush=True)
    for bug, evidence in h.known:
        print(f"FALLA CONOCIDA {bug}\n{evidence}", flush=True)
    print(f"TOTAL {len(SCENARIOS)} recorridos | {h.checks} verificaciones | {len(failed)} fallas no conocidas | "
          f"{len(set(bug for bug, _ in h.known))} bugs conocidos ({len(h.known)} evidencias) | {time.monotonic()-started:.2f}s", flush=True)
    return int(bool(failed))
