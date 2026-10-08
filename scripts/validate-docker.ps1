[CmdletBinding()]
param([string]$ProjectName = 'motorbike', [ValidateRange(1,65535)][int]$ApiPort = 8080)
$ErrorActionPreference = 'Stop'
Push-Location (Split-Path $PSScriptRoot -Parent)
try {
    docker compose -p $ProjectName config --quiet
    if ($LASTEXITCODE -ne 0) { throw 'Configuración Compose inválida.' }
    docker compose -p $ProjectName up --build -d --wait --wait-timeout 240
    if ($LASTEXITCODE -ne 0) { throw 'Compose no alcanzó el estado healthy.' }
    $health = Invoke-WebRequest "http://localhost:$ApiPort/health"
    if ($health.StatusCode -ne 200 -or ($health.Content | ConvertFrom-Json).status -ne 'Healthy') {
        throw 'Health de la API no es correcto.'
    }
    docker compose -p $ProjectName ps
    if ($LASTEXITCODE -ne 0) { throw 'No se pudo consultar Compose.' }
    docker compose -p $ProjectName exec -T postgres psql -U motorbike -d motorbike -c 'SELECT "MigrationId" FROM "__EFMigrationsHistory";'
    if ($LASTEXITCODE -ne 0) { throw 'No se pudo verificar la migración.' }
    Write-Output 'Docker validado: servicios healthy, GET /health 200 y migración PostgreSQL.'
} finally { Pop-Location }
