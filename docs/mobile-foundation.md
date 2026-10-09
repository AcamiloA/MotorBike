# Base móvil — Fase 16

La aplicación Android muestra MOTOBIKE PARK y SMART PARKING SYSTEM. Usa OpenSans, el tema oscuro y la paleta central exigida por los mockups de references. Los iconos, splash y marca son SVG propios. Las pantallas de login, restauración y sesión utilizan bindings XAML compilados; los ViewModels usan CommunityToolkit.Mvvm 8.4.0.

## Sesión y navegación

Login recibe identificación y contraseña; valida campos, evita doble envío, presenta loading y errores en español y limpia la contraseña del ViewModel al finalizar. Incluye la acción secundaria de [registro público de estudiantes](student-registration.md); no incluye recuperación de contraseña.

El token se guarda únicamente mediante SecureStorage. No se guarda en Preferences, archivos ni SQLite. Los datos del usuario permanecen en memoria. Después del login se carga el perfil real desde `/api/v1/users/me` antes de construir la navegación; no se utilizan perfiles inventados. Al reiniciar, se lee el token seguro y se valida contra el mismo endpoint.

Un 401 autenticado elimina token y perfil y vuelve al login. La invalidación usa el token de la solicitud: respuestas atrasadas no eliminan una sesión nueva y varios 401 concurrentes solo disparan una navegación. Una respuesta tardía de startup tampoco reconstruye el perfil después de logout. Un 401 de login conserva credenciales inválidas salvo ACCOUNT_PENDING/ACCOUNT_REJECTED, cuyos mensajes se muestran después de verificar la contraseña en backend. Un 403 mantiene la sesión y muestra el mensaje de permiso denegado.

Un error de red conserva el token y muestra el estado de conectividad con REINTENTAR. Logout elimina la sesión segura y reemplaza la raíz de navegación. El login vive fuera del Shell; no queda en la pila de navegación autenticada.

El Shell combina las áreas que correspondan a los roles reales USER, GUARD y ADMIN. ADMIN no agrega GUARD implícitamente y MemberType no decide roles. El panel de sesión fue la base de Fase 16; las áreas funcionales ya se implementaron en las fases 17–19. Consulte user-manual.md para el uso actual.

## HTTP y configuración

ApiOptions centraliza BaseUrl y se inyecta junto con HttpClient, AuthService, AuthSession, navegación, páginas y ViewModels. El valor se compila mediante la propiedad MSBuild ApiBaseUrl. Debug utiliza por defecto `http://10.0.2.2:5197/`, correspondiente al perfil HTTP local de la API. Release exige una URL HTTPS explícita; no se incluye una dirección de despliegue inventada.

El handler Bearer agrega el token solo a solicitudes del origen configurado y omite login y registro público de estudiantes. Las redirecciones automáticas están deshabilitadas. El cliente interpreta ApiProblemDetails (Status, Title, Detail, Code, TraceId, Errors) y no muestra JSON, excepciones, SQL ni trazas técnicas. Los errores de red y respuesta inválida usan mensajes propios.

Timeout: 20 segundos por intento. Solo GET admite un reintento automático, ante fallo de red, timeout o HTTP 408/502/503/504. No se reintentan POST, 401, 403 ni 429. No hay refresh token ni cola offline.

Android permite HTTP únicamente en Debug y deshabilita backup de la aplicación; Release declara usesCleartextTraffic=false. Los permisos de aplicación son INTERNET y ACCESS_NETWORK_STATE, además del permiso interno no exportado generado por AndroidX.

## Validación

Resultado automático de esta fase: compilación completa, incluido Android, con cero errores y cero advertencias. Domain.Tests 112, Application.Tests 196, Infrastructure.Tests 54, Api.E2E.Tests 242 y Mobile.Tests 22: total 626 aprobadas, cero fallos y cero omisiones. Los TRX se conservan en `.data/phase16/TestResults`.

Mobile.Tests compila los mismos archivos de Core y ViewModels que la aplicación, sin depender de un dispositivo Android. Verifica persistencia y restauración lógica, errores de red, códigos HTTP, reintentos, logout, doble tap, sesiones concurrentes y restricciones de origen. Api.E2E.Tests incluye pruebas del cliente móvil contra la API y PostgreSQL reales, con JWT real y almacenamiento de prueba en memoria.

La compilación de Android verifica XAML y produce APK. La ejecución en emulador/dispositivo, el almacenamiento cifrado del sistema y la revisión visual quedan para las pruebas manuales asumidas por el usuario. No se afirma haber realizado esas comprobaciones.

## Evolución en Fase 17

El área USER ahora contiene las doce pantallas funcionales descritas en [la guía USER](mobile-user.md). GUARD y ADMIN conservan sus paneles de sesión. Android añade CAMERA con hardware opcional; el permiso se solicita al capturar una fotografía. La validación histórica de Fase 16 se conserva arriba.


## Evolución en Fase 19

El área ADMIN utiliza dashboard y navegación administrativa funcional, descritos en [la guía ADMIN](mobile-admin.md). Mi cuenta reutiliza perfil y contraseña. Los roles propios modificados exigen nueva autenticación. El APK incluye bibliotecas .NET para instalación directa; las cifras anteriores conservan el resultado histórico de Fase 16.
