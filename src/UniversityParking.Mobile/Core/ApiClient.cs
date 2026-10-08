using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using UniversityParking.Contracts.Common;

namespace UniversityParking.Mobile.Core;

public sealed class ApiOptions
{
    public Uri BaseAddress { get; }
    public ApiOptions(string baseUrl, bool allowHttp = false)
    {
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri) || !string.IsNullOrEmpty(uri.UserInfo) ||
            !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment) || uri.Scheme != "https" && !(allowHttp && uri.Scheme == "http"))
            throw new ArgumentException("Configura una URL HTTPS válida para el servidor.", nameof(baseUrl));
        BaseAddress = new Uri(uri.AbsoluteUri.TrimEnd('/') + "/");
    }
}
public sealed record ClientError(string Code, string Message, ApiProblemDetails? Problem = null, int? HttpStatus = null);
public sealed record ApiResult<T>(T? Value, ClientError? Error)
{
    public bool IsSuccess => Error is null;
    public static ApiResult<T> Success(T value) => new(value, null);
    public static ApiResult<T> Failure(string code, string message, ApiProblemDetails? problem = null, int? httpStatus = null) => new(default, new(code, message, problem, httpStatus));
}
public sealed class AuthHttpHandler(IAuthSession session, IAppNavigation navigation, ApiOptions options) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.RequestUri is not { } uri || uri.Scheme != options.BaseAddress.Scheme || uri.Authority != options.BaseAddress.Authority)
            throw new InvalidOperationException("La solicitud no pertenece al servidor configurado.");
        var login = uri.AbsolutePath.EndsWith("/api/v1/auth/login", StringComparison.Ordinal);
        var token = login ? null : await session.GetTokenAsync();
        if (!string.IsNullOrWhiteSpace(token)) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await base.SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.Unauthorized && token is not null && await session.InvalidateAsync(token))
            await navigation.ShowLoginAsync("Tu sesión ha vencido. Inicia sesión nuevamente.");
        return response;
    }
}
public sealed class ApiClient(HttpClient client)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    { Converters = { new JsonStringEnumConverter(allowIntegerValues: false) } };
    public Task<ApiResult<T>> GetAsync<T>(string path, CancellationToken token = default) => SendAsync<T>(HttpMethod.Get, path, null, token);
    public Task<ApiResult<T>> PostAsync<T>(string path, object body, CancellationToken token = default) => SendAsync<T>(HttpMethod.Post, path, body, token);
    public Task<ApiResult<bool>> PutAsync(string path, object body, CancellationToken token = default) => SendAsync<bool>(HttpMethod.Put, path, body, token);
    public Task<ApiResult<bool>> PatchAsync(string path, CancellationToken token = default) => SendAsync<bool>(HttpMethod.Patch, path, null, token);
    public Task<ApiResult<bool>> PatchAsync(string path, object body, CancellationToken token = default) => SendAsync<bool>(HttpMethod.Patch, path, body, token);
    public Task<ApiResult<bool>> DeleteAsync(string path, CancellationToken token = default) => SendAsync<bool>(HttpMethod.Delete, path, null, token);
    public Task<ApiResult<bool>> PostEmptyAsync(string path, CancellationToken token = default) => SendAsync<bool>(HttpMethod.Post, path, null, token);
    public Task<ApiResult<bool>> PostCommandAsync(string path, object body, CancellationToken token = default) => SendAsync<bool>(HttpMethod.Post, path, body, token);
    public Task<ApiResult<T>> MultipartAsync<T>(string path, HttpContent content, CancellationToken token = default) => SendAsync<T>(HttpMethod.Post, path, content, token);
    private async Task<ApiResult<T>> SendAsync<T>(HttpMethod method, string path, object? body, CancellationToken token)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                using var request = new HttpRequestMessage(method, path);
                if (body is HttpContent content) request.Content = content;
                else if (body is not null) request.Content = JsonContent.Create(body, options: Json);
                using var response = await client.SendAsync(request, token);
                if (method == HttpMethod.Get && attempt == 0 && response.StatusCode is HttpStatusCode.RequestTimeout or HttpStatusCode.BadGateway or HttpStatusCode.ServiceUnavailable or HttpStatusCode.GatewayTimeout) continue;
                if (!response.IsSuccessStatusCode)
                {
                    ApiProblemDetails? problem = null;
                    try { problem = await response.Content.ReadFromJsonAsync<ApiProblemDetails>(Json, token); } catch (JsonException) { }
                    var message = response.StatusCode switch
                    {
                        HttpStatusCode.Forbidden => "No tienes permisos para realizar esta acción.",
                        HttpStatusCode.Unauthorized => path.EndsWith("auth/login", StringComparison.Ordinal) ? "Identificación o contraseña incorrectas." : "Tu sesión ha vencido. Inicia sesión nuevamente.",
                        _ when (int)response.StatusCode >= 500 => "El servidor no está disponible. Intenta nuevamente.",
                        _ => problem?.Errors?.Values.SelectMany(x => x).FirstOrDefault() ?? problem?.Detail ?? "No fue posible completar la solicitud."
                    };
                    return ApiResult<T>.Failure(string.IsNullOrWhiteSpace(problem?.Code) ? $"HTTP_{(int)response.StatusCode}" : problem.Code, message, problem, (int)response.StatusCode);
                }
                if (response.StatusCode == HttpStatusCode.NoContent && typeof(T) == typeof(bool)) return ApiResult<T>.Success((T)(object)true);
                var value = await response.Content.ReadFromJsonAsync<T>(Json, token);
                return value is null ? ApiResult<T>.Failure("INVALID_RESPONSE", "El servidor devolvió una respuesta no válida.") : ApiResult<T>.Success(value);
            }
            catch (HttpRequestException)
            {
                if (method == HttpMethod.Get && attempt == 0) continue;
                return ApiResult<T>.Failure("NETWORK_ERROR", "No fue posible conectar con el servidor.");
            }
            catch (OperationCanceledException) when (!token.IsCancellationRequested)
            {
                if (method == HttpMethod.Get && attempt == 0) continue;
                return ApiResult<T>.Failure("REQUEST_TIMEOUT", "El servidor tardó demasiado en responder.");
            }
            catch (JsonException) { return ApiResult<T>.Failure("INVALID_RESPONSE", "El servidor devolvió una respuesta no válida."); }
        }
    }
    public async Task<ApiResult<byte[]>> GetBytesAsync(string path, CancellationToken token = default)
    {
        for (var attempt = 0; ; attempt++)
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token); timeout.CancelAfter(TimeSpan.FromSeconds(20));
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, path);
                using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
                if (attempt == 0 && response.StatusCode is HttpStatusCode.RequestTimeout or HttpStatusCode.BadGateway or HttpStatusCode.ServiceUnavailable or HttpStatusCode.GatewayTimeout) continue;
                if (response.StatusCode == HttpStatusCode.Redirect && response.Headers.Location is { IsAbsoluteUri: true, Scheme: "https" } location && string.IsNullOrEmpty(location.UserInfo))
                {
                    // A signed storage URL receives no bearer token, cookies or automatic redirects.
                    using var storage = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false });
                    using var signed = await storage.GetAsync(location, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
                    return await ReadBytesAsync(signed, timeout.Token);
                }
                return await ReadBytesAsync(response, timeout.Token);
            }
            catch (Exception exception) when (exception is HttpRequestException or IOException)
            {
                if (attempt == 0) continue;
                return ApiResult<byte[]>.Failure("NETWORK_ERROR", "No fue posible cargar el archivo.");
            }
            catch (OperationCanceledException) when (!token.IsCancellationRequested)
            {
                if (attempt == 0) continue;
                return ApiResult<byte[]>.Failure("REQUEST_TIMEOUT", "El archivo tardó demasiado en cargar.");
            }
        }
    }
    private static async Task<ApiResult<byte[]>> ReadBytesAsync(HttpResponseMessage response, CancellationToken token)
    {
        if (!response.IsSuccessStatusCode) return ApiResult<byte[]>.Failure($"HTTP_{(int)response.StatusCode}",
            response.StatusCode == HttpStatusCode.Forbidden ? "No tienes permisos para abrir este archivo." : "No fue posible abrir el archivo.", httpStatus: (int)response.StatusCode);
        const int limit = 10 * 1024 * 1024;
        if (response.Content.Headers.ContentLength > limit) return ApiResult<byte[]>.Failure("FILE_TOO_LARGE", "El archivo supera 10 MB.");
        await using var stream = await response.Content.ReadAsStreamAsync(token); using var output = new MemoryStream();
        var buffer = new byte[81920]; int count;
        while ((count = await stream.ReadAsync(buffer, token)) > 0)
        {
            if (output.Length + count > limit) return ApiResult<byte[]>.Failure("FILE_TOO_LARGE", "El archivo supera 10 MB.");
            await output.WriteAsync(buffer.AsMemory(0, count), token);
        }
        return ApiResult<byte[]>.Success(output.ToArray());
    }
}
