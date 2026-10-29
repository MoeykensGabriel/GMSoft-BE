<#
  Restaura una copia nocturna de la base en una base LOCAL nueva, para comprobar que
  sirve o para recuperar datos. Nunca toca producción.

  Uso:
    1. En GitHub: Actions -> "Copia de la base" -> la corrida que quieras -> Artifacts.
       Bajá el .zip y descomprimilo: adentro está gmsoft-AAAA-MM-DD-HHMM.dump.gpg.
    2. powershell -ExecutionPolicy Bypass -File tools\backup\restaurar-copia.ps1 -Archivo C:\ruta\gmsoft-....dump.gpg
       Pide la frase de las copias (BACKUP_PASSPHRASE) y la contraseña del usuario postgres local.
#>
param(
    [Parameter(Mandatory = $true)][string]$Archivo,
    [string]$Base = ('GMSoftCopia_' + (Get-Date -Format 'yyyyMMdd_HHmm')),
    [string]$PgBin = 'C:\Program Files\PostgreSQL\18\bin',
    [string]$Gpg = 'C:\Program Files\Git\usr\bin\gpg.exe'
)
$ErrorActionPreference = 'Stop'

if (-not (Test-Path $Archivo)) { throw "No existe el archivo $Archivo" }
foreach ($exe in "$PgBin\pg_restore.exe", "$PgBin\createdb.exe", "$PgBin\psql.exe", $Gpg) {
    if (-not (Test-Path $exe)) { throw "No encuentro $exe. Ajustá -PgBin o -Gpg." }
}
if ($Base -notmatch '^[A-Za-z0-9_]+$') { throw 'El nombre de la base solo puede tener letras, números y _.' }

$frase = Read-Host 'Frase de las copias (BACKUP_PASSPHRASE)' -AsSecureString
$fraseTexto = [Runtime.InteropServices.Marshal]::PtrToStringAuto([Runtime.InteropServices.Marshal]::SecureStringToBSTR($frase))
$dump = Join-Path $env:TEMP ("gmsoft-restaurar-{0}.dump" -f [guid]::NewGuid())

try {
    $fraseTexto | & $Gpg --batch --yes --pinentry-mode loopback --passphrase-fd 0 --output $dump --decrypt $Archivo
    if ($LASTEXITCODE -ne 0) { throw 'No se pudo descifrar: revisá la frase.' }

    $env:PGHOST = 'localhost'; $env:PGUSER = 'postgres'
    $pass = Read-Host 'Contraseña del usuario postgres LOCAL' -AsSecureString
    $env:PGPASSWORD = [Runtime.InteropServices.Marshal]::PtrToStringAuto([Runtime.InteropServices.Marshal]::SecureStringToBSTR($pass))

    & "$PgBin\createdb.exe" $Base
    if ($LASTEXITCODE -ne 0) { throw "No se pudo crear la base local $Base." }
    & "$PgBin\pg_restore.exe" --no-owner --no-privileges --dbname=$Base $dump
    if ($LASTEXITCODE -ne 0) { throw 'pg_restore terminó con errores: revisá la salida de arriba.' }

    Write-Host "`nCopia restaurada en la base local $Base. Cuentas de control:" -ForegroundColor Green
    & "$PgBin\psql.exe" -d $Base -c @'
select 'Clientes activos' as que, count(*) from "Customers" where "IsActive"
union all select 'Salidas', count(*) from "DeliverySessions"
union all select 'Visitas', count(*) from "Deliveries"
union all select 'Ultima salida: ' || coalesce(max("OpenedAt")::text, '-'), 0 from "DeliverySessions";
'@
} finally {
    if (Test-Path $dump) { Remove-Item $dump -Force }
    Remove-Item Env:PGPASSWORD -ErrorAction SilentlyContinue
    $fraseTexto = $null
}
