# Noticias — Fase 13

| Método | Ruta | Permiso | Resultado |
|---|---|---|---|
| GET | `/api/v1/news` | Cuenta autenticada y activa | Noticias PUBLISHED paginadas |
| GET | `/api/v1/admin/news` | ADMIN | Todos los estados, con filtros |
| POST | `/api/v1/admin/news` | ADMIN | 201 con `id` |
| PUT | `/api/v1/admin/news/{id}` | ADMIN | 204 |
| POST | `/api/v1/admin/news/{id}/publish` | ADMIN | 204 |
| POST | `/api/v1/admin/news/{id}/archive` | ADMIN | 204 |

Crear y editar reciben JSON `{"title":"Título", "content":"Contenido"}`. Ambos textos son obligatorios; el título admite hasta 200 caracteres. No se reciben autor, estado ni fechas. `CreatedBy` proviene de la sesión y las fechas de `IClock`, siempre en UTC. No hay borrado de noticias.

Una noticia se crea DRAFT. Solo PUBLISHED aparece en la lista para usuarios autenticados, ordenada por `PublishedAt` descendente e ID estable. Los borradores y las noticias archivadas permanecen disponibles únicamente en la lista administrativa.

ADMIN puede editar DRAFT o PUBLISHED. Editar una noticia publicada conserva su estado y `PublishedAt`. Publicar cambia DRAFT a PUBLISHED; repetirlo conserva la fecha y no duplica auditoría. Archivar cambia DRAFT o PUBLISHED a ARCHIVED; repetirlo tampoco modifica fechas ni duplica auditoría. ARCHIVED no puede editarse ni publicarse de nuevo.

Cada escritura verifica el rol actual en PostgreSQL, bloquea la cuenta del actor antes de la noticia y confirma datos y auditoría en una misma transacción. Un fallo de persistencia revierte ambos. Acciones de auditoría: NEWS_CREATED, NEWS_UPDATED, NEWS_PUBLISHED y NEWS_ARCHIVED.

Ambas listas admiten `page` desde 1 y `pageSize` entre 1 y 100; valores predeterminados 1 y 20. La lista administrativa admite además `status` (DRAFT, PUBLISHED, ARCHIVED) y `search` (hasta 200 caracteres). La búsqueda ignora mayúsculas/minúsculas y busca texto literal en título y contenido: `%` y `_` no funcionan como comodines. La lista administrativa ordena por creación descendente e ID estable. Conteo y página se calculan en la misma instantánea de PostgreSQL.

Los errores usan ProblemDetails con código y traceId. Una noticia inexistente produce NEWS_NOT_FOUND (404); una transición prohibida produce INVALID_NEWS_STATE (409). USER y GUARD pueden consultar publicaciones, pero no administrar noticias.
