[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$target = Join-Path (Split-Path $PSScriptRoot -Parent) '.env'
if (!(Test-Path -LiteralPath $target)) { throw 'Primero ejecuta initialize-docker.ps1.' }
$lines = [System.IO.File]::ReadAllLines($target)
$existing = @($lines | Where-Object { $_ -match '^DEMO_PASSWORD=.+$' })
if ($existing.Count -gt 1) { throw 'Hay contraseñas demo duplicadas en .env; corrige la configuración.' }
if ($existing.Count -eq 1) { $passwordLine = $existing[0] }
else {
    $bytes = New-Object byte[] 24
    [System.Security.Cryptography.RandomNumberGenerator]::Fill($bytes)
    $passwordLine = 'DEMO_PASSWORD=Mb1' + [Convert]::ToHexString($bytes)
}
$preserved = @($lines | Where-Object { $_ -notmatch '^(SEED_ENABLED|DEMO_ENABLED|ALLOW_DEMO_IN_PRODUCTION|DEMO_PASSWORD)=' })
[System.IO.File]::WriteAllLines($target, $preserved + @('SEED_ENABLED=true', 'DEMO_ENABLED=true', 'ALLOW_DEMO_IN_PRODUCTION=true', $passwordLine))
Write-Output 'Demo habilitada para Docker local. Contraseña conservada/generada en DEMO_PASSWORD de .env; no se imprime.'
