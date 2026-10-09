# Preparación de integraciones universitarias

**Integraciones institucionales reales: NINGUNA. Arquitectura preparada: SÍ.**

No existen proveedores institucionales de producción, URLs, credenciales, scraping ni acceso a bases institucionales. Los proveedores de pruebas son exclusivos de tests.

## Contratos y capas

Application/Common/Abstractions contiene IUniversityStudentValidator e IUniversityStudentValidationService. UniversityStudentValidationRequest contiene UniversityId, UniversityCode e IdentificationNumber; Code proviene del catálogo existente, no del cliente. No incluye carné, contraseña, hash, JWT, roles o vehículos. IdentificationNumber es sensible; ToString omite su valor. No se registran payloads externos.

El proveedor declara UniversityId y recibe CancellationToken. UniversityStudentValidationResult incluye solo un estado técnico: VALID, NOT_FOUND, UNAVAILABLE, NOT_CONFIGURED o ERROR. NOT_FOUND nunca representa una caída o falta de integración. No contiene datos personales ni mensajes técnicos. Timeout o cancelación interna del proveedor se representa como UNAVAILABLE; una excepción inesperada o resultado inválido se transforma en ERROR sin exponer detalles. La cancelación del solicitante se propaga.

UniversityStudentValidationService selecciona un único proveedor por GUID. University.Name no participa en la resolución. Sin entrada habilitada devuelve NOT_CONFIGURED sin consultar proveedor. Con proveedor habilitado obtiene Code del IUniversityRepository existente y conserva su resultado semántico.

## Diagrama textual

~~~text
RegisterStudentCommandHandler [flujo actual sin cambios]
    |
    +-- futura política de admisión [todavía no conectada]
            |
            v
    IUniversityStudentValidationService
            |
            +-- configuración por UniversityId
            +-- IUniversityRepository: catálogo existente
            +-- IUniversityStudentValidator único
                    +-- proveedor ETITC [futuro]
                    +-- proveedor CMC [futuro]
                    +-- proveedor UPN [futuro]

Sin integración habilitada: NOT_CONFIGURED; registro actual normal.
~~~

El servicio está registrado en DI para su incorporación futura al handler. **El handler no lo invoca todavía**: este feature no decide si NOT_FOUND/UNAVAILABLE/ERROR deben rechazar, dejar pendiente o permitir un registro. Esa decisión requiere la política futura; no pertenece al proveedor. AutoApprove, endpoint público, contratos, estados, revisión ADMIN y Mobile conservan su comportamiento.

## Configuración tipada y validación

UniversityIntegrationsOptions se enlaza mediante Options y ValidateOnStart. Default sin integraciones:

~~~json
{
  "UniversityIntegrations": {
    "Universities": []
  }
}
~~~

Una entrada opcional contiene UniversityId del catálogo y Enabled=false. Enabled omitido también vale false. No hay propiedades de URL, token ni secreto. Para configurar una entrada mediante entorno:

~~~text
UniversityIntegrations__Universities__0__UniversityId = GUID_REAL_DEL_CATALOGO
UniversityIntegrations__Universities__0__Enabled = false
~~~

El texto GUID_REAL_DEL_CATALOGO es un marcador, no un valor válido. Use el Id existente, por ejemplo obtenido del catálogo. Nuevas entradas usan índices 1, 2, etc. Reinicie el proceso para cargar cambios.

El binding estricto rechaza propiedades desconocidas, GUID inválidos y booleanos mal formados, en vez de omitir entradas inválidas. La validación estructural rechaza identidades vacías, entradas duplicadas, proveedores sin identidad o duplicados y Enabled=true sin proveedor. La API valida además todas las referencias configuradas, incluso deshabilitadas, con el repositorio antes de aceptar tráfico, después de los pasos existentes de migraciones/seed. Un GUID desconocido falla como error de configuración. Sin entradas no añade consultas de catálogo al arranque. No se selecciona un proveedor arbitrariamente.

Para habilitar un proveedor futuro hay que implementarlo y registrarlo primero. Habilitar hoy una entrada sin proveedor impide el arranque; no equivale a VALID. Railway y S3 no fueron modificados.

## Implementación institucional futura

1. Obtener contrato oficial, autorización y política de admisión acordada. No inventar endpoints, credenciales ni respuestas.
2. Implementar IUniversityStudentValidator en Infrastructure con UniversityId estable del catálogo, minimización de datos, resultados técnicos y cancelación.
3. Registrar el proveedor como singleton seguro para concurrencia. El validador de Options es singleton; los adaptadores no deben depender de servicios scoped. El orquestador es scoped y utiliza el repositorio existente.
4. Si requiere HTTP, usar HttpClientFactory y timeout finito; añadir resiliencia solo cuando tenga una necesidad concreta. No crear HttpClient por request.
5. Credenciales externas por variables o secret manager; nunca en Domain University, repositorio, Mobile ni contrato público. No loguear IdentificationNumber, contraseñas, tokens o respuestas personales.
6. Añadir tests de contrato, disponibilidad, timeout, cancelación y seguridad. Decidir e implementar por separado la política de Application y su llamada desde RegisterStudentCommandHandler.
7. Registrar configuración y validarla. No modificar DTO, Mobile o catálogo para incorporar un proveedor.

No se añadieron migraciones, columnas, dependencias NuGet, APIs, HttpClient, auditorías de validación ficticia ni proveedores falsos de producción. ETITC/CMC/UPN permanecen sin integración real.

## Validación y límites

Baseline y regresión completa usan artefactos aislados .data/phase20 y PostgreSQL temporal de Testcontainers. Evidencia en .data/integracion-universidades; números finales en summary.json. Los tests cubren selección por universidad, resultados semánticos, opciones/startup, ausencia y duplicación de proveedores, cancelación y registro público compatible con ambos AutoApprove.

No se realizaron pruebas manuales, instalación de APK, requests a Railway ni contactos con instituciones. El usuario realiza la revisión manual. No hubo commit, push, merge ni PR.

