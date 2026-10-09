# Fase 19 — Administración en Android

## Alcance

El área ADMIN incluye dashboard, usuarios y roles, vehículos, corrección de identificador, transferencia, periodos académicos, parqueaderos, incidentes, noticias, historial general, seis reportes y auditoría. Las acciones se organizan mediante navegación jerárquica desde Administración. Crear y editar comparten formularios donde corresponde; resolver incidentes tiene un formulario propio. El perfil y cambio de contraseña reutilizan las pantallas compartidas.

El dashboard obtiene usuarios activos, vehículos activos, vehículos dentro, entradas/salidas de hoy, incidentes abiertos y periodo actual del backend. ADMIN sin GUARD no recibe acciones de check-in/check-out. ADMIN+GUARD conserva el área de portería y su acceso explícito. No se agregan cupos, puestos, mapas ni porcentajes de ocupación.

## Reglas y flujos

Usuarios: lista paginada con búsqueda, tipo de miembro, estado y rol; creación administrativa con identificación, nombre, selección de universidad del catálogo, carrera, carné, contraseña inicial y roles adicionales. USER se incluye siempre. STUDENT requiere carrera; TEACHER/STAFF la permiten opcional. Editar no modifica identificación, contraseña ni roles. El detalle muestra perfil, roles y vehículos actuales paginados, permite activar/desactivar ACTIVE/INACTIVE y asignar/retirar GUARD o ADMIN. El filtro incluye Activo, Pendiente, Inactivo y Rechazado. Un estudiante Pendiente ofrece APROBAR/RECHAZAR con confirmación; Rechazado no ofrece reapertura. Consulte [registro y revisión administrativa](student-registration.md). USER no se puede retirar. La contraseña inicial se limpia después del envío y al salir de la página. Cambiar los propios roles exige una nueva autenticación para actualizar claims y menú; desactivar la propia cuenta cierra la sesión.

Vehículos: lista con búsqueda, tipo, estado, identificación del propietario y registro. Detalle incluye propietario, estado, fotografía privada y documentos, edición de marca/modelo/color y consulta de historiales de propiedad, registro, movimientos e incidentes. Corregir identificador exige motivo y confirmación, sin cambiar tipo. Transferir busca el propietario por identificación exacta, vuelve a validarlo antes del envío, requiere usuario activo y distinto del actual y rechaza automóvil para STUDENT. La confirmación explica que se cierra la propiedad anterior y se cancela su registro vigente; el nuevo propietario debe renovar antes de ingresar.

Periodos: lista con filtro de estado/nombre y paginación local, porque la API entrega la colección completa. Crear produce PLANNED y exige inicio anterior al fin. Se permite activar PLANNED o cerrar ACTIVE con confirmación; CLOSED no ofrece reactivación. Parqueaderos: lista paginada por estado, crear/editar nombre, sede y horario; el cierre debe ser posterior a la apertura. El servidor crea las tres zonas estándar, sin campos de capacidad ni zonas solicitadas al operador. Desactivar requiere confirmación.

Incidentes: lista paginada con parqueadero, tipo, estado, fechas, identificación de usuario y placa/marco. Se pueden crear incidentes sin GUARD, con referencias opcionales, fecha/hora de Bogotá y hasta cuatro adjuntos PDF/JPEG/PNG de 10 MB. El detalle muestra datos reales y adjuntos privados. Resolver requiere descripción y confirmación; cancelar admite motivo y confirmación. Un incidente cerrado no puede resolverse nuevamente. Los archivos usan el cliente autenticado, comprobación de sesión y caché privada compartidos.

Noticias: lista paginada con búsqueda y estado DRAFT/PUBLISHED/ARCHIVED. Crear y guardar no publican. Publicar y archivar son acciones explícitas con confirmación. Los cambios se guardan antes de publicar. ARCHIVED se presenta en modo de consulta, conforme a las reglas del backend.

Historial: movimientos generales paginados con parqueadero, identificación, placa, marco, tipo, estado y fechas locales inclusivas. Los ingresos abiertos conservan salida pendiente. Reportes: accesos diarios, accesos por tipo de vehículo, por tipo de miembro, historial de vehículo, historial de usuario y actividad de celador. Se usan listas legibles con los datos reales; los reportes compuestos conservan cada sección y paginan hasta el máximo de páginas de sus secciones. Buscar un usuario o vehículo exige coincidencia exacta de identificación/placa/marco entre las páginas recibidas, sin escoger un resultado parcial.

