# Portería GUARD — Registro de acceso

El inicio de Portería conserva la selección de parqueadero activo y el dashboard. **REGISTRO DE ACCESO** abre `guard-access-control`, una sola pantalla para scanner, búsqueda manual, selección, evidencia, confirmación y resultado. ADMIN puede consultar acceso desde la API; registrar entradas/salidas exige GUARD activo, comprobado también en la base de datos.

## QR institucional y búsqueda manual

El QR transporta **Base64 UTF-8**, no CardCode. `MTAxNDI4NTU1Mw==` representa `1014285553`. Base64 es codificación: no aporta autenticidad ni seguridad. La sesión GUARD, el backend, los estados de la base, la propiedad, el registro y las restricciones de movimientos protegen el acceso. Nunca se toman roles, placa, vehículo, parqueadero u operación del QR.

Application usa `IQrIdentityParser`: hasta 256 caracteres de entrada antes de trim, Base64 válido, UTF-8 estricto y hasta 50 caracteres de identificación normalizada. Admite identificadores alfanuméricos y guion; rechaza texto estructurado, caracteres de control, bytes inválidos y entradas vacías/excesivas con `INVALID_QR_IDENTITY`: “El código QR no contiene una identificación válida.” Conserva ceros iniciales. No consulta base de datos ni verifica permisos. El Value Object existente valida la identificación resultante. CardCode permanece en User para compatibilidad histórica y otros usos.

`POST /api/v1/parking/access/lookup` admite exactamente una fuente:

~~~json
{"qrPayload":"MTAxNDI4NTU1Mw=="}
~~~

~~~json
{"identificationNumber":"1014285553"}
~~~

La acción REGISTRAR MANUALMENTE aparece siempre en la misma pantalla. Abre un formulario integrado y pausa detección; cancelar vuelve al scanner. Denegar cámara conserva esta alternativa. Ambas fuentes terminan en el mismo resolver y las mismas reglas, evidencia y operación. La policy de lookup sigue limitando a 60 consultas por minuto y usuario; comandos, a 30 solicitudes por minuto y usuario.

## Operación y evidencia

El movimiento **OPEN actual del usuario** determina EXIT. Se muestra exclusivamente su vehículo, hora de entrada, parqueadero y duración aproximada; EligibleVehicles queda vacío. No se permite seleccionar otro vehículo ni iniciar otra entrada. Sin OPEN, corresponde ENTRY: cuenta ACTIVE, vehículo ACTIVE, dueño actual, registro vigente del periodo activo, reglas del tipo de miembro, ausencia de OPEN y evidencia compatible. El servidor revalida además parqueadero, zona y horario dentro de la transacción de ingreso.

Un vehículo elegible se selecciona automáticamente; dos o más requieren selección explícita mediante tarjetas con tipo, identificador, marca/modelo e imagen. Cero vehículos muestra un mensaje y permite otra lectura o búsqueda manual. PENDING, REJECTED e INACTIVE no ingresan sin OPEN. No existe límite de entradas por día: entrada/salida/entrada/salida son válidas en la misma fecha.

La evidencia autoritativa es VehicleVerificationImage: TRANSIT_LICENSE_FRONT para carro/moto y BICYCLE_PHOTO para bicicleta. Se muestra placa o marco **registrado**, marca/modelo e imagen completa. El guarda compara físicamente vehículo y documento/foto. El OCR previo validó formato documental; **no verifica automáticamente la placa física ni se ejecuta Textract en portería**.

La proyección del lookup incluye metadatos mínimos y URL del contenido privado, sin consultas por vehículo para obtener metadatos, StorageKey, imágenes Base64 en JSON ni URLs firmadas persistidas. La imagen original se descarga por el endpoint privado existente. El visor integrado cubre la pantalla y admite pinch hasta 8x, desplazamiento acotado y cerrar; no desmonta la cámara ni reactiva detección al cerrarse.

Los legacy sin evidencia se excluyen de ENTRY y devuelven `VEHICLE_VERIFICATION_REQUIRED`: “El vehículo no tiene evidencia de verificación registrada.” La ausencia de imagen, contenido ilegible o fallo de descarga impide confirmar ENTRY. Se permite reintentar la carga. Si hay OPEN histórico, EXIT sigue disponible aunque falte evidencia, falle su descarga, venza el registro o se desactive el usuario/vehículo. No se usa una foto GENERAL como sustituto.

## Estado y cámara

