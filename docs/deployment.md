# Fase 20 — ejecución y despliegue

La Fase 21 añade [seed y demo opcional](demo.md). El seed está desactivado por
defecto; la instancia local validada en Fase 21 lo habilita expresamente.

## Docker local

Requiere Docker con contenedores Linux y PowerShell 7 para generar la configuración.
Desde la raíz del repositorio:

```powershell
./scripts/initialize-docker.ps1
docker compose config --quiet
docker compose up --build -d --wait --wait-timeout 240
docker compose ps
Invoke-RestMethod http://localhost:8080/health
```

El generador crea `.env` con secretos aleatorios y nunca sobrescribe uno existente.
Para repetir la validación automáticamente: `./scripts/validate-docker.ps1`.
Sus parámetros `-ProjectName` y `-ApiPort` permiten validar una instancia aislada.
Si 8080 está ocupado, usa `-ApiPort 8086` al generar o modifica `API_PORT` en `.env`.
La API usa .NET 10 Linux sin usuario root. PostgreSQL 17 no publica un puerto al host.
PostgreSQL y archivos privados tienen volúmenes separados que sobreviven a recreaciones.
El build solo copia los cinco proyectos del backend; excluye secretos, archivos locales,
APK, tests y referencias. No requiere MAUI para construir el contenedor.

`Database__ApplyMigrationsOnStartup=true` aplica las migraciones EF antes de aceptar
tráfico. Un error impide el arranque; no se usa EnsureCreated ni se ocultan fallos.
Fuera de Compose la opción está desactivada por defecto. No se agregan usuarios demo
ni seed en esta fase. Una base nueva no tendrá cuentas para iniciar sesión hasta
la Fase 21 o su aprovisionamiento administrativo.

`/health` comprueba también PostgreSQL. Compose espera el estado healthy de ambos
servicios. Para detener sin borrar datos: `docker compose down`. No uses `down -v`
si necesitas conservar la base y documentos. Respaldar ambos volúmenes antes de
cambiar credenciales o aplicar una migración productiva.

El HTTP local sirve para Debug Android. Emulador: `http://10.0.2.2:8080/`.
Dispositivo: IP real del equipo y el puerto elegido, con acceso de red permitido.
Release Android exige HTTPS. El APK de Fase 19 conserva su URL 5197: hay que
recompilar para apuntarlo a Compose, por ejemplo:

```powershell
dotnet build src/UniversityParking.Mobile -p:ApiBaseUrl=http://10.0.2.2:8080/ --artifacts-path .data/mobile-docker
```

## Railway: pasos que debe ejecutar el usuario

No se ha desplegado ni se ha validado una cuenta, servicio, dominio o bucket cloud.
Esta fase entrega configuración y pasos; no crea recursos externos de pago.

1. Crear un proyecto Railway y añadir PostgreSQL. Mantenerlo en la misma red privada
   que la API y habilitar respaldos según la política del proyecto.
2. Crear el servicio API con este repositorio en su raíz; Railway debe detectar
   `Dockerfile` y `railway.toml`. Subir/publicar el repositorio requiere una acción
   explícita del usuario; Codex no ha hecho commit ni push.
3. En Variables del servicio API configurar lo siguiente. `Postgres` es el nombre
   del servicio de ejemplo: sustituirlo si el servicio real tiene otro nombre.

```text
ASPNETCORE_ENVIRONMENT=Production
ConnectionStrings__DefaultConnection=Host=${{Postgres.PGHOST}};Port=${{Postgres.PGPORT}};Database=${{Postgres.PGDATABASE}};Username=${{Postgres.PGUSER}};Password=${{Postgres.PGPASSWORD}}
Jwt__Key=<secreto aleatorio de al menos 32 bytes, distinto del local>
Database__ApplyMigrationsOnStartup=true
HttpsRedirection__Enabled=false
Swagger__Enabled=false
Parking__TimeZone=America/Bogota
Storage__Provider=S3
Storage__Endpoint=<endpoint HTTPS público del proveedor S3>
Storage__Bucket=<bucket privado>
Storage__Region=<región del proveedor>
Storage__AccessKey=<credencial de acceso>
Storage__SecretKey=<credencial secreta>
Storage__ForcePathStyle=true
Storage__SignedUrlExpirationMinutes=5
```

Usar la cadena Npgsql mostrada, no pegar una URL `postgresql://` en esa variable.
Las credenciales que incluyan delimitadores de cadena deben escaparse con las
reglas de Npgsql. No guardar ni publicar los valores reales en el repositorio.
La API escucha `0.0.0.0:PORT` cuando Railway inyecta `PORT`; su valor debe ser válido.
El puerto predeterminado del contenedor es 8080.

