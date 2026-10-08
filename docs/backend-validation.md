# Validación integral del backend — Fase 15

Estado: validación automática aprobada. El usuario asumió todas las verificaciones manuales y autorizó iniciar la Fase 16. El envío multipart manual continúa sin verificarse por el agente; queda a cargo del usuario.

## Resultado automático del 7 de octubre de 2026

Compilación de toda la solución, incluido Android: cero errores y cero advertencias.

| Suite | Aprobadas | Fallidas | Omitidas |
|---|---:|---:|---:|
| Domain.Tests | 112 | 0 | 0 |
| Application.Tests | 196 | 0 | 0 |
| Infrastructure.Tests | 54 | 0 | 0 |
| Api.E2E.Tests | 239 | 0 | 0 |
| Total | 601 | 0 | 0 |

Resultados TRX conservados en `.data/phase15/TestResults`. Se usó PostgreSQL 17 real mediante Testcontainers, migraciones reales, JWT de prueba y LocalFileStorage temporal privado. Los contenedores y archivos de la fixture se limpian al terminar.

La salida se aisló en `.data/phase15` porque una API existente bloqueaba los binarios de las carpetas habituales. Se restauró con acceso a NuGet y se mantuvo esa API en ejecución. No se modificaron datos de su base de datos ni sus credenciales.

## Cobertura mínima requerida

| Flujo | Evidencia automática |
|---|---|
| Login y autorización | AuthApiTests, ApiFoundationTests y pruebas de permisos de cada módulo |
| Crear estudiante y entrar con su cuenta | BackendValidationTests.AdminCreatesStudent_WhoLogsInAndHasOnlyOwnAccess |
| Moto/bicicleta de estudiante; carro de docente | VehicleEndpointTests.RegistrationCreatesVehicleOwnerPeriodEvidenceAndAudit |
| Rechazo de carro de estudiante | VehicleEndpointTests.StudentCarRejectedWithoutDatabaseOrStorageWrites |
| Lookup, ingreso, salida e historial propio | ParkingEndpointTests.RealGuardFlow_LoginLookupEntryInsideExitAndPersonalHistory |
| Ingreso duplicado y segundo vehículo del mismo usuario | ParkingEndpointTests.InvalidEntryNeverCreatesMovementOrAudit y pruebas concurrentes |
| Salida duplicada | ParkingEndpointTests.ConcurrentExitsCloseOnceAndAuditOnlyOnce |
| Salida fuera de horario | ParkingEndpointTests.ExitRemainsAllowedWhenEntryConditionsChange, caso outside-hours |
| Incidentes | IncidentEndpointTests: permisos, referencias, archivos, estados y reversión |
| Noticias | NewsEndpointTests: visibilidad, publicación, archivo y reversión |
| Transferencia y renovación | VehicleEndpointTests.TransferAndSamePeriodRenewalPreserveOwnershipAndRegistrationHistory |
| Autorización por objeto | VehicleEndpointTests.OtherUserCannotReadEditRenewOrDownloadVehicle |
| Autorización de auditoría | ReportingEndpointTests.UserAndGuardCannotReadAdministrativeQueries |
| Health con PostgreSQL real | ApiFoundationTests.Health_ShouldCheckRealPostgres_WithoutExposingConnectionDetails |
| Almacenamiento privado temporal | BackendValidationTests.DefaultFixtureStorageIsTemporaryPrivateAndDoesNotExposeKeys |

## Cambios de esta fase

La fixture general ahora configura una carpeta temporal privada exclusiva, la elimina entre pruebas y al finalizar, y localiza explícitamente el content root de la API para funcionar también desde el host auxiliar. El registro de un estudiante y el aislamiento del almacenamiento tienen pruebas adicionales.

El esquema OpenAPI de vehículos y renovación ahora expone nombres multipart reales, como `Photos[0].File` y `Documents[1].File`, con formato binary. Antes Swagger mostraba los archivos dentro de objetos JSON y no permitía elegirlos. El formulario interactivo muestra una foto y dos documentos; la API conserva su soporte de más bloques con índices consecutivos. Una prueba comprueba estos campos en el esquema.

## Verificación manual Swagger

Se inició un servidor Kestrel de prueba en localhost:5315, con PostgreSQL descartable, cuenta sintética y un periodo activo. Se verificó en la interfaz: login 200, aplicación del token mediante Authorize y GET /api/v1/users/me 200. Tras corregir OpenAPI, se verificaron los controles Choose File para foto y documentos.

El navegador denegó el permiso al cargar la foto sintética. No se ejecutó el envío multipart manual ni se atribuye un resultado 201 a esa comprobación. Los registros multipart exitosos sí están cubiertos por las pruebas E2E de API. El servidor auxiliar se cerró y su base de datos se eliminó. La Fase 15 permanece pendiente de este paso manual exigido por la especificación.

Para repetir la validación automática:

```powershell
./scripts/validate-backend.ps1 -ArtifactsPath .data/phase15
```

Para la comprobación manual, compilar el host auxiliar y ejecutarlo:

```powershell
dotnet build scripts/SwaggerValidation/SwaggerValidation.csproj --artifacts-path .data/phase15
dotnet .data/phase15/bin/SwaggerValidation/debug/SwaggerValidation.dll
```

El host imprime identificación y contraseña de una cuenta exclusivamente de prueba. Abrir http://localhost:5315/swagger/index.html, iniciar sesión, aplicar el token con Authorize y registrar un vehículo mediante los campos indexados y adjuntos PDF/JPEG/PNG. Para moto: GENERAL, VEHICLE_REGISTRATION e INSURANCE. Pulsar Enter en la consola al terminar para cerrar y limpiar el entorno. No guardar credenciales o tokens de prueba en documentación ni usar credenciales de producción.
