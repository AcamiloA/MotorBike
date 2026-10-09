# Instalación en máquina nueva y pruebas Android

## Herramientas

En Windows, instale PowerShell 7, .NET 10 SDK y Docker Desktop con contenedores Linux.
Abra Docker y compruebe su motor. Para compilar Android instale MAUI Android 10,
Android SDK API 36 y Microsoft OpenJDK 21. Visual Studio con desarrollo MAUI puede
instalar SDK/JDK y crear el emulador; solo SDK .NET no instala todo Android.
El proyecto apunta a net10.0-android y declara mínimo Android API 21; la ejecución
en dispositivos concretos sigue pendiente. El backend solo puede compilarse sin MAUI.

```powershell
dotnet --version
docker version
docker compose version
dotnet workload install maui-android
dotnet workload list
```

Use el SDK 10 activo, no las cargas de .NET 9. En esta máquina se verificaron SDK
10.0.401, MAUI Android 10.0.20 y carga Android 36.1.69. Consulte
[dependencias Android oficiales](https://learn.microsoft.com/en-us/dotnet/android/getting-started/installation/dependencies)
y [MAUI 10](https://learn.microsoft.com/en-us/dotnet/maui/whats-new/dotnet-10?view=net-maui-10.0).
Instalaciones/licencias requieren intervención del operador; no se afirma haber
preparado otra máquina. Si SDK/JDK están en rutas no detectadas, configure sus
ubicaciones en Visual Studio; no use rutas copiadas de otro equipo.

## Obtener y preparar el proyecto

Copie el repositorio completo preservando references. Sitúese en la carpeta que
contiene UniversityParking.sln, src, tests y docker-compose.yml. No copie secretos
de otra máquina. .env solo lo consume Compose; ASP.NET Core no lo carga por sí solo.

Para Docker local con demo y puerto 8086:

```powershell
./scripts/initialize-docker.ps1 -ApiPort 8086
./scripts/enable-demo.ps1
docker compose -p motorbike-phase20 config --quiet
docker compose -p motorbike-phase20 up --build -d --wait --wait-timeout 240
Invoke-RestMethod http://localhost:8086/health
./scripts/validate-demo.ps1 -BaseUrl http://localhost:8086/
```

initialize-docker rechaza sobrescribir .env existente: si ya lo tiene, conserve
ese archivo y revise API_PORT. Para ejecutar sin demo omita enable-demo y mantenga
SEED_ENABLED/DEMO_ENABLED=false. PostgreSQL permanece en la red Compose y las
migraciones se aplican al arrancar. No se publican credenciales ni el puerto de DB.
Detalle de [Docker/Railway](deployment.md) y [cuentas demo](demo.md).

## API fuera de Docker y migraciones

Requiere una conexión PostgreSQL accesible desde el host. El hostname postgres
del Compose solo funciona dentro de su red; ese servicio no publica 5432 al host.
Con su servidor PostgreSQL configurado, introduzca la cadena Npgsql y clave JWT
sin imprimirlas (clave de al menos 32 bytes):

```powershell
$env:ConnectionStrings__DefaultConnection = Read-Host 'Cadena PostgreSQL Npgsql' -MaskInput
$env:Jwt__Key = Read-Host 'Clave JWT externa' -MaskInput
$env:Database__ApplyMigrationsOnStartup = 'true'
$env:HttpsRedirection__Enabled = 'false'
$env:Swagger__Enabled = 'true'
dotnet build src/UniversityParking.Api --artifacts-path .data/backend-local
dotnet run --project src/UniversityParking.Api --launch-profile http --artifacts-path .data/backend-local
```

El perfil http escucha 5197; Swagger habilitado está en `/swagger`, OpenAPI en
`/swagger/v1/swagger.json` y health en `/health`. Deshabilite Swagger cuando no lo
necesite. La redirección desactivada corresponde al HTTP de desarrollo; para
producción use HTTPS en el borde según deployment. Seed no se habilita por esas
variables. Consulte demo para habilitarlo explícitamente si necesita cuentas.

Migraciones manuales opcionales, con ConnectionStrings__DefaultConnection ya
definida en el entorno; se verificó el CLI 10.0.12 y sus opciones:

```powershell
dotnet tool restore --tool-manifest dotnet-tools.json
dotnet ef --version
dotnet ef database update --project src/UniversityParking.Infrastructure --startup-project src/UniversityParking.Api
```

No ejecute simultáneamente migración manual y arranque para el mismo mantenimiento.
Con una API que bloquee sus archivos binarios, use artefactos aislados para build;
la migración opcional al arrancar o Docker evitan recompilar sobre esa API.
Respalde antes de migraciones de producción: [respaldos](backups.md).

## Compilación y suites

```powershell
dotnet build UniversityParking.sln --artifacts-path .data/local
dotnet test UniversityParking.sln --no-build --artifacts-path .data/local --logger 'trx;LogFilePrefix=local' --results-directory .data/local/TestResults
```

La solución completa requiere MAUI/Android y Docker para los tests PostgreSQL.
Sin Android, compile Api y ejecute los cuatro proyectos backend por separado:

```powershell
dotnet build src/UniversityParking.Api --artifacts-path .data/backend-local
dotnet test tests/UniversityParking.Domain.Tests --artifacts-path .data/backend-local
dotnet test tests/UniversityParking.Application.Tests --artifacts-path .data/backend-local
dotnet test tests/UniversityParking.Infrastructure.Tests --artifacts-path .data/backend-local
dotnet test tests/UniversityParking.Api.E2E.Tests --artifacts-path .data/backend-local
```

Mobile.Tests compila Core/ViewModels y tampoco requiere instalar el APK. Su
resultado no acredita cámara, navegación/renderizado ni permisos nativos.
El script validate-backend.ps1, pese a su nombre histórico, compila/prueba toda
la solución y por tanto sí requiere MAUI. No use --no-build sin build previo
correcto con el mismo directorio de artefactos.

## Preparación

Requiere .NET 10 SDK, MAUI Android 10, Android SDK/JDK compatibles y una API ejecutándose con su configuración externa de PostgreSQL y JWT. Para las suites de integración se requiere Docker. Las cuentas y roles se crean por los mecanismos administrativos del backend; los estudiantes también disponen de [registro público con aprobación configurable](student-registration.md). Los APK históricos no contienen este feature; para revisarlo utilice un build actual.

La API usa por defecto el puerto HTTP 5197 en su perfil local. El emulador Android accede al equipo anfitrión mediante 10.0.2.2, no localhost. Para el dispositivo físico utiliza la URL accesible de tu servidor y cambia solo ApiBaseUrl al compilar. La dirección no contiene claves ni credenciales.

## Entregas Android anteriores

Para una máquina nueva, primero cree/inicie el emulador desde el administrador
Android de Visual Studio o conecte el dispositivo con depuración USB autorizada.
Agregue platform-tools del SDK al PATH para usar adb. Compile para la demo y
después instale su APK recién generado:

```powershell
dotnet build src/UniversityParking.Mobile -p:ApiBaseUrl=http://10.0.2.2:8086/ --artifacts-path .data/mobile-demo
adb devices
adb install -r .data/mobile-demo/bin/UniversityParking.Mobile/debug_net10.0-android/com.motobikepark.mobile-Signed.apk
```

adb devices debe mostrar un destino autorizado. Con varios, seleccione el número
de serie real mediante adb -s antes de install. En teléfono físico use su IP de
servidor real al compilar, no 10.0.2.2. Consulte
[configuración oficial de dispositivo](https://learn.microsoft.com/en-us/dotnet/maui/android/device/setup?view=net-maui-10.0).
La compilación no inicia un emulador ni acredita instalación. Las entregas .data
no forman parte de Git: si solo recibió el código, debe compilar su propio APK.

Desde la raíz del repositorio:

```powershell
dotnet build UniversityParking.sln --artifacts-path .data/phase19
dotnet test UniversityParking.sln --no-build --artifacts-path .data/phase19
```

Para cambiar el servidor de desarrollo:

```powershell
dotnet build src/UniversityParking.Mobile -p:ApiBaseUrl=http://IP-DEL-SERVIDOR:5197/ --artifacts-path .data/mobile-dev
```

Reemplaza IP-DEL-SERVIDOR por la dirección real antes de ejecutar. En Release se requiere `-p:ApiBaseUrl=https://URL-REAL/`; HTTP se rechaza y no hay URL productiva predeterminada.

APK anterior de Fase 19, que apunta a 5197: `.data/phase19/bin/UniversityParking.Mobile/debug_net10.0-android/com.motobikepark.mobile-Signed.apk`. Incluye las bibliotecas .NET para instalación directa sin despliegue del IDE. Es un APK Debug, no una publicación productiva. Puede instalarse en el emulador o dispositivo con el instalador Android o `adb install -r` seguido de su ruta. La app se identifica como `com.motobikepark.mobile`. Para la demo actual en 8086 use la entrega de Fase 21 indicada abajo.

## Comprobaciones manuales a cargo del usuario

La Fase 20 incorpora [Docker y configuración Railway](deployment.md). La instancia
Docker validada usa `http://localhost:8086`; para Android hay que recompilar con
la URL del emulador o dispositivo y ese puerto. El APK de Fase 19 conserva 5197.
La Fase 21 incorpora [seed y cuentas demo](demo.md), habilitados solo por
configuración explícita. Su APK para emulador ya apunta a 8086 y está en
`.data/phase21/com.motobikepark.mobile-demo-emulator-Signed.apk`.

1. Abrir la app: sin token debe mostrar el login oscuro con marca MOTOBIKE PARK, identificación, contraseña oculta y botón INICIAR SESIÓN.
2. Probar campos vacíos y credenciales incorrectas: debe mostrar errores en español y permitir corregirlos; no debe almacenar una sesión nueva.
3. Iniciar sesión con una cuenta válida: se valida `/users/me` y se muestra el nombre, tipo de miembro y las áreas correspondientes a sus roles. Un usuario con USER/GUARD/ADMIN ve las tres áreas; ADMIN solo no obtiene Portería.
4. Cerrar y abrir la app conservando el token: debe validar la sesión antes de mostrar el Shell.
5. Con token guardado, detener la API o perder conectividad y reiniciar: debe mostrar error y REINTENTAR, conservando el token. Al recuperar conexión, REINTENTAR debe restablecer la navegación.
6. Verificar 401 con una sesión vencida o invalidada por el servidor: debe volver al login y eliminar el token. Los 403 deben mantener la sesión.
7. Pulsar INICIAR SESIÓN varias veces rápidamente: debe procesar una sola solicitud y mostrar loading. Comprobar teclado, desplazamiento, rotación, tamaños de texto y contraste.
8. CERRAR SESIÓN debe volver al login; reiniciar debe seguir sin sesión y el botón Atrás no debe recuperar páginas autenticadas.
9. Completar el registro multipart manual pendiente de Fase 15 siguiendo `docs/backend-validation.md`.
10. Completar las comprobaciones USER de Fase 17 descritas en [la guía móvil de usuario](mobile-user.md).
11. Completar las comprobaciones GUARD de Fase 18 descritas en [la guía de portería](mobile-guard.md).
12. Completar las comprobaciones ADMIN de Fase 19 descritas en [la guía de administración](mobile-admin.md).

No se incluye validación TLS insegura ni bypass de certificados. Un servidor HTTPS de desarrollo debe utilizar un certificado confiable para el dispositivo, o usar HTTP únicamente con la compilación Debug.
