# MOTOBIKE PARK

Sistema de control de acceso vehicular universitario. Solución técnica: UniversityParking.

## Alcance y stack

Gestiona usuarios/roles, vehículos y propietarios, registros por periodo académico,
parqueaderos con zonas por tipo, ingreso/salida, incidentes, noticias, auditoría,
dashboards y seis reportes. Android ofrece USER, GUARD y ADMIN. No incluye reservas,
asignación de puestos, capacidad, mapas, barreras físicas ni
operaciones offline.

C#/.NET 10, ASP.NET Core REST, PostgreSQL 17, EF Core/Npgsql, MediatR/CQRS,
FluentValidation, JWT, xUnit/Testcontainers y .NET MAUI 10 Android con MVVM.
Storage privado Local o S3 compatible; HTTPS obligatorio para Mobile Release.

## Arquitectura y guías

Domain contiene reglas y entidades; Application casos de uso/interfaces;
Infrastructure persistencia y servicios; Api expone HTTP; Contracts comparte DTO;
Mobile consume la API sin referencias al backend. Detalle en
[arquitectura](docs/architecture.md).

- [Instalación en máquina nueva, backend, migraciones, Android y tests](docs/installation.md).
- [Manual USER, GUARD y ADMIN](docs/user-manual.md).
- [Catálogo de universidades, UniversityId y revisión de migración](docs/university-catalog.md).
- [Registro público de estudiantes, aprobación y configuración](docs/student-registration.md).
- [Arquitectura para futuras integraciones institucionales; ninguna integración real](docs/university-integrations.md).
- [Evidencia única por vehículo, reemplazo y transición legacy](docs/vehicle-verification-image.md).
- [OCR del frente de Licencia de Tránsito: formato, configuración y límites](docs/transit-license-ocr.md).
- [Docker local y configuración Railway](docs/deployment.md).
- [Seed, cuentas y APK demo](docs/demo.md).
- [Respaldos PostgreSQL y archivos privados](docs/backups.md).

## Configuración externa

| Variable | Uso / valor inicial |
|---|---|
| ConnectionStrings__DefaultConnection | Obligatoria: cadena Npgsql de PostgreSQL |
| Jwt__Key | Obligatoria: secreto de al menos 32 bytes |
| Jwt__Issuer / Jwt__Audience | UniversityParking.Api / UniversityParking.Mobile |
| Jwt__ExpirationMinutes | 480; la configuración valida ocho horas |
| Parking__TimeZone | America/Bogota |
| StudentRegistration__AutoApprove | false: PENDING y aprobación ADMIN; true: ACTIVE inmediato |
| Database__ApplyMigrationsOnStartup | false fuera de Compose; true en Compose |
| Swagger__Enabled | false; habilitar explícitamente para diagnóstico |
| HttpsRedirection__Enabled | true; false para HTTP local o TLS terminado en borde |
| Storage__Provider | Local o S3; inicialmente Local |
| Storage__LocalRootPath | App_Data/private-files; Compose usa volumen privado |
| Storage__Endpoint / Bucket / Region | S3: endpoint público HTTPS, bucket privado y región real |
| Storage__AccessKey / SecretKey | Credenciales externas del proveedor S3 |
| Storage__ForcePathStyle / SignedUrlExpirationMinutes | true / 5; firma temporal válida entre 1 y 15 minutos |
| Seed__Enabled / DemoEnabled | false; activación explícita según demo.md |
| Seed__DemoPassword / AllowDemoInProduction | Contraseña externa y permiso explícito de demo en Production |
| PORT | Si existe, la API escucha ese puerto en 0.0.0.0; Railway lo inyecta |

Las barras de la tabla agrupan propiedades del mismo prefijo; por ejemplo
Storage__Bucket y Seed__DemoEnabled son nombres completos. .env.example es una
plantilla sin secretos. .env solo lo consume Compose; el backend fuera de Docker
requiere variables de proceso u otra configuración externa. ApiBaseUrl es una
propiedad MSBuild del móvil, no una clave JWT ni una conexión PostgreSQL.

## Inicio local reproducible

PowerShell 7 y Docker Linux, desde esta raíz. En máquina nueva:

```powershell
./scripts/initialize-docker.ps1 -ApiPort 8086
./scripts/enable-demo.ps1
docker compose -p motorbike-phase20 up --build -d --wait --wait-timeout 240
Invoke-RestMethod http://localhost:8086/health
./scripts/validate-demo.ps1 -BaseUrl http://localhost:8086/
```

Si .env ya existe, omita initialize-docker y conserve sus secretos/puerto.
Sin demo, omita enable-demo y mantenga sus flags desactivados. PostgreSQL local
queda dentro de Compose; los volúmenes preservan base y archivos tras recreación.
Las migraciones se ejecutan antes de health. Detener sin borrar datos:
`docker compose -p motorbike-phase20 down`. No usar down -v para conservarlos.
Seed repetido no restablece contraseñas, roles ni transferencias editados.

## Ejecución del backend y móvil

