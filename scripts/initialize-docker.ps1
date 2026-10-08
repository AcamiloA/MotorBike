[CmdletBinding()]
param([ValidateRange(1,65535)][int]$ApiPort = 8080)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$target = Join-Path $root '.env'
if (Test-Path -LiteralPath $target) { throw '.env ya existe; se conserva. Edita su configuración si es necesario.' }
function New-Secret {
    $bytes = New-Object byte[] 48
    [System.Security.Cryptography.RandomNumberGenerator]::Fill($bytes)
    return [Convert]::ToHexString($bytes)
}
$configuration = @(
    "POSTGRES_PASSWORD=$(New-Secret)"
    "JWT_KEY=$(New-Secret)"
    "API_PORT=$ApiPort"
    'SWAGGER_ENABLED=false'
)
[System.IO.File]::WriteAllLines($target, $configuration)
Write-Output 'Configuración local creada en .env; no publiques sus secretos.'
