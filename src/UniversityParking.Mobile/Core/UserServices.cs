using System.Globalization;
using System.Net.Http.Headers;
using UniversityParking.Contracts.Common;
using UniversityParking.Contracts.Vehicles;
using UniversityParking.Contracts.Users;
using UniversityParking.Contracts.News;
using UniversityParking.Contracts.Parking;
using UniversityParking.Contracts.AcademicPeriods;
using UniversityParking.Contracts.Auth;

namespace UniversityParking.Mobile.Core;

public sealed class UserInputException(string message) : Exception(message);
public sealed record TypeChoice(string Code, string Label);

public interface IUserNavigation
{
    Task GoAsync(string route, IReadOnlyDictionary<string, object>? arguments = null);
    Task BackAsync();
    Task MessageAsync(string title, string message);
    Task<bool> ConfirmAsync(string title, string message);
}
public sealed record PickedAttachment(string FileName, string ContentType, byte[] Bytes);
public interface IAttachmentPicker
{
    Task<PickedAttachment?> PhotoAsync(bool camera);
    Task<PickedAttachment?> TransitLicenseAsync();
    Task<PickedAttachment?> DocumentAsync();
}
public interface IFileViewer { Task OpenAsync(string fileName, string contentType, byte[] bytes); void ClearCache(); }
public sealed record DocumentAttachment(string Type, PickedAttachment File, string? Number = null, DateOnly? IssuedOn = null, DateOnly? ExpiresOn = null);
public sealed record VehicleRegistrationInput(string Type, string Identifier, string Brand, string Model, string Color,
    PickedAttachment VerificationImage);
