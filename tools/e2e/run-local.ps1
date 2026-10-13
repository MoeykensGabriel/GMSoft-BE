param(
    [ValidateRange(1024, 65535)][int]$Port = 5199,
    [ValidateRange(1, 10)][int]$Runs = 1
)
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$apiDirectory = Join-Path $repo 'GMSoft.API'
$timer = [Diagnostics.Stopwatch]::StartNew()
$apiProcess = $null
$created = $false
$result = 1
$database = 'gmsoft_e2e_' + [guid]::NewGuid().ToString('N')
$savedEnvironment = @{}

function Set-TestEnvironment([string]$Name, [string]$Value) {
    if (-not $savedEnvironment.ContainsKey($Name)) {
        $savedEnvironment[$Name] = [Environment]::GetEnvironmentVariable($Name, 'Process')
    }
    [Environment]::SetEnvironmentVariable($Name, $Value, 'Process')
}

function Assert-TestDatabase {
    if ($database -notmatch '^gmsoft_e2e_[a-f0-9]{32}$') { throw 'Nombre de base inseguro.' }
}

function Invoke-AdminSql([string]$Sql) {
    # -X evita psqlrc; -w nunca pide clave; solo conecta a postgres, nunca a desarrollo.
    $output = & $psql -X -w -h localhost -p 5432 -U $serverUser -d postgres -v ON_ERROR_STOP=1 -At -c $Sql
    if ($LASTEXITCODE -ne 0) { throw 'Fallo PostgreSQL administrativo (credenciales omitidas).' }
    return $output
}

