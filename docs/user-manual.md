# Manual de usuario

## Acceso y sesión

Instale el APK con el servidor correcto según [instalación](installation.md).
En la demo de emulador, la API está en 8086 y las cuentas están en [demo](demo.md).
Ingrese identificación y contraseña. ADMIN puede crear cuentas y los estudiantes pueden usar CREAR CUENTA DE ESTUDIANTE en Login. Consulte [registro y aprobación](student-registration.md): por defecto la cuenta queda Pendiente y requiere revisión ADMIN. Cada rol habilita su área. MemberType determina tipos de vehículo,
no permisos administrativos. CERRAR SESIÓN elimina el token y vuelve al login.

Si aparece 401, vuelva a autenticarse. Con 403 mantenga la sesión y revise los
permisos. Ante problemas de conexión use REINTENTAR o actualice una lista. No repita
una escritura cuyo resultado sea incierto: consulte primero el estado guardado.
Los horarios visibles son de Bogotá. Una salida pendiente no indica una salida real.

## USER

Inicio presenta vehículos activos/propios, movimientos recientes y noticias.
En Vehículos puede consultar detalles, evidencia de verificación y documentos históricos. Registrar exige marca, modelo, color, placa para automotores o marco para bicicleta y una única imagen JPEG/PNG de máximo 5 MB. Carro/moto solicitan el frente completo de la Licencia de Tránsito; bicicleta solicita su fotografía completa. STUDENT admite moto/bicicleta; TEACHER/STAFF también carro. No se pide reverso, segunda imagen ni documentos adicionales como condición de creación.

Editar vehículo cambia marca/modelo/color. Puede activar/desactivar con confirmación. ACTUALIZAR EVIDENCIA permite al propietario o ADMIN cargar/reemplazar la imagen. Un vehículo legacy sin ella muestra pendiente y debe agregarla antes de renovar; la renovación reutiliza la imagen del vehículo. Los documentos anteriores se conservan y no se reclasifican. Si hubo transferencia, renueve como nuevo dueño. Consulte [evidencia única y revisión manual](vehicle-verification-image.md).
Historial muestra solo sus movimientos, con filtros de fecha/vehículo y páginas.
Noticias muestra publicaciones y detalle. Perfil permite editar nombre/carrera
y cambiar contraseña indicando actual, nueva y confirmación. La nueva contraseña
requiere ocho caracteres, mayúscula, minúscula y número. Cierre sesión y compruebe
su nueva contraseña. Más escenarios manuales: [guía USER](mobile-user.md).

## GUARD

Entre a Portería y seleccione parqueadero activo; uno solo se selecciona
automáticamente. El inicio muestra vehículos dentro, entradas/salidas del día e
incidentes abiertos. No muestra capacidad ni espacios disponibles.

Abra REGISTRO DE ACCESO: escanee el QR institucional Base64 UTF-8 o use REGISTRAR MANUALMENTE por identificación en la misma pantalla. Si existe un movimiento abierto, solo puede registrar la salida de ese vehículo. Sin movimiento abierto, elija un vehículo habilitado; uno se selecciona automáticamente y varios requieren elección. Revise placa/marco, marca/modelo y evidencia; toque la imagen para ampliar con los dedos y desplazarla. La comparación física es responsabilidad del guarda.

Pulse el único botón REGISTRAR INGRESO o REGISTRAR SALIDA. CONTINUAR vuelve al scanner sin desmontar la cámara. Una entrada exige evidencia visible; una salida histórica sigue disponible aunque falte imagen o el usuario/vehículo esté inactivo. Ante respuesta incierta, use VERIFICAR ESTADO y no repita a ciegas. Salir o suspender la ventana libera la cámara; al volver se inicia un contexto nuevo.

Vehículos dentro ofrece filtros y conteos por tipo. Historial permite fechas,
vehículo, usuario y estados. Incidentes permite lista/detalle y registro con tipo,
descripción, referencias opcionales y hasta cuatro adjuntos privados PDF/JPEG/PNG,
máximo 10 MB cada uno. Resolver/cancelar corresponde a ADMIN. Más escenarios:
[guía GUARD](mobile-guard.md).

## ADMIN

El inicio administrativo usa accesos jerárquicos y muestra dashboard y Mi cuenta.
ADMIN sin GUARD puede consultar acceso/historial pero no registrar entradas/salidas.
Con varios roles verá también las otras áreas.

| Función | Uso real |
|---|---|
| Usuarios | Buscar/filtrar/paginar, crear, consultar, editar, activar/desactivar y asignar/retirar GUARD/ADMIN. STUDENT exige carrera. USER no se retira; el último ADMIN activo está protegido. Cambiar roles propios obliga a nuevo login. |
| Vehículos | Filtrar, consultar foto/documentos privados, editar descripción y estado, corregir identificador con motivo y confirmar transferencia. Tipo es inmutable. Nuevo dueño debe estar activo; no transferir carro a STUDENT. Transferir cierra propiedad y cancela registro anterior. |
| Periodos | Crear PLANNED con inicio anterior al fin, activar o cerrar con confirmación. Solo uno activo; CLOSED no se reactiva. |
| Parqueaderos | Crear/editar nombre, sede y horario, activar/desactivar. Apertura anterior al cierre. Tres zonas por tipo son generadas por servidor; no se administran puestos/capacidad. |
| Incidentes | Filtrar, crear, consultar adjuntos, resolver OPEN con texto obligatorio o cancelar con motivo. Estados terminales no permiten repetir esas acciones. |
| Noticias | Crear DRAFT, editar y guardar; publicar con confirmación y archivar. Guardar no publica. ARCHIVED es de consulta y no aparece para USER. |
| Historial | Consultar movimientos generales, fechas y demás filtros; mantener OPEN con salida pendiente. |
| Reportes | Accesos diarios, por vehículo, por tipo de miembro, historial de vehículo, historial de usuario y actividad de celador. Identificaciones/placas/marcos resuelven el objeto exacto; usar filtros y páginas. |
| Auditoría | Filtrar actor/acción/entidad/fechas, abrir valores anteriores/nuevos y TraceId. No permite editar auditoría. |
| Mi cuenta | Consultar perfil, editar nombre/carrera y cambiar contraseña. |

Las acciones sensibles exigen confirmación. Si la respuesta es incierta, consulte
o actualice el detalle antes de volver a guardar. Consulte los escenarios de
[la guía ADMIN](mobile-admin.md), incluidos privacidad, permisos y doble pulsación.

## Pruebas pendientes del usuario

Recorra cada área con las cuentas demo y anote pasos, cuenta/rol, pantalla, mensaje
y resultado esperado. Verifique teclado, scroll, tamaños de texto, rotación,
cámara/galería, permiso denegado, lector de documentos, red, logout y regreso Atrás.
Estas comprobaciones nativas y visuales no se declaran ejecutadas por las suites.
Railway y S3 reales tampoco se declaran validados. El proyecto no ofrece registro
público, exportación de reportes, reservas, asignación de puestos ni funcionamiento
offline de las operaciones de parqueo.
