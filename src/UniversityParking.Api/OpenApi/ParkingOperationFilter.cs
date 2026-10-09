using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;
using UniversityParking.Api.Controllers;

namespace UniversityParking.Api.OpenApi;

public sealed class ParkingOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        if (context.MethodInfo.DeclaringType != typeof(ParkingController)) return;
        operation.Description = context.MethodInfo.Name switch
        {
            nameof(ParkingController.CheckIn) => "Solo GUARD. El servidor determina guarda, zona y hora UTC. Revalida usuario, propiedad, registro, evidencia, ausencia de OPEN y horario de Bogotá: apertura inclusiva, cierre exclusivo. MovementId opcional identifica exactamente el intento; no aporta permisos. Ingreso/salida comparten 30 solicitudes por minuto y usuario.",
            nameof(ParkingController.CheckOut) => "Solo GUARD. Cierra exactamente MovementId y verifica VehicleId, sin repetir las condiciones de entrada. Ingreso/salida comparten 30 solicitudes por minuto y usuario.",
            nameof(ParkingController.Lookup) => "GUARD o ADMIN. Envía exactamente uno: qrPayload Base64 UTF-8 o identificationNumber. Un movimiento abierto produce eligibleVehicles vacío. Máximo 60 consultas por minuto y usuario.",
            nameof(ParkingController.Inside) => "GUARD o ADMIN. Solo movimientos OPEN. Los conteos corresponden a todos los resultados filtrados, no solo a la página.",
            nameof(ParkingController.Movements) => "GUARD o ADMIN. dateFrom/dateTo son fechas YYYY-MM-DD inclusivas en America/Bogota; se convierten a límites UTC. Orden: ingreso descendente y UUID.",
            nameof(ParkingController.Movement) => "GUARD o ADMIN. Consulta privada del MovementId exacto para verificar estado OPEN/CLOSED y respuestas inciertas.",
            nameof(ParkingController.MovementVehicle) => "GUARD o ADMIN. Metadatos del vehículo de un movimiento concreto y URL de evidencia privada; no devuelve documentos históricos ni StorageKey.",
            nameof(ParkingController.MyHistory) => "Historial del usuario de la sesión. No admite userId. dateFrom/dateTo son fechas YYYY-MM-DD inclusivas en America/Bogota.",
            _ => operation.Description
        };
    }
}
