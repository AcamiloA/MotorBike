# Evidencia única de verificación por vehículo

## Regla del producto

Cada vehículo nuevo requiere **exactamente una** imagen autoritativa:

| Vehículo | Tipo derivado por el servidor | Imagen solicitada |
|---|---|---|
| CAR | TRANSIT_LICENSE_FRONT | Frente completo de la Licencia de Tránsito |
| MOTORCYCLE | TRANSIT_LICENSE_FRONT | Frente completo de la Licencia de Tránsito |
| BICYCLE | BICYCLE_PHOTO | Fotografía de la bicicleta completa |

Carro/moto no solicitan foto del vehículo, reverso ni segunda imagen. Bicicleta no requiere licencia o soporte de propiedad para esta evidencia. El [OCR del frente de Licencia de Tránsito](transit-license-ocr.md) ahora exige formato/legibilidad en escrituras nuevas de carro/moto; bicicleta no usa OCR. No comprueba autenticidad ni compara/corrige placa; scanner/zoom GUARD y QR siguen pendientes.

## Registro y contrato multipart

POST /api/v1/vehicles conserva Type, Plate o FrameNumber, Brand, Model y Color. El único archivo es **VerificationImage.File**. No acepta Photos[], Documents[], VerificationImage.Type, VerificationImageType, OwnerId, StorageKey, campos desconocidos ni archivos múltiples. El tipo de evidencia se deriva de Vehicle.Type en servidor.

Solo JPEG/PNG, máximo 5 MB. Se validan extensión, MIME, tamaño, magic bytes y nombre seguro mediante FileUploadValidator. PDF, HEIC y documentos arbitrarios se rechazan. No se comprime ni reduce la resolución de la copia privada. La validación OCR es heurística de formato/legibilidad y no certifica autenticidad; consulte transit-license-ocr.md.

Vehicle, ownership, registration e imagen se guardan en una transacción. UploadedFileBatch retira subidas nuevas si upload o DB fallan antes del commit. Si el resultado de COMMIT es incierto conserva el archivo que podría ya estar referenciado.

## Lectura y acceso privado

VehicleDetailResponse expone VerificationImage nullable, con Id, Type, OriginalFileName, ContentType, SizeBytes y ContentUrl. Photos/Documents permanecen como histórico. La UI no infiere evidencia desde GENERAL. VehicleResponse utiliza VerificationImagePreviewUrl; la proyección de listados evita una consulta por vehículo.

GET /api/v1/vehicles/{id}/verification-image/content reutiliza la autorización de contenido fotográfico: propietario, ADMIN y GUARD según roles actuales; usuarios ajenos no obtienen acceso. Responde contenido Local o redirección autorizada a S3 firmada temporal. Cache-Control privado/no-store y X-Content-Type-Options nosniff. Se conserva el original privado, no una miniatura destructiva.

La base guarda StorageKey, nunca URLs firmadas. Los contratos públicos no exponen StorageKey y S3 permanece privado. No se modificaron buckets, ACL, Railway o credenciales.

## Legacy y migración

20261009011853_AddVehicleVerificationImages crea vehicle_verification_images con FK Restrict, UNIQUE vehicle_id y CHECK de tipo, tamaño y MIME. Mantiene vehículo, fotos, documentos, claves y archivos anteriores. No convierte GENERAL de carro/moto en licencia ni convierte PDFs o VEHICLE_REGISTRATION históricos. El snapshot y Designer están actualizados.

Durante la transición se admite 0 o 1 imagen por vehículo; nunca más de 1. Nuevos registros HTTP requieren 1. Vehículos legacy y demo históricos pueden tener 0 y muestran “Evidencia de verificación pendiente.” No se borran ni desactivan automáticamente. Las imágenes sintéticas del seed tampoco se reclasifican como licencias auténticas.

Down se detiene si la tabla contiene evidencias nuevas; no las elimina. La migración se valida únicamente mediante PostgreSQL temporal de pruebas. No se aplicó a producción ni a bases reales del usuario. El despliegue requiere revisar la migración antes de usar el código nuevo.

