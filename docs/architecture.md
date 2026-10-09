# Arquitectura

## Capas y dependencias

| Proyecto | Responsabilidad | Dependencias de proyectos |
|---|---|---|
| Domain | Entidades, valores, estados y reglas del negocio | Ninguna |
| Application | Casos de uso CQRS, MediatR, validación e interfaces | Domain |
| Infrastructure | EF Core/Npgsql, repositorios, transacciones, reloj, hashing, JWT, storage y seed | Application, Domain |
| Contracts | DTO de solicitudes y respuestas REST | Ninguna |
| Api | Controllers, autorización, ProblemDetails, límites, Swagger y arranque | Application, Infrastructure, Contracts |
| Mobile | MAUI Android, MVVM, Shell por roles y cliente HTTP | Contracts |

Infrastructure implementa las interfaces de Application. Domain no conoce HTTP,
EF, MAUI ni servicios externos. Api es la raíz de composición del backend y
MauiProgram la del móvil. Los cambios se realizan por comandos; las consultas
proyectan modelos de lectura. FluentValidation valida entradas y los handlers
aplican reglas entre agregados. No se usan entidades EF como contratos públicos.

## Modelo del negocio

User distingue MemberType (STUDENT/TEACHER/STAFF) de los roles USER/GUARD/ADMIN.
UserCredential almacena el hash; UserRole vincula permisos. Student no puede tener
un automóvil. Vehicle identifica automotores por placa y bicicletas por marco.
VehicleOwnership mantiene la historia de propietarios; solo uno puede ser vigente.
VehicleRegistration pertenece a propietario y periodo; una transferencia cierra
la propiedad anterior y cancela el registro, y el nuevo propietario debe renovar.
Fotografías y documentos conservan metadatos y claves privadas de almacenamiento.

AcademicPeriod usa PLANNED/ACTIVE/CLOSED y admite un único activo. ParkingLot tiene
horario y tres zonas por tipo de vehículo. ParkingMovement usa OPEN/CLOSED, identifica
usuario/vehículo/parqueadero/zona y celadores de ingreso/salida. Incident conserva
referencias opcionales y adjuntos, con resolución/cancelación. NewsItem usa
DRAFT/PUBLISHED/ARCHIVED. AuditLog registra actor, acción, entidad, cambios y TraceId.

PostgreSQL contiene restricciones e índices únicos para un periodo activo,
propiedad vigente, registro por vehículo/propietario/periodo y movimiento abierto
por vehículo y por usuario. Las transacciones y bloqueos de repositorios protegen
las operaciones concurrentes; las restricciones son una segunda barrera. Los
instantes se guardan en UTC; horarios y días de filtros se interpretan en Bogotá.

## Autenticación y permisos

Login recibe identificación y contraseña. Se verifica el hash de ASP.NET Core
Identity y se emite JWT HS256 de ocho horas, con issuer, audience, sub y roles.
La API valida firma, algoritmo, emisor, destinatario y vencimiento. La clave es
externa y no se incluye en Mobile. Los casos de uso comprueban también actor
activo, permisos y propiedad de objetos; no basta con ocultar un botón.

El móvil guarda el token en SecureStorage, valida `/api/v1/users/me` al restaurar
sesión y presenta áreas según roles. Un 401 limpia la sesión; un 403 la conserva.
Un cambio de roles propios desde ADMIN obliga a autenticarse de nuevo. USER no
es retirable y se protege al último administrador activo. ADMIN puede consultar
acceso, pero solo GUARD registra ingreso/salida. El [registro público de estudiantes](student-registration.md) fuerza STUDENT/USER y crea PENDING por defecto o ACTIVE con AutoApprove; ADMIN aprueba/rechaza solo PENDING. Login verifica contraseña antes del estado y no emite JWT para PENDING/REJECTED.

## Flujo de portería

GUARD selecciona un parqueadero activo y escanea un carné o busca identificación.
El código se trata como texto opaco. Si existe movimiento abierto se ofrece salida;
en otro caso se muestran vehículos elegibles. El servidor valida actores, vehículo,
propiedad, registro del periodo, horario y ausencia de movimientos incompatibles.
Ingreso y salida requieren confirmación; la salida cierra el movimiento existente
y calcula duración. No hay reservas, puestos, cupos, mapas ni barreras físicas.

Las escrituras móviles no se reintentan automáticamente. Ante respuesta incierta
se consulta el estado antes de permitir otra acción. El escáner y los formularios
evitan doble envío; cámara denegada permite búsqueda manual. Listas y reportes
incluyen filtros, estados de carga/error/vacío y paginación.

## Archivos privados

IFileStorage implementa Local fuera de wwwroot o S3 compatible. Las rutas de
contenido verifican autorización por objeto. Un usuario ajeno recibe 404 para
ocultar la existencia del vehículo. S3 devuelve URL HTTPS firmada temporal tras
autorizar; el móvil no envía el Bearer a otro origen. Sus archivos temporales se
limpian al inicio/logout. Health prueba PostgreSQL, pero no acredita acceso S3.

## Ejecución y despliegue

Docker publica la API .NET 10 sin root, PostgreSQL 17 en red interna y volúmenes
separados para base/archivos. Migraciones de arranque son opcionales; un fallo
impide readiness. Seed es opcional e idempotente, y la demo en Production requiere
habilitación explícita y contraseña externa. Conserva cambios de las cuentas demo.
Railway tiene Dockerfile, PORT y healthcheck preparados; su dominio, PostgreSQL,
HTTPS y S3 reales siguen pendientes de acción externa. Consulte
[deployment](deployment.md), [demo](demo.md) y [backups](backups.md).

## Validación y límites

Domain/Application prueban reglas y casos de uso; Infrastructure/Api.E2E usan
PostgreSQL real mediante Testcontainers. Mobile.Tests verifica Core/ViewModels,
no renderiza Android. La Fase 21 aprobó 745 casos; sus TRX se conservan.
Instalación, cámara, permisos, lectores externos de documentos y revisión visual
permanecen a cargo del usuario. No se acredita distribución Android ni cloud.

## Integraciones universitarias futuras

Application dispone de contratos y un orquestador por UniversityId; Options valida referencias y proveedores al inicio. No hay proveedores institucionales reales. El handler de registro conserva su flujo y AutoApprove; la política externa se incorporará posteriormente. Consulte [contratos, diagrama y configuración](university-integrations.md).

## Evidencia autoritativa por vehículo

VehicleVerificationImage deriva su tipo del vehículo y tiene FK Restrict e índice único por VehicleId. Los nuevos registros requieren una imagen; legacy puede carecer de ella sin relabel ni eliminación. Reemplazo transaccional con UploadedFileBatch y retiro anterior posterior al commit. Consulte [contratos, migración y transición](vehicle-verification-image.md).
