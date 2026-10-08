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
            nameof(ParkingController.CheckIn) => "Solo GUARD. El servidor determina guarda, zona y hora UTC. Valida las reglas de ingreso y el horario de Bogotá: apertura inclusiva, cierre exclusivo. Ingreso/salida comparten 30 solicitudes por minuto y usuario.",
            nameof(ParkingController.CheckOut) => "Solo GUARD. Cierra el movimiento abierto sin repetir las condiciones de entrada. Ingreso/salida comparten 30 solicitudes por minuto y usuario.",
            nameof(ParkingController.Lookup) => "GUARD o ADMIN. Envía exactamente uno: cardCode o identificationNumber. Un movimiento abierto produce eligibleVehicles vacío. Máximo 60 consultas por minuto y usuario.",
            nameof(ParkingController.Inside) => "GUARD o ADMIN. Solo movimientos OPEN. Los conteos corresponden a todos los resultados filtrados, no solo a la página.",
            nameof(ParkingController.Movements) => "GUARD o ADMIN. dateFrom/dateTo son fechas YYYY-MM-DD inclusivas en America/Bogota; se convierten a límites UTC. Orden: ingreso descendente y UUID.",
            nameof(ParkingController.MyHistory) => "Historial del usuario de la sesión. No admite userId. dateFrom/dateTo son fechas YYYY-MM-DD inclusivas en America/Bogota.",
            _ => operation.Description
        };
    }
}