## Agregar o reemplazar

PUT /api/v1/vehicles/{id}/verification-image recibe solo VerificationImage.File. Puede usarlo el propietario actual o ADMIN activo, con permisos revalidados en Application. GUARD sin ADMIN no puede modificarla, incluso si es propietario; ADMIN+GUARD conserva permiso ADMIN. No amplía otros permisos de vehículos.

El caso de uso bloquea las filas según el patrón existente, valida y sube la nueva imagen, actualiza metadata/auditoría, confirma transacción y entonces intenta eliminar el archivo anterior. Nunca borra primero la imagen válida. Un fallo antes del commit compensa la subida nueva y conserva la evidencia anterior. Si la limpieza posterior falla, la nueva evidencia sigue válida y el archivo anterior puede requerir limpieza operativa posterior; no se oculta un fallo DB ni se repite la escritura automáticamente.

El namespace es vehicles/{vehicleId}/verification/{archivo}. VEHICLE_REGISTERED incluye VerificationImageType. VEHICLE_VERIFICATION_IMAGE_UPDATED registra actor y metadata legible, sin bytes ni StorageKey. Los archivos legacy no se eliminan en el reemplazo.

## Renovación y Mobile

La evidencia pertenece al vehículo, no al periodo académico. Renovar reutiliza la imagen existente sin volver a subirla ni exigir documentos antiguos. Legacy sin imagen recibe 409 VEHICLE_VERIFICATION_IMAGE_REQUIRED: “Evidencia de verificación pendiente. Actualiza la imagen antes de renovar.” Los documentos enviados opcionalmente al endpoint de renovación se conservan como histórico, sin ser condición ni evidencia autoritativa.

Mobile muestra un único bloque EVIDENCIA DE VERIFICACIÓN, con título y ayuda según tipo, TOMAR FOTO y SELECCIONAR IMAGEN. Cambiar tipo limpia la selección. Detalles USER/ADMIN muestran la nueva imagen o pendiente y ofrecen ACTUALIZAR EVIDENCIA cuando hay permiso. Renovación no solicita documentos ni otra fotografía. Salir del formulario de actualización limpia la imagen; selecciones/respuestas tardías y cambios de sesión no deben aplicarse a otra pantalla.

API y Mobile se actualizan juntos; APKs históricos con Photos/Documents no usan el contrato nuevo. Las tablas y endpoints de lectura legacy se mantienen para transición. Su eventual retiro queda como deuda futura y requiere revisión separada.

## Validación y revisión del usuario

Baseline, build y regresión final se registran en .data/evidencia-vehiculo y summary.json. Las pruebas cubren metadata/tipos, restricciones, migración sin relabel, registro único, multipart, permisos/contenido, compensación/rollback, reemplazo, renovación y Mobile.

Pruebas manuales pendientes, a cargo del usuario:
1. Carro/moto: comprobar un único frente de licencia; bicicleta: una foto completa. No debe aparecer GENERAL, reverso o documentos obligatorios.
2. Cambiar tipo antes de guardar y comprobar limpieza. Probar JPEG/PNG, PDF rechazado, selección cancelada, cámara denegada, tamaño y taps duplicados.
3. Abrir un legacy y comprobar pendiente, históricos conservados y carga de imagen antes de renovar.
4. Reemplazar como propietario/ADMIN; comprobar imagen nueva y renovación sin nueva subida. Probar usuario ajeno y GUARD sin permiso de modificación.
5. Revisar imagen original, texto legible sin reducción impuesta, teclado, desplazamiento y rotación. No se acredita zoom GUARD en este feature.
6. Interrumpir una solicitud y consultar el detalle antes de repetirla; salir o cambiar sesión durante selección/envío.

Codex no instaló APK, usó cámara/dispositivo físico, subió archivos a Railway/S3 productivo o aplicó migraciones reales. No hubo commit, push, merge ni PR.
