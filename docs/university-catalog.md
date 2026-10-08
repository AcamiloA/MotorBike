# Catálogo de universidades

## Estado final de C7

Rama: feature/completar-catalogo-universidades, creada desde main
610dc49e1f0fb7bb82214ed5ee37f00be71ae4ea.
Implementación C1–C7 preparada para revisión. Feature completa: NO.
Apta para merge: NO hasta aprobación del usuario. La revisión del código
y las pruebas manuales del usuario siguen pendientes.
No se hizo commit, push, merge ni despliegue; no se aplicó la migración a producción.

## Modelo y datos de referencia

User conserva únicamente UniversityId como referencia autoritativa. La FK requerida
users.university_id apunta a universities.id, tiene índice ix_users_university_id
y borrado Restrict. UpdateProfile conserva la referencia. No existe columna
de texto libre university en el modelo final.

Se reutilizan University, UniversityIds y UniversityResponse existentes en main.

| Id | Code | Name |
|---|---|---|
| a1100000-0000-4000-8000-000000000001 | ETITC | ETITC |
| a1100000-0000-4000-8000-000000000002 | CMC | Colegio Mayor de Cundinamarca |
| a1100000-0000-4000-8000-000000000003 | UPN | U. Pedagógica |

UniversityConfiguration define HasData con IDs y fechas UTC constantes.
Los tres registros se insertan por migración independientemente de Seed__Enabled
y Seed__DemoEnabled. No se insertan en startup ni se recrean mediante DemoSeed.

Code admite 20 caracteres y tiene índice UNIQUE ux_universities_code.
Name admite 200 caracteres y no es identidad ni es único.
Todas las columnas son requeridas; checks protegen código canónico no vacío,
nombre no vacío y updated_at >= created_at. GetByIdAsync incluye referencias
inactivas para lectura histórica. GetActiveAsync ordena por Name y Code.

## Contratos y validación

CreateUserRequest y UpdateUserRequest requieren universityId UUID. Ejemplo de
referencia: "universityId": "a1100000-0000-4000-8000-000000000002".
No reciben university, universityName ni código como fuente de escritura.
El JSON de estas solicitudes rechaza campos desconocidos; GUID vacío se valida.

Crear exige referencia existente y activa. Editar permite conservar la actual
inactiva, pero no cambiar a otra inactiva. El backend mantiene estas validaciones
independientemente del Picker. Errores: UNIVERSITY_NOT_FOUND,
UNIVERSITY_INACTIVE y VALIDATION_ERROR, con respuesta 400 estándar.

UserProfileResponse y UserListItemResponse incluyen universityId y universityName.
Los nombres se resuelven por lote de IDs en la página, sin consultas por usuario.
Perfil y detalle resuelven incluso referencias inactivas.

## API de catálogo

GET /api/v1/universities admite acceso anónimo y responde solo con activas:
id, code y name. No expone fechas ni isActive. Sin activas responde [].
No se agregaron operaciones de administración del catálogo.

~~~json
[
  { "id": "a1100000-0000-4000-8000-000000000002", "code": "CMC", "name": "Colegio Mayor de Cundinamarca" },
  { "id": "a1100000-0000-4000-8000-000000000001", "code": "ETITC", "name": "ETITC" },
  { "id": "a1100000-0000-4000-8000-000000000003", "code": "UPN", "name": "U. Pedagógica" }
]
~~~

PublicCatalog reutiliza GeneralPermitLimit: 120 solicitudes por IP/minuto,
ventana fija, sin cola. Su política explícita cubre anónimos, excluidos del
limitador global. Excederla devuelve 429 application/problem+json con
RATE_LIMIT_EXCEEDED mediante el manejo existente. Health y Login no cambian.

## Mobile

Creación y edición administrativas usan Picker con Name y conservan Id.
No existe entrada libre. Creación muestra "Seleccionar universidad" sin elegir
ETITC ni otra opción automáticamente. Guardar exige selección explícita,
catálogo y detalles cargados, y una opción perteneciente a la colección ofrecida.

Edición selecciona por User.UniversityId. Si la actual no aparece entre activas,
añade exclusivamente esa referencia actual y muestra un aviso; puede conservarse.
No ofrece otras referencias inactivas. Perfil, listado y detalle muestran Name.

Carga bloquea selección y guardado. Error muestra un mensaje y permite
ACTUALIZAR / REINTENTAR UNIVERSIDADES. En creación, actualizar conserva
una selección solo si continúa activa. En edición recarga datos persistidos:
los cambios de formulario sin guardar se reemplazan. Una respuesta tardía de
otra sesión no repuebla el formulario. No se elimina el bloqueo de escritura
incierta ni se reintentan automáticamente mutaciones.
Los ViewModels se prueban sin interfaz nativa; Android se compila.
Validación visual/en dispositivo: pendiente del usuario.

## Seed, reportes y auditoría

DemoSeed asigna UniversityIds.Etitc a nuevos usuarios y no crea universidades.
Respeta universidad, nombre, carrera, contraseñas y roles de usuarios existentes.
Los flags deshabilitado/base/demo no alteran el catálogo ni sus fechas.

Los reportes y exportaciones actuales no tienen un campo de universidad.
Se mantienen contratos, consultas y resultados. Se comprueba que cambiar una
referencia no cambia accesos por día/tipo de miembro ni el historial del vehículo.