Para backend fuera de Docker configure PostgreSQL accesible desde el host y JWT;
[installation.md](docs/installation.md) incluye prompts protegidos, migraciones
manuales opcionales y el comando dotnet run para el perfil HTTP 5197. No use el
hostname postgres de Compose desde Windows ni copie secretos dentro de Mobile.

Build Android para el emulador contra la demo:

```powershell
dotnet build src/UniversityParking.Mobile -p:ApiBaseUrl=http://10.0.2.2:8086/ --artifacts-path .data/mobile-demo
```

La entrega ya preparada es
`.data/phase21/com.motobikepark.mobile-demo-emulator-Signed.apk`, Debug firmado con
bibliotecas incluidas. Instale con el instalador Android o adb install -r y su ruta.
Para dispositivo físico use la IP real del host. Release exige una URL HTTPS real
y firma de distribución del usuario; no se acredita publicación en una tienda.

## Respaldos y límites de verificación

```powershell
./scripts/backup-db.ps1 -OutputFile backups/motorbike-demo.dump
./scripts/restore-db.ps1 -InputFile backups/motorbike-demo.dump -Database motorbike_restore_review
```

Restore utiliza una base nueva y no sobrescribe la demo. El dump no incluye el
contenido de archivos: respalde también private-files/S3 según backups.md.
Los respaldos contienen datos personales/hashes y deben conservarse protegidos.

Railway tiene configuración y pasos preparados; dominio, HTTPS, PostgreSQL y S3
cloud reales siguen pendientes de acción externa. Health no comprueba S3.
Las suites móviles no prueban la UI ni permisos Android. Las verificaciones nativas
y visuales están a cargo del usuario. El seed demo usa archivos sintéticos sin
validez legal y no debe activarse en un servidor con usuarios reales.

## Requisitos de desarrollo

- .NET 10 SDK.
- Carga de trabajo MAUI para Android (`dotnet workload install maui-android`).
- Android SDK y JDK compatibles con .NET MAUI 10.

## Estructura

- `src/`: Domain, Application, Infrastructure, Contracts, Api y Mobile.
- `tests/`: Domain.Tests, Application.Tests, Infrastructure.Tests, Api.E2E.Tests y Mobile.Tests.
- `references/`: antecedentes académicos y mockups; conservar originales.
- `docs/` y `scripts/`: guías de arquitectura, instalación, uso, despliegue, validación y respaldos.

## Estado

Registro de estudiantes: R1–R7 completados técnicamente: build backend/Android con 0 errores/advertencias y 1028 pruebas aprobadas (0 fallidas/omitidas). Configuración, migración y revisión final en [student-registration](docs/student-registration.md). Las pruebas manuales del usuario siguen pendientes; no hubo despliegue ni migración a producción. Los APK históricos no incluyen este feature. Resultados históricos: Fase 23 completada en su verificación automática: compilación backend/Android sin errores ni advertencias, 745 pruebas aprobadas y Docker saludable con la imagen final. Consulta [el informe final y sus límites](docs/validation-phase23.md). Seed/demo local y APK de Fase 21 disponibles; Railway/S3 y pruebas nativas Android siguen pendientes del usuario. LISTO PARA REVISIÓN.

## Hitos anteriores

Fase 14 completada: dominio, persistencia PostgreSQL, autenticación JWT, base de API y gestión de usuarios/roles, vehículos, transferencias, renovaciones, archivos privados, periodos académicos y parqueaderos con zonas estándar, acceso por portería, ingreso, salida, historial, incidentes con adjuntos privados, noticias, consulta de auditoría, dashboards y seis reportes. La implementación Android se describe en las fases 16–19 siguientes.

Fase 15: validación automática integral aprobada (601 pruebas) y formulario multipart de Swagger corregido. Las verificaciones manuales quedan a cargo del usuario, quien autorizó continuar. Consulta [el informe de validación del backend](docs/backend-validation.md).

Fase 16 implementada: base Android MAUI con identidad visual MOTOBIKE PARK, login MVVM, token en SecureStorage, validación inicial de sesión, logout, Shell por roles y cliente HTTP centralizado. Consulta [la base móvil](docs/mobile-foundation.md) y [la instalación y pruebas manuales](docs/installation.md).

Validación de Fase 16: compilación completa sin errores ni advertencias y 626 pruebas aprobadas (22 móviles y 604 de backend/integración). Las comprobaciones visuales y en dispositivo quedan a cargo del usuario.

Fase 17 completada: doce pantallas USER en Android para inicio, vehículos, registro con fotografía y soportes privados, edición, activación/desactivación, renovación, historial personal, noticias y perfil/cambio de contraseña. Consulta [la guía USER y las pruebas manuales](docs/mobile-user.md).

Validación de Fase 17: compilación completa sin errores ni advertencias y 655 pruebas aprobadas: 112 de dominio, 196 de aplicación, 54 de infraestructura, 247 de API integral y 46 móviles. Cero fallos y cero omisiones. APK y TRX en `.data/phase17`. Las pruebas manuales quedan a cargo del usuario.

Fase 18 implementada: once pantallas GUARD con escáner de carné y búsqueda manual, selección de parqueadero, dashboard, ingreso/salida confirmados, verificación de estado tras respuestas inciertas, vehículos dentro, incidentes con adjuntos privados e historial. Consulta [la guía de portería y pruebas manuales](docs/mobile-guard.md).