public static class VehiclePresentation
{
    public static IReadOnlyList<string> AllowedTypes(string? memberType) => memberType == "STUDENT" ? ["MOTORCYCLE", "BICYCLE", "SCOOTER"] : ["MOTORCYCLE", "BICYCLE", "SCOOTER", "CAR"];
    public static IReadOnlyList<string> RequiredDocuments(string type) => type is "BICYCLE" or "SCOOTER" ? ["OWNERSHIP_SUPPORT"] : ["VEHICLE_REGISTRATION", "INSURANCE"];
    public static string TypeName(string type) => type switch { "CAR" => "Carro", "BICYCLE" => "Bicicleta", "SCOOTER" => "Scooter", _ => "Motocicleta" };
    public static string DocumentName(string type) => type switch { "INSURANCE" => "Seguro", "OWNERSHIP_SUPPORT" => "Soporte de propiedad", _ => "Documento del vehículo" };
    public static string RegistrationName(string state) => state switch { "ACTIVE" => "Registro vigente", "CANCELLED" => "Registro cancelado", "EXPIRED" => "Registro vencido", _ => "Sin registro vigente" };
    public static string Icon(string type) => type switch { "CAR" => "car.png", "BICYCLE" => "bicycle.png", "SCOOTER" => "scooter.png", _ => "motorcycle.png" };
    public static string VerificationLabel(string? type) => type is "BICYCLE" or "SCOOTER" ? (type == "SCOOTER" ? "Foto del scooter" : "Foto de la bicicleta") : "Frente de la Licencia de Tránsito";
    public static string VerificationHelp(string? type) => type is "BICYCLE" or "SCOOTER"
        ? "Toma una fotografía clara donde se vea el vehículo completo."
        : "Toma una fotografía clara del frente completo de la Licencia de Tránsito. Asegúrate de que el texto y la placa sean legibles.";
}
public static class MobileDates
{
    public static DateTimeOffset Bogota(DateTimeOffset value) => TimeZoneInfo.ConvertTime(value, TimeZoneInfo.FindSystemTimeZoneById("America/Bogota"));
    public static string Display(DateTimeOffset value) => Bogota(value).ToString("dd/MM/yyyy HH:mm", CultureInfo.GetCultureInfo("es-CO"));
    public static DateTime Today => Bogota(DateTimeOffset.UtcNow).Date;
}
public static class AttachmentValidation
{
    public static PickedAttachment Validate(string name, string? mime, byte[] bytes, bool photo)
    {
        var fileName = Path.GetFileName(name.Replace('\\', '/'));
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        var expected = extension switch { ".jpg" or ".jpeg" => "image/jpeg", ".png" => "image/png", ".pdf" when !photo => "application/pdf", _ => "" };
        if (bytes.Length > (photo ? 5 : 10) * 1024 * 1024) throw new UserInputException(photo ? "La foto supera 5 MB." : "El documento supera 10 MB.");
        if (expected == "" || bytes.Length == 0 || fileName.Length is 0 or > 255 || fileName.Any(char.IsControl) ||
            !string.IsNullOrWhiteSpace(mime) && mime != "application/octet-stream" && !mime.Equals(expected, StringComparison.OrdinalIgnoreCase))
            throw new UserInputException("Selecciona un archivo PDF, JPEG o PNG válido.");
        var signature = expected switch
        {
            "image/png" => bytes.AsSpan().StartsWith(new byte[] {137,80,78,71,13,10,26,10}),
            "image/jpeg" => bytes.AsSpan().StartsWith(new byte[] {255,216,255}),
            _ => bytes.AsSpan().StartsWith("%PDF-"u8)
        };
        if (!signature) throw new UserInputException("El contenido no corresponde al tipo de archivo seleccionado.");
        return new(fileName, expected, bytes);
    }
}
public sealed class UserApiService(ApiClient api)
{
    public Task<ApiResult<IReadOnlyList<VehicleResponse>>> MyVehiclesAsync() => api.GetAsync<IReadOnlyList<VehicleResponse>>("api/v1/vehicles/me");
    public Task<ApiResult<VehicleDetailResponse>> VehicleAsync(Guid id) => api.GetAsync<VehicleDetailResponse>($"api/v1/vehicles/{id}");
    public Task<ApiResult<AcademicPeriodResponse>> CurrentPeriodAsync() => api.GetAsync<AcademicPeriodResponse>("api/v1/academic-periods/current");
    public Task<ApiResult<UserProfileResponse>> ProfileAsync() => api.GetAsync<UserProfileResponse>("api/v1/users/me");
    public Task<ApiResult<bool>> EditProfileAsync(string name, string? career,string? email=null,string? phone=null) => api.PutAsync("api/v1/users/me", new UpdateMyProfileRequest(name, career,email,phone));
    public Task<ApiResult<bool>> ChangePasswordAsync(string current, string next) => api.PostCommandAsync("api/v1/auth/change-password", new ChangePasswordRequest(current, next));
    public Task<ApiResult<bool>> ArchiveVehicleAsync(Guid id) => api.DeleteAsync($"api/v1/vehicles/{id}"); public Task<ApiResult<bool>> EditVehicleAsync(Guid id, string brand, string model, string color,string? identifier=null) => api.PutAsync($"api/v1/vehicles/{id}", new UpdateVehicleRequest(brand, model, color,identifier));
    public Task<ApiResult<bool>> VehicleStatusAsync(Guid id, bool activate) => api.PatchAsync($"api/v1/vehicles/{id}/{(activate ? "activate" : "deactivate")}");
    public Task<ApiResult<PagedResponse<NewsResponse>>> NewsAsync(int page = 1) => api.GetAsync<PagedResponse<NewsResponse>>($"api/v1/news?page={page}&pageSize=20");
    public Task<ApiResult<PagedResponse<ParkingMovementResponse>>> HistoryAsync(DateOnly? from, DateOnly? to, Guid? vehicleId, int page = 1) =>
        api.GetAsync<PagedResponse<ParkingMovementResponse>>($"api/v1/parking/history/me?dateFrom={from?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}&dateTo={to?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}&page={page}&pageSize=20" + (vehicleId.HasValue ? $"&vehicleId={vehicleId}" : ""));
    public Task<ApiResult<byte[]>> FileAsync(string url) => api.GetBytesAsync(url);
    public async Task<ApiResult<VehicleCreatedResponse>> RegisterAsync(VehicleRegistrationInput input)
    {
        using var form = new MultipartFormDataContent();
        Add(form, "Type", input.Type); Add(form, input.Type is "BICYCLE" or "SCOOTER" ? "FrameNumber" : "Plate", input.Identifier);
        Add(form, "Brand", input.Brand); Add(form, "Model", input.Model); Add(form, "Color", input.Color);
        AddFile(form, "VerificationImage.File", AttachmentValidation.Validate(input.VerificationImage.FileName,
            input.VerificationImage.ContentType, input.VerificationImage.Bytes, true));
        return await api.MultipartAsync<VehicleCreatedResponse>("api/v1/vehicles", form);
    }
    public async Task<ApiResult<VehicleRenewedResponse>> RenewAsync(Guid id, IReadOnlyList<DocumentAttachment> documents)
    {
        if (documents.Count == 0)
        {
            // The API explicitly accepts an empty body when existing evidence is reused.
            using var empty = new ByteArrayContent([]);
            empty.Headers.ContentType = new MediaTypeHeaderValue("multipart/form-data");
            empty.Headers.ContentType.Parameters.Add(new NameValueHeaderValue("boundary", "NoNewDocuments"));
            return await api.MultipartAsync<VehicleRenewedResponse>($"api/v1/vehicles/{id}/renew", empty);
        }
        using var form = Multipart(documents); return await api.MultipartAsync<VehicleRenewedResponse>($"api/v1/vehicles/{id}/renew", form);
    }
    private static MultipartFormDataContent Multipart(IReadOnlyList<DocumentAttachment> documents)
    {
        var form = new MultipartFormDataContent();
        for (var index = 0; index < documents.Count; index++)
        {
            var item = documents[index]; var prefix = $"Documents[{index}]"; Add(form, prefix + ".Type", item.Type); AddFile(form, prefix + ".File", item.File);
            if (!string.IsNullOrWhiteSpace(item.Number)) Add(form, prefix + ".DocumentNumber", item.Number.Trim());
            if (item.IssuedOn.HasValue) Add(form, prefix + ".IssuedOn", item.IssuedOn.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            if (item.ExpiresOn.HasValue) Add(form, prefix + ".ExpiresOn", item.ExpiresOn.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        }
        return form;
    }
    public async Task<ApiResult<bool>> UpdateVerificationImageAsync(Guid id, PickedAttachment image)
    {
        using var form = new MultipartFormDataContent();
        AddFile(form, "VerificationImage.File", AttachmentValidation.Validate(image.FileName, image.ContentType, image.Bytes, true));
        return await api.PutMultipartAsync($"api/v1/vehicles/{id}/verification-image", form);
    }
    private static void Add(MultipartFormDataContent form, string name, string value) => form.Add(new StringContent(value.Trim()), name);
    private static void AddFile(MultipartFormDataContent form, string name, PickedAttachment file)
    {
        var content = new ByteArrayContent(file.Bytes); content.Headers.ContentType = new MediaTypeHeaderValue(file.ContentType); form.Add(content, name, file.FileName);
    }
}