Auditoría: lista paginada con actor por identificación, acción, tipo/identificador de entidad y fechas. El detalle presenta valores anteriores y nuevos separados por campos, más TraceId. No permite escribir ni borrar auditoría.

## Transporte y empaquetado

Las rutas administrativas existentes se consumen bajo `/api/v1`: `dashboard/admin`, `users`, `users/{id}`, activación/desactivación y roles; `vehicles`, detalle, estado, identificador y transferencia; `academic-periods`, creación, activate/close; `parking-lots`, creación/edición/estado; `incidents`, detalle, resolve/cancel y archivos; `admin/news`, edición, publish/archive; `parking/movements`; seis rutas de `reports` y `audit-logs`.

Las escrituras no se reintentan automáticamente. Ante resultado incierto se bloquea una nueva escritura en el formulario y se indica consultar el estado. Detalles de usuario, vehículo e incidente permiten actualizar antes de otra acción. Loading, errores, reintento, vacío y paginación reutilizan la base móvil. La búsqueda usa un botón explícito, sin emitir solicitudes por cada tecla.

Se habilitó EmbedAssembliesIntoApk para que el APK Debug incluya las bibliotecas .NET y pueda instalarse directamente sin el despliegue rápido del IDE. Esto corrige el empaquetado de entrega detectado en esta fase. Debug permite HTTP solo para desarrollo; Release sigue exigiendo HTTPS. La ejecución Android y la revisión visual permanecen a cargo del usuario.

## Pruebas manuales a cargo del usuario

1. Instalar el APK actual con `adb install -r` o el instalador Android y la API accesible según `installation.md`. Para la demo del emulador use `.data/phase21/com.motobikepark.mobile-demo-emulator-Signed.apk` y 8086; el de Fase 19 apuntaba a 5197. Verificar arranque desde el lanzador sin IDE.
2. Entrar como ADMIN/USER y como ADMIN/GUARD/USER. Revisar áreas, dashboard, Mi cuenta y logout; ADMIN sin GUARD no debe registrar entradas/salidas.
3. Filtrar/paginar usuarios. Crear STUDENT sin carrera y luego con carrera; crear TEACHER/STAFF con carrera opcional. Probar campos vacíos, duplicados, contraseña inválida y roles adicionales.
4. Consultar detalle y vehículos actuales. Editar datos, cancelar y confirmar desactivación. Asignar/retirar GUARD/ADMIN; USER no debe ser retirable. Probar las protecciones del último administrador activo que impone el servidor y la nueva autenticación al cambiar roles propios.
5. Filtrar/paginar vehículos. Abrir foto y documentos privados, editar solo marca/modelo/color y comprobar historial. Corregir identificador con motivo y confirmación; tipo no debe ser editable.
6. Buscar nuevo propietario por identificación, probar coincidencias parciales, inactivo, actual propietario y STUDENT para automóvil. Cancelar y confirmar transferencia. Verificar propiedad anterior cerrada, registro cancelado y renovación requerida al nuevo propietario.
7. Crear periodo PLANNED, activar y cerrar con confirmación. Probar fechas iguales/invertidas, periodos superpuestos y periodo cerrado sin reactivación. El backend decide las restricciones de concurrencia.
8. Crear/editar parqueadero, revisar tres zonas generadas por servidor y horario válido. Cancelar y confirmar desactivación; no debe haber campos de puestos ni capacidad.
9. Filtrar/listar incidentes y crear uno con referencias opcionales y adjuntos. Abrir adjuntos, resolver con texto obligatorio o cancelar con motivo. Verificar que solo se opera sobre incidentes OPEN y se refleja la resolución.
10. Crear noticia y comprobar DRAFT. Editar, guardar, publicar con confirmación, comprobar disponibilidad en USER y archivar. La noticia archivada debe ser de consulta y dejar de aparecer en la lista pública.
11. Consultar historial general con todas las variantes de filtros. Ejecutar los seis reportes, incluyendo historiales con varias páginas/secciones y celador por identificación. Revisar horas de Bogotá, conteos y movimientos abiertos sin salida inventada.
12. Filtrar auditoría y abrir detalle de cambios, valores y TraceId. Probar resultados vacíos, falta de conexión, permisos 403, sesión 401, doble tap y resultado incierto sin repetición automática de escrituras.
13. Revisar teclado, scroll, contraste, rotación y tamaños de texto. Tras logout, Atrás no debe recuperar administración ni abrir archivos privados.