Push-Location $repo
try {
    if ($Port -eq 5142) { throw 'El puerto 5142 esta prohibido para esta prueba.' }
    # Comprobar disponibilidad antes de crear base o iniciar procesos.
    $listener = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback, $Port)
    try { $listener.Start() } finally { $listener.Stop() }
    $psqlCommand = Get-Command psql -ErrorAction SilentlyContinue
    if ($psqlCommand) { $psql = $psqlCommand.Source }
    else {
        $psql = Get-ChildItem 'C:/Program Files/PostgreSQL/*/bin/psql.exe' -ErrorAction SilentlyContinue |
            Sort-Object FullName -Descending | Select-Object -First 1 -ExpandProperty FullName
    }
    if (-not $psql) { throw 'Se requiere psql en PATH o instalado en Program Files/PostgreSQL.' }

    # Misma configuracion de desarrollo; nunca imprimir la cadena ni las claves.
    $configured = $null
    foreach ($file in @('appsettings.json', 'appsettings.Development.json')) {
        $path = Join-Path $apiDirectory $file
        if (Test-Path -LiteralPath $path) {
            $config = Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
            if ($config.ConnectionStrings.DefaultConnection) { $configured = $config.ConnectionStrings.DefaultConnection }
        }
    }
    [xml]$project = Get-Content (Join-Path $apiDirectory 'GMSoft.API.csproj')
    $secretId = $project.SelectSingleNode('//UserSecretsId')
    if ($secretId) {
        $secretFile = Join-Path $env:APPDATA "Microsoft/UserSecrets/$($secretId.InnerText)/secrets.json"
        if (Test-Path -LiteralPath $secretFile) {
            $secrets = Get-Content -LiteralPath $secretFile -Raw | ConvertFrom-Json
            if ($secrets.'ConnectionStrings:DefaultConnection') { $configured = $secrets.'ConnectionStrings:DefaultConnection' }
            elseif ($secrets.ConnectionStrings.DefaultConnection) { $configured = $secrets.ConnectionStrings.DefaultConnection }
        }
    }
    if ($env:ConnectionStrings__DefaultConnection) { $configured = $env:ConnectionStrings__DefaultConnection }
    foreach ($name in @('DATABASE_PUBLIC_URL', 'DATABASE_URL', 'CONNECTION_STRING', 'GMSOFT_TEST_CONNECTION')) {
        $value = [Environment]::GetEnvironmentVariable($name, 'Process')
        if (-not [string]::IsNullOrWhiteSpace($value)) { $configured = $value }
    }
    if (-not $configured) { throw 'No hay configuracion PostgreSQL de desarrollo.' }
    $source = New-Object System.Data.Common.DbConnectionStringBuilder
    try {
        if ($configured -match '^postgres(ql)?://') {
            $uri = [uri]$configured
            $credentials = $uri.UserInfo.Split(':', 2)
            $source['Host'] = $uri.Host
            $source['Port'] = $(if ($uri.Port -gt 0) { $uri.Port } else { 5432 })
            $source['Username'] = [uri]::UnescapeDataString($credentials[0])
            $source['Password'] = $(if ($credentials.Length -gt 1) { [uri]::UnescapeDataString($credentials[1]) } else { '' })
        } else { $source.set_ConnectionString($configured) }
    } catch { throw 'Configuracion PostgreSQL invalida (valor omitido).' }
    $serverPort = $(if ($source.ContainsKey('Port')) { [int]$source['Port'] } else { 5432 })
    if ($source['Host'] -notin @('localhost', '127.0.0.1', '::1') -or $serverPort -ne 5432) {
        throw 'Este ejecutor solo permite PostgreSQL local en localhost:5432.'
    }
    $serverUser = [string]$source['Username']
    if (-not $serverUser -and $source.ContainsKey('User ID')) { $serverUser = [string]$source['User ID'] }
    if (-not $serverUser) { throw 'Falta usuario PostgreSQL.' }
    $connection = New-Object System.Data.Common.DbConnectionStringBuilder
    $connection['Host'] = 'localhost'
    $connection['Port'] = 5432
    $connection['Username'] = $serverUser
    $connection['Password'] = [string]$source['Password']
    Assert-TestDatabase
    $connection['Database'] = $database
    $connection['Include Error Detail'] = $false
    Set-TestEnvironment 'PGPASSWORD' ([string]$source['Password'])
    Set-TestEnvironment 'PGCONNECT_TIMEOUT' '5'
    Set-TestEnvironment 'PGOPTIONS' ''
    Set-TestEnvironment 'PGSERVICE' ''
    Set-TestEnvironment 'PGHOSTADDR' ''
    Set-TestEnvironment 'PGSSLMODE' 'prefer'

    dotnet build GMSoft.slnx -c Release
    if ($LASTEXITCODE -ne 0) { throw 'Fallo dotnet build Release.' }
    Assert-TestDatabase
    Invoke-AdminSql ('CREATE DATABASE "' + $database + '"') | Out-Null
    $created = $true
    Write-Host "Base efimera creada: $database"
    Set-TestEnvironment 'CONNECTION_STRING' $connection.get_ConnectionString()
    Set-TestEnvironment 'JWT_SECRET_KEY' ('e2e-only-' + [guid]::NewGuid().ToString('N') + [guid]::NewGuid().ToString('N'))
    Set-TestEnvironment 'Database__MigrateOnStartup' 'true'
    Set-TestEnvironment 'Seed__AdminUserName' 'e2eadmin'
    Set-TestEnvironment 'SEED_ADMIN_PASSWORD' 'E2eAdmin1234'
    Set-TestEnvironment 'ASPNETCORE_URLS' "http://localhost:$Port"
    Set-TestEnvironment 'ASPNETCORE_ENVIRONMENT' 'Production'
    Set-TestEnvironment 'DOTNET_ENVIRONMENT' 'Production'
    Set-TestEnvironment 'GMSOFT_BASE_URL' "http://localhost:$Port"
    Set-TestEnvironment 'GMSOFT_ADMIN_USER' 'e2eadmin'
    Set-TestEnvironment 'GMSOFT_ADMIN_PASSWORD' 'E2eAdmin1234'
    Set-TestEnvironment 'CustomerActivity__RedAfterMissedWeeks' '2'
    Set-TestEnvironment 'CustomerActivity__BlackAfterMissedWeeks' '4'
    $logDirectory = Join-Path $repo 'artifacts/e2e'
    New-Item -ItemType Directory -Path $logDirectory -Force | Out-Null
    $stdout = Join-Path $logDirectory 'api.stdout.log'
    $stderr = Join-Path $logDirectory 'api.stderr.log'
    # Ejecutar DLL Release directamente: el PID es la API, sin dotnet run ni hijos.
    # No se leen perfiles; --no-launch-profile se pasa tambien explicitamente.
    $dll = Join-Path $apiDirectory 'bin/Release/net10.0/GMSoft.API.dll'
    $apiProcess = Start-Process -FilePath (Get-Command dotnet).Source -ArgumentList @('"' + $dll + '"', '--no-launch-profile') `
        -WorkingDirectory $apiDirectory -WindowStyle Hidden -PassThru -RedirectStandardOutput $stdout -RedirectStandardError $stderr
    Write-Host "API propia PID $($apiProcess.Id), puerto $Port; logs en artifacts/e2e."
    $ready = $false
    for ($attempt = 0; $attempt -lt 60; $attempt++) {
        if ($apiProcess.HasExited) { throw 'La API termino antes de responder. Ver artifacts/e2e/api.*.log.' }
        try {
            $health = Invoke-WebRequest "http://localhost:$Port/api/health" -UseBasicParsing -TimeoutSec 2
            if ($health.StatusCode -eq 200) { $ready = $true; break }
        } catch { Start-Sleep -Seconds 1 }
    }
    if (-not $ready) { throw 'La API no respondio al health check.' }
    $ownedListeners = @(Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction Stop |
        Where-Object { $_.OwningProcess -eq $apiProcess.Id })
    if ($ownedListeners.Count -eq 0) { throw 'El listener no pertenece al PID iniciado por el ejecutor.' }
    $result = 0
    for ($run = 1; $run -le $Runs; $run++) {
        if ($run -gt 1) {
            Write-Host 'Espera de 61s entre suites para renovar la ventana de login (misma base y API).'
            Start-Sleep -Seconds 31
            Start-Sleep -Seconds 30
        }
        Write-Host "Ejecucion $run/$Runs sobre la misma base efimera."
        python tools/e2e-smoke.py | Tee-Object -FilePath (Join-Path $logDirectory "suite-$run.log")
        if ($LASTEXITCODE -ne 0) { $result = 1 }
    }
} catch {
    # No incluir objetos de configuracion, cadenas ni excepciones de sus parsers.
    Write-Host "FALLA ejecutor: $($_.Exception.Message)"
    $result = 1
} finally {
    try {
        if ($apiProcess) {
            if (-not $apiProcess.HasExited) { Stop-Process -Id $apiProcess.Id -Force; $apiProcess.WaitForExit() }
            Write-Host "API propia PID $($apiProcess.Id) terminada: $($apiProcess.HasExited)."
        }
    } catch { Write-Host 'FALLA al detener la API propia.'; $result = 1 }
    try {
        if ($created) {
            Assert-TestDatabase
            Invoke-AdminSql ('DROP DATABASE IF EXISTS "' + $database + '" WITH (FORCE)') | Out-Null
            Write-Host "Base eliminada: $database"
            $remaining = @(Invoke-AdminSql "SELECT datname FROM pg_database WHERE starts_with(datname, 'gmsoft_e2e_') ORDER BY datname")
            Write-Host "Bases gmsoft_e2e_* restantes: $($remaining.Count) $($remaining -join ', ')"
            if ($remaining.Count -gt 0) { $result = 1 }
        }
        if ($apiProcess) {
            $connections = @(Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue)
            Write-Host "Listeners finales en puerto ${Port}: $($connections.Count)"
            if ($connections.Count -gt 0) { $result = 1 }
        }
    } catch { Write-Host "FALLA limpieza: $($_.Exception.Message)"; $result = 1 }
    foreach ($name in $savedEnvironment.Keys) {
        [Environment]::SetEnvironmentVariable($name, $savedEnvironment[$name], 'Process')
    }
    Pop-Location
    Write-Host ('Tiempo total ejecutor: {0:N2}s; codigo: {1}' -f $timer.Elapsed.TotalSeconds, $result)
}
exit $result
