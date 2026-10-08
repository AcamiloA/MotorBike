[CmdletBinding()]
param([string]$ProjectName = 'motorbike-phase20', [string]$OutputFile)
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Split-Path $PSScriptRoot -Parent))
if (!$OutputFile) { $OutputFile = 'backups/motorbike-' + [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss') + '-' + [Guid]::NewGuid().ToString('N').Substring(0,8) + '.dump' }
$target = [IO.Path]::GetFullPath((Join-Path $root $OutputFile))
if (!$target.StartsWith($root + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'El respaldo debe permanecer dentro del repositorio; cópialo después a su destino protegido.' }
if (Test-Path -LiteralPath $target) { throw 'El archivo de respaldo ya existe; no se sobrescribe.' }
$temporary = '/tmp/motorbike-backup-' + [Guid]::NewGuid().ToString('N') + '.dump'
Push-Location $root
try {
    $container = (docker compose -p $ProjectName ps -q postgres | Out-String).Trim()
    if ($LASTEXITCODE -ne 0 -or !$container) { throw 'PostgreSQL de ese proyecto Compose no está ejecutándose.' }
    docker exec $container pg_dump -U motorbike -d motorbike --format=custom --file=$temporary
    if ($LASTEXITCODE -ne 0) { throw 'pg_dump falló; no se entrega un respaldo incompleto.' }
    $null = docker exec $container pg_restore --list $temporary
    if ($LASTEXITCODE -ne 0) { throw 'El dump no puede leerse con pg_restore.' }
    $null = [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($target))
    docker cp "${container}:$temporary" $target
    if ($LASTEXITCODE -ne 0) { throw 'No se pudo copiar el respaldo binario.' }
    if ((Get-Item -LiteralPath $target).Length -eq 0) { throw 'Respaldo vacío.' }
    [pscustomobject]@{File=$target;Bytes=(Get-Item -LiteralPath $target).Length;Sha256=(Get-FileHash -LiteralPath $target).Hash}
} finally {
    if ($container) { docker exec $container rm -f $temporary | Out-Null }
    Pop-Location
}
