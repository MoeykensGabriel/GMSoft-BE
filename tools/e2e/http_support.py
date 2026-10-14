"""HTTP, diagnosticos y datos independientes por recorrido. Sin acceso SQL."""
import json
import os
import time
import uuid
import urllib.error
import urllib.request
from datetime import datetime, timedelta, timezone

BASE = os.environ.get("GMSOFT_BASE_URL", "http://localhost:5199").rstrip("/")
ARGENTINA = timezone(timedelta(hours=-3))


def uid():
    return str(uuid.uuid4())


def local_date(instant):
    return datetime.fromisoformat(instant.replace("Z", "+00:00")).astimezone(ARGENTINA).date()


class Failure(AssertionError):
    pass


class Http:
    def __init__(self):
        self.checks = 0
        self.last = "Sin solicitudes"
        self.logins = []
        self.known = []

    def eq(self, label, actual, expected):
        self.checks += 1
        if actual != expected:
            raise Failure(f"{label}: esperado {expected!r}; recibido {actual!r}\n{self.last}")

    def call(self, method, path, body=None, token=None, status=200):
        request = urllib.request.Request(BASE + path, method=method,
            data=json.dumps(body).encode() if body is not None else None,
            headers={"Content-Type": "application/json", **({"Authorization": "Bearer " + token} if token else {})})
        try:
            response = urllib.request.urlopen(request, timeout=30)
        except urllib.error.HTTPError as error:
            response = error
        except (urllib.error.URLError, TimeoutError) as error:
            self.last = f"{method} {path}\nSin respuesta HTTP: {type(error).__name__}"
            raise Failure(self.last) from error
        with response:
            raw = response.read().decode("utf-8", errors="replace")
            code = response.code
        safe_body = "<credenciales omitidas>" if path == "/api/auth/login" else json.dumps(body, ensure_ascii=False)
        safe_raw = "<autenticacion omitida>" if path == "/api/auth/login" and code == 200 else raw
        self.last = f"{method} {path}\nPedido: {safe_body}\nHTTP {code}\nRespuesta: {safe_raw}"
        self.eq("codigo HTTP", code, status)
        try:
            return json.loads(raw) if raw.strip() else None
        except ValueError as error:
            raise Failure("Respuesta no JSON\n" + self.last) from error

    def problem(self, method, path, body, token, status, field=None):
        result = self.call(method, path, body, token, status)
        self.eq("ProblemDetails.status", result.get("status"), status)
        self.eq("ProblemDetails.title", bool(result.get("title")), True)
        if field is not None:
            errors = result.get("errors", {})
            self.eq(f"errors contiene {field}", any(field.lower() in key.lower() and bool(value)
                    for key, value in errors.items()), True)
        else:
            self.eq("ProblemDetails.detail", bool(result.get("detail")), True)
            self.eq("ProblemDetails.instance", result.get("instance"), path.split("?")[0])
        return result

    def login(self, user, password):
        # Ventana conservadora. Nunca reintentar automaticamente una escritura.
        now = time.monotonic()
        self.logins = [t for t in self.logins if now - t < 61]
        if len(self.logins) >= 9:
            delay = max(0, 61 - (now - self.logins[0]))
            print(f"  Espera de login: {delay:.1f}s (limite real de la API)", flush=True)
            time.sleep(delay)
        self.logins.append(time.monotonic())
        return self.call("POST", "/api/auth/login", {"userName": user, "password": password})

    def known_check(self, bug, operation):
        """Reservado para una regresion identificada y documentada, nunca todo un dia."""
        try:
            operation()
        except Failure as error:
            if not any(name == bug and evidence == str(error) for name, evidence in self.known):
                self.known.append((bug, str(error)))


