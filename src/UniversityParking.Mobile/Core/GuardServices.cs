using System.Globalization;
using System.Net.Http.Headers;
using UniversityParking.Contracts.Common;
using UniversityParking.Contracts.Parking;
using UniversityParking.Contracts.ParkingLots;
using UniversityParking.Contracts.Incidents;
using UniversityParking.Contracts.Reporting;

namespace UniversityParking.Mobile.Core;

public interface IGuardSelectionStore { Guid? Get(Guid userId); void Set(Guid userId, Guid? lotId); }
public interface IScannerPermission { Task<bool> RequestAsync(); }
public sealed record AccessContext(ParkingAccessRequest Request, ParkingAccessResponse Result);
public sealed record EntryContext(AccessContext Access, EligibleVehicleResponse Vehicle);
public sealed record IncidentContext(Guid ParkingLotId, Guid? UserId = null, Guid? VehicleId = null, Guid? MovementId = null, string? UserName = null, string? VehicleIdentifier = null, string? ParkingLotName = null);
public sealed record IncidentInput(IncidentContext Context, string Type, string Description, DateTimeOffset? OccurredAt, IReadOnlyList<PickedAttachment> Attachments);

public sealed class GuardApiService(ApiClient api)
{
    private static string Query(string path, params (string Key, object? Value)[] values) => path + "?" + string.Join("&", values.Where(x => x.Value is not null && x.Value.ToString() != "")
        .Select(x => Uri.EscapeDataString(x.Key) + "=" + Uri.EscapeDataString(x.Value is DateOnly date ? date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : Convert.ToString(x.Value, CultureInfo.InvariantCulture)!)));
    public Task<ApiResult<PagedResponse<ParkingLotResponse>>> LotsAsync(int page = 1) => api.GetAsync<PagedResponse<ParkingLotResponse>>($"api/v1/parking-lots?status=ACTIVE&page={page}&pageSize=100");
    public Task<ApiResult<GuardDashboardResponse>> DashboardAsync(Guid lot) => api.GetAsync<GuardDashboardResponse>($"api/v1/dashboard/guard?parkingLotId={lot}");
    public Task<ApiResult<ParkingAccessResponse>> LookupAsync(ParkingAccessRequest request) => api.PostAsync<ParkingAccessResponse>("api/v1/parking/access/lookup", request);
    public Task<ApiResult<ParkingMovementResponse>> CheckInAsync(Guid user, Guid vehicle, Guid lot) => api.PostAsync<ParkingMovementResponse>("api/v1/parking/check-in", new CheckInVehicleRequest(user, vehicle, lot));
    public Task<ApiResult<ParkingMovementResponse>> CheckOutAsync(Guid vehicle) => api.PostAsync<ParkingMovementResponse>("api/v1/parking/check-out", new CheckOutVehicleRequest(vehicle));
    public Task<ApiResult<VehiclesInsideResponse>> InsideAsync(Guid? lot, string? type, string? search, int page) => api.GetAsync<VehiclesInsideResponse>(Query("api/v1/parking/inside", ("parkingLotId", lot), ("vehicleType", type), ("search", search?.Trim()), ("page", page), ("pageSize", 20)));
    public Task<ApiResult<PagedResponse<ParkingMovementResponse>>> HistoryAsync(Guid? lot, DateOnly? from, DateOnly? to, string? identification, string? plate, string? frame, string? type, string? status, int page) =>
        api.GetAsync<PagedResponse<ParkingMovementResponse>>(Query("api/v1/parking/movements", ("parkingLotId", lot), ("dateFrom", from), ("dateTo", to), ("identificationNumber", identification?.Trim()), ("plate", plate?.Trim()), ("frameNumber", frame?.Trim()), ("vehicleType", type), ("status", status), ("page", page), ("pageSize", 20)));
    public Task<ApiResult<PagedResponse<IncidentResponse>>> IncidentsAsync(Guid? lot, string? type, string? status, DateOnly? from, DateOnly? to, int page) => api.GetAsync<PagedResponse<IncidentResponse>>(Query("api/v1/incidents", ("parkingLotId", lot), ("type", type), ("status", status), ("dateFrom", from), ("dateTo", to), ("page", page), ("pageSize", 20)));
    public Task<ApiResult<IncidentDetailResponse>> IncidentAsync(Guid id) => api.GetAsync<IncidentDetailResponse>($"api/v1/incidents/{id}");
    public Task<ApiResult<byte[]>> FileAsync(string path) => api.GetBytesAsync(path);
    public async Task<ApiResult<IncidentCreatedResponse>> CreateIncidentAsync(IncidentInput input)
    {
        using var form = new MultipartFormDataContent();
        void Add(string name, object? value) { if (value is not null) form.Add(new StringContent(Convert.ToString(value, CultureInfo.InvariantCulture)!), name); }
        Add("ParkingLotId", input.Context.ParkingLotId); Add("UserId", input.Context.UserId); Add("VehicleId", input.Context.VehicleId); Add("ParkingMovementId", input.Context.MovementId);
        Add("Type", input.Type); Add("Description", input.Description.Trim()); Add("OccurredAt", input.OccurredAt?.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
        for (var i = 0; i < input.Attachments.Count; i++)
        {
            var file = input.Attachments[i]; var content = new ByteArrayContent(file.Bytes); content.Headers.ContentType = new MediaTypeHeaderValue(file.ContentType);
            form.Add(content, $"Attachments[{i}]", file.FileName);
        }
        return await api.MultipartAsync<IncidentCreatedResponse>("api/v1/incidents", form);
    }
}
public static class GuardPresentation
{
    public static string IncidentType(string type) => type switch { "DAMAGE" => "Daño", "ACCIDENT" => "Accidente", "SECURITY" => "Seguridad", "DOCUMENT" => "Documentación", "BEHAVIOR" => "Comportamiento", _ => "Otro" };
    public static string IncidentStatus(string status) => status switch { "OPEN" => "Abierto", "RESOLVED" => "Resuelto", "CANCELLED" => "Cancelado", _ => status };
    public static bool Uncertain(ClientError? error) => error is not null && (error.Code is "REQUEST_TIMEOUT" or "NETWORK_ERROR" or "INVALID_RESPONSE" || error.HttpStatus is 408 || error.HttpStatus >= 500);
    public static string AccessError(ClientError error) => error.Code switch
    {
        "VEHICLE_ALREADY_INSIDE" => "El vehículo ya está dentro.", "USER_ALREADY_HAS_VEHICLE_INSIDE" => "El usuario ya tiene un vehículo dentro.",
        "PARKING_LOT_CLOSED" => "El parqueadero está cerrado.", "VEHICLE_REGISTRATION_REQUIRED" => "El vehículo necesita un registro vigente.",
        "USER_INACTIVE" => "El usuario está inactivo.", "VEHICLE_INACTIVE" => "El vehículo está inactivo.", _ => error.Message
    };
    public static readonly TypeChoice[] VehicleTypes = [new("", "Todos los tipos"), new("CAR", "Carro"), new("MOTORCYCLE", "Moto"), new("BICYCLE", "Bicicleta")];
    public static readonly TypeChoice[] IncidentTypes = Enum.GetNames<IncidentKind>().Select(x => new TypeChoice(x, IncidentType(x))).ToArray();
    public static DateTimeOffset BogotaInstant(DateTime date, TimeSpan time) => new(DateTime.SpecifyKind(date.Date.Add(time), DateTimeKind.Unspecified), TimeSpan.FromHours(-5));
}
