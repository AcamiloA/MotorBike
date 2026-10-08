# Fase 17 — Funcionalidades USER en Android

## Alcance implementado

Inicio muestra datos reales: vehículos activos, vehículos propios, movimientos recientes y noticias publicadas. Incluye accesos a registro e historial. No muestra cupos, mapas ni asignación de espacios.

Las doce pantallas son Inicio, Mis vehículos, Detalle de vehículo, Registrar vehículo, Editar vehículo, Renovar registro, Mi historial, Noticias, Detalle de noticia, Perfil, Editar perfil y Cambiar contraseña. Las áreas GUARD y ADMIN están implementadas en las fases 18 y 19; sus guías describen las funciones correspondientes.

STUDENT puede registrar motocicleta o bicicleta; TEACHER y STAFF también automóvil. MemberType determina los tipos permitidos y no sustituye los roles. El registro envía placa para automotores o número de marco para bicicletas, fotografía GENERAL y los soportes exigidos. Editar vehículo solo permite marca, modelo y color. Desactivar exige confirmación. Renovar conserva el vehículo y permite reutilizar documentos existentes o aportar reemplazos.

Historial personal tiene paginación y filtros de fecha y vehículo, muestra instantes en Bogotá y conserva las salidas pendientes como pendientes. Noticias consulta únicamente contenido publicado; el detalle usa el elemento recibido en la lista. Perfil permite editar nombre y carrera. Cambio de contraseña valida confirmación y política local, envía solo contraseña actual y nueva, y limpia los campos sensibles.

## API y archivos

Todas las rutas tienen prefijo `/api/v1`: `vehicles/me`, `vehicles/{id}`, `vehicles`, `vehicles/{id}/activate`, `vehicles/{id}/deactivate`, `vehicles/{id}/renew`, `academic-periods/current`, `parking/history/me`, `news`, `users/me` y `auth/change-password`. Los archivos se obtienen mediante las URL privadas recibidas del backend y el cliente autenticado.

Fotografía: cámara o galería, JPEG/PNG, máximo 5 MB y vista previa. Documentos: PDF/JPEG/PNG, máximo 10 MB, número y fechas opcionales. Se comprueban extensión, MIME y firma básica antes del envío. Motocicleta/automóvil requieren matrícula y seguro; bicicleta requiere soporte de propiedad. Cámara solicita permiso al utilizarla y no exige hardware para instalar la app.

Renovación sin reemplazos envía un cuerpo multipart de longitud cero con Content-Type y boundary; esto permite al formulario opcional del backend reutilizar los documentos. Con reemplazos utiliza partes indexadas. Las operaciones de escritura no tienen reintentos automáticos y se impide el doble envío durante carga.

Los documentos se abren desde una caché privada temporal, con comprobación de sesión antes de abrir y limpieza al iniciar o cerrar sesión. Las redirecciones de descarga solo siguen HTTPS y no transmiten el Bearer a otro origen. Listas y formularios presentan carga, error, reintento o estado vacío según corresponda; las listas permiten actualizar.

## Comprobaciones manuales a cargo del usuario

1. Instalar el APK actual indicado en `installation.md` con la API accesible. Para la demo del emulador use `.data/phase21/com.motobikepark.mobile-demo-emulator-Signed.apk` y la API en 8086; la ruta de Fase 17 corresponde a una entrega anterior.
2. Entrar como STUDENT: comprobar que solo aparecen motocicleta y bicicleta. Entrar como TEACHER/STAFF y comprobar automóvil. Revisar las áreas de cuentas con varios roles.
3. Registrar motocicleta y bicicleta con sus identificadores y soportes; comprobar que el detalle y la lista muestran los datos reales y la fotografía. Probar campos vacíos, archivos incompatibles y archivos que superen los límites.
4. Probar galería, cámara, cancelar selección y denegar permiso. Revisar vista previa, teclado, desplazamiento, rotación, contraste y tamaños de texto.
5. Abrir documentos privados. Editar marca/modelo/color; confirmar que propietario, identificador y tipo no se editan. Cancelar y confirmar desactivación; volver a activar.
6. Con un periodo académico nuevo y activo, renovar un vehículo sin nuevos soportes y luego con reemplazos donde corresponda. Verificar que conserva el vehículo y crea su registro para el periodo actual.
7. Consultar historial con fechas, vehículo y varias páginas. Revisar horas de Bogotá, entradas abiertas, resultados vacíos, errores de conexión y actualización.
8. Abrir noticias publicadas y sus detalles; comprobar paginación y texto completo. Las noticias no publicadas no deben aparecer.
9. Consultar perfil; editar solo nombre y carrera. Cambiar contraseña con confirmación incorrecta y luego correcta; cerrar sesión y entrar con la nueva contraseña.
10. Pulsar acciones de guardado repetidamente durante carga. Probar conexión interrumpida, 401 y 403. Tras logout, Atrás no debe recuperar páginas autenticadas ni permitir abrir documentos de la sesión anterior.

Estas verificaciones en dispositivo y la revisión visual quedan pendientes de ejecución por el usuario. La compilación y las pruebas automáticas no equivalen a ejecutarlas.


## Validación automática

Compilación completa de UniversityParking.sln, incluido Android: cero errores y cero advertencias. Regresión completa: 655 pruebas aprobadas, cero fallos y cero omisiones (Domain 112, Application 196, Infrastructure 54, Api.E2E 247, Mobile 46). Los TRX finales están en .data/phase17/TestResults; la ejecución integral final corresponde a phase17_net10.0_20261007174112.trx.

Las pruebas móviles compilan Core y ViewModels compartidos. Las nuevas pruebas integrales ejercitan el cliente móvil con JWT real y PostgreSQL real, incluyendo registro multipart, archivos privados, edición, estado, noticias, historial, perfil, contraseña y renovación sin reemplazos. No ejecutan la interfaz ni permisos nativos Android.

