[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$InputFile,
    [Parameter(Mandatory)][ValidatePattern('^[a-zA-Z_][a-zA-Z0-9_]{0,62}$')][string]$Database,
    [string]$ProjectName = 'motorbike-phase20'
)
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Split-Path $PSScriptRoot -Parent))
$source = [IO.Path]::GetFullPath((Join-Path $root $InputFile))
if (!(Test-Path -LiteralPath $source -PathType Leaf)) { throw 'El dump indicado no existe.' }
if ($Database -ieq 'motorbike' -or $Database -ieq 'postgres' -or $Database -match '^template') { throw 'Usa una base nueva; no se restaura encima de una base de ejecución o sistema.' }
$temporary = '/tmp/motorbike-restore-' + [Guid]::NewGuid().ToString('N') + '.dump'
Push-Location $root
try {
    $container = (docker compose -p $ProjectName ps -q postgres | Out-String).Trim()
    if ($LASTEXITCODE -ne 0 -or !$container) { throw 'PostgreSQL de ese proyecto Compose no está ejecutándose.' }
    docker cp $source "${container}:$temporary"
    if ($LASTEXITCODE -ne 0) { throw 'No se pudo copiar el dump binario.' }
    $null = docker exec $container pg_restore --list $temporary
    if ($LASTEXITCODE -ne 0) { throw 'Formato de respaldo inválido; no se creó ninguna base.' }
    # createdb refuses existing names; never DROP or overwrite a database.
    docker exec $container createdb -U motorbike --maintenance-db=motorbike $Database
    if ($LASTEXITCODE -ne 0) { throw 'No se creó una base nueva; si ya existe, el script no la modifica.' }
    docker exec $container pg_restore -U motorbike --dbname=$Database --single-transaction --exit-on-error --no-owner --no-privileges $temporary
    if ($LASTEXITCODE -ne 0) { throw 'Restauración fallida; la transacción se revierte y la base nueva se conserva para diagnóstico.' }
    docker exec $container psql -U motorbike -d $Database -v ON_ERROR_STOP=1 -c 'SELECT "MigrationId" FROM "__EFMigrationsHistory";'
    if ($LASTEXITCODE -ne 0) { throw 'El respaldo no contiene una base MotorBike válida con historial de migraciones.' }
    Write-Output "Restauración completada en $Database. La API y la base motorbike permanecen sin cambios."
} finally {
    if ($container) { docker exec $container rm -f $temporary | Out-Null }
    Pop-Location
}
