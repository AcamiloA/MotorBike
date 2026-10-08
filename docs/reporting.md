# Auditoría, dashboards y reportes — Fase 14

## Permisos y rutas

| Ruta GET | Permiso | Resultado |
|---|---|---|
| `/api/v1/audit-logs` | ADMIN | Auditoría paginada |
| `/api/v1/dashboard/admin` | ADMIN | Conteos globales y periodo académico activo |
| `/api/v1/dashboard/guard?parkingLotId={id}` | GUARD | Conteos del parqueadero solicitado |
| `/api/v1/reports/access/daily` | ADMIN | Ingresos y salidas por día local |
| `/api/v1/reports/access/by-vehicle-type` | ADMIN | Ingresos y salidas por tipo de vehículo |
| `/api/v1/reports/access/by-member-type` | ADMIN | Ingresos y salidas por MemberType del usuario del movimiento |
| `/api/v1/reports/vehicles/{id}/history` | ADMIN | Propiedades, registros, movimientos e incidentes |
| `/api/v1/reports/users/{id}/history` | ADMIN | Propiedades históricas/actuales, movimientos e incidentes |
| `/api/v1/reports/guards/{id}/activity` | ADMIN | Ingresos, salidas y reportes de incidentes realizados por el usuario |

ADMIN no implica GUARD: la cuenta necesita GUARD explícitamente para su dashboard. Los handlers verifican cuenta activa y roles actuales en PostgreSQL. No existe asignación obligatoria de un guardia a un parqueadero.

## Fechas y conteos

Los tres reportes de acceso requieren `dateFrom` y `dateTo` (`YYYY-MM-DD`), admiten `parkingLotId` opcional y rechazan rangos invertidos. Actividad de guardia requiere las mismas fechas. Ambos extremos son días inclusivos de America/Bogota; en PostgreSQL se aplica inicio UTC inclusivo y comienzo del día siguiente exclusivo. No se impone un máximo arbitrario de días.

Cada ingreso se cuenta por `CheckInAt` y cada salida por `CheckOutAt`, de forma independiente. Una salida cuenta aunque el ingreso haya ocurrido antes del rango. Los grupos diarios omiten días sin eventos; los grupos por tipo omiten tipos sin eventos. El MemberType corresponde al usuario asociado al movimiento, no al propietario actual del vehículo.

Los dashboards calculan hoy con `IClock` y el día local de Bogotá. Vehículos dentro se deriva de movimientos OPEN, e incidentes abiertos de estado OPEN. El dashboard administrativo devuelve periodo nulo si no existe uno activo. El dashboard de guardia filtra todos sus conteos por parqueadero. La actividad de guardia cuenta incidentes por `CreatedAt`, que refleja cuándo se reportaron, y no por la fecha histórica del suceso.

## Historiales

Cada colección del historial tiene su propia respuesta paginada: `items`, `page`, `pageSize`, `totalCount`, `totalPages`. Los parámetros compartidos `page` y `pageSize` se aplican a todas las colecciones; valores predeterminados 1 y 20, máximo 100. Una página alta devuelve elementos vacíos conservando el conteo. El ID del usuario o vehículo debe existir, aunque esté inactivo.

Las propiedades históricas se conservan tras transferencias. Los incidentes se incluyen por referencia directa al usuario/vehículo o por el movimiento asociado. No se incluyen archivos, claves de almacenamiento, contraseñas, hashes ni tokens. Las consultas proyectan solo los campos del reporte y utilizan una instantánea consistente para conteos y colecciones.

## Auditoría

Filtros: `actorUserId`, `action`, `entityType`, `entityId`, `dateFrom`, `dateTo`, `page`, `pageSize`. Acción y tipo de entidad usan coincidencia exacta; admiten hasta 100 caracteres. Los días son inclusivos de Bogotá. Orden: `CreatedAt` descendente e ID estable. Colecciones vacías responden 200; no existen rutas de creación, edición o eliminación de auditoría.

Se mantienen las acciones de usuario y roles, vehículo y registros, ingresos/salidas, incidentes, noticias, periodos y parqueaderos. Esta fase añade VEHICLE_REGISTRATION_CREATED al registro inicial y VEHICLE_REGISTRATION_CANCELLED cuando la transferencia cancela el registro vigente. Se escriben en la transacción de la operación real; no se crean registros retrospectivos. La renovación mantiene VEHICLE_REGISTRATION_RENEWED.

Los valores auditados se construyen explícitamente en cada operación y excluyen contraseñas, hashes, JWT, credenciales, cadenas de conexión y contenido de documentos. La consulta muestra actor, acción, entidad, valores anteriores/nuevos, IP, traceId y fecha UTC únicamente a ADMIN.
