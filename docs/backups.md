# Respaldos y recuperación

## PostgreSQL local en Compose

Los scripts requieren PowerShell 7 y Docker con el servicio postgres ejecutándose.
Usan pg_dump/pg_restore del propio PostgreSQL 17, sin instalar clientes en Windows.
Desde la raíz:

```powershell
./scripts/backup-db.ps1 -ProjectName motorbike-phase20 -OutputFile backups/motorbike-demo.dump
./scripts/restore-db.ps1 -ProjectName motorbike-phase20 -InputFile backups/motorbike-demo.dump -Database motorbike_restore_review
```

Backup sin OutputFile genera un nombre único con fecha UTC. No sobrescribe archivos.
El dump es formato custom, con esquema, datos, índices, restricciones e historial
EF. Se crea como archivo dentro del contenedor y se copia con docker cp, evitando
redirecciones de binarios mediante PowerShell. El resultado informa ruta, tamaño
y SHA256. No imprime contraseña ni contenido de tablas.

Restore copia el binario, comprueba su formato, crea una base NUEVA y restaura en
una transacción con salida ante error. No hace DROP ni usa --clean. Rechaza motorbike,
postgres, nombres de sistema y nombres inválidos; createdb rechaza cualquier base
ya existente. Un fallo de restauración revierte la transacción y conserva la base
nueva para diagnóstico. Se omiten propietarios/permisos del servidor original:
los objetos quedan bajo el usuario motorbike. No restaura usuarios globales de
PostgreSQL ni credenciales de infraestructura; los roles USER/GUARD/ADMIN del
negocio sí forman parte de las tablas respaldadas.

El recorrido de Fase 22 restauró `backups/phase22-validation.dump` en
`motorbike_restore_phase22`, conservando la base demo. Para comprobar otra recuperación
elija un nombre nuevo, no reutilice esa base. Revise migraciones, usuarios, vehículos,
propiedades, registros e historial antes de cambiar la conexión de la API.
Los scripts no cambian la conexión ni reinician la API automáticamente.

## Archivos privados: respaldo separado

El dump contiene referencias de archivos, no su contenido. Con Local respalde el
volumen private-files, y con S3 respalde/versione los objetos y sus claves. Mantenga
base y archivos del mismo punto operativo. Para un respaldo coordinado, programe
mantenimiento sin escrituras, detenga la API de ese Compose, respalde base y volumen,
y restablezca el servicio. No detenga un servidor distinto por confundir proyectos.

Ejemplo para la instancia local actual; genera un archivo nuevo sin sobrescribir:

```powershell
$stamp = [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss')
$archiveDir = Join-Path (Get-Location) 'backups'
$null = New-Item -ItemType Directory -Force -Path $archiveDir
docker compose -p motorbike-phase20 stop api
try {
    ./scripts/backup-db.ps1 -OutputFile "backups/motorbike-$stamp.dump"
    docker run --rm --mount 'type=volume,source=motorbike-phase20_private-files,target=/files,readonly' --mount "type=bind,source=$archiveDir,target=/backup" mcr.microsoft.com/dotnet/aspnet:10.0 sh -c "test ! -e /backup/private-files-$stamp.tar.gz && tar -czf /backup/private-files-$stamp.tar.gz -C /files ."
    if ($LASTEXITCODE -ne 0) { throw 'Respaldo de archivos fallido.' }
} finally {
    docker compose -p motorbike-phase20 up -d --wait --wait-timeout 180 api
}
```

Ese procedimiento de volumen/ventana de mantenimiento se documenta para el operador;
en Fase 22 se ejecutó el recorrido de base, sin detener la demo ni restaurar su volumen.
Para recuperar archivos use un volumen nuevo, conserve las mismas claves/rutas y
verifique lectura de soportes autorizados. Antes de conmutar producción, configure
la API con la base y storage restaurados, seed desactivado y configuración externa
correcta. En Compose una conexión alternativa requiere un override de environment
para ConnectionStrings__DefaultConnection; no basta editar una variable que el
archivo Compose no consume. No se entrega un cambio automático de producción.

## Railway y S3

Railway no está desplegado ni sus respaldos verificados. Configure la política de
respaldos de PostgreSQL en el servicio real. Si usa cliente pg_dump, use una versión
compatible con el servidor y variables PGHOST/PGPORT/PGUSER/PGDATABASE/PGPASSWORD
externas; no ponga contraseñas en comandos ni en URLs de documentos. Restaure primero
en una base de revisión. Para S3 use versionado/respaldos del proveedor y verifique
los objetos contra las claves almacenadas. No haga público el bucket.

Los respaldos contienen datos personales y hashes de credenciales. Están excluidos
de Git; cópielos a un destino protegido, cifrado y con retención definida, junto
con su SHA256. Verificar un dump con --list no sustituye probar una restauración.

Fuentes de las opciones: [pg_dump 17](https://www.postgresql.org/docs/17/app-pgdump.html)
y [pg_restore 17](https://www.postgresql.org/docs/17/app-pgrestore.html).
