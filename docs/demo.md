# Fase 21 — seed y datos demo

## Activar en Docker local

Desde la raíz del repositorio, con PowerShell 7:

```powershell
# Solo si no existe .env:
./scripts/initialize-docker.ps1 -ApiPort 8086
# Conserva los secretos existentes y genera/conserva la contraseña demo:
./scripts/enable-demo.ps1
docker compose -p motorbike-phase20 up --build -d --wait --wait-timeout 240
./scripts/validate-demo.ps1 -BaseUrl http://localhost:8086/
```

La instancia iniciada en Fase 20 conserva su nombre Compose y volúmenes para no
crear otra base. Cambiar el nombre de proyecto crea volúmenes independientes.
La API anterior del usuario en 5197 no se modifica. Este seed se ejecuta en 8086.

La contraseña común inicial está en `DEMO_PASSWORD` de `.env`, generado y excluido
de Git. Abre ese archivo local para consultarla; los scripts no la imprimen.
No hay contraseña de ejecución incluida en el código, Dockerfile, APK o documentación.
La contraseña de test existe exclusivamente en las suites automatizadas.

| Actor | Identificación | Carné | Tipo de miembro | Roles iniciales |
|---|---|---|---|---|
| Admin | 900000001 | DEMO-ADMIN | STAFF | USER, ADMIN |
| Guard | 900000002 | DEMO-GUARD | STAFF | USER, GUARD |
| Student | 900000003 | DEMO-STUDENT | STUDENT | USER |
| Teacher | 900000004 | DEMO-TEACHER | TEACHER | USER |
| Staff | 900000005 | DEMO-STAFF | STAFF | USER |

ADMIN puede consultar acceso pero requiere GUARD adicional para registrar ingreso
o salida. El estudiante tiene carrera de Ingeniería de Sistemas.

## Datos y coherencia

- Roles USER/GUARD/ADMIN; periodo 2026-2, del 1 de julio al 31 de diciembre.
- Parqueadero Demo, sede ETITC Demo, 06:00–22:00 de Bogotá; zonas Carros, Motos,
  Bicicletas. No hay cupos, reservas ni zonas editables fuera del alcance.
- Student: moto DEM21M y bicicleta DEMO-BICI-2026. Teacher: carro DEM021.
  No se crea carro para Student. Staff, Admin y Guard comienzan sin vehículos.
- Tres propiedades vigentes. Registros del periodo solo si 2026-2 está activo
  y la fecha de Bogotá está dentro de sus fechas.
- Imagen PNG mínima de ejemplo y soportes PDF con texto DEMO — SIN VALIDEZ LEGAL.
  Son archivos sintéticos privados para probar visualización y permisos, no
  fotografías reales ni documentos de tránsito. Moto/carro tienen matrícula
  y seguro; bicicleta tiene soporte de propiedad. Los PDFs vencen al fin del periodo.
- Noticia publicada de bienvenida, claramente marcada DEMO.
- Un movimiento histórico CLOSED de la moto, 08:00–10:00 del día anterior de
  Bogotá, solo si periodo, horario, actores, propiedad y registro lo permiten.
  No se crea movimiento OPEN ni se atribuye una salida pendiente artificial.

Si existe otro periodo ACTIVE se conserva y 2026-2 queda PLANNED. Un periodo
cerrado no se reactiva. Fuera de las fechas de 2026-2 no se simulan registros
vigentes ni historial incoherente; la activación debe resolverla el administrador.

## Configuración y comportamiento al repetirse

| Variable | Predeterminado | Función |
|---|---|---|
| Seed__Enabled | false | Habilita datos base |
| Seed__DemoEnabled | false | Habilita cuentas/vehículos/noticia/historial demo |
| Seed__DemoPassword | sin valor | Contraseña inicial obligatoria para demo |
| Seed__AllowDemoInProduction | false | Autorización explícita de demo en entorno Production |

En Compose se alimentan desde SEED_ENABLED, DEMO_ENABLED, DEMO_PASSWORD y
ALLOW_DEMO_IN_PRODUCTION de `.env`. Docker local utiliza entorno Production,
por lo que `enable-demo.ps1` habilita explícitamente esa opción para esa instancia.
No habilitar demo en un servidor con usuarios reales. Railway sigue pendiente;
el seed no despliega ni crea servicios cloud.

La contraseña requiere ocho caracteres, mayúscula, minúscula y número. Se almacena
su hash mediante el mismo servicio que la autenticación. El seed se ejecuta después
de las migraciones y antes de aceptar tráfico. Un fallo impide readiness.

Los IDs reservados empiezan por `21de0000-0000-4000-8000-`; roles se reutilizan por
código y periodo por nombre. Una transacción PostgreSQL y un advisory lock
serializan ejecuciones concurrentes. Los archivos se crean por el proveedor
IFileStorage configurado; si falla el seed, se revierte la base y se intenta limpiar
los archivos que acababa de cargar. Un fallo de limpieza se registra y requiere
revisión de storage; no se borran archivos preexistentes.
Si se pierde la confirmación del commit, se conservan esos archivos hasta verificar
el resultado en PostgreSQL, para no borrar soportes que podrían estar referenciados.

