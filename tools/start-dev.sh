#!/usr/bin/env bash
# Inicia API y frontend en una sola sesión de desarrollo de Linux.
set -euo pipefail
set +m

backend_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)"
frontend_dir="${1:-${GMSOFT_FE_DIR:-}}"
if [[ -z "$frontend_dir" ]]; then
  for candidate in "$backend_dir/../GMSoft-FE" "$backend_dir/../GM-SoftFE"; do
    if [[ -f "$candidate/package.json" ]]; then
      frontend_dir="$candidate"
      break
    fi
  done
fi
if [[ ! -f "$frontend_dir/package.json" ]]; then
  echo 'No se encontró el frontend. Indicá su carpeta: bash tools/start-dev.sh /ruta/GMSoft-FE' >&2
  exit 1
fi
for command_name in dotnet node npm setsid; do
  if ! command -v "$command_name" >/dev/null 2>&1; then
    echo "Falta $command_name. Instalalo antes de iniciar los proyectos." >&2
    exit 1
  fi
done
if [[ ! -d "$frontend_dir/node_modules" ]]; then
  echo "Primero ejecutá npm install en la carpeta del frontend: $frontend_dir" >&2
  exit 1
fi

server_ip="${GMSOFT_SERVER_IP:-}"
if [[ -z "$server_ip" && -f "$HOME/.config/gmsoft/server-ip" ]]; then
  server_ip="$(cat "$HOME/.config/gmsoft/server-ip")"
fi
if [[ -z "$server_ip" ]]; then
  server_ip="$(hostname -I | awk '{for (i=1; i<=NF; i++) if ($i ~ /^[0-9]+\.[0-9]+\.[0-9]+\.[0-9]+$/) {print $i; exit}}')"
fi
if [[ -z "$server_ip" || "$server_ip" == localhost || "$server_ip" == 127.* || "$server_ip" == 0.0.0.0 || "$server_ip" == ::1 ]]; then
  echo 'No se encontró una IP de red. Indicála con GMSOFT_SERVER_IP, por ejemplo:' >&2
  echo 'GMSOFT_SERVER_IP=192.168.1.71 bash tools/start-dev.sh' >&2
  exit 1
fi
export ASPNETCORE_ENVIRONMENT=Development
# El iniciador arranca esta API en el puerto 5000. Un valor anterior de Vite o de
# .env.local no debe enviar al celular a su propio localhost.
export VITE_API_URL="http://$server_ip:5000"
export CORS_ORIGINS="${CORS_ORIGINS:+$CORS_ORIGINS,}http://$server_ip:3000,http://localhost:3000"
if [[ -z "${JWT_SECRET_KEY:-}" && -f "$HOME/.config/gmsoft/jwt-secret" ]]; then
  export JWT_SECRET_KEY="$(cat "$HOME/.config/gmsoft/jwt-secret")"
fi

backend_pid=''
frontend_pid=''
cleanup() {
  # Cada proyecto tiene su grupo de procesos: se detienen también sus hijos.
  [[ -z "$backend_pid" ]] || kill -TERM -- "-$backend_pid" 2>/dev/null || true
  [[ -z "$frontend_pid" ]] || kill -TERM -- "-$frontend_pid" 2>/dev/null || true
  wait 2>/dev/null || true
}
trap cleanup EXIT
trap 'exit 130' INT
trap 'exit 143' TERM
trap 'exit 129' HUP

echo "API: http://$server_ip:5000"
echo "Abrí el panel desde Windows o el celular: http://$server_ip:3000/login"
echo 'Iniciando ambos proyectos. Ctrl+C detiene los dos.'
(
  cd -- "$backend_dir"
  exec setsid dotnet run --project GMSoft.API --no-launch-profile --urls http://0.0.0.0:5000
) &
backend_pid=$!
(
  cd -- "$frontend_dir"
  # La IP concreta evita que Vite anuncie localhost como dirección del panel.
  exec setsid npm run dev -- --host "$server_ip" --port 3000 --strictPort
) &
frontend_pid=$!

if wait -n "$backend_pid" "$frontend_pid"; then
  status=0
else
  status=$?
fi
echo 'Uno de los proyectos terminó. Se detiene también el otro.' >&2
exit "$status"
