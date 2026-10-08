# Validación de Fase 22

## Alcance

README consolidado; architecture.md, installation.md, user-manual.md y backups.md;
scripts backup-db.ps1 y restore-db.ps1. Se actualizaron referencias de guías móviles
que todavía presentaban GUARD/ADMIN como futuros. No cambia código del backend ni
interfaz móvil. La Fase 23 no se ha iniciado.

## Comprobado el 7 de octubre de 2026

- SDK 10.0.401, MAUI Android 10.0.20 y carga Android 36.1.69 presentes.
- Restauración del manifiesto de herramientas y dotnet ef --version: 10.0.12.
- CLI acepta opciones de database update y dotnet run --artifacts-path.
- Build completo de UniversityParking.sln con artefactos .data/phase20 y ApiBaseUrl
  del emulador 8086: cero errores y cero advertencias, 21,21 segundos.
- backup-db generó backups/phase22-validation.dump, formato custom, 57.560 bytes.
- restore-db creó motorbike_restore_phase22, conservó la migración
  20261007060306_InitialCreate y los cinco usuarios, tres vehículos, cinco documentos
  y un movimiento. La base motorbike de la demo no fue sustituida.
- Backup rechaza un archivo existente y conserva su hash; restore rechaza motorbike
  y una base destino existente. No se ejecutó ningún DROP ni restauración sobre demo.
- Comparación de contenido: las 17 tablas del negocio coinciden mediante conteos
  y digests calculados en PostgreSQL, sin imprimir sus datos. Se conservaron las
  74 restricciones y 65 índices públicos. Evidencia: .data/phase22/backup-validation.json.
- Un dump inválido fue rechazado antes de crear su base destino; la consulta a
  pg_database confirmó que no se creó. Sintaxis de todos los scripts PowerShell
  analizada sin errores. Enlaces relativos de README/docs comprobados.
- Compose sigue healthy y GET /health responde 200. El dump está excluido por Git.

La última regresión de código fue Fase 21: 745 pruebas aprobadas, sin fallos u
omisiones. En Fase 22 se prueban los scripts de respaldo/restauración y la coherencia
documental; no se presenta la suite anterior como una nueva ejecución.

## Límites y pendientes

La instalación en una máquina nueva, licencias, certificados de dispositivo,
UI/cámara/permisos Android, mantenimiento coordinado del volumen privado y
conmutación de una recuperación real quedan a cargo del operador. Railway y S3
reales siguen sin desplegar/verificar. Los comandos de esas operaciones están
documentados como acciones del operador, no como ejecuciones realizadas aquí.
El respaldo y la base de restauración se conservan para revisión. Los dumps están
excluidos de Git y requieren protección por contener datos personales y hashes.
