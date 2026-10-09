# Gestión de usuarios, vehículos y UX

La identidad institucional se distingue de los roles de seguridad: STUDENT y TEACHER derivan USER; ADMINISTRATIVE deriva ADMIN; GUARD deriva GUARD. En el registro público siempre se crea STUDENT/USER. Para registros históricos se aplica la prioridad ADMIN, GUARD, USER, sin convertir automáticamente usuarios a TEACHER ni inventar correos o teléfonos.

Las altas exigen correo y teléfono. El correo se normaliza con Trim y ToLowerInvariant y se comprueba mediante un índice único sobre NormalizedEmail. El teléfono se mantiene como texto, conserva el prefijo + y elimina separadores. La creación administrativa captura nombres y apellidos y los conserva en FullName, reutilizando el modelo existente. Admite CC, CE, TI y PASSPORT. Para administrativos y guardas sin universidad seleccionada se utiliza la institución del administrador creador; el modelo existente conserva su relación obligatoria con universidades.

El código de carné se mantiene únicamente por compatibilidad interna. Las altas generan un valor único; no se solicita ni se usa para QR o acceso. Los identificadores de acceso siguen siendo IdentificationNumber.

## Contraseñas y sesiones

Los usuarios creados administrativamente quedan ACTIVE con MustChangePassword=true. Un login con contraseña temporal entrega un challenge opaco de 256 bits, almacenado mediante hash, de cinco minutos y propósito TEMPORARY_CHANGE. No entrega un JWT de sesión. El cliente abre una pantalla pública de cambio, sin establecer una sesión. Al finalizar se invalida el challenge y se vuelve a Login para autenticarse normalmente.

La recuperación entrega por correo un código criptográfico de ocho dígitos, cuyo hash se almacena durante veinte minutos. Cada nueva solicitud invalida los anteriores. Se permiten cinco fallos por challenge y se utiliza el limitador público existente de autenticación. Los challenges solo sirven una vez. El cambio administrativo y la recuperación rotan SecurityStamp, invalidando tokens anteriores incluso si PasswordChangedAt coincide en el mismo segundo. El cambio normal de contraseña también invalida challenges pendientes.

Solicitud pública: `POST /api/v1/auth/password-recovery/request`, con Email. Respuesta neutral cuando el transporte está disponible: “Si existe una cuenta asociada a este correo, recibirás instrucciones para restablecer tu contraseña.” No se devuelve el código ni el ID de recuperación.

Confirmación: `POST /api/v1/auth/password-recovery/complete`, con Email, Code y NewPassword. Cambio temporal: `POST /api/v1/auth/temporary-password/complete`, con ChallengeId, Token y NewPassword. Restablecimiento administrativo: `POST /api/v1/users/{id}/reset-password`, con TemporaryPassword.

## Correo SMTP y Gmail

Application usa IEmailSender; Infrastructure implementa SmtpEmailSender con MailKit. La configuración versionada está deshabilitada y contiene valores vacíos. Al habilitarla se valida al startup. No se conecta a SMTP cuando está deshabilitada.

Variables que el operador configurará posteriormente en Railway:

~~~text
EmailDelivery__Enabled=true
EmailDelivery__Provider=Smtp
Smtp__Host=smtp.gmail.com
Smtp__Port=587
Smtp__UseStartTls=true
Smtp__Username=<GMAIL_TEMPORAL>
Smtp__Password=<GOOGLE_APP_PASSWORD>
Smtp__FromEmail=<GMAIL_TEMPORAL>
Smtp__FromName=MotorBike Park
~~~

Usar una Google App Password, nunca la contraseña normal de Gmail. No agregar valores reales a Git, archivos de configuración versionados ni logs. STARTTLS es obligatorio cuando UseStartTls=true; no se utiliza degradación silenciosa a una conexión sin TLS. UseStartTls=false usa TLS desde la conexión, para un servidor/puerto que lo soporte.

