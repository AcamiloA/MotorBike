# Validación de formato del frente de la Licencia de Tránsito

## Alcance

AWS Textract DetectDocumentText extrae texto temporal desde los bytes JPEG/PNG de VerificationImage, máximo 5MB. Un validador determinístico local busca señales del formato y legibilidad. **Esto no determina autenticidad, falsedad, originalidad ni adulteración.** No hay consulta RUNT, hologramas, reverso, comparación definitiva de placa, scanner/zoomGUARD o endpointOCR público.

Solo registros y reemplazos de CAR/MOTORCYCLE ejecutan OCR cuando DocumentOcr.Enabled=true; el default es false. Bicicleta conserva su fotografía sin OCR, independientemente de la flag. Imágenes existentes no se modifican ni analizan en lote; startup, lectura y renovación no ejecutan OCR. No se introduce migración.

## Arquitectura y orden

Application declara IDocumentTextExtractor, ITransitLicenseFormatValidator e ITransitLicenseValidationService. Domain y Application no conocen SDK AWS; Infrastructure contiene AwsTextractDocumentTextExtractor y AWSSDK.Textract4.0.100.16. Restore compatible con S3 4.x; dependencia compartida AWSSDK.Core4.0.102.9.

~~~text
Escritura autenticada y autorizada
  -> FileUploadValidator
  -> solo carro/moto: ValidationService
       -> Enabled=false: no extracción ni clasificación
       -> Enabled=true: IDocumentTextExtractor -> FormatValidator -> solo VALID
  -> upload + entidades/metadata + commit
  -> reemplazo: cleanup anterior después de commit confirmado
~~~