| Estado | Comportamiento |
|---|---|
| INITIALIZING | Comprueba sesión, lotes y permiso de cámara. |
| SCANNING | Cámara montada y detección activa si hay permiso. |
| PROCESSING_IDENTITY | Una sola consulta; detección pausada. |
| MANUAL_LOOKUP | Formulario integrado; detección pausada. |
| SELECTING_VEHICLE | Elección explícita entre varias tarjetas. |
| CONFIRMING_ENTRY | Evidencia cargada y un único botón REGISTRAR INGRESO. |
| CONFIRMING_EXIT | Movimiento exacto y un único botón REGISTRAR SALIDA. |
| SUBMITTING | Botón bloqueado; no doble envío ni detección. |
| SUCCESS_ENTRY / SUCCESS_EXIT | Resultado y CONTINUAR. |
| ERROR | Mensaje controlado; volver a escanear o verificar resultado incierto. |

CameraBarcodeReaderView se crea una vez por permanencia en pantalla. Procesar QR, elegir vehículo, confirmar o abrir zoom solo cambia IsDetecting; el control continúa montado. Eventos repetidos se reclaman una vez. CONTINUAR/cancelar limpia identidad, vehículo, movimiento, imagen y errores y regresa a SCANNING. Cambiar parqueadero invalida el contexto y descarta respuestas tardías.

La cámara se desconecta al abandonar la página o detenerse la ventana. Al reanudarse se inicializa un contexto nuevo, sin procesar códigos antiguos. Cambiar sesión/token/perfil descarta el contexto y la imagen en memoria. Antes de un POST operativo se comprueba la sesión inicial; el handler HTTP verifica también el token esperado, como metadato local que nunca se transmite, para no enviar una operación con otro guarda.

## Salida exacta, incertidumbre y concurrencia

~~~json
{"movementId":"UUID-del-movimiento-confirmado","vehicleId":"UUID-del-vehiculo"}
~~~

`POST /api/v1/parking/check-out` exige ambos UUID. Observa MovementId sin tracking, bloquea usuarios y vehículo en el orden compartido con los otros comandos y vuelve a leer **ese mismo MovementId con FOR UPDATE**. Debe estar OPEN y pertenecer a VehicleId. Cierra exactamente ese movimiento. Si Movement 1 se cerró y el vehículo creó Movement 2, una petición antigua de Movement 1 devuelve conflicto y deja Movement 2 abierto. La petición antigua que solo incluye VehicleId ya no es operativa.

El botón de ingreso/salida es la confirmación; no abre otro diálogo genérico. Mobile no reintenta POST automáticamente. Para ENTRY genera MovementId antes de enviar y la API lo usa como identidad del movimiento; otros clientes pueden omitirlo y dejar que el servidor genere uno. No es una autorización ni cambia esquema. Reutilizar un UUID produce conflicto y no modifica el movimiento anterior.

Ante timeout, red, 408, 5xx o respuesta inválida, Mobile consulta `GET /api/v1/parking/movements/{movementId}`. ENTRY solo reconoce su UUID, usuario, vehículo, parqueadero y guarda; EXIT reconoce ese UUID CLOSED y su vehículo. Si no puede verificarlo, bloquea otra confirmación y ofrece VERIFICAR ESTADO: “No fue posible confirmar el estado. Verifica antes de intentar nuevamente.” No considera un OPEN de otro intento como éxito. El resultado final y duración provienen del servidor.

Los índices parciales únicos existentes `ux_parking_movements_open_user` y `ux_parking_movements_open_vehicle`, con `status = 'OPEN'`, garantizan un máximo de un OPEN por usuario y vehículo. Se conservan las comprobaciones de Application y la traducción de conflictos PostgreSQL. **No se crea migración ni índices duplicados**: no cambia persistencia.

Vehículos dentro abre la misma pantalla con su MovementId, verifica el movimiento y obtiene metadatos por `GET /api/v1/parking/movements/{movementId}/vehicle`. Ambos GET exigen GUARD/ADMIN activo. Historial, dashboard, reportes e incidentes conservan sus endpoints. Se retiraron las páginas, ViewModels y rutas anteriores de scan/manual/result/check-in/check-out.

## Validación automatizada y pruebas manuales pendientes

La suite cubre parser, QR/manual, CardCode ajeno coincidente, evidencia compatible y legacy, ciclos diarios, salida exacta/antigua, reglas frescas, permisos, límites, auditoría, restricciones concurrentes PostgreSQL, estados Mobile, duplicados, sesión/lote/lifecycle, cargas de imagen, incertidumbre y matemáticas del zoom. Se ejecuta con bases temporales de tests y OCR sintético, sin AWS ni movimientos reales. El build incluye Android.

Codex no usa cámara física, instala APK, escanea carné real ni ejecuta movimientos en producción. El usuario comprobará en dispositivo: continuidad real de cámara, permiso denegado, QR institucional, manual/cancelar, dos vehículos, comparación visual y pinch/pan de documentos y bicicletas; éxito/CONTINUAR, background y cambio de sesión/lote; evidencia fallida en ENTRY/EXIT; doble tap, pérdida de red y el movimiento antiguo. Ver también [evidencia de vehículos](vehicle-verification-image.md) y [límites del OCR documental](transit-license-ocr.md).
