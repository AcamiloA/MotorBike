# Fase 23 — Verificación final

## Alcance y cambios

Verificación de la solución existente, sin funcionalidades nuevas. Se retiraron
12 archivos de plantillas Windows/iOS/MacCatalyst: Mobile solo declara Android.
Se corrigieron dos advertencias del seed para registrar únicamente el tipo de
excepción, evitando mensajes de proveedores que pudieran contener datos sensibles.
Los recursos sintéticos del seed son demostrativos, explícitos y opcionales;
no sustituyen datos reales en la operación normal. Los Placeholder de Entry son
ayudas de entrada, y NotSupportedException corresponde a operaciones no admitidas
en streams de solo lectura o conversores de una dirección.

## Arquitectura y seguridad

Se comprobaron referencias: Application → Domain; Infrastructure → Application y
Domain; Api → Application, Infrastructure y Contracts; Mobile → Contracts.
Domain y Contracts no referencian capas del backend ni MAUI.

JWT valida emisor, audiencia, firma HS256, expiración y sujeto GUID; tolerancia
de reloj de un minuto. Contraseñas persistidas mediante PasswordHasher de Identity.
La autorización usa roles explícitos y permisos por objeto: ADMIN sin GUARD no
puede ingresar ni sacar vehículos. Archivos Local fuera del contenido público;
S3 sin ACL pública en el código, con firmas temporales. Esto no certifica la
política de un bucket real, que todavía no se ha configurado ni inspeccionado.

Revisión de logs sin cuerpos de solicitudes, contraseñas o JWT; Mobile sin cadena
PostgreSQL ni credenciales de storage. Release requiere URL HTTPS y no se encontró
bypass de certificados. Android deshabilita backup. .env, .data, dumps y APK están
ignorados por Git. El repositorio no tiene archivos versionados todavía: la revisión
de secretos cubre el árbol fuente actual y las exclusiones, no una publicación ni
un historial remoto. Credenciales de tests son fixtures, no cuentas productivas.

## Base de datos y API

Migración 20261007060306_InitialCreate presente. Índices parciales únicos para un
periodo ACTIVE, una propiedad vigente, un movimiento OPEN por vehículo y por usuario.
Registro único por vehículo/usuario/periodo, permitiendo transferencia en el mismo
periodo. Las pruebas usan PostgreSQL real y comprueban restricciones, carreras de
ingreso/salida, rollback y autorización. No existe ParkingSpot.

Docker usa PostgreSQL privado sin puerto de host, storage persistente privado y API
sin root. Se reconstruyó la imagen después de corregir los logs y se recreó solo
la API de motorbike-phase20; se verificó igualdad entre imagen construida y ejecutada.
GET http://localhost:8086/health responde 200. validate-demo.ps1 verificó las cinco
cuentas, roles, vehículos, noticias, historial y acceso privado permitido/denegado.
Evidencia sin tokens ni contraseñas: .data/phase23/demo-validation.json.
La API previa del usuario y los contenedores ajenos se conservaron.

## Criterios de aceptación de Parte 10

| Escenarios | Comprobación automática / límite |
|---|---|
| 1–3, 10, 24 | UserEndpointTests, VehicleEndpointTests y MobileUserFeatureIntegrationTests: creación, roles, registro multipart y restricciones STUDENT/CAR |
| 4–9, 11–13, 19, 21–22 | ParkingEndpointTests: lookup, ingreso/salida, conflictos, horario Bogotá, roles y concurrencia |
| 14–15, 20, 26, 33–34 | VehicleEndpointTests y Safety: transferencia, renovación, privacidad, desactivación y validación de archivos |
| 16–18 | IncidentEndpointTests, NewsEndpointTests y ReportingEndpointTests: estados, publicación y auditoría |
| 23, 25, 27 | AdministrationEndpointTests, UserEndpointTests y ConstraintTests: periodo único y desactivaciones con movimientos abiertos |
| 28–30 | GuardFeatureTests: lecturas repetidas, permiso denegado y timeout sin repetir POST; cámara nativa pendiente |
| 31–32 | MobileFoundationTests y MobileAuthIntegrationTests: restauración, 401 y fallo de red; SecureStorage real pendiente |
| 35–36 | FileCompensationTests y VehicleEndpointTests.Safety: rollback de archivos y conservación tras commit |
| 37–39 | ApiFoundationTests y suites de colecciones: vacío 200, rate limit 429 y health 200/503 |
| 40 | DemoSeedTests: idempotencia, concurrencia, cuentas, restricciones y errores de storage |

