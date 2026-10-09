using System.Globalization;
using System.Text.Json;
using UniversityParking.Contracts.Common;
using UniversityParking.Contracts.Users;
using UniversityParking.Contracts.Universities;
using UniversityParking.Contracts.Vehicles;
using UniversityParking.Contracts.AcademicPeriods;
using UniversityParking.Contracts.ParkingLots;
using UniversityParking.Contracts.Parking;
using UniversityParking.Contracts.Incidents;
using UniversityParking.Contracts.News;
using UniversityParking.Contracts.Reporting;

namespace UniversityParking.Mobile.Core;

public sealed class AdminApiService(ApiClient api)
{
    public static string Query(string path, params (string Key, object? Value)[] values) => path + "?" + string.Join("&", values.Where(x => x.Value is not null && x.Value.ToString() != "")
        .Select(x => Uri.EscapeDataString(x.Key) + "=" + Uri.EscapeDataString(x.Value is DateOnly date ? date.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture) : Convert.ToString(x.Value,CultureInfo.InvariantCulture)!)));
    public Task<ApiResult<AdminDashboardResponse>> DashboardAsync() => api.GetAsync<AdminDashboardResponse>("api/v1/dashboard/admin");
    public Task<ApiResult<PagedResponse<UserListItemResponse>>> UsersAsync(string? search, string? member, string? status, string? role, int page) => api.GetAsync<PagedResponse<UserListItemResponse>>(Query("api/v1/users",("search",search?.Trim()),("memberType",member), ("status",status),("role",role),("page",page),("pageSize",20)));
    public Task<ApiResult<UserProfileResponse>> UserAsync(Guid id) => api.GetAsync<UserProfileResponse>($"api/v1/users/{id}");
    public Task<ApiResult<UniversityResponse[]>> UniversitiesAsync() => api.GetAsync<UniversityResponse[]>("api/v1/universities");
    public Task<ApiResult<UserCreatedResponse>> CreateUserAsync(CreateUserRequest request) => api.PostAsync<UserCreatedResponse>("api/v1/users",request);
    public Task<ApiResult<bool>> EditUserAsync(Guid id, UpdateUserRequest request) => api.PutAsync($"api/v1/users/{id}",request);
    public Task<ApiResult<bool>> ReviewStudentAsync(Guid id, bool approve) => api.PatchAsync($"api/v1/users/{id}/registration/{(approve ? "approve" : "reject")}");
    public Task<ApiResult<bool>> UserStatusAsync(Guid id, bool active) => api.PatchAsync($"api/v1/users/{id}/{(active ? "activate" : "deactivate")}");
    public Task<ApiResult<bool>> AssignRoleAsync(Guid id, string role) => api.PostCommandAsync($"api/v1/users/{id}/roles",new AssignRoleRequest(role));
    public Task<ApiResult<bool>> RemoveRoleAsync(Guid id, string role) => api.DeleteAsync($"api/v1/users/{id}/roles/{Uri.EscapeDataString(role)}");
    public Task<ApiResult<PagedResponse<VehicleResponse>>> VehiclesAsync(string? search,string? type,string? status,string? owner,string? registration,int page) => api.GetAsync<PagedResponse<VehicleResponse>>(Query("api/v1/vehicles",("search",search?.Trim()),("type",type),("status",status),("ownerIdentificationNumber",owner?.Trim()),("registrationState",registration),("page",page),("pageSize",20)));
    public Task<ApiResult<VehicleDetailResponse>> VehicleAsync(Guid id) => api.GetAsync<VehicleDetailResponse>($"api/v1/vehicles/{id}");
    public Task<ApiResult<bool>> VehicleStatusAsync(Guid id,bool active) => api.PatchAsync($"api/v1/vehicles/{id}/{(active ? "activate" : "deactivate")}");
    public Task<ApiResult<bool>> CorrectAsync(Guid id,string identifier,string reason) => api.PatchAsync($"api/v1/vehicles/{id}/identifier",new CorrectVehicleIdentifierRequest(identifier.Trim(),reason.Trim()));
    public Task<ApiResult<bool>> TransferAsync(Guid id,string owner,string reason) => api.PostCommandAsync($"api/v1/vehicles/{id}/transfer",new TransferVehicleRequest(owner.Trim(),reason.Trim()));
    public Task<ApiResult<IReadOnlyList<AcademicPeriodResponse>>> PeriodsAsync() => api.GetAsync<IReadOnlyList<AcademicPeriodResponse>>("api/v1/academic-periods");
    public Task<ApiResult<AcademicPeriodCreatedResponse>> CreatePeriodAsync(CreateAcademicPeriodRequest request) => api.PostAsync<AcademicPeriodCreatedResponse>("api/v1/academic-periods",request);
    public Task<ApiResult<bool>> PeriodActionAsync(Guid id,bool activate) => api.PostEmptyAsync($"api/v1/academic-periods/{id}/{(activate ? "activate" : "close")}");
    public Task<ApiResult<PagedResponse<ParkingLotResponse>>> LotsAsync(string? status,int page) => api.GetAsync<PagedResponse<ParkingLotResponse>>(Query("api/v1/parking-lots",("status",status),("page",page),("pageSize",20)));
    public Task<ApiResult<ParkingLotCreatedResponse>> CreateLotAsync(CreateParkingLotRequest request) => api.PostAsync<ParkingLotCreatedResponse>("api/v1/parking-lots",request);
    public Task<ApiResult<bool>> EditLotAsync(Guid id,UpdateParkingLotRequest request) => api.PutAsync($"api/v1/parking-lots/{id}",request);
    public Task<ApiResult<bool>> LotStatusAsync(Guid id,bool active) => api.PatchAsync($"api/v1/parking-lots/{id}/{(active ? "activate" : "deactivate")}");
    public Task<ApiResult<PagedResponse<IncidentResponse>>> IncidentsAsync(Guid? lot,Guid? user,Guid? vehicle,string? type,string? status,DateOnly? from,DateOnly? to,int page) => api.GetAsync<PagedResponse<IncidentResponse>>(Query("api/v1/incidents",("parkingLotId",lot),("userId",user),("vehicleId",vehicle),("type",type),("status",status),("dateFrom",from),("dateTo",to),("page",page),("pageSize",20)));
    public Task<ApiResult<IncidentDetailResponse>> IncidentAsync(Guid id) => api.GetAsync<IncidentDetailResponse>($"api/v1/incidents/{id}");
    public Task<ApiResult<bool>> ResolveAsync(Guid id,string resolution) => api.PostCommandAsync($"api/v1/incidents/{id}/resolve",new ResolveIncidentRequest(resolution.Trim()));
    public Task<ApiResult<bool>> CancelAsync(Guid id,string? reason) => api.PostCommandAsync($"api/v1/incidents/{id}/cancel",new CancelIncidentRequest(string.IsNullOrWhiteSpace(reason)?null:reason.Trim()));
    public Task<ApiResult<PagedResponse<NewsResponse>>> NewsAsync(string? status,string? search,int page) => api.GetAsync<PagedResponse<NewsResponse>>(Query("api/v1/admin/news",("status",status),("search",search?.Trim()),("page",page),("pageSize",20)));
    public Task<ApiResult<NewsCreatedResponse>> CreateNewsAsync(string title,string content) => api.PostAsync<NewsCreatedResponse>("api/v1/admin/news",new CreateNewsRequest(title.Trim(),content.Trim()));
    public Task<ApiResult<bool>> EditNewsAsync(Guid id,string title,string content) => api.PutAsync($"api/v1/admin/news/{id}",new UpdateNewsRequest(title.Trim(),content.Trim()));
    public Task<ApiResult<bool>> NewsActionAsync(Guid id,bool publish) => api.PostEmptyAsync($"api/v1/admin/news/{id}/{(publish?"publish":"archive")}");
    public Task<ApiResult<PagedResponse<ParkingMovementResponse>>> HistoryAsync(Guid? lot,string? identification,string? plate,string? frame,string? type,string? status,DateOnly from,DateOnly to,int page) => api.GetAsync<PagedResponse<ParkingMovementResponse>>(Query("api/v1/parking/movements",("parkingLotId",lot),("identificationNumber",identification?.Trim()),("plate",plate?.Trim()),("frameNumber",frame?.Trim()),("vehicleType",type),("status",status),("dateFrom",from),("dateTo",to),("page",page),("pageSize",20)));
    public Task<ApiResult<PagedResponse<AuditResponse>>> AuditAsync(Guid? actor,string? action,string? entity,Guid? entityId,DateOnly from,DateOnly to,int page) => api.GetAsync<PagedResponse<AuditResponse>>(Query("api/v1/audit-logs",("actorUserId",actor),("action",action?.Trim()),("entityType",entity?.Trim()),("entityId",entityId),("dateFrom",from),("dateTo",to),("page",page),("pageSize",20)));
    public Task<ApiResult<IReadOnlyList<DailyAccessResponse>>> DailyAsync(DateOnly from,DateOnly to,Guid? lot) => api.GetAsync<IReadOnlyList<DailyAccessResponse>>(Query("api/v1/reports/access/daily",("dateFrom",from),("dateTo",to),("parkingLotId",lot)));
    public Task<ApiResult<IReadOnlyList<AccessGroupResponse>>> GroupAsync(bool vehicles,DateOnly from,DateOnly to,Guid? lot) => api.GetAsync<IReadOnlyList<AccessGroupResponse>>(Query($"api/v1/reports/access/by-{(vehicles?"vehicle":"member")}-type",("dateFrom",from),("dateTo",to),("parkingLotId",lot)));
    public Task<ApiResult<VehicleHistoryResponse>> VehicleHistoryAsync(Guid id,int page=1) => api.GetAsync<VehicleHistoryResponse>($"api/v1/reports/vehicles/{id}/history?page={page}&pageSize=20");
    public Task<ApiResult<UserHistoryResponse>> UserHistoryAsync(Guid id,int page=1) => api.GetAsync<UserHistoryResponse>($"api/v1/reports/users/{id}/history?page={page}&pageSize=20");
    public Task<ApiResult<GuardActivityResponse>> GuardActivityAsync(Guid id,DateOnly from,DateOnly to) => api.GetAsync<GuardActivityResponse>(Query($"api/v1/reports/guards/{id}/activity",("dateFrom",from),("dateTo",to)));
    public Task<ApiResult<byte[]>> FileAsync(string path) => api.GetBytesAsync(path);
    public async Task<ApiResult<UserListItemResponse>> FindUserAsync(string identification)
    {
        var text=identification.Trim(); if(text.Length==0)return ApiResult<UserListItemResponse>.Failure("VALIDATION_ERROR","Escribe la identificación.");
        for(var page=1;;page++)
        {var result=await UsersAsync(text,null,null,null,page);if(!result.IsSuccess)return new(null,result.Error);var exact=result.Value!.Items.FirstOrDefault(x=>x.IdentificationNumber.Equals(text,StringComparison.OrdinalIgnoreCase));if(exact is not null)return ApiResult<UserListItemResponse>.Success(exact);if(page>=result.Value.TotalPages)break;}
        return ApiResult<UserListItemResponse>.Failure("USER_NOT_FOUND","No se encontró un usuario con esa identificación.");
    }
    public async Task<ApiResult<VehicleResponse>> FindVehicleAsync(string identifier)
    {
        var text=identifier.Trim();if(text.Length==0)return ApiResult<VehicleResponse>.Failure("VALIDATION_ERROR","Escribe la placa o número de marco.");
        for(var page=1;;page++)
        {var result=await VehiclesAsync(text,null,null,null,null,page);if(!result.IsSuccess)return new(null,result.Error);var exact=result.Value!.Items.FirstOrDefault(x=>AdminPresentation.Identifier(x).Equals(text,StringComparison.OrdinalIgnoreCase));if(exact is not null)return ApiResult<VehicleResponse>.Success(exact);if(page>=result.Value.TotalPages)break;}
        return ApiResult<VehicleResponse>.Failure("VEHICLE_NOT_FOUND","No se encontró un vehículo con ese identificador.");
    }
}
public static class AdminPresentation
{
    public static readonly TypeChoice[] Members = [new("STUDENT","Estudiante"),new("TEACHER","Docente"),new("STAFF","Personal")];
    public static readonly TypeChoice[] Statuses = [new("ACTIVE","Activo"),new("INACTIVE","Inactivo")];
    public static readonly TypeChoice[] UserStatuses = [new("ACTIVE","Activo"),new("INACTIVE","Inactivo"),new("PENDING","Pendiente"),new("REJECTED","Rechazado")];
    public static readonly TypeChoice[] Roles = [new("USER","USER"),new("GUARD","GUARD"),new("ADMIN","ADMIN")];
    public static TypeChoice[] All(IEnumerable<TypeChoice> choices) => new[] { new TypeChoice("","Todos") }.Concat(choices).ToArray();
    public static string Identifier(VehicleResponse vehicle) => vehicle.Plate ?? vehicle.FrameNumber ?? "";
    public static string Member(string code) => Members.FirstOrDefault(x=>x.Code==code)?.Label ?? code;
    public static string Status(string code) => UserStatuses.FirstOrDefault(x=>x.Code==code)?.Label ?? code;
    public static void RequireAdmin(IAuthSession session) { if(session.User?.Roles.Contains("ADMIN")!=true) throw new UserInputException("Se requiere el rol ADMIN para esta acción."); }
    public static string AuditValues(string? text)
    {
        if(string.IsNullOrWhiteSpace(text))return "Sin valores";
        try { using var doc=JsonDocument.Parse(text); return doc.RootElement.ValueKind == JsonValueKind.Object ? string.Join("\n",doc.RootElement.EnumerateObject().Select(x=>$"{x.Name}: {x.Value}")) : doc.RootElement.ToString(); }
        catch(JsonException){return text;}
    }
}