USER_CREATED y USER_UPDATED guardan UniversityId y UniversityName.
La edición guarda ID/nombre anterior y nuevo; el perfil conserva la referencia.
Estos nombres son evidencia histórica en AuditLog, sin duplicarse en User.
Se conservan formato JSON, actor y transacción. No se reescriben auditorías previas.

## Migración y límites de aplicación

20261008140000_AddUniversityCatalog es la única migración nueva de esta rama,
no publicada. Crea catálogo y tres referencias, añade university_id nullable,
hace backfill por nombre exacto con trim y comparación sin distinguir mayúsculas,
aborta si queda algún valor desconocido, aplica NOT NULL/FK/índice y elimina
la columna legacy. No modifica IDs, credenciales, roles ni fechas de usuarios.
La migración inicial histórica permanece intacta.

Los códigos CMC/UPN o nombres alternativos no se convierten implícitamente:
valores desconocidos requieren revisión del operador antes de migrar.
La prueba de fallo verifica rollback del catálogo, backfill e historial de EF.
Down restaura los nombres canónicos mediante join antes de quitar FK/columna
y catálogo; no recupera espacios o mayúsculas originales del texto antiguo.

Solo se aplicó automáticamente en PostgreSQL temporal de Testcontainers.
No se ejecutó actualización manual, no se accedió a PostgreSQL real ni a Railway.
Antes de aplicar a una base existente, el operador debe revisar los valores
legacy y respaldar la base. La aprobación de código/merge no equivale a ejecutar
una migración. Esta rama no incluye registro público ni integraciones institucionales.

## Evidencia de validación

Baseline: 774 pruebas, build completo sin errores ni advertencias.
Los resultados finales por checkpoint están en .data/completar-catalogo-universidades.
La carpeta .data está excluida de Git; estas rutas son evidencias locales.

| Checkpoint | Pruebas aprobadas | Alcance |
|---|---:|---|
| C1 | 784 | Persistencia y datos de referencia |
| C2 | 794 | Relación requerida y transición legacy |
| C3 | 807 | Contratos GUID y validación |
| C4 | 818 | API pública y rate limiting |
| C5 | 829 | Picker y estados Mobile |
| C6 | 835 | Seed, reportes y auditoría |

C5: el resultado final combina las suites originales con Mobile repetido en
C5/final-mobile. Se corrigió una simulación de error que no contemplaba el
reintento automático de GET 503; no se cambió producción para resolverla.
C6: 835 aprobadas, cero fallos/omisiones; Domain 127, Application 202,
Infrastructure 72, Api.E2E 291 y Mobile 143.

Reproducción de C7, con Docker disponible para bases temporales:

~~~powershell
dotnet build UniversityParking.sln --artifacts-path .data/phase20 -p:ApiBaseUrl=http://10.0.2.2:8086/
dotnet test UniversityParking.sln --no-build --artifacts-path .data/phase20 --logger 'trx;LogFilePrefix=catalog-C7' --results-directory .data/completar-catalogo-universidades/C7
~~~

Los artefactos aislados evitan sobrescribir la API existente.
Build Debug Android para emulador; no acredita Release ni instalación en dispositivo.
No se hicieron pruebas manuales, llamadas reales, instalación de APK, Compose,
redeploy ni cambios en Railway/S3. No se hizo commit, push o merge.

## Revisión manual pendiente del usuario

- Crear usuario: ver tres nombres y exigir selección explícita.
- Editar: preseleccionar la universidad actual y cambiarla conservando los otros datos.
- Verificar listado, detalle y perfil con nombre humano.
- Revisar referencia actual inactiva y la imposibilidad de elegir otra inactiva.
- Probar error de catálogo, reintento y guardado bloqueado durante carga.
- Revisar auditoría antes/después y recorridos existentes de los roles.
- Revisar el diff completo y autorizar expresamente cualquier commit futuro.

La Definition of Done conserva los 54 requisitos en
.data/completar-catalogo-universidades/definition-of-done.md.

### Resultado final C7

Build completo, incluido Android: cero errores y advertencias, 8,83 s.
Regresión completa: **835/835 aprobadas, cero fallos, cero omisiones**.
Domain 127; Application 202; Infrastructure 72; Api.E2E 291; Mobile 143.
API terminó en 2 min 55 s. C7 no requirió cambios adicionales de código.
Los tests existentes de integración, migración, seed, reportes y Mobile pasaron.
Git status, diff/stat y diff --check revisados. El stat solo cuenta archivos
rastreados; también se revisaron los nuevos archivos de catálogo y documentación.
Evidencias finales locales: C7/*.trx, C7-summary.json, C7-git-status.txt
y C7-git-diff-stat.txt bajo .data/completar-catalogo-universidades.

Definition of Done: **52/54**. Solo quedan revisión de código y pruebas manuales
del usuario. C1–C7 completados técnicamente. Feature completa: NO hasta cerrar
la revisión acordada. Apta para merge: NO hasta aprobación explícita del usuario.
Commit: pendiente de revisión del usuario, no realizado. Push/merge: no realizados.
No se modificaron configuraciones de producción ni la migración inicial histórica.