La matriz indica cobertura entre capas, no 40 ejecuciones manuales independientes.
UI USER/GUARD/ADMIN implementada; los tests móviles ejercitan Core/ViewModels y
clientes HTTP, sin ejecutar las vistas nativas.

## Documentación y respaldos

Sintaxis de siete scripts PowerShell válida y 38 enlaces locales comprobados en
16 documentos antes de añadir este reporte. Docker y validación de demo se ejecutaron
en esta fase. Build/tests se ejecutan con artefactos aislados. Comandos de instalación
y EF revisados en Fase 22; no se afirma instalación en máquina nueva.
Backup/restore ya verificados en Fase 22: 17 tablas idénticas, 74 restricciones y
65 índices, restauración en base nueva; evidencias conservadas en .data/phase22.
No se repitió una recuperación ni se sustituyó la base de la demo.

## Pendientes externos y manuales

El usuario asume las pruebas manuales. Seguir listas de installation.md,
mobile-user.md, mobile-guard.md, mobile-admin.md y user-manual.md para instalación,
apariencia, navegación, permisos, cámara/QR, selección/apertura de archivos,
SecureStorage, ciclo de vida, conectividad real y todos los flujos de Parte 7.
No se certifica rendimiento, disponibilidad productiva ni aceptación visual.

APK demo de Fase 21: .data/phase21/com.motobikepark.mobile-demo-emulator-Signed.apk,
con API http://10.0.2.2:8086/. Firma Debug para revisión; distribución Release requiere
URL real HTTPS y firma del operador. No se ejecutó instalación en dispositivo.
Railway, bucket privado S3 real, HTTPS público, recuperación coordinada de archivos
y pruebas de una instalación nueva permanecen pendientes. Docker local y tests con
dobles de S3 no sustituyen esos pasos. No se declara el producto 100% terminado.

La inspección del APK demo confirmó bibliotecas Mobile y Contracts para ARM64 y
x86_64; tamaño 89.125.206 bytes. Evidencia: .data/phase23/apk-validation.json.

## Resultado de build y regresión

Validación ejecutada el 7 de octubre de 2026, hora de Bogotá. Build completo después
de las correcciones: cero errores y cero advertencias, 3 minutos 1,57 segundos,
incluido empaquetado Android. Docker publish Release también correcto.

Regresión posterior a las correcciones: **745 aprobadas, cero fallos y cero omisiones**.

| Suite | Aprobadas |
|---|---:|
| Domain | 112 |
| Application | 196 |
| Infrastructure | 54 |
| Api.E2E | 268 |
| Mobile | 115 |

TRX finales: .data/phase23/TestResults/corrected. La suite API tardó 2 minutos
39 segundos; archivo phase23-final_net10.0_20261007213313.trx. La primera regresión
de esta fase también pasó 745 pruebas y se conserva en TestResults/final; la carpeta
corrected corresponde al código entregado.

Comandos ejecutados:

```powershell
dotnet build UniversityParking.sln --artifacts-path .data/phase20 -p:ApiBaseUrl=http://10.0.2.2:8086/
dotnet test UniversityParking.sln --no-build --artifacts-path .data/phase20 --logger 'trx;LogFilePrefix=phase23-final' --results-directory .data/phase23/TestResults/corrected
./scripts/validate-docker.ps1 -ProjectName motorbike-phase20 -ApiPort 8086
./scripts/validate-demo.ps1 -BaseUrl http://localhost:8086 -OutputPath .data/phase23/demo-validation.json
dotnet tool restore --tool-manifest dotnet-tools.json
dotnet ef --version
```

Se comprobó UID 1654 del proceso API, cinco índices críticos reales y /health 200.
No se encontraron TODO/FIXME/NotImplementedException ni mocks de ejecución en src.
La búsqueda de los tres secretos locales no encontró coincidencias en archivos
publicables de fuente, tests, scripts, configuración o documentación.
Sin commits ni publicación. **LISTO PARA REVISIÓN**: termina la Fase 23 automática;
la aceptación manual y las acciones externas permanecen abiertas según lo descrito.