Se comprueba la disponibilidad del transporte antes de buscar una cuenta, también para correos inexistentes, sin enviar mensajes a esos correos. Un servicio deshabilitado o fallo de conexión/autenticación produce EMAIL_DELIVERY_UNAVAILABLE/503 para cualquier dirección válida. Los fallos durante el envío devuelven el mismo error técnico genérico y revierten la transacción: no consumen challenges anteriores ni dejan un código nuevo sin entregar. Los logs registran únicamente un código técnico y la correlación; nunca el destinatario, el cuerpo ni el código. El envío SMTP añade una operación de red para cuentas existentes, por lo que no se garantiza indistinguibilidad temporal perfecta ni frente a fallos específicos de un destinatario.

Referencia de la API utilizada: [MailKit ConnectAsync y STARTTLS](https://mimekit.net/docs/html/M_MailKit_Net_Smtp_SmtpClient_ConnectAsync_2.htm).

## Vehículos e historial

STUDENT puede crear BICYCLE, SCOOTER y MOTORCYCLE. TEACHER también puede crear CAR. El backend comprueba el tipo institucional y el rol USER; ocultar CAR en el selector no sustituye la validación. SCOOTER utiliza serial, foto SCOOTER_PHOTO y una zona compatible; no requiere Licencia de Tránsito ni llama a OCR.

PUT `/api/v1/vehicles/{id}` admite marca, modelo, color e identificador, con autorización de propietario/administrador. El tipo es inmutable. Los campos textuales se normalizan a uppercase en el dominio tanto al crear como al editar. La app aplica un behavior reutilizable al escribir.

DELETE `/api/v1/vehicles/{id}` archiva únicamente un vehículo del propietario. Rechaza movimientos OPEN. Conserva el vehículo, propietarios, registros, movimientos y metadatos históricos; lo excluye de Mis Vehículos y de nuevas entradas. Los índices únicos excluyen DeletedAt no nulo y permiten reutilizar el identificador.

Antes del commit se guarda una tarea de eliminación con la key exacta de la evidencia activa. Después se intenta eliminar mediante IFileStorage.DeleteAsync. Los documentos/fotos legacy históricos se conservan. No se enumeran prefijos ni se elimina un bucket. Las referencias compartidas a una key bloquean su eliminación y quedan pendientes para revisión.

Reintento operativo autenticado como ADMIN: `POST /api/v1/vehicles/file-deletions/retry`, sin cuerpo. Procesa hasta cien tareas pendientes y devuelve su conteo; conserva key, intentos y estado en pending_file_deletions. Revisar FILE_DELETION_FAILED y FILE_DELETION_SHARED_KEY por TaskId, sin exponer keys en la UI. DeleteObject elimina la versión corriente según la configuración del bucket; no purga versiones históricas de un bucket versionado.

## Interfaz y revisión manual

Los filtros se abren desde un embudo junto al título. DraftFilters es independiente de AppliedFilters; Apply recarga y conserva el estado, Clear borra los filtros aplicados y queda deshabilitado si no hay ninguno. Las fechas pueden desactivarse; los reportes que requieren un intervalo conservan su intervalo de consulta predeterminado. El dashboard contiene información y los accesos permanecen en el acordeón.

Los campos de contraseña usan PasswordField con ojo, estado local, binding TwoWay y descripción accesible Mostrar/Ocultar contraseña. La cámara de Licencia de Tránsito oculta su ActionBar y conserva orientación, guía, recorte, preview, repetir, usar y cancelar. No se modifica DocumentOcr__Enabled.

El usuario debe validar físicamente teclado Next/Go, foco y cursor al alternar visibilidad o uppercase, paneles en pantallas pequeñas, temas claro/oscuro y recorte real de cámara. Esta iteración no realiza pruebas físicas ni instala APK. Tampoco configura Railway, aplica migraciones reales ni envía correos de prueba reales.