No se afirma haber ejecutado estas pruebas manuales ni validado la interfaz en dispositivo.

## Archivos y cobertura de pruebas

- `Core/AdminServices.cs`: rutas administrativas, filtros escapados, búsqueda exacta de usuarios/vehículos y presentación de auditoría.
- `Core/ApiClient.cs`: PATCH con cuerpo, DELETE y POST sin cuerpo; conserva la política de no reintentar escrituras.
- `ViewModels/AdminLists.cs`: dashboard y listas administrativas con filtros, estados y paginación.
- `ViewModels/AdminForms.cs`: formularios y detalles, confirmaciones, validación, roles, transferencias, periodos, parqueaderos, noticias e incidentes.
- `ViewModels/AdminReports.cs`: seis reportes y registro de incidentes ADMIN sin GUARD.
- `Pages/Admin/AdminPages.cs`: navegación jerárquica, pantallas/formularios reutilizables y rutas de administración.
- `MauiProgram.cs`, `AppShell.xaml.cs` y el proyecto Mobile: DI, área ADMIN funcional y APK con bibliotecas incluidas.
- `AdminFeatureTests.cs`: permisos, filtros, campos permitidos, carrera, contraseña inicial, doble envío, USER obligatorio, roles propios, deactivaciones, coincidencia exacta, transferencia, corrección, periodos, horarios, noticias, incidentes, reportes, auditoría, escrituras sin reintento y selecciones vacías.
- `ParkingEndpointTests.MobileAdmin.cs`: dos escenarios integrales con JWT, PostgreSQL y API reales. Cubren usuarios/roles/estado, vehículos/corrección/transferencia/historial, noticias, periodos, parqueaderos, incidentes/adjuntos/resolución/cancelación, dashboard, seis reportes, historial general, auditoría y límite ADMIN/GUARD.

La compilación inicial mostró advertencias por captura duplicada de sesión en clases derivadas; se corrigieron usando la propiedad de la clase base. Una aserción de transferencia comparaba un prefijo de campo, por lo que también coincidía con el nombre válido; se corrigió para inspeccionar claves JSON exactas. El empaquetado Debug dependía de despliegue rápido y se corrigió para incluir las bibliotecas en el APK. Los TRX previos y posteriores se conservan, incluyendo el fallo de esa aserción.

La suite móvil final incluye 115 casos: 40 nuevos de administración y 75 de fases anteriores. Las pruebas compilan Core y ViewModels compartidos; no ejecutan MAUI en dispositivo. La comprobación del APK final confirmó bibliotecas UniversityParking.Mobile y Contracts para ARM64 y x86_64 dentro del archivo firmado Debug.

## Resultado final

Compilación completa de UniversityParking.sln, incluido Android: cero errores y cero advertencias. Regresión completa: 729 pruebas aprobadas, cero fallos y cero omisiones (Domain 112, Application 196, Infrastructure 54, Api.E2E 252, Mobile 115). Las pruebas nuevas añaden 40 casos móviles y dos escenarios integrales a la base de Fase 18.

Los TRX finales están en `.data/phase19/TestResults`; el resultado integral de API corresponde a `phase19_net10.0_20261007185953.trx`. La compilación utiliza el directorio aislado `.data/phase19` y no detuvo la API existente. La entrega es el APK Debug firmado en `.data/phase19/bin/UniversityParking.Mobile/debug_net10.0-android/com.motobikepark.mobile-Signed.apk`.

No se modificó el backend de producción ni los originales de references, no se hicieron commits y no se inició despliegue. Ejecución en dispositivo, permisos nativos, conectividad real y revisión visual pendientes del usuario según lo acordado.

El resultado de esta guía corresponde al cierre de Fase 19. Consulte README para el estado actual de las fases y docs/user-manual.md para el manual consolidado.


## Catálogo de universidades

Crear y editar usuarios utiliza un Picker que muestra el nombre. La creación
exige selección explícita; la edición selecciona la referencia actual y permite
conservarla si está inactiva, con un aviso. Durante carga no permite guardar.
Ante error se puede reintentar. Actualizar en edición vuelve a cargar los datos
persistidos. Listado, detalle y perfil muestran UniversityName; las solicitudes
envían UniversityId. Detalles y validación actual en [catálogo](university-catalog.md).
Las cifras y APK de la Fase 19 anteriores son evidencias históricas.
