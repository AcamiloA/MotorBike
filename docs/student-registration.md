# Registro público de estudiantes

## Configuración

La API enlaza StudentRegistrationOptions mediante Options y valida la configuración al iniciar. En appsettings.json:

~~~json
{
  "StudentRegistration": {
    "AutoApprove": false
  }
}
~~~

La variable de entorno equivalente es **StudentRegistration__AutoApprove**.

| Valor | Estado inicial | Acceso |
|---|---|---|
| false (predeterminado) | PENDING | Requiere aprobación ADMIN |
| true | ACTIVE | Puede iniciar sesión |

La opción se carga al iniciar el backend; reinicie la API cuando cambie su configuración. Mobile no envía la opción. No modifica usuarios existentes, creación administrativa ni seed. Railway y S3 no fueron modificados.

## Contrato y seguridad

POST /api/v1/auth/register/student es anónimo y recibe exclusivamente:

~~~json
{
  "identificationNumber": "IDENTIFICACION_NUEVA",
  "fullName": "Nombre del estudiante",
  "universityId": "UUID_REAL_DEL_CATALOGO",
  "career": "Carrera",
  "cardCode": "CODIGO_NUEVO",
  "password": "CONTRASENA_QUE_CUMPLA_LA_POLITICA"
}
~~~

El ejemplo contiene marcadores; no debe enviarse literalmente. UniversityId debe ser un GUID real de GET /api/v1/universities y corresponder a una universidad activa. Mobile muestra Name y envía Id, sin texto libre ni universidad predeterminada.

Identificación admite hasta 50 caracteres, nombre y carrera 200, carné 150; todos son obligatorios. Contraseña: al menos 8 caracteres, mayúscula, minúscula y número. Se utiliza IPasswordHasher existente. Identificación y carné mantienen unicidad, incluso en cuentas rechazadas.

El backend fuerza STUDENT y USER. El JSON rechaza campos desconocidos, incluidos MemberType, Roles, Status, AutoApprove y ConfirmPassword. Usuario, credencial, rol y auditoría se guardan dentro de una transacción. Respuesta 201: solo userId y status ACTIVE/PENDING, sin JWT ni secretos.

La política StudentRegistration permite 5 solicitudes por IP remota por minuto, ventana fija y sin cola; al exceder responde 429 RATE_LIMIT_EXCEEDED. No confía en un X-Forwarded-For arbitrario. Detrás de un proxy, la IP remota puede representar al proxy y compartir límite entre clientes: revise el forwarding confiable del despliegue antes del uso real. Este feature no modifica esa infraestructura.

## Estados, login y ADMIN

| Estado | Etiqueta | Login con contraseña correcta |
|---|---|---|
| ACTIVE | Activo | JWT y acceso según roles |
| PENDING | Pendiente | 401 ACCOUNT_PENDING: Tu registro está pendiente de aprobación. |
| INACTIVE | Inactivo | 401 AUTH_USER_INACTIVE: El usuario está inactivo. |
| REJECTED | Rechazado | 401 ACCOUNT_REJECTED: Tu solicitud de registro no fue aprobada. |

La contraseña se verifica antes del estado. Contraseña incorrecta o cuenta inexistente conservan AUTH_INVALID_CREDENTIALS; no revelan aprobación/rechazo. PENDING/REJECTED no reciben JWT ni privilegios operativos. ACTIVE=0 e INACTIVE=1 conservan ordinales; PENDING=2 y REJECTED=3.

ADMIN filtra los cuatro estados en Usuarios. El detalle de STUDENT PENDING ofrece APROBAR/RECHAZAR con confirmación:

- PATCH /api/v1/users/{id}/registration/approve: PENDING → ACTIVE.
- PATCH /api/v1/users/{id}/registration/reject: PENDING → REJECTED.

Requieren ADMIN autenticado y activo, revalidado contra la base, y retornan 204. Usuario inexistente: 404. Estado/tipo incompatible: 409 INVALID_USER_STATUS_TRANSITION. USER y GUARD sin ADMIN no pueden revisar. Revisiones concurrentes se serializan: una transición gana.

Activar/desactivar conserva ACTIVE ↔ INACTIVE; no aprueba PENDING ni reabre REJECTED. No existe reapertura ni motivo de rechazo en este alcance. Auditoría: STUDENT_REGISTERED con actor null; STUDENT_REGISTRATION_APPROVED/REJECTED con ADMIN real, estados y universidad legible. No incluye contraseñas, hashes ni JWT.

## Mobile

CREAR CUENTA DE ESTUDIANTE abre una página pública fuera del Flyout. Complete identificación, nombre, universidad, carrera, carné, contraseña y confirmación. Durante la carga del catálogo se bloquean Picker y envío; ante error puede reintentar o volver.

ConfirmPassword solo se valida localmente. El envío no adjunta bearer, no se reintenta automáticamente y bloquea taps duplicados. Limpia contraseñas después de responder y al salir. Si la escritura tiene resultado incierto, consulte la cuenta en Login antes de repetir el registro; recargar universidades no habilita otro envío.

- ACTIVE: “Tu cuenta fue creada correctamente. Ya puedes iniciar sesión.”
- PENDING: “Tu registro fue recibido y está pendiente de aprobación.”

Después vuelve a Login sin guardar token, crear sesión ni abrir el área autenticada. Las respuestas tardías no reemplazan una sesión o página nueva.

## Migración y compatibilidad

20261008214600_AddStudentRegistrationStatuses amplía exclusivamente ck_users_status a ACTIVE/PENDING/INACTIVE/REJECTED. EF guarda strings; snapshot y Designer reflejan el CHECK. No cambia IDs, campos ni estados de usuarios actuales.

Down rechaza la reversión si existen PENDING/REJECTED; no borra ni remapea cuentas. Se verificó únicamente en PostgreSQL temporal de tests. No se aplicó a producción ni a una base real del usuario. Antes del despliegue, el usuario debe revisar y aplicar la migración según installation.md.

## Validación y pruebas manuales pendientes

Build incluido Android y regresión final: evidencia en .data/registro-estudiantes/R7 y R7-summary.json. Los tests usan bases temporales y no acreditan navegación nativa ni revisión visual. Los APK históricos de Fase 21 no contienen este feature: use un build actual según installation.md.

Escenarios a ejecutar por el usuario en un entorno de prueba:

1. AutoApprove=false: catálogo remoto, selección obligatoria, confirmación, mensaje PENDING, limpieza y retorno a Login; contraseña correcta informa pendiente e incorrecta credenciales inválidas.
2. ADMIN revisa perfil/universidad/auditoría; aprobar permite login. Rechazar informa rechazo y no ofrece reapertura. USER/GUARD sin ADMIN no pueden revisar.
3. AutoApprove=true con cuenta distinta: ACTIVE y retorno sin sesión; comprobar login posterior. Restaurar false al terminar.
4. Duplicados, vacíos, universidad inactiva, fallo de catálogo y límite de solicitudes. Interrumpir un envío no debe reintentarlo automáticamente.
5. Teclado, desplazamiento, tamaños, rotación, Atrás y taps repetidos. Salir durante una respuesta no debe provocar navegación tardía ni conservar contraseñas.
6. Usuarios anteriores, creación ADMIN y seed conservan estado/acceso; vehículos, portería, roles y archivos privados mantienen comportamiento previo.

Codex no ejecutó estas pruebas manuales, instalaciones APK ni operaciones en Railway/S3.