Al repetir no se duplican usuarios, hashes, roles asignados, vehículos, propietarios,
registros, soportes, noticia ni historial. No se restablecen contraseñas, roles,
perfiles, identificadores, estados o transferencias modificadas por el usuario.
Tampoco se reparan archivos borrados externamente ni se sustituyen credenciales
existentes. Si otra cuenta ya usa una identificación/carné reservada, la restricción
de unicidad rechaza el seed y revierte la transacción, sin apropiarse de esa cuenta.

Cambiar DEMO_PASSWORD después de crear cuentas no cambia sus contraseñas. Usar
el cambio de contraseña autenticado. Para desactivar futuras ejecuciones poner
SEED_ENABLED=false y DEMO_ENABLED=false y recrear la API; los datos ya creados
permanecen. La desactivación no borra usuarios ni archivos.

## Probar Android

APK Debug preparado para el emulador con `http://10.0.2.2:8086/`:
`.data/phase21/com.motobikepark.mobile-demo-emulator-Signed.apk`.
Incluye las bibliotecas .NET para instalación independiente del IDE. El package
sigue siendo `com.motobikepark.mobile`. Su hash y metadatos están en
`.data/phase21/mobile-demo.json`; no se ha instalado ni probado visualmente.

El APK anterior apunta a 5197. Para utilizar esta demo en el emulador:

```powershell
dotnet build src/UniversityParking.Mobile -p:ApiBaseUrl=http://10.0.2.2:8086/ --artifacts-path .data/mobile-demo
```

En dispositivo físico sustituir 10.0.2.2 por la IP real del equipo. Usar las
identificaciones de la tabla y la contraseña del `.env`. Escanear DEMO-STUDENT
requiere un código de barras/QR cuyo contenido sea exactamente ese texto; también
puede escribirse la identificación en la búsqueda manual de GUARD.

Login limita intentos por IP. `validate-demo.ps1` inicia sesión una vez por cada
actor y reutiliza los tokens. Si se repite inmediatamente o hay otros intentos
simultáneos puede responder 429: respetar Retry-After antes de repetir. No se
desactiva ni aumenta ese límite para la demo.
El script espera una vez el Retry-After de hasta un minuto; si persiste el límite
o la espera es mayor, informa el error para repetir después.

Las pruebas nativas y visuales siguen a cargo del usuario. El script valida por
HTTP login de los cinco actores, perfiles, vehículos, consulta GUARD, historial,
noticias, administración y privacidad de soportes sin registrar entradas reales.
El flujo ingreso/salida se verifica automáticamente en PostgreSQL aislado.
Los soportes de un vehículo ajeno responden 404 para ocultar su existencia;
el propietario recibe 200. Se conserva esa política de privacidad del backend.

## Evidencia

Validación Docker real: cinco cuentas con login correcto, Student con moto y
bicicleta, Teacher con carro, consulta GUARD con dos vehículos elegibles,
historial personal con un CLOSED, noticias y administración accesibles.
Documento privado: propietario 200, usuario ajeno 404. Reporte sin contraseñas
ni tokens en `.data/phase21/demo-validation.json`.

Tres ejecuciones de arranque conservan las mismas cantidades: roles 3, usuarios 5,
credenciales 5, asignaciones de rol 7, periodo 1, parqueadero 1, zonas 3, vehículos 3,
propiedades 3, registros 3, fotos 3, documentos 5, movimientos 1 y noticia 1.
Evidencia: `.data/phase21/seed-repeat.json`. Reconstrucción y arranque de la imagen
actual verificados; API y PostgreSQL healthy, health 200.

Quince pruebas nuevas del seed aprobadas: deshabilitado sin escrituras, base sin
cuentas, tres ejecuciones, login/flujos de todos los actores, ingreso/salida,
archivos privados, conservación de cambios de contraseña/roles/estado/transferencia,
periodo activo existente, fechas fuera de 2026-2, contraseñas inválidas,
autorización Production, colisión de identificación, concurrencia, arranque con
configuración y rollback/limpieza ante fallo de storage.

Compilación completa sin errores ni advertencias. Se reutilizó
`--artifacts-path .data/phase20` para conservar la caché Android; la evidencia
final de esta fase se guarda en `.data/phase21/TestResults/final`. El APK demo
se compiló explícitamente con ApiBaseUrl=8086 y se copió a `.data/phase21`.

Regresión final: 745 pruebas aprobadas, cero fallos y cero omisiones (Domain 112,
Application 196, Infrastructure 54, Api.E2E 268 y Mobile 115). El resultado API
está en `TestResults/final/phase21_net10.0_20261007204941.trx`, bajo `.data/phase21`.
El script demo se ejecutó otra vez contra la imagen actual, incluida una repetición
inmediata que recibió 429, esperó 60 segundos y finalizó correctamente sin cambiar
los límites de seguridad.

Durante la preparación se corrigió el CRC del PNG de ejemplo y se ajustaron las
pruebas al comportamiento existente: ADMIN consulta acceso pero no hace check-in,
login tiene límite por IP y un documento ajeno devuelve 404. Las ejecuciones
intermedias fallidas se conservan en `TestResults`; la carpeta `final` contiene
la regresión aprobada. No quedan fallos pendientes de estas comprobaciones.