4. Aprovisionar un bucket privado S3 compatible y credenciales con permisos de
   leer, crear y eliminar objetos solo en ese bucket. Obtener endpoint y región
   del proveedor. No añadir políticas de lectura pública. Las URLs firmadas se
   entregan al móvil después de verificar permisos y deben ser accesibles por
   HTTPS desde el dispositivo, no solo desde una red interna. No usar Local en
   Railway sin un volumen persistente; esta configuración usa S3.
5. Generar un dominio público HTTPS en Networking. Railway termina TLS en su
   borde; se desactiva la redirección en la API para el HTTP interno y el healthcheck.
   No exponer el puerto interno como un servidor HTTP público ni confiar en
   encabezados reenviados de clientes arbitrarios. No hay bypass de certificados.
6. Desplegar. El healthcheck `/health` debe responder 200 antes de activar la
   versión. Revisar logs de migraciones y errores sin divulgar secretos.
   Si PostgreSQL todavía no está disponible, el arranque falla y Railway reintenta
   según la política configurada; corregir variables o disponibilidad si persiste.
7. Verificar `https://DOMINIO-REAL/health`, HTTPS sin certificados inválidos y
   consultas reales a PostgreSQL. Swagger debe estar desactivado (404) salvo que
   se habilite explícitamente para diagnóstico; al habilitarlo verificar
   `/swagger/v1/swagger.json` y deshabilitarlo tras la revisión.
8. Con una cuenta autorizada y datos reales, cargar un documento, descargarlo
   con autorización y comprobar denegación a otro usuario sin permiso, persistencia
   tras redeploy y expiración de la URL firmada. Health no prueba acceso S3.
   Las cuentas/demo se preparan en la Fase 21; estos checks no se declaran ejecutados.
9. Recompilar Android con la URL real, sin secretos dentro de la app:

```powershell
dotnet build src/UniversityParking.Mobile -c Release -p:ApiBaseUrl=https://DOMINIO-REAL/ --artifacts-path .data/mobile-release
```

La firma de distribución Android requiere el almacén de claves del usuario;
este comando no acredita una publicación ni instalación en dispositivo.
Comprobar login y un flujo por rol desde la app contra el servidor real.

## Fuentes oficiales

- [Healthchecks y PORT](https://docs.railway.com/deployments/healthchecks).
- [Docker Compose a servicios Railway](https://docs.railway.com/guides/docker-compose).
- [PostgreSQL y variables](https://docs.railway.com/databases/postgresql).
- [Configuración como código](https://docs.railway.com/config-as-code/reference).

## Resultado de validación

Validación local del 7 de octubre de 2026:

- Compose construido desde Dockerfile; PostgreSQL y API healthy.
- `GET http://localhost:8086/health` devuelve 200 y `{"status":"Healthy"}`.
- PostgreSQL registra `20261007060306_InitialCreate` exactamente una vez.
- API ejecutándose con UID 1654 (`app`), sin root.
- Un contenedor temporal con `PORT=invalid` rechaza la configuración antes de
  iniciar la API; la instancia principal continúa ejecutándose.
- Recreación de ambos contenedores conserva migración y archivo de prueba privado;
  el archivo temporal se eliminó al terminar la comprobación.
- Al detener exclusivamente PostgreSQL de este Compose, health devuelve 503;
  al restablecerlo, ambos servicios regresan a healthy y health devuelve 200.
- `scripts/validate-docker.ps1 -ProjectName motorbike-phase20 -ApiPort 8086`
  ejecutado satisfactoriamente, incluido un segundo build desde caché.
- `.env` está excluido por Git. La instancia de validación usa el proyecto
  Compose `motorbike-phase20`, con la API publicada en 8086, sin interferir con
  la API previa del usuario en 5197.

Railway y S3 reales requieren la acción del usuario indicada arriba; no se
sustituyen por Docker local. La validación de Android sigue a cargo del usuario.

La suite API integral aprobó 253 pruebas, cero fallos y cero omisiones, incluida
la nueva comprobación de migraciones de arranque repetidas. Evidencia TRX:
`.data/phase20/TestResults/phase20_net10.0_20261007200719.trx`.

Compilación de `UniversityParking.sln --artifacts-path .data/phase20`: cero errores
y cero advertencias. Las cinco suites suman 730 pruebas aprobadas: Domain 112,
Application 196, Infrastructure 54, Api.E2E 253 y Mobile 115. Cero fallos y cero
omisiones; sus archivos TRX están en `.data/phase20/TestResults`.
