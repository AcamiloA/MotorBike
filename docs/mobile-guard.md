# Fase 18 — Portería GUARD en Android

## Implementación

Las once pantallas son Control de acceso, Escanear carné, Buscar por identificación, Resultado de acceso, Registrar ingreso, Registrar salida, Vehículos dentro, Incidentes, Registrar incidente, Detalle de incidente e Historial de parqueo. La navegación exige GUARD; ADMIN por sí solo no agrega esta capacidad. Las funciones administrativas de Fase 19 están descritas en mobile-admin.md.

El parqueadero se selecciona entre los activos, leyendo todas las páginas. Con uno se autoselecciona; con varios se requiere selección. Solo su identificador se conserva en Preferences, separado por usuario. Cada actualización vuelve a comprobar los activos. El dashboard obtiene Dentro, Entradas hoy, Salidas hoy e Incidentes abiertos de la API; no muestra cupos, puestos ni porcentajes de ocupación.

El escáner usa ZXing.Net.Maui.Controls 0.10.4 para MAUI 10, inicializado con UseBarcodeReader según la [documentación oficial](https://github.com/Redth/ZXing.Net.Maui). Solicita permiso CAMERA al entrar. Cámara denegada conserva la búsqueda manual. El carné se trata como cadena opaca, sin abrir URL ni ejecutar su contenido. Una lectura detiene detección y cámara, reclama la solicitud una sola vez y comparte la pantalla de resultado con la búsqueda manual. Salir de la pantalla o suspender la ventana detiene detección, desconecta el handler y descarta navegación de respuestas tardías. Reactivar requiere una acción explícita.

Resultado de acceso muestra usuario, tipo y estado. Un usuario inactivo no puede iniciar un ingreso. Si tiene un movimiento abierto se ofrece salida; en otro caso se muestran vehículos habilitados con selección única. Un vehículo habilitado se preselecciona. Ingreso consulta nuevamente el acceso, comprueba el parqueadero y exige confirmación; salida verifica el movimiento concreto y exige confirmación. Los botones se bloquean durante ejecución y después del éxito. La duración final proviene de la API, y las horas se muestran en Bogotá.

Las escrituras no se reintentan automáticamente. Ante timeout, fallo de red, respuesta inválida, HTTP 408 o error de servidor, se consulta el estado antes de habilitar otra acción. Ingreso verifica mediante lookup; salida consulta el historial hasta encontrar el mismo movimiento. Si la verificación falla permanece bloqueado y ofrece VERIFICAR ESTADO. Si el estado ya refleja el resultado se muestra como verificado. Si no hay ingreso registrado, otra entrada requiere confirmación explícita. Un movimiento anterior ya cerrado no envía una nueva salida.

Dentro permite filtrar por tipo y búsqueda, elegir parqueadero, actualizar y paginar; muestra contadores de la respuesta y accesos a salida e incidente. Historial permite fechas inclusivas locales, identificación, placa, marco, tipo, estado y paginación. Un ingreso abierto muestra salida pendiente.

Incidentes permite listar, filtrar por parqueadero/tipo/estado/fechas, paginar, registrar y consultar detalle. Desde un movimiento se prellenan usuario, vehículo, movimiento y parqueadero. En un registro independiente se puede asociar un usuario por identificación y elegir su vehículo habilitado; un movimiento actual conserva sus referencias. Ocurrencia opcional se interpreta en Bogotá y se envía UTC. Se admiten hasta cuatro adjuntos PDF/JPEG/PNG de 10 MB, con selección, eliminación y validación previa. Los adjuntos se abren mediante descarga autenticada y caché privada compartida con Fase 17. Un registro con resultado incierto se bloquea y pide revisar la lista antes de crear otro. Esta área no ofrece resolver ni cancelar incidentes; esas operaciones corresponden a administración.

## Rutas utilizadas

Todas bajo `/api/v1`: GET `parking-lots?status=ACTIVE`, GET `dashboard/guard`, POST `parking/access/lookup`, POST `parking/check-in`, POST `parking/check-out`, GET `parking/inside`, GET `parking/movements`, GET/POST `incidents`, GET `incidents/{id}` y GET de URL privada de adjunto. Ingreso solo envía UserId, VehicleId y ParkingLotId; salida solo VehicleId. No se envían horas de ingreso/salida, duración ni espacios desde Android.

## Pruebas manuales a cargo del usuario

1. Instalar el APK actual y conectar la API según `installation.md`. Para la demo del emulador use `.data/phase21/com.motobikepark.mobile-demo-emulator-Signed.apk` y 8086. Iniciar sesión como GUARD y como USER/GUARD/ADMIN; verificar navegación. ADMIN sin GUARD no debe ofrecer portería.
2. Probar cero, uno y varios parqueaderos activos. Elegir uno, reiniciar sesión y comprobar que se recuerda únicamente para esa cuenta. Desactivar el elegido desde administración y actualizar; no debe conservar una selección inválida.
3. Revisar contadores reales del dashboard y selección de parqueadero. No debe aparecer mapa, capacidad, puestos ni disponibilidad.
4. Escanear carné QR/código de barras. Mantenerlo frente a la cámara: debe hacer una sola consulta. Probar código desconocido y cadena con forma de URL: no debe abrir navegador. Salir durante consulta y comprobar que no se navega por una respuesta tardía.
5. Denegar cámara y realizar búsqueda manual por identificación. Probar permiso concedido, revocado, app en segundo plano y reactivación explícita; verificar liberación de cámara.
6. Buscar usuario inactivo, usuario sin vehículos habilitados, uno con varios vehículos y uno con vehículo dentro. Revisar estados y acciones ofrecidas.
7. Confirmar/cancelar ingreso, probar doble tap y verificar el vehículo dentro. Probar parqueadero cerrado, vehículo inactivo, registro vencido y usuario ya dentro.
8. Abrir salida desde lookup y desde Dentro. Cancelar, confirmar y comprobar hora y duración devueltas. Una pantalla anterior ya cerrada no debe cerrar otro ingreso posterior.
9. Cortar conectividad durante ingreso/salida. No debe repetirse silenciosamente la escritura. Recuperar conexión y usar VERIFICAR ESTADO; comprobar que el resultado coincide con backend antes de realizar otra acción.
10. Filtrar/paginar Dentro e Historial, incluyendo entradas abiertas, fechas de Bogotá, resultados vacíos y errores. Registrar incidente desde un movimiento y revisar referencias sin reescribirlas.
11. Crear incidente independiente con asociación opcional, fecha/hora opcional y adjuntos. Probar cancelar selección, archivos inválidos, límites y apertura privada en detalle. Revisar resolución existente, filtros y paginación; GUARD no debe resolver/cancelar.
12. Comprobar teclado, desplazamiento, contraste, rotación y tamaños de texto. Logout o 401 debe cerrar la sesión; Atrás no debe recuperar portería ni adjuntos privados.

No se ha ejecutado la interfaz ni los permisos nativos en dispositivo. Estas verificaciones quedan a cargo del usuario, según su autorización.

## Archivos y comprobaciones automáticas

- `Core/GuardServices.cs`: contratos móviles, filtros escapados, rutas de API, multipart de incidentes y mensajes de acceso.
- `ViewModels/GuardViewModels.cs`: selección de parqueadero, permisos, búsqueda/escáner, resultados, ingreso/salida, verificación posterior, listas e incidentes.
- `Pages/Guard/GuardPages.cs`: once pantallas, bindings, navegación y ciclo de vida de la cámara.
- `Services/GuardPlatformServices.cs`: permiso de cámara y selección no sensible por cuenta en Preferences.
- `MauiProgram.cs`, `AppShell.xaml.cs`, `UserPlatformServices.cs` y el proyecto Mobile: DI, rutas, Shell GUARD y dependencia ZXing.
- `GuardFeatureTests.cs`: 29 casos nuevos sobre selección/paginación de parqueaderos, aislamiento por sesión, permisos, lecturas duplicadas y tardías, búsqueda manual, usuario inactivo, confirmación, doble envío, timeout, verificación y filtros/adjuntos.
- `ParkingEndpointTests.Mobile.cs`: tres casos integrales nuevos con login móvil, scan/manual lookup, ingreso, incidente privado, Dentro, salida, historial y rechazo a ADMIN sin GUARD.

Los primeros errores de compilación de las vistas y notificaciones fueron corregidos. Una prueba usaba un identificador de parqueadero diferente en cada respuesta; se corrigió para representar un parqueadero estable. La suite móvil final pasó sus 75 casos. Los resultados previos fallidos se conservan en TRX junto a las ejecuciones posteriores, sin ocultar errores.

## Resultado de validación

Compilación completa de UniversityParking.sln, incluido Android: cero errores y cero advertencias. Regresión integral: 687 pruebas aprobadas, cero fallos y cero omisiones (Domain 112, Application 196, Infrastructure 54, Api.E2E 250, Mobile 75). El TRX integral de API es `.data/phase18/TestResults/phase18_net10.0_20261007181842.trx`; los demás TRX de la misma ejecución están en esa carpeta.

La revisión final ocultó la selección de ingreso para usuarios inactivos o con movimiento abierto; se recompiló el APK y se verificó nuevamente la suite móvil. No cambió el backend. Build y tests se ejecutan en el directorio aislado de artefactos sin detener la API que ya estaba en ejecución. No se modificaron los originales de references ni se crearon commits.

Este resultado corresponde al cierre de Fase 18. Consulte README para el estado
actual y user-manual.md para el manual consolidado.