Una extracción usa una única llamada DetectDocumentTextAsync, que mapea LINE/WORD/confianza/boundingbox. No sube a S3 para OCR ni requiere permiso S3 para Textract. Documentación oficial: [Detecting Document Text](https://docs.aws.amazon.com/textract/latest/dg/detecting-document-text.html).

## Reglas determinísticas

Se normalizan mayúsculas, acentos, espacios, saltos y puntuación. Se tolera distancia de edición1 por token de5o más caracteres; tokens cortos como VIN requieren coincidencia exacta. Se buscan frases consecutivas dentro de una línea o dos adyacentes. Los campos se cuentan una sola vez aunque se repitan.

Pesos centrales en TransitLicenseRules:
- LICENCIA DE TRANSITO:35.
- PLACA:15.
- REPUBLICA DE COLOMBIA y MINISTERIO DE TRANSPORTE:15cada.
- Campos característicos:5cada, máximo30.
- Score máximo100.

VALID requiere título y placa con confianza>=75, al menos una señal institucional>=75, al menos5campos característicos>=75, confianza promedio>=80 y score>=90. No exige todos los campos. La confianza promedio pondera tokens de palabras, o líneas si no hay palabras. Los porcentajes ausentes/inválidos se tratan como0.

UNREADABLE: menos de6tokens útiles o confianza promedio<35.
REVIEW_REQUIRED: texto evaluable con señales claras (título o LICENCIA parcial; o señal institucional y>=3campos), sin cumplir todos los gates de VALID.
INVALID: texto evaluable sin señales suficientes.

Estos umbrales son heurísticos, validados con fixtures sintéticos; no están calibrados con documentos reales ni garantizan formato/autenticidad de toda imagen. Nuevas variaciones institucionales pueden requerir ajustes controlados.

## Estados y mensajes

| Resultado | HTTP | Código | Comportamiento |
|---|---|---|---|
| VALID |201/204 | Sin error | Permite guardar |
| REVIEW_REQUIRED |400 | TRANSIT_LICENSE_REVIEW_REQUIRED | Nueva imagen más clara |
| INVALID |400 | TRANSIT_LICENSE_INVALID_FORMAT | Solicita frente con formato esperado |
| UNREADABLE |400 | TRANSIT_LICENSE_UNREADABLE | Solicita foto legible sin reflejos |
| Error técnico |503 | DOCUMENT_OCR_UNAVAILABLE | Reintentar; no clasifica el documento |

Mensajes exactos:
- “No pudimos validar completamente el formato. Intenta tomar una fotografía más clara.”
- “La imagen no corresponde al formato esperado de una Licencia de Tránsito colombiana.”
- “No pudimos leer el documento. Evita reflejos, acerca la tarjeta y toma nuevamente la fotografía.”
- “No fue posible validar el documento en este momento. Intenta nuevamente.”

Errores documentales no crean vehículo/ownership/registration/imagen ni suben archivos. Un reemplazo rechazado mantiene DB y archivo anteriores. No hay cola de revisión humana: REVIEW_REQUIRED no acepta la imagen.

Mobile mantiene la misma pantalla y los campos. Muestra “Guardando evidencia...” para carro/moto con IsBusy, sin afirmar que el documento ya fue validado; para bicicleta muestra guardado normal. Error documental limpia solo la imagen; OCR_UNAVAILABLE la conserva y permite reintentar porque se rechazó antes de escribir. Otros resultados inciertos conservan controles existentes.

## Configuración y despliegue pendiente del usuario

~~~json
{
  "Textract": {
    "Region": "us-east-1",
    "TimeoutSeconds": 15
  }
}
~~~

Variables: Textract__Region y Textract__TimeoutSeconds (1–60). Options valida región conocida por SDK y rango, y rechaza propiedades desconocidas. Timeout global incluye resolución del cliente y extracción; cancelación del solicitante se propaga. SDK admite un retry interno; no hay retries manuales ni Polly. Mobile tiene su timeout HTTP existente; se recomienda mantener OCRpor debajo de éste (default15s).

El cliente se resuelve únicamente al extraer; no busca credenciales ni llama AWS en startup, lecturas o bicicleta. Usa la [cadena estándar de credenciales AWS SDKv4](https://docs.aws.amazon.com/sdk-for-net/v4/developer-guide/creds-assign.html), independiente de StorageOptions y Storage__AccessKey/SecretKey. Preferir identidad/rol temporal del runtime; si se configuran variables, usar AWS_ACCESS_KEY_ID, AWS_SECRET_ACCESS_KEY y AWS_SESSION_TOKEN según corresponda, fuera del repositorio. No hay credenciales en appsettings.

El permiso mínimo es textract:DetectDocumentText; no requiere AmazonTextractFullAccess ni permisos S3 por la entrada Bytes. Ejemplo a revisar/aplicar por el responsable de AWS, nunca ejecutado por Codex:

~~~json
{
  "Version": "2012-10-17",
  "Statement": [{
    "Effect": "Allow",
    "Action": "textract:DetectDocumentText",
    "Resource": "*"
  }]
}
~~~

Referencia oficial: [permisos para operaciones síncronas y menor privilegio](https://docs.aws.amazon.com/textract/latest/dg/security_iam_id-based-policy-examples.html). La región debe soportar Textract y el runtime tener permisos/credenciales; con Enabled=true, la falta de servicio o configuración de credenciales retorna503durante escritura y no acepta la evidencia. Enabled=false no intenta OCR y no devuelve DOCUMENT_OCR_UNAVAILABLE por falta de Textract.

## Privacidad, pruebas y límites

Texto, palabras, geometría y confianza son temporales; no se guardan en DB, auditoría ni respuesta pública. No se extrae/persiste nombre, identificación, VIN, motor o placa. Vehicle.Plate no se corrige ni se compara con OCR. Los modelos textuales redactan ToString y el adapter no registra payload o excepciones SDK; LogResponsefalse. Logging existente solo tipo de operación, éxito, duración y traceId.

Las suites reemplazan IDocumentTextExtractor por fakes de texto sintético exclusivamente en tests; tests del adapter usan un cliente SDK sobreescrito sin red ni credenciales reales. No se suben documentos de ciudadanos al repositorio. Evidencia en .data/ocr-licencia.

Codex no llamó AWS real, cambió IAM/AWS/bucket/Railway, instaló APK o realizó pruebas manuales. El usuario acumula las pruebas manuales para después. Producción requiere su configuración externa de credenciales/permiso; no se acredita una validación real con Textract.

No hubo commit, push, merge o PR. Las signed URLs, privacidad S3 y VehicleVerificationImage no se modifican.

## Bandera DocumentOcr

`Application/Documents/DocumentOcrOptions.cs` contiene Enabled=false por defecto. Infrastructure enlaza la sección DocumentOcr mediante opciones tipadas y valida su configuración. `TransitLicenseValidationService.ValidateAsync` decide antes de invocar IDocumentTextExtractor; el adaptador AWS no conoce la flag.

~~~json
"DocumentOcr": { "Enabled": false }
~~~

En Railway y cualquier host .NET: `DocumentOcr__Enabled=false` omite deliberadamente extracción y validación documental. CAR/MOTO siguen exigiendo una imagen compatible, JPEG/PNG, máximo 5 MB, con validación de firma/extensión/MIME, almacenamiento privado y VehicleVerificationImage. **OCR OFF no significa “documento validado”: solo se almacenó la evidencia requerida.** No se llama IDocumentTextExtractor, AWS Textract ni el validador de formato.

`DocumentOcr__Enabled=true` mantiene todas las reglas, umbrales, estados y mensajes anteriores. Un fallo técnico conserva DOCUMENT_OCR_UNAVAILABLE/HTTP 503: no existe fallback que acepte una imagen por error de Textract. BICYCLE nunca usa OCR en ninguno de los modos. La configuración se toma al iniciar el host; cambiar la variable requiere reiniciarlo. Una flag inválida falla la configuración, sin asumir false silenciosamente. `.env.example` y Docker Compose incluyen el default apagado; no se modificaron variables reales, credenciales ni IAM.

## Captura horizontal y recorte

La cámara de CAR/MOTO abre una Activity Android dedicada en landscape con CameraX, ya disponible a través de las dependencias existentes. Usa cámara trasera, preview compatible y guía central con proporción aproximada de tarjeta. Preview e ImageCapture comparten ViewPort para representar el mismo campo de visión. Al capturar se fija la orientación durante esa exposición y se toman CropRect y RotationDegrees de ImageProxy.

El procesamiento en memoria decodifica el buffer sin aplicar EXIF por separado, recorta primero el viewport compartido y rota según los metadatos CameraX. DocumentCaptureGeometry transforma el rectángulo visible al bitmap derecho: escala AspectFill/AspectFit, offsets de imagen/letterboxing, coordenadas en unidades iguales y límites de píxeles. Android mide preview y overlay en píxeles nativos, por lo que la densidad no introduce una segunda escala. El crop final conserva únicamente la región de la guía. La salida es JPEG regenerado sin EXIF de orientación, calidad 90, lado máximo 2048 sin ampliar imágenes pequeñas, y mantiene el límite de 5 MB.

Se muestra el resultado recortado, con USAR FOTO y REPETIR. Solo USAR FOTO entrega el archivo al ViewModel. Repetir/cancelar libera preview y cámara; todos los ImageProxy y bitmaps se cierran/disponen. No se escribe la foto original ni se crean archivos temporales de captura: no hay duplicados persistentes que limpiar. La Activity tiene su propia política horizontal; al cerrarla vuelve la Activity MAUI con su política normal. El picker de galería y BICYCLE_PHOTO conservan el flujo existente, sin crop de licencia.

Los bytes de ese crop confirmado son los enviados por el multipart existente, almacenados como evidencia y, si Enabled=true, entregados a OCR antes del upload. No se analiza una foto original distinta de la evidencia final. Los endpoints, privacidad S3, autorización y esquema no cambian.

Las pruebas automatizadas cubren geometría, AspectFill/AspectFit, densidad, rotaciones, bounds, dimensiones, confirmación/repetición/cancelación, selección del picker por tipo y ambos modos OCR. El encuadre real y la orientación en cada dispositivo requieren las pruebas manuales del usuario; Codex no utilizó cámara ni instaló APK.

Referencias: [transformaciones CameraX](https://developer.android.com/media/camera/camerax/transform-output) y [ViewPort compartido](https://developer.android.com/reference/androidx/camera/core/ViewPort).
