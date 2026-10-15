"""Segundo reparto de demostracion, para comparar camiones en las estadisticas.

Reutiliza toda la logica de seed.py (ventas reales por la API, recepciones,
rendiciones y fechas llevadas a su dia) con otro camion, otro chofer, otras zonas
y otros clientes. Mismas seis semanas que la Kangoo, asi los periodos coinciden.

Uso (PowerShell, desde GM-SoftBE, con la API levantada en http://localhost:5142):
    $env:DEMO_DRIVER_PASSWORD = '<contrasena del chofer>'
    python tools/demo-route/seed_second.py            # solo muestra el plan
    python tools/demo-route/seed_second.py --apply    # lo escribe
"""
import os
import random
import sys

sys.path.insert(0, os.path.dirname(__file__))
import seed  # noqa: E402

W, R, K = seed.W, seed.R, seed.K
seed.MANIFEST = os.path.abspath(os.path.join(seed.ROOT, "..", "demo-route-second-manifest.json"))
seed.VEHICLE = {"name": "Toyota Hilux", "licensePlate": "AF789TH", "type": "Pickup", "currentKilometers": 61500}
seed.DRIVER = {"firstName": "Lucas", "lastName": "Romero", "documentNumber": "35123456",
               "phone": "3815550177", "userName": "reparto3"}
# Una zona por dia; Tafi Viejo es grande y ocupa dos.
seed.ZONE_OF_DAY = {1: "Tafí Viejo", 2: "Alderetes", 3: "El Cadillal", 4: "Tafí Viejo", 5: "La Florida", 6: "Ranchillos"}

# Recorrido mas flojo que el de la Kangoo a proposito: mas clientes en rojo y negro,
# para que la comparacion muestre diferencias.
STREETS = {
    "Tafí Viejo": ["Av. Alem", "Av. Sáenz Peña", "Rivadavia", "Belgrano", "Las Heras", "Juan B. Justo"],
    "Alderetes": ["Av. Libertad", "San Martín", "Moreno", "Mitre"],
    "El Cadillal": ["Ruta 341", "Los Ceibos", "Las Lomas"],
    "La Florida": ["Av. Independencia", "Sarmiento", "9 de Julio"],
    "Ranchillos": ["Av. San Martín", "Perú", "Mendoza", "Catamarca"],
}
NAMES = ["Ana Rojas", "Bruno Castro", "Carolina Vega", "Damián Luna", "Elena Prado", "Facundo Ríos", "Gisela Ortega",
         "Héctor Salinas", "Inés Cabrera", "Joaquín Reyes", "Karina Funes", "Leandro Paz", "Melina Toledo",
         "Norberto Gil", "Olga Barrera", "Pedro Maidana", "Quimey Sosa", "Rocío Lazarte", "Santiago Moya",
         "Teresa Bravo", "Ulises Acosta", "Viviana Peralta", "Walter Godoy", "Ximena Correa", "Yamila Ruiz",
         "Zulma Herrera", "Alan Ferreyra", "Belén Ojeda", "César Quinteros", "Daniela Arias", "Esteban Molina",
         "Fabiana Ledesma", "Gonzalo Pereyra", "Hilda Montenegro", "Iván Carrizo", "Julia Benítez"]
SHOPS = ["Kiosco La Parada", "Almacén Don Pepe", "Verdulería Los Hermanos", "Gomería El Rápido",
         "Panadería San Cayetano", "Despensa Mi Barrio", "Taller El Turco", "Club Juventud Unida"]


def customers():
    rng = random.Random(20261015)
    names, shops, rows = list(NAMES), list(SHOPS), {}
    rng.shuffle(names)
    rng.shuffle(shops)
    per_day = {1: 7, 2: 6, 3: 5, 4: 6, 5: 6, 6: 6}  # 36, uno por nombre
    for day, count in per_day.items():
        zone = seed.ZONE_OF_DAY[day]
        streets, number = STREETS[zone], rng.randint(100, 300)
        for _ in range(count):
            business = shops.pop() if shops and rng.random() < 0.25 else None
            number += rng.randint(40, 180)
            status = rng.choices([W, R, K], weights=[50, 32, 18])[0]
            rows.setdefault(zone, []).append((business, names.pop(), f"{rng.choice(streets)} {number}", [day], status))
    return rows


seed.CUSTOMERS = customers()

if __name__ == "__main__":
    sys.exit(seed.main())
