# Incidentes — Fase 12

GUARD y ADMIN pueden crear y consultar incidentes. Solo ADMIN puede resolverlos o cancelarlos. USER no tiene acceso operativo, incluso cuando está referenciado en el incidente. Cada caso de uso comprueba también los roles y el estado actual de la cuenta en PostgreSQL.

| Método | Ruta | Resultado |
|---|---|---|
| POST | `/api/v1/incidents` | 201 con `id` |
| GET | `/api/v1/incidents` | Lista paginada |
| GET | `/api/v1/incidents/{id}` | Incidente y metadatos de adjuntos |
| POST | `/api/v1/incidents/{id}/resolve` | 204 |
| POST | `/api/v1/incidents/{id}/cancel` | 204 |
| GET | `/api/v1/incidents/{incidentId}/attachments/{attachmentId}/content` | Archivo privado o redirección autorizada |

## Creación

Enviar `multipart/form-data`: `ParkingLotId`, `Type`, `Description`; opcionales `UserId`, `VehicleId`, `ParkingMovementId`, `OccurredAt` y archivos `Attachments[0]`, `Attachments[1]`, etc. Los índices deben ser únicos y consecutivos desde cero. No se admiten campos como `ReportedBy`, `StorageKey` o `Status`.

Tipos: DAMAGE, ACCIDENT, SECURITY, DOCUMENT, BEHAVIOR, OTHER. Las referencias deben existir. Si se suministra un movimiento, el parqueadero y los IDs opcionales de usuario/vehículo deben coincidir con sus datos históricos. No se exige propiedad actual ni vigencia de registro para reportar un incidente histórico.

`ReportedBy` proviene de la sesión. `OccurredAt` admite un instante con zona horaria y se normaliza a UTC; si se omite se usa `IClock.UtcNow`.

## Adjuntos

PDF, JPEG y PNG, hasta 10 MB por archivo y 50 MB por solicitud. Se valida nombre, extensión, MIME, firma básica y tamaño real de todos los archivos antes de subirlos. Las firmas básicas no constituyen un análisis antivirus.

Las claves privadas siguen `incidents/{incidentId}/attachments/{uuid}.{ext}` y no se exponen en las respuestas. Los enlaces del detalle apuntan al endpoint autenticado, que verifica la pertenencia del adjunto. Local transmite el archivo; S3 emite una URL firmada temporal. Las respuestas indican `private, no-store` y `nosniff`.

Un fallo anterior a COMMIT elimina solo los archivos recién subidos, con compensación best-effort. Si el resultado de COMMIT es incierto, se conservan los archivos para evitar eliminar evidencia posiblemente confirmada. Los adjuntos históricos permanecen al resolver o cancelar.

## Estados y consultas

Resolver requiere JSON `{"resolution":"Texto obligatorio"}` y estado OPEN. Registra administrador, hora UTC y auditoría. Cancelar admite cuerpo vacío o `{"reason":"Motivo opcional"}`; solo cambia OPEN a CANCELLED. Repetir la cancelación no modifica datos ni duplica auditoría. RESOLVED y CANCELLED no se pueden resolver de nuevo. Las escrituras bloquean la cuenta del actor y el incidente dentro de la misma transacción.

Filtros: `status`, `type`, `parkingLotId`, `userId`, `vehicleId`, `dateFrom`, `dateTo`, `page`, `pageSize`. Las fechas filtran `OccurredAt` por días inclusivos de America/Bogota. Página desde 1, tamaño de 1 a 100, orden por ocurrencia descendente e ID estable. Conteo y página se leen en una misma instantánea de PostgreSQL.

La conexión con un bucket S3 real se valida durante la fase de despliegue; esta fase prueba almacenamiento local y la autorización previa al acceso firmado.