class Day:
    def __init__(self, http, admin):
        self.h, self.admin = http, admin
        self.suffix = uuid.uuid4().hex[:20]
        self.zone = self.a("POST", "/api/zones", {"name": "E2E " + self.suffix}, 201)
        self.vehicle = self.new_vehicle()
        self.products = [self.a("POST", "/api/products", {
            "detail": f"E2E {i} {self.suffix}", "commercialDetail": "Agua",
            "salePrice": 3500, "tracking": "ByBalance", "isPublished": True}, 201) for i in range(2)]
        self.product = self.products[0]
        user = "e2e" + self.suffix
        driver = self.a("POST", "/api/drivers", {"firstName": "E2E", "lastName": self.suffix,
            "documentNumber": self.suffix, "phone": "3811234567", "userName": user,
            "password": "Chofer1234", "vehicleId": self.vehicle}, 201)
        login = self.h.login(user, "Chofer1234")
        self.h.eq("driverId del token", login["driverId"], driver)
        self.driver = driver
        self.token = login["token"]
        self.customer = self.new_customer()
        self.session = None
        self.km = 120000

    def a(self, method, path, body=None, status=200):
        return self.h.call(method, path, body, self.admin, status)

    def d(self, method, path, body=None, status=200):
        return self.h.call(method, path, body, self.token, status)

    def new_vehicle(self):
        return self.a("POST", "/api/vehicles", {"name": "Camion " + uid()[:12],
            "licensePlate": uuid.uuid4().hex[:15], "type": "Pickup", "currentKilometers": 120000}, 201)

    def prospect(self, days=None):
        return {"contactName": "Cliente " + uid(), "phone": "3811234567",
                "address": "Calle 123", "visitDays": days or [1]}

    def new_customer(self, days=None):
        return self.a("POST", "/api/customers", {**self.prospect(days),
            "vehicleId": self.vehicle, "zoneId": self.zone}, 201)

    def load_body(self, quantity=100, days=None):
        return {"clientRequestId": uid(), "routeDays": days or [1],
                "items": [{"productId": self.product, "quantity": quantity}]}

    def load(self, quantity=100, days=None):
        return self.a("POST", f"/api/vehicles/{self.vehicle}/load", self.load_body(quantity, days))

    def open(self):
        self.session = self.d("POST", "/api/sessions/open",
            {"zoneId": self.zone, "kilometersAtOpen": self.km}, 201)
        self.h.eq("carga consumida", self.a("GET", f"/api/vehicles/{self.vehicle}/load"), [])
        self.date = local_date(self.current()["openedAt"])
        return self.session

    def start(self, quantity=100, days=None):
        self.load(quantity, days)
        self.open()

    def current(self):
        return self.d("GET", "/api/sessions/current")

    def sale_body(self, quantity=10, empty=0, payment=None, customer=None):
        return {"clientRequestId": uid(), "customerId": customer or self.customer, "type": "Sale",
                "items": [{"productId": self.product, "quantity": quantity}], "containersOut": [],
                "containersIn": [{"productId": self.product, "quantity": empty}] if empty else [],
                "payment": payment}

    def sale(self, quantity=10, empty=0, payment=None, customer=None):
        return self.d("POST", "/api/deliveries", self.sale_body(quantity, empty, payment, customer))

    def account(self, customer=None):
        return self.a("GET", f"/api/customers/{customer or self.customer}/account")

    def summary(self):
        return self.a("GET", f"/api/sessions/daily-summary?vehicleId={self.vehicle}&date={self.date}")

    def finish(self, cash=0, full_missing=0, empty_missing=0):
        returns = []
        for line in self.current()["stock"]:
            for state, key, missing in [("Full", "fullOnBoard", full_missing), ("Empty", "emptyOnBoard", empty_missing)]:
                quantity = line[key] - (missing if line["productId"] == self.product else 0)
                if quantity > 0:
                    returns.append({"productId": line["productId"], "state": state, "quantity": quantity})
        self.km += 50
        closed = self.a("POST", f"/api/sessions/{self.session}/close",
                        {"kilometersAtClose": self.km, "returns": returns})
        self.h.eq("recepcion cuadra", closed["cuadraTodo"], not (full_missing or empty_missing))
        self.a("POST", f"/api/sessions/{self.session}/settlement", {"amountReceived": cash})
        self.h.eq("estado final", self.a("GET", f"/api/sessions/{self.session}")["status"], "Closed")
        self.d("GET", "/api/sessions/current", status=204)
        summary = self.summary()
        self.h.eq("dia recibido", summary["dayTotals"]["isClosed"], True)
        self.h.eq("rendiciones pendientes", summary["dayTotals"]["pendingSettlements"], 0)
        self.h.eq("salida fuera del home", self.session in [s["sessionId"] for s in
                  self.a("GET", "/api/sessions/active-departures")], False)
        return summary

    def restock_body(self):
        return {"clientRequestId": uid(), "notes": " En ruta ", "items": [
            {"productId": p, "quantity": q} for p, q in zip(self.products, [3, 2])]}

    def promotion_body(self, quantity=3):
        return {"clientRequestId": uid(), "prospect": self.prospect(),
                "items": [{"productId": self.product, "quantity": quantity}]}

    def close_promotion(self, convert=True):
        return {"clientRequestId": uid(), "convertToCustomer": convert, "customer": None,
                "containersReturned": [] if convert else [{"productId": self.product, "quantity": 2}]}

    def route_path(self, day=1, vehicle=None):
        return f"/api/route-planning?vehicleId={vehicle or self.vehicle}&zoneId={self.zone}&day={day}"

    def customers(self, driver=False):
        return (self.d if driver else self.a)("GET",
            f"/api/customers?zoneId={self.zone}&vehicleId={self.vehicle}&pageSize=100")

    def report_product(self):
        return next(p for p in self.a("GET", "/api/reports/containers-out") if p["productId"] == self.product)