Regresión completa de Fase 18: 687 pruebas aprobadas, cero fallos y cero omisiones (Domain 112, Application 196, Infrastructure 54, Api.E2E 250 y Mobile 75). Compilación completa sin errores ni advertencias. APK y TRX en `.data/phase18`. Las comprobaciones en dispositivo quedan a cargo del usuario.

Fase 19 completada: administración de usuarios/roles, vehículos y transferencias, periodos, parqueaderos, incidentes, noticias, historial, seis reportes, auditoría y cuenta propia. Consulta [la guía ADMIN y pruebas manuales](docs/mobile-admin.md).

Validación de Fase 19: compilación completa con cero errores y cero advertencias; 729 pruebas aprobadas, cero fallos y cero omisiones (Domain 112, Application 196, Infrastructure 54, Api.E2E 252, Mobile 115). APK Debug instalable con bibliotecas .NET incluidas y TRX en `.data/phase19`. Las pruebas manuales y visuales quedan a cargo del usuario.

## Validación

Fase 22: compilación completa con cero errores/advertencias; CLI EF 10.0.12 y
opciones documentadas comprobados; backup binario y restore en base nueva verificados,
incluido rechazo de sobrescritura y bases existentes. No hubo cambios del backend
ni Mobile; la última regresión de sus suites es la de Fase 21, 745 aprobadas.
Evidencia de documentación/operación en [validation-phase22](docs/validation-phase22.md).

Fase 21: seed opcional con roles, periodo 2026-2, parqueadero y tres zonas;
cinco cuentas demo con contraseña externa, moto/bicicleta de Student, carro de
Teacher, soportes privados, noticia publicada y un historial CLOSED coherente.
Tres ejecuciones reales de arranque sin duplicados y login HTTP de los cinco
actores verificados. Conserva contraseñas, roles, estados y transferencias editados.
Compilación completa con cero errores y cero advertencias; 745 pruebas aprobadas
(Domain 112, Application 196, Infrastructure 54, Api.E2E 268, Mobile 115), cero
fallos y cero omisiones. TRX finales en `.data/phase21/TestResults/final`.
Consulta [cuentas, configuración y pruebas demo](docs/demo.md).
APK Debug para emulador: `.data/phase21/com.motobikepark.mobile-demo-emulator-Signed.apk`,
con BaseUrl `http://10.0.2.2:8086/`. La contraseña inicial está en DEMO_PASSWORD de
`.env`, excluido de Git. Las verificaciones Android nativas siguen a cargo del usuario.

Fase 20: Dockerfile .NET 10, Compose con PostgreSQL 17 y archivos privados persistentes,
migraciones opcionales al arrancar, soporte `PORT`, configuración Railway y scripts
de preparación/validación. Docker local comprobado: ambos servicios healthy,
`GET /health` 200, migración aplicada una vez y persistencia tras recreación.
Compilación completa con cero errores y cero advertencias; 730 pruebas aprobadas
(Domain 112, Application 196, Infrastructure 54, Api.E2E 253, Mobile 115), sin
fallos ni omisiones. Evidencia en `.data/phase20/TestResults`.
Consulta [ejecución Docker y pasos exactos de Railway](docs/deployment.md).
La instancia validada permanece en `http://localhost:8086`, proyecto Compose
`motorbike-phase20`; esa validación se hizo sin demo y conserva la API previa
del usuario en 5197. Railway, S3 y pruebas manuales Android no se declaran ejecutados.

```powershell
dotnet build UniversityParking.sln
dotnet test tests/UniversityParking.Domain.Tests
dotnet test tests/UniversityParking.Application.Tests
dotnet test tests/UniversityParking.Infrastructure.Tests
dotnet test tests/UniversityParking.Api.E2E.Tests
dotnet test tests/UniversityParking.Mobile.Tests
```

Las pruebas de integración usan PostgreSQL real mediante Testcontainers y requieren Docker disponible. La API requiere `ConnectionStrings__DefaultConnection` y `Jwt__Key` mediante configuración externa; no hay credenciales de ejecución incluidas en el repositorio.

El proveedor de archivos se configura con `Storage__Provider` (`Local` o `S3`). Local utiliza `Storage__LocalRootPath` fuera de `wwwroot`; S3 requiere endpoint, bucket, región y claves mediante configuración externa. Su conexión cloud se verificará durante despliegue.



El parqueo opera con `Parking__TimeZone=America/Bogota` y persiste instantes UTC. Los filtros `dateFrom`/`dateTo` del historial y de incidentes son días locales inclusivos (`YYYY-MM-DD`). Consulta [la documentación de incidentes](docs/incidents.md) para permisos, adjuntos y transiciones.

Las noticias publicadas están disponibles para usuarios autenticados; su administración es exclusiva de ADMIN. Consulta [la documentación de noticias](docs/news.md) para rutas, filtros y reglas de estado.

Consulta [auditoría, dashboards y reportes](docs/reporting.md) para permisos, conteos por día de Bogotá e historiales paginados.
